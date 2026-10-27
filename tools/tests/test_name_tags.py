"""Tests for tools/name_tags.py: the grammar-tag step for TAOM's Portuguese and Italian names (#1023).

The table is synthetic in the unit tests; ShippedTableTests read the tracked tools/translation_name_tags.json.
"""

import contextlib
import io
import os
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import name_tags as nt  # noqa: E402


def table():
    return {
        "version": 1,
        "languages": {
            "BR": {
                "plain": "{.NS}",
                "allowed": ["{.NS}", "{.MS}", "{.FS}", "{.MP}", "{.FP}"],
                "head_nouns": {"Aldeia": "{.FS}", "Reino": "{.MS}", "Alto Reino": "{.MS}", "Gobelins": "{.MP}"},
                "proper_prefixes": [],
                "pins": {},
                "tags": {"k_erebor": "{.NS}", "k_aldeia": "{.FS}", "k_amp": "{.NS}"},
            },
            "IT": {
                "plain": "{.FN}",
                "allowed": ["{.FN}", "{.MN}", "{.MS}", "{.FS}", "{.MP}", "{.FP}"],
                "head_nouns": {"Villaggio": "{.MS}"},
                "proper_prefixes": [],
                "pins": {},
                "tags": {"k_erebor": "{.FN}"},
            },
        },
    }


def xml_doc(rows, eol="\r\n"):
    lines = ['<?xml version="1.0" encoding="utf-8"?>', "<base>", "  <strings>"]
    lines += [f'    <string id="{i}" text="{t}" />' for i, t in rows]
    lines += ["  </strings>", "</base>", ""]
    return eol.join(lines)


class TagTextTests(unittest.TestCase):
    def test_with_tag_PlainName_GetsLeadingTag(self):
        self.assertEqual(nt.with_tag("Erebor", "{.NS}"), "{.NS}Erebor")

    def test_with_tag_AlreadyTagged_IsUnchanged(self):
        self.assertEqual(nt.with_tag("{.NS}Erebor", "{.NS}"), "{.NS}Erebor")

    def test_with_tag_DifferentLeadingTag_IsReplacedNotStacked(self):
        self.assertEqual(nt.with_tag("{.MS}Erebor", "{.NS}"), "{.NS}Erebor")

    def test_with_tag_Twice_NeverDoubleTags(self):
        once = nt.with_tag("Casa de Bard", "{.FS}")
        self.assertEqual(nt.with_tag(once, "{.FS}"), once)
        self.assertEqual(once.count("{.FS}"), 1)

    def test_strip_leading_tags_RemovesEveryLeadingTag(self):
        self.assertEqual(nt.strip_leading_tags("{.MP}{.MS}Erebor"), "Erebor")

    def test_strip_leading_tags_LeavesInnerBraces(self):
        self.assertEqual(nt.strip_leading_tags("Reino de {NAME}"), "Reino de {NAME}")


class HeadNounTests(unittest.TestCase):
    def test_head_noun_tag_FirstWord_Matches(self):
        self.assertEqual(nt.head_noun_tag("BR", "Aldeia do Castelo", table()), ("Aldeia", "{.FS}"))

    def test_head_noun_tag_LongestPhraseWins(self):
        self.assertEqual(nt.head_noun_tag("BR", "Alto Reino de Lindon", table()), ("Alto Reino", "{.MS}"))

    def test_head_noun_tag_IsCaseInsensitive(self):
        self.assertEqual(nt.head_noun_tag("BR", "aldeia X", table()), ("Aldeia", "{.FS}"))

    def test_head_noun_tag_RequiresWholeWord(self):
        self.assertIsNone(nt.head_noun_tag("BR", "Aldeiaxyz", table()))

    def test_head_noun_tag_NounJoinedByHyphens_IsFoundThroughTheHyphen(self):
        t = table()
        t["languages"]["BR"]["head_nouns"]["Prole"] = "{.FS}"
        self.assertEqual(nt.head_noun_tag("BR", "Prole-das-Rochas", t), ("Prole", "{.FS}"))

    def test_head_noun_tag_HyphenatedNounInTheTable_MatchesWhole(self):
        t = table()
        t["languages"]["BR"]["head_nouns"]["Uruk-hai"] = "{.MP}"
        t["languages"]["BR"]["head_nouns"]["Uruk"] = "{.MS}"
        self.assertEqual(nt.head_noun_tag("BR", "Uruk-hai Combatentes", t), ("Uruk-hai", "{.MP}"))

    def test_head_noun_tag_HyphenatedProperName_IsNone(self):
        self.assertIsNone(nt.head_noun_tag("BR", "Blaidd-luth", table()))

    def test_head_noun_tag_PlainName_IsNone(self):
        self.assertIsNone(nt.head_noun_tag("BR", "Erebor", table()))


