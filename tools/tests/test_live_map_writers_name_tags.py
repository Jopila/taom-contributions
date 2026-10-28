"""The three live-map writers keep the grammar tag of the names they write (#1023).

Apply-MapVillageNames.py rewrites a row's text, add_map_villages.py adds rows, rename_map_settlements.py renames ids.
Each is checked on a synthetic file or a temp BANNERLORD_GAME_DIR tree; none touches the real install.
"""

import contextlib
import io
import os
import runpy
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import add_map_villages as amv  # noqa: E402
import name_tags as nt  # noqa: E402
import rename_map_settlements as rms  # noqa: E402

P = "Settlements.Settlement.name."
TABLE = {
    "languages": {
        "BR": {"plain": "{.NS}", "allowed": ["{.NS}", "{.FS}"], "head_nouns": {}, "pins": {},
               "tags": {P + "village_RU8_4": "{.NS}", P + "village_X_1": "{.FS}", P + "village_X_2": "{.FS}"}},
        "IT": {"plain": "{.FN}", "allowed": ["{.FN}"], "head_nouns": {}, "pins": {},
               "tags": {P + "village_RU8_4": "{.FN}", P + "village_X_1": "{.FN}"}},
    }
}
LANGS = ["BR", "CNs", "CNt", "DE", "FR", "IT", "JP", "KO", "PL", "RU", "SP", "TR"]
APPLY = Path(__file__).resolve().parent.parent / "Apply-MapVillageNames.py"


def loc_xml(rows):
    body = "".join(f'    <string id="{P}{i}" text="{t}" />\r\n' for i, t in rows)
    return f'<?xml version="1.0" encoding="utf-8"?>\r\n<base>\r\n  <strings>\r\n{body}  </strings>\r\n</base>\r\n'


