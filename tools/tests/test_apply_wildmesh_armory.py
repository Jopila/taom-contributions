#!/usr/bin/env python3
"""Unit tests for the WildMesh3D mounts armory writer (tools/apply_wildmesh_armory.py, #794).

Pure stdlib, synthetic game dir, no real install needed (--game-dir points the whole script at a temp tree).
Sibling: test_apply_animalia_armory.py. Every case builds its own pristine tree, because the script mutates
files in place and a shared fixture would let one test's writes leak into the next.
"""
import hashlib
import io
import os
import re
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from contextlib import ExitStack, redirect_stderr, redirect_stdout
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import apply_wildmesh_armory as wm  # noqa: E402

BOM = b"\xef\xbb\xbf"
REPO = Path(__file__).resolve().parents[2]
SNAPSHOT_SETS = REPO / "docs" / "reference" / "lotrlome-armory-snapshot" / "action_sets.xml"
ALL_HORSE_ACTIONS = sorted({action for overrides in wm.OVERRIDES.values() for action in overrides})

ARMORY_ACTION_SETS = (
    "<action_sets>\r\n"
    "\t<!-- ============================== WARG ============================== -->\r\n"
    "\t<action_set id=\"as_warg\" skeleton=\"warg_skeleton\" base_set=\"as_horse\" />\r\n"
    "\t<!-- ============================== /ANIMALIA ============================== -->\r\n"
    "\t<!-- ============================== /WARG ============================== -->\r\n"
    "</action_sets>\r\n"
)
ARMORY_HORSES = (
    "<Items>\r\n"
    "  <Item\r\n"
    "      id=\"taom_animalia_moose_a\"\r\n"
    "      name=\"{=taom_animalia_moose_a}Mirkwood Moose\"\r\n"
    "      mesh=\"animalia_moose_big\"\r\n"
    "      Type=\"Horse\">\r\n"
    "      <ItemComponent><Horse monster=\"Monster.taom_animalia_moose\" body_length=\"100\" /></ItemComponent>\r\n"
    "  </Item>\r\n"
    "</Items>\r\n"
)
ARMORY_SUBMODULE = (
    "<Module>\r\n"
    "\t<Xmls>\r\n"
    "\t\t<XmlNode>\r\n"
    "\t\t\t<XmlName id=\"Monsters\" path=\"Monsters/LOTR/lotr_monster_animalia\"/>\r\n"
    "\t\t\t<IncludedGameTypes>\r\n"
    "\t\t\t\t<GameType value = \"Campaign\"/>\r\n"
    "\t\t\t</IncludedGameTypes>\r\n"
    "\t\t</XmlNode>\r\n"
    "\t</Xmls>\r\n"
    "</Module>\r\n"
)
ARMORY_LOC = (
    "<?xml version='1.0' encoding='utf-8'?>\r\n"
    "<base>\r\n"
    "  <strings>\r\n"
    "    <string id=\"taom_animalia_elk_a\" text=\"Mirkwood Elk\"/>\r\n"
    "    <string id=\"taom_animalia_moose_a\" text=\"Mirkwood Moose\"/>\r\n"
    "    <string id=\"warg_brown\" text=\"[Isengard] Warg\"/>\r\n"
    "  </strings>\r\n"
    "</base>\r\n"
)
SUB = "Modules/LOTRLOME_Armory/SubModule.xml"
SETS = "Modules/LOTRLOME_Armory/ModuleData/action_sets.xml"
HORSES = "Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml"
MONS = "Modules/LOTRLOME_Armory/ModuleData/Monsters/LOTR/lotr_monster_wildmesh.xml"
LOC = "Modules/LOTRLOME_Armory/ModuleData/Languages/loc_LOTRAOM_horses.xml"
NATIVE = "Modules/Native/ModuleData/action_sets.xml"
TRACKED = [SUB, SETS, HORSES, MONS, LOC]
EDITED = [SUB, SETS, HORSES, LOC]
# sha256 of today's MONSTER_FILE (utf-8), taken from the shipped writer before the table-driven renderer
MONSTER_FILE_SHA256 = "e468a4e33e0d36b51fa562373f9e27698e52fe3ded79b6bce519dcee86ca526f"