class TagRowTests(unittest.TestCase):
    def test_tag_row_TableId_IsTagged(self):
        self.assertEqual(nt.tag_row("BR", "k_erebor", "Erebor", table()), "{.NS}Erebor")

    def test_tag_row_HeadNounId_GetsTheNounsTag(self):
        self.assertEqual(nt.tag_row("BR", "k_aldeia", "Aldeia Norte", table()), "{.FS}Aldeia Norte")

    def test_tag_row_IdNotInTable_IsUnchanged(self):
        self.assertEqual(nt.tag_row("BR", "other", "Erebor", table()), "Erebor")

    def test_tag_row_OtherLanguage_IsNeverTagged(self):
        for lang in ("DE", "FR", "CNs", "TR", "RU"):
            self.assertEqual(nt.tag_row(lang, "k_erebor", "Erebor", table()), "Erebor")

    def test_tag_row_ItalianUsesItsOwnTag(self):
        self.assertEqual(nt.tag_row("IT", "k_erebor", "Erebor", table()), "{.FN}Erebor")

    def test_tag_entries_KeepsOrderAndTagsOnlyTableIds(self):
        out = nt.tag_entries("BR", [("a", "A"), ("k_erebor", "Erebor"), ("b", "B")], table())
        self.assertEqual(out, [("a", "A"), ("k_erebor", "{.NS}Erebor"), ("b", "B")])

    def test_tag_entries_Twice_IsStable(self):
        once = nt.tag_entries("BR", [("k_erebor", "Erebor")], table())
        self.assertEqual(nt.tag_entries("BR", once, table()), once)


class CarryTagTests(unittest.TestCase):
    """The live-map writers rewrite a row's text; the tag has to survive or follow the table."""

    def test_carry_tag_TableId_GetsTheTableTag(self):
        self.assertEqual(nt.carry_tag("BR", "k_aldeia", "Aldeia Velha", "Aldeia Nova", table()), "{.FS}Aldeia Nova")

    def test_carry_tag_TableTagBeatsTheOldRowsTag(self):
        self.assertEqual(nt.carry_tag("BR", "k_erebor", "{.MS}Velho", "Novo", table()), "{.NS}Novo")

    def test_carry_tag_IdNotInTable_KeepsTheOldRowsTag(self):
        self.assertEqual(nt.carry_tag("BR", "other", "{.FS}Aldeia Velha", "Aldeia Nova", table()), "{.FS}Aldeia Nova")

    def test_carry_tag_IdNotInTableAndOldRowUntagged_IsPlain(self):
        self.assertEqual(nt.carry_tag("BR", "other", "Velho", "Novo", table()), "Novo")

    def test_carry_tag_NewTextAlreadyTagged_IsNeverStacked(self):
        self.assertEqual(nt.carry_tag("BR", "k_erebor", "x", "{.NS}Novo", table()), "{.NS}Novo")

    def test_carry_tag_OtherLanguage_IsUnchanged(self):
        self.assertEqual(nt.carry_tag("DE", "k_erebor", "{.NS}Alt", "Neu", table()), "Neu")