class ApplyMapVillageNamesTests(unittest.TestCase):
    MASTER = (f'<S><Settlement id="village_RU8_4" name="{{={P}village_RU8_4}}Old" />'
              f'<Settlement id="village_RU8_3" name="{{={P}village_RU8_3}}Old" />'
              f'<Settlement id="village_RU8_2" name="{{={P}village_RU8_2}}Iôrig-Vargh" /></S>')
    ROWS = [("village_RU8_4", "Old"), ("village_RU8_2", "Ыыы")]

    def _tree(self, td, rows_by_lang, master=None):
        data = Path(td) / "Modules" / "TAOM_Map" / "ModuleData"
        cache = Path(td) / "cache"
        data.mkdir(parents=True)
        cache.mkdir()
        if master is not False:
            (data / "settlements.xml").write_bytes((self.MASTER if master is None else master).encode("utf-8"))
        for lang in LANGS:
            f = data / "Languages" / lang / "loc_settlements.xml"
            f.parent.mkdir(parents=True)
            f.write_bytes(loc_xml(rows_by_lang.get(lang, self.ROWS)).encode("utf-8"))
            (cache / f"{lang.lower()}.json").write_bytes(
                ('{\n  "' + P + 'village_RU8_2": "Ыыы",\n  "' + P + 'village_RU8_4": "Altes"\n}').encode("utf-8"))
        return data, cache

    def _main(self, td, argv=("--apply",)):
        old = os.environ.get("BANNERLORD_GAME_DIR")
        os.environ["BANNERLORD_GAME_DIR"] = td
        try:
            with mock.patch.object(nt, "load_table", return_value=TABLE), \
                    contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                ns = runpy.run_path(str(APPLY), run_name="apply_map_village_names")
                return ns["main"](list(argv), cache_dir=str(Path(td) / "cache"))
        finally:
            if old is None:
                del os.environ["BANNERLORD_GAME_DIR"]
            else:
                os.environ["BANNERLORD_GAME_DIR"] = old

    def _read(self, data, cache):
        self.master_after = (data / "settlements.xml").read_text(encoding="utf-8")
        self.cache_after = {lang: (cache / f"{lang.lower()}.json").read_text(encoding="utf-8") for lang in LANGS}
        return {lang: (data / "Languages" / lang / "loc_settlements.xml").read_text(encoding="utf-8") for lang in LANGS}

    def _run(self, td, rows_by_lang, argv=("--apply",), master=None):
        data, cache = self._tree(td, rows_by_lang, master)
        self.assertEqual(self._main(td, argv), 0)
        return self._read(data, cache)

    def test_script_TableIdInBrazilianAndItalian_KeepsTheTableTag(self):
        with tempfile.TemporaryDirectory() as td:
            out = self._run(td, {})
        self.assertIn('text="{.NS}Iôrig-Mukh"', out["BR"])
        self.assertIn('text="{.FN}Iôrig-Mukh"', out["IT"])

    def test_script_OtherLanguages_GetThePlainName(self):
        with tempfile.TemporaryDirectory() as td:
            out = self._run(td, {})
        for lang in ("CNs", "DE", "FR", "RU", "TR"):
            self.assertIn('text="Iôrig-Mukh"', out[lang])
            self.assertNotIn('text="{.', out[lang])

    def test_script_IdNotInTheTable_KeepsTheOldRowsTag(self):
        with tempfile.TemporaryDirectory() as td:
            out = self._run(td, {"BR": [("village_RU8_3", "{.FS}Velha")]})
        self.assertIn('text="{.FS}Iôrig-Tün"', out["BR"])

    def test_script_RenamedId_UpdatesTheMasterAndLeavesTheCache(self):
        with tempfile.TemporaryDirectory() as td:
            self._run(td, {})
        self.assertIn('}Iôrig-Mukh"', self.master_after)
        self.assertNotIn("village_RU8_4", self.cache_after["DE"])

    def test_script_UnchangedName_KeepsItsTranslationAndCacheRow(self):
        # The English of village_RU8_2 already matches the table, so its translated row stays.
        with tempfile.TemporaryDirectory() as td:
            out = self._run(td, {})
        self.assertIn('text="Ыыы"', out["RU"])
        self.assertIn("village_RU8_2", self.cache_after["RU"])

    def test_script_DryRun_WritesNothing(self):
        with tempfile.TemporaryDirectory() as td:
            out = self._run(td, {}, argv=())
        self.assertIn('village_RU8_4}Old"', self.master_after)
        self.assertIn('text="Old"', out["DE"])
        self.assertIn("village_RU8_4", self.cache_after["DE"])

    def test_script_SecondRun_ChangesNothing(self):
        with tempfile.TemporaryDirectory() as td:
            data, cache = self._tree(td, {})
            self.assertEqual(self._main(td), 0)
            first = self._read(data, cache)
            self.assertEqual(self._main(td), 0)
            second = self._read(data, cache)
        self.assertEqual(first["BR"], second["BR"])
        self.assertEqual(second["BR"].count("{.NS}"), 1)

    def test_script_MasterWithABom_KeepsExactlyOneBom(self):
        # The live master carries a UTF-8 BOM; the other fixtures do not.
        with tempfile.TemporaryDirectory() as td:
            self._run(td, {}, master="﻿" + self.MASTER)
            raw = (Path(td) / "Modules" / "TAOM_Map" / "ModuleData" / "settlements.xml").read_bytes()
        self.assertTrue(raw.startswith(b"\xef\xbb\xbf<S>"))

    def test_script_Apply_BacksUpEachChangedFileOutsideTheXmlGlob(self):
        with tempfile.TemporaryDirectory() as td:
            self._run(td, {})
            data = Path(td) / "Modules" / "TAOM_Map" / "ModuleData"
            master_baks = list(data.glob("settlements.xml.bak_villagenames_*"))
            de_baks = list((data / "Languages" / "DE").glob("loc_settlements.xml.bak_villagenames_*"))
            self.assertEqual(len(master_baks), 1)
            self.assertIn('village_RU8_4}Old"', master_baks[0].read_text(encoding="utf-8"))
            self.assertEqual(len(de_baks), 1)

    def test_script_LocFileThatDoesNotParse_RefusesAndWritesNothing(self):
        with tempfile.TemporaryDirectory() as td:
            data, cache = self._tree(td, {})
            (data / "Languages" / "DE" / "loc_settlements.xml").write_bytes(b'<base><strings><string id="x" text="Old" />')
            self.assertEqual(self._main(td), 1)
            out = self._read(data, cache)
        self.assertIn('village_RU8_4}Old"', self.master_after)
        self.assertIn('text="Old"', out["FR"])
        self.assertIn("village_RU8_4", self.cache_after["FR"])

    def test_script_AWriteThatFails_IsFinishedByARerun(self):
        # The master decides what a run renames, so it is written last: a run that fails part-way
        # leaves the master on the old name and the next run finishes every language and the cache.
        import stat
        with tempfile.TemporaryDirectory() as td:
            data, cache = self._tree(td, {})
            de = data / "Languages" / "DE" / "loc_settlements.xml"
            os.chmod(de, stat.S_IREAD)
            try:
                with self.assertRaises(PermissionError):
                    self._main(td)
            finally:
                os.chmod(de, stat.S_IREAD | stat.S_IWRITE)
            self.assertIn('village_RU8_4}Old"', (data / "settlements.xml").read_text(encoding="utf-8"))
            self.assertEqual(self._main(td), 0)
            out = self._read(data, cache)
        self.assertIn('}Iôrig-Mukh"', self.master_after)
        for lang in LANGS:
            self.assertIn("Iôrig-Mukh", out[lang], lang)
            self.assertNotIn("village_RU8_4", self.cache_after[lang], lang)

    def test_script_NewNameAnotherSettlementCarries_RefusesAndWritesNothing(self):
        master = self.MASTER.replace("</S>", f'<Settlement id="castle_Z1" name="{{={P}castle_Z1}}Iôrig-Mukh" /></S>')
        with tempfile.TemporaryDirectory() as td:
            data, cache = self._tree(td, {}, master)
            self.assertEqual(self._main(td), 1)
            out = self._read(data, cache)
        self.assertIn('village_RU8_4}Old"', self.master_after)
        self.assertIn('text="Old"', out["DE"])
        self.assertIn("village_RU8_4", self.cache_after["DE"])

    def test_script_TwoTableIdsSwappingNames_IsNotAClash(self):
        swapped = (f'<S><Settlement id="village_RU8_4" name="{{={P}village_RU8_4}}Iôrig-Tün" />'
                   f'<Settlement id="village_RU8_3" name="{{={P}village_RU8_3}}Iôrig-Mukh" /></S>')
        with tempfile.TemporaryDirectory() as td:
            self._run(td, {}, master=swapped)
        self.assertIn('village_RU8_4}Iôrig-Mukh"', self.master_after)
        self.assertIn('village_RU8_3}Iôrig-Tün"', self.master_after)

    def test_script_MasterWithNoTableId_Exits2(self):
        with tempfile.TemporaryDirectory() as td:
            self._tree(td, {}, master=f'<S><Settlement id="town_Q1" name="{{={P}town_Q1}}Q" /></S>')
            self.assertEqual(self._main(td), 2)

    def test_script_MissingMaster_Exits2(self):
        with tempfile.TemporaryDirectory() as td:
            self._tree(td, {}, master=False)
            self.assertEqual(self._main(td), 2)


