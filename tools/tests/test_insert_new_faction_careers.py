#!/usr/bin/env python3
"""Tests for tools/insert_new_faction_careers.py: the shipped careers, the clone machinery, and a
dry run, a check and an apply on temp copies of the four files.

Run:  python -m pytest tools/tests/test_insert_new_faction_careers.py -q

The ways a cloned career goes wrong while the C# coverage tests stay green: text that still names
the source culture (Erebor's Arkenstone under an Ered Luin career), an eligibility row left pointing
at the source culture, one source cloned twice into two careers that read the same, a dwarf career
whose passives only work on a mount, and an archetype row that is missing (the ability becomes a
no-op with a toast) or that makes a dwarf career Cavalry. And the ways a run goes wrong: a dry run
that writes, a --check that passes while a culture lacks its careers, and an apply that writes some
of the four files when a splice anchor is missing or a splice breaks a file.
"""
import contextlib
import io
import json
import os
import re
import shutil
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import insert_new_faction_careers as ic  # noqa: E402

IOC = ic.ROOT / "Main" / "Features" / "CareerSystem" / "CareerSystemIoC.cs"
FILES = ("CAREERS_XML", "ABILITIES_XML", "CHOICES_XML", "MENU_JSON")

# Erebor's places and people, from the lore rather than from the tool's tables.
EREBOR_IDENTITY = ("Erebor", "Lonely Mountain", "Dale", "Esgaroth", "Smaug", "Iron Hills", "Dain",
                   "Dáin", "Ironfoot", "Ravenhill", "Arkenstone", "Mithril", "mithril")


def eredluin_rows():
    return list(ic.targets_for("eredluin"))


def career_text(new_id):
    """Every {=KEY}default value in the shipped Career, AbilityTemplate, root Choice and the six
    ChoiceGroups of one career."""
    careers = ic.CAREERS_XML.read_text(encoding="utf-8")
    abilities = ic.ABILITIES_XML.read_text(encoding="utf-8")
    choices = ic.CHOICES_XML.read_text(encoding="utf-8")
    blocks = [ic.extract(careers, rf'  <Career id="{new_id}".*?</Career>', new_id),
              ic.extract(abilities, rf'  <AbilityTemplate id="{new_id}_ability".*?/>', new_id),
              ic.extract(choices, rf'  <Choice id="{new_id}_root".*?</Choice>', new_id)]
    blocks += re.findall(rf'  <ChoiceGroup id="{new_id}_[^"]*".*?</ChoiceGroup>', choices, re.S)
    return re.findall(r'="\{=[^}]+\}([^"]*)"', "\n".join(blocks))


def run(argv):
    out = io.StringIO()
    with contextlib.redirect_stdout(out):
        rc = ic.main(argv)
    return rc, out.getvalue()


class Machinery(unittest.TestCase):
    def setUp(self):
        self.files = (ic.CAREERS_XML.read_text(encoding="utf-8"),
                      ic.ABILITIES_XML.read_text(encoding="utf-8"),
                      ic.CHOICES_XML.read_text(encoding="utf-8"))

    def test_a_source_cloned_twice_gives_two_distinct_careers(self):
        twice = [r for r in eredluin_rows() if r[0] == "ironguard"]
        self.assertEqual(len(twice), 2)
        built = [ic.build("eredluin", *row, *self.files) for row in twice]
        # Compare the text after the {=KEY} prefix: the keys always differ, the words might not.
        shown = re.compile(r'display_name="\{=[^}]*\}([^"]+)"')
        ids = [re.search(r'<Career id="([^"]+)"', b[0]).group(1) for b in built]
        names = [shown.search(b[0]).group(1) for b in built]
        abilities = [shown.search(b[1]).group(1) for b in built]
        self.assertEqual(len(set(ids)), 2)
        self.assertEqual(len(set(names)), 2)
        self.assertEqual(len(set(abilities)), 2)
        for career, _, _ in built:
            self.assertIn('portrait_sprite="career_ironguard_portrait"', career)

    def test_a_clone_keeps_its_source_effects(self):
        careers, abilities, choices = self.files
        for row in eredluin_rows():
            src_id = row[0]
            _, _, built = ic.build("eredluin", *row, *self.files)
            source = ic.extract(choices, rf'  <Choice id="{src_id}_root".*?</Choice>', src_id)
            source += "".join(re.findall(rf'  <ChoiceGroup id="{src_id}_[^"]*".*?</ChoiceGroup>',
                                         choices, re.S))
            effects = re.compile(r"<PassiveEffect [^>]*/>")
            with self.subTest(career=row[2]):
                self.assertEqual(effects.findall(built), effects.findall(source))

    def test_generated_ered_luin_text_has_no_em_or_en_dash(self):
        for row in eredluin_rows():
            out = "\n".join(ic.build("eredluin", *row, *self.files))
            payload = re.sub(r"<!--.*?-->", "", out, flags=re.S)
            with self.subTest(career=row[2]):
                self.assertNotIn("\u2014", payload)
                self.assertNotIn("\u2013", payload)