class TableIdTests(unittest.TestCase):
    def test_rename_ids_MovesTagsAndPinsInEveryLanguage(self):
        t = table()
        t["languages"]["BR"]["pins"] = {"k_aldeia": "{.FS}"}
        t["languages"]["IT"]["tags"]["k_aldeia"] = "{.MS}"
        moved = nt.rename_ids(t, {"k_aldeia": "k_vila"})
        self.assertEqual(moved, 3)
        self.assertNotIn("k_aldeia", t["languages"]["BR"]["tags"])
        self.assertEqual(t["languages"]["BR"]["tags"]["k_vila"], "{.FS}")
        self.assertEqual(t["languages"]["BR"]["pins"], {"k_vila": "{.FS}"})
        self.assertEqual(t["languages"]["IT"]["tags"]["k_vila"], "{.MS}")

    def test_rename_ids_UnknownId_MovesNothing(self):
        t = table()
        self.assertEqual(nt.rename_ids(t, {"nope": "x"}), 0)
        self.assertEqual(t, table())

    def test_rename_ids_KeepsTheTagsSortedById(self):
        t = table()
        nt.rename_ids(t, {"k_aldeia": "a_first"})
        self.assertEqual(list(t["languages"]["BR"]["tags"]), sorted(t["languages"]["BR"]["tags"]))

    def test_dump_table_EndsWithANewlineAndKeepsNonAscii(self):
        out = nt.dump_table({"a": "Mão"})
        self.assertTrue(out.endswith("\n"))
        self.assertIn("Mão", out)


class RetagXmlTests(unittest.TestCase):
    def test_retag_xml_AddsTagAndKeepsEverythingElse(self):
        src = xml_doc([("k_erebor", "Erebor"), ("k_amp", "Cais &amp; Porto"), ("z", "Z")])
        new, changes = nt.retag_xml(src, "BR", table())
        self.assertIn('text="{.NS}Erebor"', new)
        self.assertIn('text="{.NS}Cais &amp; Porto"', new)
        self.assertIn('id="z" text="Z"', new)
        self.assertEqual([c[0] for c in changes], ["k_erebor", "k_amp"])
        self.assertEqual(new.count("\r\n"), src.count("\r\n"))
        self.assertNotIn("\n\n", new.replace("\r\n", ""))

    def test_retag_xml_SecondRun_ChangesNothing(self):
        src = xml_doc([("k_erebor", "Erebor")])
        once, _ = nt.retag_xml(src, "BR", table())
        twice, changes = nt.retag_xml(once, "BR", table())
        self.assertEqual(once, twice)
        self.assertEqual(changes, [])

    def test_retag_xml_LfFile_StaysLf(self):
        src = xml_doc([("k_erebor", "Erebor")], eol="\n")
        new, _ = nt.retag_xml(src, "BR", table())
        self.assertNotIn("\r", new)

    def test_retag_xml_NonTaggedLanguage_IsUntouched(self):
        src = xml_doc([("k_erebor", "Erebor")])
        new, changes = nt.retag_xml(src, "DE", table())
        self.assertEqual(new, src)
        self.assertEqual(changes, [])

    def test_retag_xml_ResultParses(self):
        src = xml_doc([("k_erebor", 'Er&quot;ebor')])
        new, _ = nt.retag_xml(src, "BR", table())
        ET.fromstring(new.encode("utf-8"))


