#!/usr/bin/env python3
"""The write stage of generate_new_faction_kingdoms.py: a plain run is a dry run, and one output that
does not parse leaves all four files unwritten.

Run:  python -m unittest tools.tests.test_generate_new_faction_kingdoms_writes

main() runs against the shipped ModuleData with write_file replaced by a recorder, so nothing on disk
changes. A changed description gives the run something to write; without it the data is in sync and a
"nothing written" assertion would hold for the wrong reason.
"""
import contextlib
import copy
import io
import os
import sys
import unittest
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_new_faction_kingdoms as gen  # noqa: E402


def run_main(argv, kingdoms=None, upsert=None):
    """(exit code, paths written, stdout) of main() with the writer recorded."""
    written = []
    out = io.StringIO()
    patches = [mock.patch.object(sys, "argv", ["generate_new_faction_kingdoms.py"] + argv),
               mock.patch.object(gen, "write_file", lambda path, data: written.append(os.path.basename(path)))]
    if kingdoms is not None:
        patches.append(mock.patch.object(gen, "KINGDOMS", kingdoms))
    if upsert is not None:
        patches.append(mock.patch.object(gen, "upsert", upsert))
    with contextlib.ExitStack() as stack:
        for p in patches:
            stack.enter_context(p)
        stack.enter_context(contextlib.redirect_stdout(out))
        code = gen.main()
    return code, written, out.getvalue()


def edited_kingdoms():
    """KINGDOMS with Cardolan's description changed, so a run has a file to write."""
    kingdoms = copy.deepcopy(gen.KINGDOMS)
    kingdoms["cardolan"]["desc"] += " Edited by the test."
    return kingdoms


class WriteStageTests(unittest.TestCase):
    def test_a_run_without_apply_writes_nothing(self):
        code, written, out = run_main(["--only", "cardolan"], kingdoms=edited_kingdoms())
        self.assertEqual(code, 0)
        self.assertEqual(written, [])
        self.assertIn("would change: taom_spkingdoms.xml", out)
        self.assertIn("DRY RUN", out)

    def test_apply_writes_the_changed_file(self):
        code, written, _ = run_main(["--only", "cardolan", "--apply"], kingdoms=edited_kingdoms())
        self.assertEqual(code, 0)
        self.assertEqual(written, ["taom_spkingdoms.xml"])

    def test_check_exits_one_and_writes_nothing_on_a_change(self):
        code, written, _ = run_main(["--only", "cardolan", "--check"], kingdoms=edited_kingdoms())
        self.assertEqual(code, 1)
        self.assertEqual(written, [])

    def test_check_and_apply_together_are_refused(self):
        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
            run_main(["--only", "cardolan", "--check", "--apply"])

    def test_one_unparsable_output_stops_every_write(self):
        real_upsert = gen.upsert

        def breaking_upsert(text, close_tag, payload, marker):
            # spk changes (valid) and lords breaks (invalid): the valid file must not land alone.
            if close_tag == "</NPCCharacters>":
                payload += "  <NPCCharacter id=\"broken\"\n"
            return real_upsert(text, close_tag, payload, marker)

        code, written, out = run_main(["--only", "cardolan", "--apply"], kingdoms=edited_kingdoms(),
                                      upsert=breaking_upsert)
        self.assertEqual(code, 1)
        self.assertEqual(written, [])
        self.assertIn("REFUSING", out)
        self.assertIn("lords.xml", out)


class FemaleHairTests(unittest.TestCase):
    def test_a_face_template_without_a_female_hair_tag_is_refused(self):
        kingdoms = copy.deepcopy(gen.KINGDOMS)
        del kingdoms["rhudaur"]["female_hair_tag"]
        with self.assertRaisesRegex(RuntimeError, "rhudaur"):
            run_main(["--only", "rhudaur"], kingdoms=kingdoms)

    def test_the_shipped_realms_with_a_face_template_name_a_hair_tag(self):
        for kid, k in gen.KINGDOMS.items():
            if k.get("face_template"):
                self.assertTrue(k.get("female_hair_tag"), kid)


if __name__ == "__main__":
    unittest.main()
