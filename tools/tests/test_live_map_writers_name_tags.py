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
    def _run(self, td, rows_by_lang):
        data = Path(td) / "Modules" / "TAOM_Map" / "ModuleData"
        data.mkdir(parents=True)
        (data / "settlements.xml").write_bytes(
            f'<S><Settlement id="village_RU8_4" name="{{={P}village_RU8_4}}Old" /></S>'.encode("utf-8"))
        for lang in LANGS:
            f = data / "Languages" / lang / "loc_settlements.xml"
            f.parent.mkdir(parents=True)
            f.write_bytes(loc_xml(rows_by_lang.get(lang, [("village_RU8_4", "Old")])).encode("utf-8"))
        old = os.environ.get("BANNERLORD_GAME_DIR")
        os.environ["BANNERLORD_GAME_DIR"] = td
        try:
            with mock.patch.object(nt, "load_table", return_value=TABLE), contextlib.redirect_stdout(io.StringIO()):
                runpy.run_path(str(APPLY), run_name="apply_map_village_names")
        finally:
            if old is None:
                del os.environ["BANNERLORD_GAME_DIR"]
            else:
                os.environ["BANNERLORD_GAME_DIR"] = old
        return {lang: (data / "Languages" / lang / "loc_settlements.xml").read_text(encoding="utf-8") for lang in LANGS}

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
            self.assertNotIn("{.", out[lang])

    def test_script_IdNotInTheTable_KeepsTheOldRowsTag(self):
        with tempfile.TemporaryDirectory() as td:
            out = self._run(td, {"BR": [("village_RU8_3", "{.FS}Velha")]})
        self.assertIn('text="{.FS}Iôrig-Tün"', out["BR"])

    def test_script_SecondRun_ChangesNothing(self):
        with tempfile.TemporaryDirectory() as td:
            first = self._run(td, {})
            data = Path(td) / "Modules" / "TAOM_Map" / "ModuleData"
            old = os.environ.get("BANNERLORD_GAME_DIR")
            os.environ["BANNERLORD_GAME_DIR"] = td
            try:
                with mock.patch.object(nt, "load_table", return_value=TABLE), contextlib.redirect_stdout(io.StringIO()):
                    runpy.run_path(str(APPLY), run_name="apply_map_village_names")
            finally:
                if old is None:
                    del os.environ["BANNERLORD_GAME_DIR"]
                else:
                    os.environ["BANNERLORD_GAME_DIR"] = old
            second = (data / "Languages" / "BR" / "loc_settlements.xml").read_text(encoding="utf-8")
        self.assertEqual(first["BR"], second)
        self.assertEqual(second.count("{.NS}"), 1)


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