class ShippedCareers(unittest.TestCase):
    def test_check_reports_nothing_pending_on_the_shipped_data(self):
        rc, out = run(["--check"])
        self.assertEqual(rc, 0, out)

    def test_ered_luin_has_three_careers_and_no_rider(self):
        rows = eredluin_rows()
        self.assertEqual(len(rows), 3)
        self.assertNotIn("ram_rider", [r[0] for r in rows])

    def test_every_ered_luin_career_is_eligible_for_ered_luin_alone(self):
        root = ET.parse(ic.CAREERS_XML).getroot()
        for row in eredluin_rows():
            career = root.find(f"./Career[@id='{row[2]}']")
            with self.subTest(career=row[2]):
                self.assertIsNotNone(career)
                self.assertEqual([c.get("id") for c in career.iter("Culture")], ["eredluin"])

    def test_no_ered_luin_career_passive_needs_a_mount(self):
        root = ET.parse(ic.CHOICES_XML).getroot()
        for row in eredluin_rows():
            for group in root.findall(f"./ChoiceGroup[@career_id='{row[2]}']"):
                for effect in group.iter("PassiveEffect"):
                    with self.subTest(career=row[2], group=group.get("id")):
                        self.assertFalse(effect.get("type", "").startswith("Mount"))

    def test_ered_luin_career_text_names_no_erebor_place_or_person(self):
        for row in eredluin_rows():
            for value in career_text(row[2]):
                for word in EREBOR_IDENTITY:
                    with self.subTest(career=row[2], word=word):
                        self.assertNotIn(word, value)

    def test_ered_luin_career_text_has_no_em_or_en_dash(self):
        for row in eredluin_rows():
            for value in career_text(row[2]):
                with self.subTest(career=row[2], value=value[:40]):
                    self.assertNotIn("\u2014", value)
                    self.assertNotIn("\u2013", value)

    def test_every_ered_luin_career_has_a_career_menu_entry(self):
        menu = json.loads(ic.MENU_JSON.read_text(encoding="utf-8"))
        ids = {e["career_string_id"] for e in menu}
        for row in eredluin_rows():
            self.assertIn(row[2], ids)

    def test_every_ered_luin_career_has_a_foot_archetype_in_the_ioc_map(self):
        # An unmapped career gets the no-op ability executor (a toast and nothing else), and a
        # Cavalry row would hand a dwarf the mounted kit roster.
        rows = dict(re.findall(r'\["([a-z_]+)"\]\s*=\s*CareerArchetype\.(\w+)',
                               IOC.read_text(encoding="utf-8")))
        expected = {"ironguard": "Infantry", "crossbow_master": "Ranged"}
        for row in eredluin_rows():
            with self.subTest(career=row[2]):
                self.assertEqual(rows.get(row[2]), expected[row[0]])