class AddMapVillagesTests(unittest.TestCase):
    def test_tagged_rows_KnownIdInBrazilian_GetsTheTableTag(self):
        self.assertEqual(amv.tagged_rows("BR", [("village_X_1", "Aldeia X")], TABLE), [("village_X_1", "{.FS}Aldeia X")])

    def test_tagged_rows_UnknownId_IsPlain(self):
        self.assertEqual(amv.tagged_rows("BR", [("village_Y_1", "Aldeia Y")], TABLE), [("village_Y_1", "Aldeia Y")])

    def test_tagged_rows_LanguageWithoutTags_IsPlain(self):
        self.assertEqual(amv.tagged_rows("DE", [("village_X_1", "Dorf X")], TABLE), [("village_X_1", "Dorf X")])

    def test_loc_row_WithATaggedName_WritesTheTagInTheText(self):
        row = amv.loc_row("village_X_1", amv.tagged_rows("IT", [("village_X_1", "Villaggio X")], TABLE)[0][1])
        self.assertEqual(row, f'    <string id="{P}village_X_1" text="{{.FN}}Villaggio X" />')


class RenameMapSettlementsTests(unittest.TestCase):
    def test_rename_table_ids_MovesTheTagsOfRenamedSettlements(self):
        t = {"languages": {"BR": {"tags": {P + "village_X_1": "{.FS}", P + "keep": "{.NS}"}, "pins": {}},
                           "IT": {"tags": {P + "village_X_1": "{.FN}"}, "pins": {P + "village_X_1": "{.FN}"}}}}
        renames = [rms.Rename("village_X_1", "village_Z_1", "town_Z")]
        moved = rms.rename_table_ids(t, renames)
        self.assertEqual(moved, 3)
        self.assertEqual(t["languages"]["BR"]["tags"], {P + "keep": "{.NS}", P + "village_Z_1": "{.FS}"})
        self.assertEqual(t["languages"]["IT"]["pins"], {P + "village_Z_1": "{.FN}"})

    def test_rename_table_ids_NothingToMove_ReturnsZero(self):
        t = {"languages": {"BR": {"tags": {P + "other": "{.NS}"}, "pins": {}}}}
        self.assertEqual(rms.rename_table_ids(t, [rms.Rename("village_X_1", "village_Z_1", "town_Z")]), 0)

    def test_the_first_batch_renames_are_in_the_shipped_table_no_more(self):
        table = nt.load_table()
        for r in rms.RENAMES:
            self.assertNotIn(P + r.old, table["languages"]["BR"]["tags"])
            self.assertNotIn(P + r.old, table["languages"]["IT"]["tags"])


if __name__ == "__main__":
    unittest.main()