class PlanFileTests(unittest.TestCase):
    def _file(self, td, rows=None):
        p = Path(td) / "std_taom_x_por-BR.xml"
        p.write_bytes(xml_doc(rows or [("k_erebor", "Erebor")]).encode("utf-8"))
        return p

    def _write(self, p, backup=False):
        changes, new_raw = nt.plan_file(p, "BR", table())
        if new_raw is not None:
            nt.write_planned(p, new_raw, backup=backup)
        return changes

    def test_plan_file_DryRun_WritesNothing(self):
        with tempfile.TemporaryDirectory() as td:
            p = self._file(td)
            before = p.read_bytes()
            changes, new_raw = nt.plan_file(p, "BR", table())
            self.assertEqual(len(changes), 1)
            self.assertIn(b"{.NS}Erebor", new_raw)
            self.assertEqual(p.read_bytes(), before)
            self.assertEqual([f.name for f in Path(td).iterdir()], [p.name])

    def test_write_planned_MakesNoBackupByDefault(self):
        with tempfile.TemporaryDirectory() as td:
            p = self._file(td)
            self._write(p)
            self.assertIn(b"{.NS}Erebor", p.read_bytes())
            self.assertEqual([f.name for f in Path(td).iterdir()], [p.name])

    def test_write_planned_WithBackup_BacksUpTheOriginalFirst(self):
        with tempfile.TemporaryDirectory() as td:
            p = self._file(td)
            before = p.read_bytes()
            self._write(p, backup=True)
            backups = [f for f in Path(td).iterdir() if ".bak_nametags_" in f.name]
            self.assertEqual(len(backups), 1)
            self.assertEqual(backups[0].read_bytes(), before)
            self.assertIn(b"{.NS}Erebor", p.read_bytes())

    def test_plan_file_SecondPlanAfterAWrite_HasNothingToWrite(self):
        with tempfile.TemporaryDirectory() as td:
            p = self._file(td)
            self._write(p, backup=True)
            changes, new_raw = nt.plan_file(p, "BR", table())
            self.assertEqual((changes, new_raw), ([], None))

    def test_plan_file_UnparseableSource_Raises(self):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "std_taom_bad_por-BR.xml"
            p.write_bytes(b'<base><strings><string id="k_erebor" text="Erebor" </strings></base>')
            with self.assertRaises(ValueError):
                nt.plan_file(p, "BR", table())

    def test_plan_file_ResultThatNoLongerParses_Raises(self):
        with tempfile.TemporaryDirectory() as td:
            p = self._file(td)
            broken = xml_doc([("k_erebor", "Erebor")]).replace("</strings>", "")
            with mock.patch.object(nt, "retag_xml", return_value=(broken, [("k_erebor", "Erebor", "{.NS}Erebor")])):
                with self.assertRaises(ValueError) as ctx:
                    nt.plan_file(p, "BR", table())
            self.assertIn("does not parse", str(ctx.exception))

    def test_plan_file_ResultThatChangesMoreThanTheTag_Raises(self):
        with tempfile.TemporaryDirectory() as td:
            p = self._file(td)
            changed = xml_doc([("k_erebor", "{.NS}Moria")])
            with mock.patch.object(nt, "retag_xml", return_value=(changed, [("k_erebor", "Erebor", "{.NS}Moria")])):
                with self.assertRaises(ValueError) as ctx:
                    nt.plan_file(p, "BR", table())
            self.assertIn("more than the leading tag", str(ctx.exception))

    def test_plan_file_ResultWithADifferentIdList_Raises(self):
        with tempfile.TemporaryDirectory() as td:
            p = self._file(td)
            changed = xml_doc([("k_other", "{.NS}Erebor")])
            with mock.patch.object(nt, "retag_xml", return_value=(changed, [("k_erebor", "Erebor", "{.NS}Erebor")])):
                with self.assertRaises(ValueError):
                    nt.plan_file(p, "BR", table())

    def test_write_planned_KeepsByteOrderMark(self):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "std_taom_bom_por-BR.xml"
            p.write_bytes(b"\xef\xbb\xbf" + xml_doc([("k_erebor", "Erebor")]).encode("utf-8"))
            self._write(p)
            self.assertTrue(p.read_bytes().startswith(b"\xef\xbb\xbf"))


class TableArgumentTests(unittest.TestCase):
    def test_tag_row_WithoutATable_IsATypeError(self):
        with self.assertRaises(TypeError):
            nt.tag_row("BR", "k_erebor", "Erebor")

    def test_tag_entries_WithoutATable_IsATypeError(self):
        with self.assertRaises(TypeError):
            nt.tag_entries("BR", [("k_erebor", "Erebor")])

    def test_carry_tag_WithoutATable_IsATypeError(self):
        with self.assertRaises(TypeError):
            nt.carry_tag("BR", "k_erebor", "x", "y")

    def test_load_table_ReadsTheTablePathAtCallTime(self):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "t.json"
            p.write_text('{"languages": {}}', encoding="utf-8")
            with mock.patch.object(nt, "TABLE_PATH", p):
                self.assertEqual(nt.load_table(), {"languages": {}})


