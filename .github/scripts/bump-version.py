#!/usr/bin/env python3
import sys
import re

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
            # Minor bump, reset patch
            return f"{m_maj}.{m_min + 1}.0"
        else:
            # Patch bump
            return f"{m_maj}.{m_min}.{m_pat + 1}"
    else:
        # Branch version is already higher than main (e.g. manually bumped)
        return f"{b_maj}.{b_min}.{b_pat}"

if __name__ == '__main__':
    main_v = sys.argv[1] if len(sys.argv) > 1 else "0.0.0"
    branch_v = sys.argv[2] if len(sys.argv) > 2 else "0.1.0"
    branch_name = sys.argv[3] if len(sys.argv) > 3 else ""
    print(bump_version(main_v, branch_v, branch_name))
