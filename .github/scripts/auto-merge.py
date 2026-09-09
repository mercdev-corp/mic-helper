#!/usr/bin/env python3
import json
import os
import re
import subprocess
import sys
import time

def run_cmd(cmd, check=True, capture=True):
    cmd_str = ' '.join(cmd) if isinstance(cmd, list) else cmd
    print(f"--> Running: {cmd_str}")
    res = subprocess.run(
        cmd,
        shell=isinstance(cmd, str),
        text=True,
        capture_output=capture,
        check=check
    )
    return res.stdout.strip() if capture else ""

def parse_semver(v_str):
    if not v_str:
        return (0, 0, 0)
    v_str = v_str.strip().lstrip('v')
    numbers = re.findall(r'\d+', v_str)
    parts = [int(p) for p in numbers[:3]]
    while len(parts) < 3:
        parts.append(0)
    return tuple(parts)

def bump_version(main_v_str, branch_v_str, branch_name):
    m_maj, m_min, m_pat = parse_semver(main_v_str)
    b_maj, b_min, b_pat = parse_semver(branch_v_str)

    # If branch version <= main version, bump it
    if (b_maj, b_min, b_pat) <= (m_maj, m_min, m_pat):
        branch_lower = branch_name.lower().strip()
        if branch_lower.startswith(('feat/', 'feature/')):
            # Minor bump, reset patch: 0.1.0 -> 0.2.0
            return f"{m_maj}.{m_min + 1}.0"
        else:
            # Patch bump: 0.1.0 -> 0.1.1
            return f"{m_maj}.{m_min}.{m_pat + 1}"
    else:
        # Branch version is already higher than main (e.g. manually set)
        return f"{b_maj}.{b_min}.{b_pat}"

def get_candidate_prs():
    # 1. PR from review event
    review_pr = os.environ.get("REVIEW_PR_NUMBER", "").strip()
    if review_pr and review_pr != "0":
        return [review_pr]

    # 2. PR from workflow_dispatch input
    input_pr = os.environ.get("INPUT_PR_NUMBER", "").strip()
    if input_pr:
        return [input_pr]

    # 3. PR from workflow_run event
    head_branch = os.environ.get("WORKFLOW_RUN_HEAD_BRANCH", "").strip()
    if head_branch:
        out = run_cmd(["gh", "pr", "list", "--head", head_branch, "--base", "main", "--state", "open", "--json", "number", "--jq", ".[].number"])
        prs = [line.strip() for line in out.splitlines() if line.strip()]
        if prs:
            return prs

    # 4. Fallback: all open PRs targeting main
    out = run_cmd(["gh", "pr", "list", "--base", "main", "--state", "open", "--json", "number", "--jq", ".[].number"])
    return [line.strip() for line in out.splitlines() if line.strip()]

def check_approval(pr_data):
    review_decision = pr_data.get("reviewDecision")
    if review_decision == "APPROVED":
        return True, "reviewDecision is APPROVED"
    if review_decision == "CHANGES_REQUESTED":
        return False, "reviewDecision is CHANGES_REQUESTED"

    # In repositories without branch protection, reviewDecision is empty.
    # Fallback to inspecting reviews and latestReviews.
    latest_reviews = pr_data.get("latestReviews") or []
    if not latest_reviews:
        # Reconstruct latest review per user from full reviews list
        reviews = pr_data.get("reviews") or []
        user_reviews = {}
        for r in reviews:
            user = (r.get("author") or {}).get("login")
            if user:
                user_reviews[user] = r
        latest_reviews = list(user_reviews.values())

    authorized_roles = {"OWNER", "MEMBER", "COLLABORATOR"}
    approved_by = []
    changes_requested_by = []

    for r in latest_reviews:
        state = r.get("state")
        author_info = r.get("author") or {}
        author = author_info.get("login", "unknown")
        assoc = r.get("authorAssociation", "")

        # Only consider reviews from repo members/owners/collaborators
        if assoc in authorized_roles:
            if state == "CHANGES_REQUESTED":
                changes_requested_by.append(author)
            elif state == "APPROVED":
                approved_by.append(author)

    if changes_requested_by:
        return False, f"Changes requested by: {', '.join(changes_requested_by)}"

    if approved_by:
        return True, f"Approved by authorized reviewer(s): {', '.join(approved_by)}"

    return False, f"No approved reviews from authorized reviewers (reviewDecision='{review_decision}')"