class ArticleLedTests(unittest.TestCase):
    def _table(self):
        t = table()
        t["languages"]["BR"]["articles"] = ["O", "A", "Os", "As"]
        t["languages"]["IT"]["articles"] = ["Il", "La", "L'"]
        t["languages"]["BR"]["tags"]["k_morannon"] = "{.MS}"
        return t

    def test_leads_with_article_PortugueseArticle_IsTrue(self):
        self.assertTrue(nt.leads_with_article("BR", "O Morannon", self._table()))

    def test_leads_with_article_TagIsIgnored(self):
        self.assertTrue(nt.leads_with_article("BR", "{.MS}O Morannon", self._table()))

    def test_leads_with_article_ElidedItalianArticle_IsTrue(self):
        self.assertTrue(nt.leads_with_article("IT", "L'Occhio", self._table()))

    def test_leads_with_article_NameWithoutOne_IsFalse(self):
        self.assertFalse(nt.leads_with_article("BR", "Morannon", self._table()))
        self.assertFalse(nt.leads_with_article("BR", "Oasis do Sul", self._table()))

    def test_leads_with_article_LanguageWithoutArticles_IsFalse(self):
        self.assertFalse(nt.leads_with_article("DE", "Der Berg", self._table()))

    def test_leads_with_article_EmptyText_IsFalse(self):
        self.assertFalse(nt.leads_with_article("BR", "  ", self._table()))

    def test_main_ApplyOnAnArticleLedRow_ExitsTwoNamesTheRowAndWritesNothing(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td) / "Languages"
            (root / "BR").mkdir(parents=True)
            f = root / "BR" / "std_taom_a_por-BR.xml"
            f.write_bytes(xml_doc([("k_morannon", "O Morannon"), ("k_erebor", "Erebor")]).encode("utf-8"))
            before = f.read_bytes()
            err = io.StringIO()
            with mock.patch.object(nt, "LANGUAGE_DIR", root), mock.patch.object(nt, "load_table", return_value=self._table()), \
                    contextlib.redirect_stderr(err):
                code = nt.main(["--lang", "BR", "--apply"])
            self.assertEqual(code, 2)
            self.assertIn("k_morannon", err.getvalue())
            self.assertEqual(f.read_bytes(), before)

    def test_main_DryRunOnAnArticleLedRow_WarnsAndExitsZero(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td) / "Languages"
            (root / "BR").mkdir(parents=True)
            f = root / "BR" / "std_taom_a_por-BR.xml"
            f.write_bytes(xml_doc([("k_morannon", "O Morannon")]).encode("utf-8"))
            out = io.StringIO()
            with mock.patch.object(nt, "LANGUAGE_DIR", root), mock.patch.object(nt, "load_table", return_value=self._table()), \
                    contextlib.redirect_stdout(out):
                code = nt.main(["--lang", "BR"])
            self.assertEqual(code, 0)
            self.assertIn("k_morannon", out.getvalue())

    def test_main_ApplyOnAlreadyTaggedArticleLedRow_StillExitsTwo(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td) / "Languages"
            (root / "BR").mkdir(parents=True)
            (root / "BR" / "std_taom_a_por-BR.xml").write_bytes(xml_doc([("k_morannon", "{.MS}O Morannon")]).encode("utf-8"))
            with mock.patch.object(nt, "LANGUAGE_DIR", root), mock.patch.object(nt, "load_table", return_value=self._table()), \
                    contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(nt.main(["--lang", "BR", "--apply"]), 2)


MAP_ID = "Settlements.Settlement.name.town_EN1"


class FakeInstall:
    """A temp Modules folder, named to the tools through BANNERLORD_GAME_MODULES, with one live loc_settlements.xml per language."""

    def __init__(self, td, langs=("BR",)):
        self.modules = Path(td) / "Modules"
        self.files = {}
        for lang in langs:
            f = self.modules / "TAOM_Map" / "ModuleData" / "Languages" / lang / "loc_settlements.xml"
            f.parent.mkdir(parents=True)
            f.write_bytes(xml_doc([(MAP_ID, "Epicrotea")]).encode("utf-8"))
            self.files[lang] = f

    def __enter__(self):
        self._old = os.environ.get("BANNERLORD_GAME_MODULES")
        os.environ["BANNERLORD_GAME_MODULES"] = str(self.modules)
        return self

    def __exit__(self, *exc):
        if self._old is None:
            del os.environ["BANNERLORD_GAME_MODULES"]
        else:
            os.environ["BANNERLORD_GAME_MODULES"] = self._old