def _native_action_sets_xml(actions=ALL_HORSE_ACTIONS):
    rows = "".join('\t\t<action type="%s" animation="horse_template_%d" alternative_group="%d" />\n' % (a, i, i % 3)
                   for i, a in enumerate(actions))
    return ("<action_sets>\n\t<action_set id=\"as_horse\" skeleton=\"horse_skeleton\">\n" + rows
            + "\t</action_set>\n</action_sets>\n")


def _write_bom_crlf(path: Path, text: str, bom=True):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes((BOM if bom else b"") + text.encode("utf-8"))


def _add_clips(root: Path, clips):
    armory = root / "Modules" / "LOTRLOME_Armory"
    for clip in clips:
        animal = clip.split("_")[2]
        d = armory / "Assets" / "creature" / "creatures" / "animations" / animal
        d.mkdir(parents=True, exist_ok=True)
        (d / (clip + "_anm.tpac")).write_bytes(b"placeholder")


def _build(root: Path, clips=None, native_actions=ALL_HORSE_ACTIONS, nl="\r\n"):
    native = root / NATIVE
    native.parent.mkdir(parents=True, exist_ok=True)
    native.write_text(_native_action_sets_xml(native_actions), encoding="utf-8")
    armory = root / "Modules" / "LOTRLOME_Armory"
    _write_bom_crlf(armory / "ModuleData" / "Monsters" / "LOTR" / "lotr_monster_animalia.xml",
                    "<Monsters>\r\n\t<Monster id=\"taom_animalia_moose\" base_monster=\"horse\" />\r\n</Monsters>\r\n")
    _write_bom_crlf(root / SUB, ARMORY_SUBMODULE)
    _write_bom_crlf(root / SETS, ARMORY_ACTION_SETS)
    _write_bom_crlf(root / HORSES, ARMORY_HORSES)
    _write_bom_crlf(root / LOC, ARMORY_LOC, bom=False)
    if nl == "\n":
        for rel in EDITED:
            p = root / rel
            p.write_bytes(p.read_bytes().replace(b"\r\n", b"\n"))
    _add_clips(root, wm.wanted_clips() if clips is None else clips)


def _snapshot(root: Path):
    return {rel: ((root / rel).read_bytes() if (root / rel).is_file() else None) for rel in TRACKED}


def _backups(root: Path):
    return sorted(str(p.relative_to(root)) for p in (root / "Modules" / "LOTRLOME_Armory").rglob("*") if ".bak" in p.name)


def _text(root: Path, rel):
    return (root / rel).read_bytes().decode("utf-8")


def _parses(text):
    return ET.fromstring(text.lstrip("﻿")) is not None


def _is_pure_insertion(old, new):
    """Deleting one contiguous span of `new` gives `old` back."""
    if old == new:
        return True
    d = len(new) - len(old)
    if d <= 0:
        return False
    i = next((x for x in range(len(old)) if old[x] != new[x]), len(old))
    return new[:i] + new[i + d:] == old


def _set_sig(elem):
    return dict(elem.attrib), [dict(c.attrib) for c in elem]


class CliTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def _run(self, *argv, running=False):
        buf = io.StringIO()
        with redirect_stdout(buf), mock.patch.object(wm, "game_or_kit_running", return_value=running):
            code = wm.main(list(argv) + ["--game-dir", str(self.root)])
        return code, buf.getvalue()

    def _exit_code(self, *argv):
        with self.assertRaises(SystemExit) as cm, redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()), \
                mock.patch.object(wm, "game_or_kit_running", return_value=False):
            wm.main(list(argv) + ["--game-dir", str(self.root)])
        return cm.exception.code

    def _applied(self):
        _build(self.root)
        code, out = self._run("--apply")
        self.assertEqual(code, 0, out)

    def _edit(self, rel, old, new):
        p = self.root / rel
        data = p.read_bytes()
        self.assertIn(old.encode(), data, old)
        p.write_bytes(data.replace(old.encode(), new.encode()))

    def _assert_refused_unchanged(self, expect, *argv):
        before, backups = _snapshot(self.root), _backups(self.root)
        code, out = self._run(*(argv or ("--apply",)))
        self.assertEqual(code, 1, out)
        self.assertIn(expect, out)
        self.assertEqual(before, _snapshot(self.root))
        self.assertEqual(backups, _backups(self.root), "a refused run wrote a backup")
        return out

    def _sixth(self, stack):
        boar_item = next(s for s in wm.ITEM_STATS if s["animal"] == "boar")
        stack.enter_context(mock.patch.object(wm, "ANIMALS", wm.ANIMALS + ("wildboar",)))
        stack.enter_context(mock.patch.object(wm, "MONSTERS", dict(wm.MONSTERS, wildboar=wm.MONSTERS["boar"])))
        stack.enter_context(mock.patch.object(wm, "ITEM_STATS", wm.ITEM_STATS + (dict(boar_item, animal="wildboar", name="Wild Boar"),)))
        stack.enter_context(mock.patch.object(wm, "OVERRIDES", dict(
            wm.OVERRIDES, wildboar={k: v.replace("_boar_", "_wildboar_") for k, v in wm.OVERRIDES["boar"].items()})))
        _add_clips(self.root, wm.wanted_clips())

    def test_dry_run_reports_five_steps_and_writes_nothing(self):
        _build(self.root)
        before = _snapshot(self.root)
        code, out = self._run()
        self.assertEqual(code, 0, out)
        for marker in ("Monsters: new lotr_monster_wildmesh.xml", "SubModule.xml: Monsters block inserted",
                       "action_sets.xml: as_wildmesh_boar", "LOTRAOM_horses.xml: 5 items",
                       "loc_LOTRAOM_horses.xml: 5 English rows", "dry run"):
            self.assertIn(marker, out)
        self.assertEqual(before, _snapshot(self.root))
        self.assertEqual(_backups(self.root), [])

    def test_monster_file_text_is_pinned(self):
        self.assertEqual(hashlib.sha256(wm.MONSTER_FILE.encode("utf-8")).hexdigest(), MONSTER_FILE_SHA256)
        ids = re.findall(r'id="(taom_wildmesh_\w+)"', wm.MONSTER_FILE)
        self.assertEqual(ids, ["taom_wildmesh_" + a for a in wm.ANIMALS])

    def test_apply_writes_the_approved_recipe_and_a_second_apply_changes_no_byte(self):
        _build(self.root)
        code, out = self._run("--apply")
        self.assertEqual(code, 0, out)
        md = self.root / "Modules/LOTRLOME_Armory/ModuleData"
        monsters = (md / "Monsters/LOTR/lotr_monster_wildmesh.xml").read_text(encoding="utf-8")
        sizes = dict(re.findall(r'id="(taom_wildmesh_\w+)"[^/]*?taom_body_length="(\d+)"', monsters, re.S))
        self.assertEqual(sizes, {"taom_wildmesh_boar": "100", "taom_wildmesh_warthog": "100",
                                 "taom_wildmesh_mountaingoat": "100", "taom_wildmesh_deer": "100",
                                 "taom_wildmesh_moose": "150"})
        horses = (md / "LOTRLOME_items/LOTRAOM_horses.xml").read_bytes()
        self.assertTrue(horses.startswith(BOM))
        text = horses.decode("utf-8")
        self.assertNotRegex(text, r"(?<!\r)\n", "a bare LF entered a CRLF file")
        for animal, name in (("boar", "War Boar"), ("warthog", "Bristleback Boar"), ("mountaingoat", "Iron Hills Goat"),
                             ("deer", "Mirkwood Stag"), ("moose", "Greenwood Moose")):
            m = re.search(r'<Item\s+id="taom_wildmesh_%s_a".*?</Item>' % animal, text, re.S)
            self.assertIsNotNone(m, animal)
            self.assertIn('name="{=taom_wildmesh_%s_a}%s"' % (animal, name), m.group(0))
            self.assertIn('mesh="wildmesh_%s"' % animal, m.group(0))
            self.assertIn('monster="Monster.taom_wildmesh_%s"' % animal, m.group(0))
            self.assertIn('body_length="100"', m.group(0), "the placeholder Items.xsd requires")
        before = _snapshot(self.root)
        code2, out2 = self._run("--apply")
        self.assertEqual(code2, 0, out2)
        self.assertIn("nothing to write", out2)
        self.assertEqual(before, _snapshot(self.root))

    def test_borrowed_gaits_bind_the_lenders_clips(self):
        _build(self.root)
        self.assertEqual(self._run("--apply")[0], 0)
        sets = (self.root / SETS).read_text(encoding="utf-8")

        def body(sid):
            return re.search(r'<action_set id="%s" [^>]*>(.*?)</action_set>' % sid, sets, re.S).group(1)
        self.assertIn('type="act_horse_forward_canter" animation="anim_wildmesh_boar_runslow_f"', body("as_wildmesh_warthog"))
        self.assertIn('type="act_horse_forward_gallop_left_foot" animation="anim_wildmesh_boar_runfast_f"', body("as_wildmesh_warthog"))
        self.assertIn('type="act_horse_forward_gallop_right_foot_stand" animation="anim_wildmesh_deer_runfast_f_stand"',
                      body("as_wildmesh_mountaingoat"))
        for animal in wm.ANIMALS:
            for twin in ("_town_and_village", "_map"):
                self.assertIn('id="as_wildmesh_%s%s"' % (animal, twin), sets)

    def test_every_override_of_every_animal_keeps_the_vanilla_attributes(self):
        _build(self.root)
        self.assertEqual(self._run("--apply")[0], 0)
        native = ET.parse(self.root / NATIVE).getroot()
        vanilla = {a.get("type"): dict(a.attrib) for s in native.iter("action_set") if s.get("id") == "as_horse"
                   for a in s.iter("action")}
        written = {s.get("id"): s for s in ET.fromstring(_text(self.root, SETS).lstrip("﻿")).iter("action_set")}
        for animal, overrides in wm.OVERRIDES.items():
            with self.subTest(animal=animal):
                actions = {a.get("type"): dict(a.attrib) for a in written["as_wildmesh_" + animal].iter("action")}
                self.assertEqual(sorted(actions), sorted(overrides))
                for atype, clip in overrides.items():
                    got, want = dict(actions[atype]), dict(vanilla[atype])
                    self.assertEqual(got.pop("animation"), clip, atype)
                    want.pop("animation")
                    self.assertEqual(got, want, atype)

    def test_a_missing_clip_refuses_and_writes_nothing(self):
        gone = "anim_wildmesh_deer_runfast_f"
        _build(self.root, clips=wm.wanted_clips() - {gone})
        self._assert_refused_unchanged("CLIP MISSING: %s" % gone)

    def test_an_override_that_is_not_an_as_horse_action_refuses(self):
        _build(self.root, native_actions=[a for a in ALL_HORSE_ACTIONS if a != "act_horse_strike_front"])
        self._assert_refused_unchanged("NOT AN ACTION: ")

    def test_missing_anchors_refuse_and_name_what_is_missing(self):
        for rel, old, expect in (
                (SUB, "lotr_monster_animalia", "lotr_monster_animalia"),
                (SETS, " /WARG ", "/WARG"),
                (HORSES, 'id="taom_animalia_moose_a"', "taom_animalia_moose_a"),
                (LOC, 'id="taom_animalia_moose_a"', "taom_animalia_moose_a")):
            with self.subTest(rel=rel):
                self.tearDown()
                self.setUp()
                _build(self.root)
                p = self.root / rel
                p.write_bytes(p.read_bytes().replace(old.encode(), b"gone_for_the_test"))
                self._assert_refused_unchanged(expect)

    def test_an_item_deleted_by_hand_is_put_back(self):
        self._applied()
        p = self.root / HORSES
        text = p.read_bytes().decode("utf-8")
        gone = re.search(r'\r\n\s*<Item\s+id="taom_wildmesh_moose_a".*?</Item>', text, re.S)
        p.write_bytes((text[:gone.start()] + text[gone.end():]).encode("utf-8"))
        self.assertNotIn("taom_wildmesh_moose_a", _text(self.root, HORSES))
        code, out = self._run("--apply")
        self.assertEqual(code, 0, out)
        back = _text(self.root, HORSES)
        self.assertIn('id="taom_wildmesh_moose_a"', back)
        self.assertNotRegex(back, r"(?<!\r)\n")
        self.assertTrue(_parses(back))
        before = _snapshot(self.root)
        code2, out2 = self._run("--apply")
        self.assertEqual(code2, 0, out2)
        self.assertIn("nothing to write", out2)
        self.assertEqual(before, _snapshot(self.root))

    def test_an_animal_partly_present_in_one_file_is_refused_naming_the_missing_id(self):
        self._applied()
        self._edit(SETS, 'id="as_wildmesh_moose_map"', 'id="as_gone_moose_map"')
        self._assert_refused_unchanged("as_wildmesh_moose_map", "--apply")

    def test_a_monsters_file_with_no_closing_tag_is_refused_without_a_restore_hint(self):
        for label, content in (("0 bytes", b""), ("self-closing", b"<Monsters />\n")):
            with self.subTest(label):
                self.tearDown()
                self.setUp()
                _build(self.root)
                (self.root / MONS).write_bytes(content)
                out = self._assert_refused_unchanged("lotr_monster_wildmesh.xml")
                self.assertIn("delete", out)
                self.assertNotIn("restore", out.lower())

    def test_a_monsters_file_missing_one_animal_gets_it_inserted(self):
        self._applied()
        p = self.root / MONS
        text = p.read_bytes().decode("utf-8")
        gone = re.search(r'\t<Monster\s+id="taom_wildmesh_moose".*?/>\n', text, re.S)
        p.write_bytes((text[:gone.start()] + text[gone.end():]).encode("utf-8"))
        code, out = self._run("--apply")
        self.assertEqual(code, 0, out)
        back = _text(self.root, MONS)
        self.assertEqual(sorted(re.findall(r'id="(taom_wildmesh_\w+)"', back)), sorted("taom_wildmesh_" + a for a in wm.ANIMALS))
        self.assertTrue(_parses(back))
        before = _snapshot(self.root)
        code2, out2 = self._run("--apply")
        self.assertEqual(code2, 0, out2)
        self.assertIn("nothing to write", out2)
        self.assertEqual(before, _snapshot(self.root))

    def test_a_sixth_animal_is_one_pure_insertion_per_file_on_an_applied_tree(self):
        self._applied()
        before = _snapshot(self.root)
        with ExitStack() as stack:
            self._sixth(stack)
            code, out = self._run("--apply")
            self.assertEqual(code, 0, out)
            after = _snapshot(self.root)
            self.assertEqual(before[SUB], after[SUB], "SubModule.xml must stay untouched")
            for rel in (MONS, SETS, HORSES, LOC):
                with self.subTest(rel=rel):
                    old, new = before[rel].decode("utf-8"), after[rel].decode("utf-8")
                    self.assertNotEqual(old, new)
                    self.assertTrue(_is_pure_insertion(old, new), "more than one insertion")
                    self.assertTrue(_parses(new))
                    self.assertIn("wildboar", new)
            self.assertIn('text="Wild Boar"', after[LOC].decode("utf-8"))
            code2, out2 = self._run("--apply")
            self.assertEqual(code2, 0, out2)
            self.assertIn("nothing to write", out2)
            self.assertEqual(after, _snapshot(self.root))

    def test_a_new_run_backs_up_under_its_own_stamp_and_never_overwrites_a_backup(self):
        _build(self.root)
        with mock.patch.object(wm, "backup_suffix", return_value=".bak-wildmesh-20261015-100000"):
            self.assertEqual(self._run("--apply")[0], 0)
        applied = _snapshot(self.root)
        first = {b: (self.root / b).read_bytes() for b in _backups(self.root)}
        with ExitStack() as stack:
            self._sixth(stack)
            stack.enter_context(mock.patch.object(wm, "backup_suffix", return_value=".bak-wildmesh-20261015-110000"))
            self.assertEqual(self._run("--apply")[0], 0)
        for b, data in first.items():
            self.assertEqual((self.root / b).read_bytes(), data, "an earlier run's backup was touched")
        for rel in (SETS, HORSES, LOC, MONS):
            self.assertEqual((self.root / (rel + ".bak-wildmesh-20261015-110000")).read_bytes(), applied[rel], rel)

    def test_apply_backs_up_each_edited_file_once_under_a_per_run_stamp(self):
        _build(self.root)
        pristine = _snapshot(self.root)
        self.assertEqual(self._run("--apply")[0], 0)
        for rel in EDITED:
            found = list((self.root / rel).parent.glob(Path(rel).name + ".bak-wildmesh-*"))
            self.assertEqual(len(found), 1, rel)
            self.assertRegex(found[0].name, r"\.bak-wildmesh-\d{8}-\d{6}$")
            self.assertEqual(found[0].read_bytes(), pristine[rel], rel)
        self.assertEqual(len(_backups(self.root)), len(EDITED), "the new Monsters file needs no backup")
        taken = _backups(self.root)
        self.assertEqual(self._run("--apply")[0], 0)
        self.assertEqual(taken, _backups(self.root))

    def test_apply_refuses_while_the_game_or_kit_runs(self):
        _build(self.root)
        before = _snapshot(self.root)
        code, out = self._run("--apply", running=True)
        self.assertEqual(code, 2, out)
        self.assertEqual(before, _snapshot(self.root))

    def test_an_empty_game_dir_exits_2(self):
        buf = io.StringIO()
        with redirect_stdout(buf), mock.patch.object(wm, "game_dir", side_effect=AssertionError("fell back")):
            self.assertEqual(wm.main(["--game-dir", ""]), 2)

    def test_a_game_dir_without_the_armory_exits_2(self):
        self.assertEqual(self._exit_code(), 2)

    def test_the_dry_run_never_asks_whether_the_game_runs(self):
        _build(self.root)
        buf = io.StringIO()
        with redirect_stdout(buf), mock.patch.object(wm, "game_or_kit_running",
                                                     side_effect=AssertionError("guard called on a read-only path")):
            code = wm.main(["--game-dir", str(self.root)])
        self.assertEqual(code, 0, buf.getvalue())

    def test_an_apply_names_every_file_it_wrote(self):
        _build(self.root)
        code, out = self._run("--apply")
        self.assertEqual(code, 0, out)
        self.assertIn("APPLIED 5 file(s)", out)
        for name in ("lotr_monster_wildmesh.xml", "action_sets.xml", "LOTRAOM_horses.xml", "SubModule.xml",
                     "loc_LOTRAOM_horses.xml"):
            self.assertIn(name, out)

    def test_every_edited_file_keeps_its_own_line_endings(self):
        for nl in ("\r\n", "\n"):
            with self.subTest(nl=repr(nl)):
                self.tearDown()
                self.setUp()
                _build(self.root, nl=nl)
                pristine = _snapshot(self.root)
                self.assertEqual(self._run("--apply")[0], 0)
                for rel in EDITED:
                    data = (self.root / rel).read_bytes()
                    self.assertEqual(data.startswith(BOM), pristine[rel].startswith(BOM), rel)
                    text = data.decode("utf-8")
                    if nl == "\r\n":
                        self.assertNotRegex(text, r"(?<!\r)\n", "%s gained a bare LF" % rel)
                    else:
                        self.assertNotIn("\r", text, "%s gained a CR" % rel)

    def test_a_malformed_action_sets_file_gives_parse_failed_and_writes_nothing(self):
        _build(self.root)
        self._edit(SETS, "<action_sets>", "<action_sets><broken")
        self._assert_refused_unchanged("PARSE FAILED: action_sets.xml")

    def test_a_missing_input_exits_2(self):
        for rel in (HORSES, SETS, SUB, NATIVE, LOC):
            with self.subTest(rel=rel):
                self.tearDown()
                self.setUp()
                _build(self.root)
                (self.root / rel).unlink()
                before = _snapshot(self.root)
                self.assertEqual(self._exit_code("--apply"), 2)
                self.assertEqual(before, _snapshot(self.root))

    def test_a_registration_that_only_extends_the_token_still_gets_the_insert(self):
        _build(self.root)
        self._edit(SUB, '</XmlNode>', '</XmlNode>\r\n\t\t<XmlNode><XmlName id="Monsters" '
                                      'path="Monsters/LOTR/lotr_monster_wildmesh_extra"/></XmlNode>')
        code, out = self._run("--apply")
        self.assertEqual(code, 0, out)
        self.assertEqual(_text(self.root, SUB).count('path="Monsters/LOTR/lotr_monster_wildmesh"'), 1)
        self.assertIn("lotr_monster_wildmesh_extra", _text(self.root, SUB))

    def test_a_duplicated_animalia_anchor_is_refused(self):
        _build(self.root)
        node = re.search(r"\t\t<XmlNode>.*?</XmlNode>\r\n", _text(self.root, SUB), re.S).group(0)
        self._edit(SUB, node, node + node)
        self._assert_refused_unchanged("exactly once")

    def test_the_loc_step_writes_the_item_defaults_after_the_animalia_moose_and_is_idempotent(self):
        _build(self.root)
        self.assertEqual(self._run("--apply")[0], 0)
        text = _text(self.root, LOC)
        rows = {r.get("id"): r.get("text") for r in ET.fromstring(text).iter("string")}
        for s in wm.ITEM_STATS:
            self.assertEqual(rows["taom_wildmesh_%s_a" % s["animal"]], s["name"])
            self.assertEqual(re.search(r'name="\{=[^}]+\}([^"]+)"', wm._ITEM % s).group(1), s["name"])
        self.assertRegex(text, r'taom_animalia_moose_a" text="Mirkwood Moose"/>\r\n    <string id="taom_wildmesh_boar_a"')
        before = _snapshot(self.root)
        code, out = self._run("--apply")
        self.assertEqual(code, 0, out)
        self.assertIn("nothing to write", out)
        self.assertEqual(before, _snapshot(self.root))


