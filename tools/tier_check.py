"""Assert the catalogue's tier sections partition the pattern set exactly.

A pattern listed in two tiers, or in none, is invisible to every other check in
this repository: the catalogue still renders, every link still resolves, and the
count in the prose still reads plausibly. Nothing else here counts rows.

The sibling project shipped seventeen READMEs carrying stale test counts for
exactly that reason -- nothing checked arithmetic, so a number written once
drifted silently from the next commit onwards. This checker exists so the same
class of defect cannot reach this catalogue.

Exit codes:
  0  the partition holds
  1  it does not, and every problem found is named
  2  the catalogue could not be read at all -- "cannot tell" is not a pass
"""

import pathlib
import re
import sys

CATALOGUE = pathlib.Path("docs/cloud-pattern-catalogue-v1.0.0.md")

EXPECTED_TOTAL = 44
EXPECTED_PER_TIER = {1: 6, 2: 9, 3: 8, 4: 6, 5: 9, 6: 6}

TIER_HEADING = re.compile(r"^## Tier (\d) . (.+)$")
# A row is `| Name | ... |` or `| [Name](path) | ... |`. The name is captured
# either way, because a pattern moves from plain text to a link the moment its
# README exists, and the count must not change when it does.
ROW = re.compile(r"^\|\s*\[?([A-Z][^\]|]*?)\]?(?:\([^)]*\))?\s*\|")

# The header row of every tier table matches ROW as well, and a checker that
# counted it would report 45 patterns and be wrong in the safe direction only
# by accident.
NOT_A_PATTERN = {"Pattern", "Tier", "Section"}


def main() -> int:
    if not CATALOGUE.is_file():
        print(f"tiers: CANNOT CHECK - {CATALOGUE} not found "
              f"(run from the repository root)")
        return 2

    text = CATALOGUE.read_text(encoding="utf-8")

    per_tier: dict[int, list[str]] = {}
    seen: dict[str, list[int]] = {}
    current: int | None = None

    for line in text.splitlines():
        heading = TIER_HEADING.match(line)
        if heading:
            current = int(heading.group(1))
            per_tier.setdefault(current, [])
            continue
        # Any other section heading closes the tier. Without this, every table
        # after the last tier -- the README section contract, for one -- counts
        # as tier 6 and the total silently overshoots.
        if line.startswith("## "):
            current = None
            continue
        if current is None:
            continue
        row = ROW.match(line)
        if not row:
            continue
        name = row.group(1).strip()
        if not name or name in NOT_A_PATTERN:
            continue
        per_tier[current].append(name)
        seen.setdefault(name, []).append(current)

    problems: list[str] = []

    if sorted(per_tier) != sorted(EXPECTED_PER_TIER):
        problems.append(
            f"expected tiers {sorted(EXPECTED_PER_TIER)}, found {sorted(per_tier)}")
    else:
        for tier, expected in sorted(EXPECTED_PER_TIER.items()):
            actual = len(per_tier[tier])
            if actual != expected:
                problems.append(f"tier {tier} holds {actual} patterns, expected {expected}")

    total = sum(len(names) for names in per_tier.values())
    if total != EXPECTED_TOTAL:
        problems.append(f"expected {EXPECTED_TOTAL} patterns in total, found {total}")

    duplicated = {name: tiers for name, tiers in seen.items() if len(tiers) > 1}
    for name, tiers in sorted(duplicated.items()):
        problems.append(f"{name} appears in tiers {tiers}, and must appear in exactly one")

    if problems:
        for problem in problems:
            print(f"tiers: {problem}")
        return 1

    print(f"tiers: OK - {total} patterns, {len(per_tier)} tiers, each in exactly one")
    return 0


if __name__ == "__main__":
    sys.exit(main())