def live_table():
    t = table()
    t["languages"]["BR"]["tags"][MAP_ID] = "{.NS}"
    t["languages"]["IT"]["tags"][MAP_ID] = "{.FN}"
    return t


class LiveWriteGuardTests(unittest.TestCase):
    def _main(self, args):
        with mock.patch.object(nt, "load_table", return_value=live_table()):
            return nt.main(args)

    def test_main_ApplyOnTheLiveMapWithoutTheFlag_IsRefusedAndWritesNothing(self):
        with tempfile.TemporaryDirectory() as td, FakeInstall(td) as inst:
            before = inst.files["BR"].read_bytes()
            code = self._main(["--lang", "BR", "--target", "live-map", "--apply"])
            self.assertEqual(code, 2)
            self.assertEqual(inst.files["BR"].read_bytes(), before)
            self.assertEqual([x.name for x in inst.files["BR"].parent.iterdir()], ["loc_settlements.xml"])

    def test_main_ApplyOnTheLiveMapWithTheFlag_WritesAndBacksUpOnce(self):
        with tempfile.TemporaryDirectory() as td, FakeInstall(td) as inst:
            before = inst.files["BR"].read_bytes()
            code = self._main(["--lang", "BR", "--target", "live-map", "--apply", "--allow-live-write"])
            self.assertEqual(code, 0)
            self.assertIn(b"{.NS}Epicrotea", inst.files["BR"].read_bytes())
            backups = [x for x in inst.files["BR"].parent.iterdir() if ".bak_nametags_" in x.name]
            self.assertEqual(len(backups), 1)
            self.assertEqual(backups[0].read_bytes(), before)

    def test_main_SecondApplyOnTheLiveMap_ChangesNoBytesAndMakesNoBackup(self):
        with tempfile.TemporaryDirectory() as td, FakeInstall(td) as inst:
            self._main(["--lang", "BR", "--target", "live-map", "--apply", "--allow-live-write"])
            after = inst.files["BR"].read_bytes()
            code = self._main(["--lang", "BR", "--target", "live-map", "--apply", "--allow-live-write"])
            self.assertEqual(code, 0)
            self.assertEqual(inst.files["BR"].read_bytes(), after)
            self.assertEqual(len([x for x in inst.files["BR"].parent.iterdir() if ".bak_nametags_" in x.name]), 1)

    def test_main_DryRunOnTheLiveMap_NeedsNoFlagAndWritesNothing(self):
        with tempfile.TemporaryDirectory() as td, FakeInstall(td) as inst:
            before = inst.files["BR"].read_bytes()
            code = self._main(["--lang", "BR", "--target", "live-map"])
            self.assertEqual(code, 0)
            self.assertEqual(inst.files["BR"].read_bytes(), before)

    def test_main_LiveMapFileMissing_IsAnError(self):
        with tempfile.TemporaryDirectory() as td, FakeInstall(td, langs=("BR",)):
            self.assertEqual(self._main(["--lang", "IT", "--target", "live-map"]), 2)

    def test_main_LiveMapWithoutAnInstall_IsAnError(self):
        with tempfile.TemporaryDirectory() as td:
            old = os.environ.get("BANNERLORD_GAME_MODULES")
            os.environ["BANNERLORD_GAME_MODULES"] = str(Path(td) / "nothing")
            try:
                self.assertEqual(self._main(["--lang", "BR", "--target", "live-map"]), 2)
            finally:
                if old is None:
                    del os.environ["BANNERLORD_GAME_MODULES"]
                else:
                    os.environ["BANNERLORD_GAME_MODULES"] = old