class LiveRecipeParityTests(unittest.TestCase):
    """A replay of this script must not revert a live hand edit, and a reinstall that drops the WildMesh data
    must fail here (RCA rca-wildmesh-mounts-2026-10-15.md row 3). Skipped only where the LOTRLOME_Armory folder
    itself is absent (CI, the laptop)."""

    def setUp(self):
        root = Path(os.environ.get("BANNERLORD_GAME_DIR") or wm._DEFAULT_GAME_DIR)
        self.root = root
        self.armory = root / "Modules" / "LOTRLOME_Armory"
        self.md = self.armory / "ModuleData"
        if not self.armory.is_dir():
            self.skipTest("LOTRLOME_Armory is not installed here")

    def _live_sets(self, path):
        return {s.get("id"): s for s in ET.parse(path).getroot().iter("action_set")}

    def test_the_monsters_file_exists_and_matches_the_recipe(self):
        path = self.md / "Monsters" / "LOTR" / "lotr_monster_wildmesh.xml"
        self.assertTrue(path.is_file(), "the WildMesh Monsters file is missing from the live Armory")
        recipe = {m.get("id"): m.attrib for m in ET.fromstring(wm.MONSTER_FILE.split("?>", 1)[1]).iter("Monster")}
        live = {m.get("id"): m.attrib for m in ET.parse(path).getroot().iter("Monster")}
        self.assertEqual(recipe, live)

    def test_the_items_match_the_recipe(self):
        items = {i.get("id"): i for i in ET.fromstring("<Items>" + wm.ITEMS + "</Items>").iter("Item")}
        live_items = {i.get("id"): i for i in ET.parse(self.md / "LOTRLOME_items" / "LOTRAOM_horses.xml").getroot().iter("Item")}
        for iid, item in items.items():
            self.assertIn(iid, live_items, "%s is missing from the live Armory" % iid)
            self.assertEqual(dict(item.attrib), dict(live_items[iid].attrib), iid)
            self.assertEqual(dict(item.find(".//Horse").attrib), dict(live_items[iid].find(".//Horse").attrib), iid)

    def test_every_action_set_and_twin_matches_the_recipe(self):
        native = self.root / "Modules" / "Native" / "ModuleData" / "action_sets.xml"
        self.assertTrue(native.is_file(), "the Native action_sets.xml is missing")
        problems = []
        lines, _ = wm.action_set_lines(wm.ANIMALS, wm.native_vanilla(native, problems))
        self.assertEqual(problems, [])
        recipe = {s.get("id"): s for s in ET.fromstring("<r>" + "".join(lines) + "</r>").iter("action_set")}
        live = self._live_sets(self.md / "action_sets.xml")
        for sid, want in recipe.items():
            self.assertIn(sid, live, "%s is missing from the live action_sets.xml" % sid)
            self.assertEqual(_set_sig(live[sid]), _set_sig(want), sid)

    def test_the_submodule_registers_the_monsters_file(self):
        text = (self.armory / "SubModule.xml").read_bytes().decode("utf-8")
        self.assertIn(wm.SUBMODULE_TOKEN, text)

    def test_every_bound_clip_exists(self):
        clips = set()
        for _, _, files in os.walk(self.armory / "Assets" / "creature" / "creatures" / "animations"):
            clips.update(f[:-len("_anm.tpac")] for f in files if f.endswith("_anm.tpac"))
        self.assertEqual(sorted(wm.wanted_clips() - clips), [])

    def test_the_english_loc_rows_carry_the_item_defaults(self):
        path = self.md / "Languages" / "loc_LOTRAOM_horses.xml"
        self.assertTrue(path.is_file(), "the English horses loc file is missing")
        rows = {r.get("id"): r.get("text") for r in ET.parse(path).getroot().iter("string")}
        for s in wm.ITEM_STATS:
            rid = "taom_wildmesh_%s_a" % s["animal"]
            self.assertIn(rid, rows, "%s has no English row" % rid)
            self.assertEqual(rows[rid], s["name"], rid)

    def test_the_repo_snapshot_carries_every_live_wildmesh_set(self):
        live = self._live_sets(self.md / "action_sets.xml")
        snap = self._live_sets(SNAPSHOT_SETS)
        wanted = sorted(sid for sid in live if sid.startswith("as_wildmesh_"))
        self.assertTrue(wanted, "the live action_sets.xml holds no as_wildmesh_ set")
        for sid in wanted:
            self.assertIn(sid, snap, "%s is missing from the repo snapshot" % sid)
            self.assertEqual(_set_sig(snap[sid]), _set_sig(live[sid]), sid)


if __name__ == "__main__":
    unittest.main(verbosity=2)
