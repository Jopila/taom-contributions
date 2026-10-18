#!/usr/bin/env python3
"""The ModuleData regions a generator owns must still match a fresh run of that generator.

Run:  python -m unittest tools.tests.test_generator_regions_in_sync

A hand edit inside a generated region passes every data gate and is silently reverted by the
generator's next --apply. This shipped three times in three days (2026-10-16 KEYforce javelin rows,
2026-10-18 orc binder region, 2026-10-18 Arthedain template and start war, #818); the lessons said
"check who owns the region" and that check was skipped each time. Each case here runs the owner's
own --check, so the edit fails /verify instead of waiting for a regeneration.

Not covered yet: promote_borrowed_cultures.py --only arthedain --check is red for a reason that
predates this test (it re-appends its block at the end of the file, and the Arthedain start point
has drifted from the live map); add it once that is fixed.
"""
import os
import subprocess
import sys
import unittest

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REPO = os.path.dirname(TOOLS)

# (generator, its check arguments) for every generator that owns a marker region in the repo's
# ModuleData and has a --check that passes today.
OWNERS = [
    ("generate_arthedain_troops.py", ["--check"]),
    ("generate_new_faction_kingdoms.py", ["--only", "arthedain", "--check"]),
]


class GeneratorRegionsInSyncTests(unittest.TestCase):
    def test_every_owned_region_matches_a_fresh_run(self):
        for script, args in OWNERS:
            with self.subTest(script=script):
                run = subprocess.run(
                    [sys.executable, "-B", os.path.join(TOOLS, script)] + args,
                    cwd=REPO, capture_output=True, text=True, timeout=600)
                self.assertEqual(
                    run.returncode, 0,
                    "%s %s reports drift: a hand edit inside its region, or the generator changed "
                    "without --apply.\n%s%s" % (script, " ".join(args), run.stdout[-2000:], run.stderr[-2000:]))


if __name__ == "__main__":
    unittest.main()