def evaluate_and_merge_pr(pr_number):
    print(f"\n================ Evaluating PR #{pr_number} ================")
    out = run_cmd([
        "gh", "pr", "view", str(pr_number),
        "--json", "number,state,baseRefName,headRefName,reviewDecision,reviews,latestReviews,statusCheckRollup"
    ])
    pr_data = json.loads(out)

    state = pr_data.get("state")
    base_ref = pr_data.get("baseRefName")
    head_ref = pr_data.get("headRefName")
    review_decision = pr_data.get("reviewDecision")
    status_checks = pr_data.get("statusCheckRollup", [])

    print(f"PR #{pr_number}: State={state}, Base={base_ref}, Head={head_ref}, ReviewDecision={review_decision}")

    if state != "OPEN":
        print(f"PR #{pr_number} is not OPEN (current state: {state}). Skipping.")
        return False

    if base_ref != "main":
        print(f"PR #{pr_number} does not target main (targets: {base_ref}). Skipping.")
        return False

    is_approved, reason = check_approval(pr_data)
    if not is_approved:
        print(f"PR #{pr_number} is not approved: {reason}. Skipping.")
        return False

    print(f"PR #{pr_number} approval check passed: {reason}")

    # Filter out auto-merge checks so we don't wait on our own job
    relevant_checks = [
        c for c in status_checks
        if "auto-merge" not in c.get("name", "").lower()
        and "auto merge" not in c.get("name", "").lower()
    ]

    if not relevant_checks:
        print(f"PR #{pr_number} has no completed CI status checks. Skipping.")
        return False

    pending_checks = [c for c in relevant_checks if c.get("status") != "COMPLETED"]
    if pending_checks:
        names = [c.get("name") for c in pending_checks]
        print(f"PR #{pr_number} has {len(pending_checks)} pending check(s): {names}. Skipping.")
        return False

    failed_checks = [
        c for c in relevant_checks
        if c.get("conclusion") not in ("SUCCESS", "NEUTRAL", "SKIPPED")
    ]
    if failed_checks:
        names = [(c.get("name"), c.get("conclusion")) for c in failed_checks]
        print(f"PR #{pr_number} has failing check(s): {names}. Skipping.")
        return False

    print(f"PR #{pr_number} satisfies all conditions: APPROVED and all CI tests SUCCESS!")

    # Fetch and checkout branch
    run_cmd(["git", "fetch", "origin", "main"])
    run_cmd(["git", "fetch", "origin", head_ref])
    run_cmd(["git", "checkout", "-B", head_ref, f"origin/{head_ref}"])

    # Get version in main
    try:
        main_v_raw = run_cmd(["git", "show", "origin/main:VERSION"])
    except Exception:
        main_v_raw = "0.0.0"

    # Get version in branch
    if os.path.exists("VERSION"):
        with open("VERSION", "r", encoding="utf-8") as f:
            branch_v_raw = f.read().strip()
    else:
        branch_v_raw = "0.1.0"

    print(f"Version in main: '{main_v_raw}'")
    print(f"Version in {head_ref}: '{branch_v_raw}'")

    new_version = bump_version(main_v_raw, branch_v_raw, head_ref)
    print(f"Computed target version: '{new_version}'")

    if new_version != branch_v_raw:
        print(f"Updating VERSION to {new_version}...")
        with open("VERSION", "w", encoding="utf-8") as f:
            f.write(f"{new_version}\n")

        run_cmd(["git", "config", "user.name", "github-actions[bot]"])
        run_cmd(["git", "config", "user.email", "github-actions[bot]@users.noreply.github.com"])
        run_cmd(["git", "add", "VERSION"])
        run_cmd(["git", "commit", "-m", f"chore(release): bump version to {new_version} [skip ci]"])
        run_cmd(["git", "push", "origin", head_ref])
        print(f"Pushed version bump commit to {head_ref}.")
        time.sleep(3)

    # Merge PR and delete branch
    print(f"Merging PR #{pr_number} into main and deleting branch {head_ref}...")
    try:
        run_cmd(["gh", "pr", "merge", str(pr_number), "--merge", "--delete-branch"])
        print(f"Successfully merged PR #{pr_number}!")
        return True
    except Exception as err:
        print(f"Standard merge failed ({err}). Trying with --admin flag...")
        run_cmd(["gh", "pr", "merge", str(pr_number), "--merge", "--delete-branch", "--admin"])
        print(f"Successfully merged PR #{pr_number} with --admin!")
        return True

def main():
    prs = get_candidate_prs()
    if not prs:
        print("No open candidate PRs found.")
        return

    print(f"Evaluating candidate PR(s): {prs}")
    merged_count = 0
    for pr in prs:
        try:
            if evaluate_and_merge_pr(pr):
                merged_count += 1
        except Exception as err:
            print(f"Failed to process PR #{pr}: {err}")

    print(f"\nCompleted. Merged {merged_count} PR(s).")

if __name__ == "__main__":
    main()