class DryRunAndApply(unittest.TestCase):
    """The tool on temp copies of the four files without Ered Luin's three careers, each written
    with the tool's own byte-faithful read() and write()."""

    def setUp(self):
        tmp = Path(tempfile.mkdtemp(prefix="careers_"))
        self.addCleanup(shutil.rmtree, tmp, True)
        self.ids = [row[2] for row in eredluin_rows()]
        alt = "|".join(self.ids)
        drop = {
            "CAREERS_XML": [rf'\n\n  <Career id="(?:{alt})".*?</Career>'],
            "ABILITIES_XML": [rf'\n  <AbilityTemplate id="(?:{alt})_ability".*?/>'],
            "CHOICES_XML": [r"\n\n  <!-- [^\n]*\(eredluin\), cloned from [^\n]*-->",
                            rf'\n  <Choice id="(?:{alt})_root".*?</Choice>',
                            rf'\n\n  <ChoiceGroup id="(?:{alt})_[^"]*".*?</ChoiceGroup>'],
        }
        self.paths = {}
        for name in FILES:
            text, nl = ic.read(getattr(ic, name))
            if name == "MENU_JSON":
                menu = [e for e in json.loads(text) if e["career_string_id"] not in self.ids]
                text = json.dumps(menu, indent=4, ensure_ascii=False) + "\n"
            else:
                for pattern in drop[name]:
                    text = re.sub(pattern, "", text, flags=re.S)
                ET.fromstring(text.encode("utf-8"))
                for new_id in self.ids:
                    self.assertNotIn(f'id="{new_id}', text, f"{name}: the fixture kept {new_id}")
            self.paths[name] = tmp / getattr(ic, name).name
            ic.write(self.paths[name], text, nl)
        self.assertNotIn('<Culture id="eredluin" />', ic.read(self.paths["CAREERS_XML"])[0])
        for name in FILES:
            patch = mock.patch.object(ic, name, self.paths[name])
            patch.start()
            self.addCleanup(patch.stop)

    def snapshot(self):
        return {name: path.read_bytes() for name, path in self.paths.items()}

    def test_check_fails_and_a_dry_run_writes_nothing_while_a_culture_lacks_its_careers(self):
        before = self.snapshot()
        rc, out = run(["--check"])
        self.assertEqual(rc, 1, out)
        self.assertEqual(before, self.snapshot())
        rc, out = run([])
        self.assertEqual(rc, 0, out)
        self.assertIn("DRY RUN", out)
        self.assertEqual(before, self.snapshot())

    def test_apply_adds_each_career_to_all_four_files_and_a_rerun_changes_nothing(self):
        rc, out = run(["--apply"])
        self.assertEqual(rc, 0, out)
        careers = ic.read(self.paths["CAREERS_XML"])[0]
        abilities = ic.read(self.paths["ABILITIES_XML"])[0]
        choices = ic.read(self.paths["CHOICES_XML"])[0]
        menu = {e["career_string_id"] for e in json.loads(ic.read(self.paths["MENU_JSON"])[0])}
        for new_id in self.ids:
            with self.subTest(career=new_id):
                self.assertIn(f'<Career id="{new_id}"', careers)
                self.assertIn(f'<AbilityTemplate id="{new_id}_ability"', abilities)
                self.assertIn(f'<Choice id="{new_id}_root"', choices)
                self.assertIn(new_id, menu)
        once = self.snapshot()
        rc, out = run(["--check"])
        self.assertEqual(rc, 0, out)
        run(["--apply"])
        self.assertEqual(once, self.snapshot(), "a second --apply changed the files")

    def test_a_missing_splice_anchor_is_refused_in_every_mode_and_nothing_is_written(self):
        # An indented closing tag still parses, but str.replace finds no "\n</AbilityTemplates>" and
        # returns the text unchanged, so the parse alone could not see the lost splice.
        text, nl = ic.read(self.paths["ABILITIES_XML"])
        ic.write(self.paths["ABILITIES_XML"],
                 text.replace("\n</AbilityTemplates>", "\n  </AbilityTemplates>"), nl)
        before = self.snapshot()
        for argv in (["--apply"], ["--check"], []):
            with self.subTest(argv=argv):
                with self.assertRaises(SystemExit) as raised:
                    run(argv)
                self.assertIn(ic.ABILITIES_XML.name, str(raised.exception))
                self.assertEqual(before, self.snapshot())

    def test_a_splice_that_breaks_a_file_is_refused_and_nothing_is_written(self):
        real_build = ic.build

        def broken_build(*args):
            career, ability, choices = real_build(*args)
            return career + "\n<broken", ability, choices

        before = self.snapshot()
        with mock.patch.object(ic, "build", side_effect=broken_build):
            with self.assertRaises(ET.ParseError):
                run(["--apply"])
        self.assertEqual(before, self.snapshot())


if __name__ == "__main__":
    unittest.main()