class PlanThenWriteTests(unittest.TestCase):
    def _repo(self, td):
        root = Path(td) / "Languages"
        (root / "BR").mkdir(parents=True)
        good = root / "BR" / "std_taom_a_por-BR.xml"
        bad = root / "BR" / "std_taom_b_por-BR.xml"
        good.write_bytes(xml_doc([("k_erebor", "Erebor")]).encode("utf-8"))
        bad.write_bytes(b"<base><strings><string id=\"k_erebor\" text=\"x\" </strings></base>")
        return root, good, bad

    def test_main_OneUnparseableFile_WritesNoFileAndExitsTwo(self):
        with tempfile.TemporaryDirectory() as td:
            root, good, bad = self._repo(td)
            before = good.read_bytes()
            with mock.patch.object(nt, "LANGUAGE_DIR", root), mock.patch.object(nt, "load_table", return_value=table()):
                code = nt.main(["--lang", "BR", "--apply"])
            self.assertEqual(code, 2)
            self.assertEqual(good.read_bytes(), before)

    def test_main_RepoApply_WritesAllFilesAndMakesNoBackup(self):
        with tempfile.TemporaryDirectory() as td:
            root, good, bad = self._repo(td)
            bad.write_bytes(xml_doc([("k_erebor", "Erebor")]).encode("utf-8"))
            with mock.patch.object(nt, "LANGUAGE_DIR", root), mock.patch.object(nt, "load_table", return_value=table()):
                code = nt.main(["--lang", "BR", "--apply"])
            self.assertEqual(code, 0)
            self.assertIn(b"{.NS}Erebor", good.read_bytes())
            self.assertIn(b"{.NS}Erebor", bad.read_bytes())
            self.assertEqual(len(list((root / "BR").iterdir())), 2)

    def test_main_LanguageWithNoFiles_IsAnError(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td) / "Languages"
            root.mkdir()
            with mock.patch.object(nt, "LANGUAGE_DIR", root), mock.patch.object(nt, "load_table", return_value=table()):
                self.assertEqual(nt.main(["--lang", "BR"]), 2)


class ShippedTableTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.table = nt.load_table()

    def test_table_CoversPortugueseAndItalianOnly(self):
        self.assertEqual(sorted(self.table["languages"]), ["BR", "IT"])

    def test_table_EveryTagIsAllowedForItsLanguage(self):
        for lang, spec in self.table["languages"].items():
            allowed = set(spec["allowed"])
            for sid, tag in spec["tags"].items():
                self.assertIn(tag, allowed, f"{lang} {sid}")

    def test_table_EveryHeadNounTagIsAllowedForItsLanguage(self):
        for lang, spec in self.table["languages"].items():
            for noun, tag in spec["head_nouns"].items():
                self.assertIn(tag, spec["allowed"], f"{lang} {noun}")

    def test_table_PlainTagIsTheNoArticleForm(self):
        self.assertEqual(self.table["languages"]["BR"]["plain"], "{.NS}")
        self.assertIn(self.table["languages"]["IT"]["plain"], ("{.FN}", "{.MN}"))

    def test_table_BrazilianHasNoProperNounTagOtherThanNS(self):
        self.assertNotIn("{.FN}", self.table["languages"]["BR"]["allowed"])
        self.assertNotIn("{.MN}", self.table["languages"]["BR"]["allowed"])

    def test_table_ItalianHasNoNSTag(self):
        self.assertNotIn("{.NS}", self.table["languages"]["IT"]["allowed"])

    def test_table_NamesTheHeadNounsTheIssueListed(self):
        it = self.table["languages"]["IT"]["head_nouns"]
        br = self.table["languages"]["BR"]["head_nouns"]
        for noun in ("Villaggio", "Accampamento", "Insenatura", "Regno"):
            self.assertIn(noun, it)
        for noun in ("Aldeia", "Vila", "Acampamento"):
            self.assertIn(noun, br)

    def test_table_HasTagsForBothLanguages(self):
        for lang in ("BR", "IT"):
            self.assertGreater(len(self.table["languages"][lang]["tags"]), 1000)


if __name__ == "__main__":
    unittest.main()
