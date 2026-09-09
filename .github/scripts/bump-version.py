#!/usr/bin/env python3
import sys
import re

def parse_semver(v_str):
    if not v_str:
        return (0, 1, 0)
    v_str = v_str.strip().lstrip('v')
    numbers = re.findall(r'\d+', v_str)
    parts = [int(p) for p in numbers[:3]]
    while len(parts) < 3:
        parts.append(0)
    return tuple(parts)

def compute_next_version(current_tag, text_context=""):
    maj, min, pat = parse_semver(current_tag)
    context_lower = (text_context or "").lower()

    if "breaking change" in context_lower or "!:" in context_lower:
        # Major bump: 0.1.1 -> 1.0.0
        return f"{maj + 1}.0.0"
    elif any(k in context_lower for k in ("feat/", "feature/", "feat:", "feat(")):
        # Minor bump: 0.1.1 -> 0.2.0
        return f"{maj}.{min + 1}.0"
    else:
        # Patch bump: 0.1.1 -> 0.1.2
        return f"{maj}.{min}.{pat + 1}"

if __name__ == '__main__':
    current_tag = sys.argv[1] if len(sys.argv) > 1 else "v0.1.0"
    context = sys.argv[2] if len(sys.argv) > 2 else ""
    print(compute_next_version(current_tag, context))
