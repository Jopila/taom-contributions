"""Tests for tools/register_armory_names.py against a temp Armory tree."""
import contextlib
import io
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import register_armory_names as ran  # noqa: E402

LANG_HEAD = ("<?xml version='1.0' encoding='utf-8'?>{n}"
             "<base type=\"string\">{n}  <tags>{n}    <tag language=\"English\"/>{n}  </tags>{n}  <strings>{n}")
LANG_TAIL = "  </strings>{n}</base>{n}"


def lang_file(rows, nl="\r\n", bom=False):
    body = "".join('    <string id="%s" text="%s"/>%s' % (k, t, nl) for k, t in rows)
    text = LANG_HEAD.format(n=nl) + body + LANG_TAIL.format(n=nl)
    return (b"\xef\xbb\xbf" if bom else b"") + text.encode("utf-8")


class Armory(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.modules = Path(self.tmp.name) / "Modules"
        self.md = self.modules / "LOTRLOME_Armory" / "ModuleData"
        (self.md / "LOTRLOME_items" / "harad").mkdir(parents=True)
        (self.md / "LOTRLOME_items" / "gondor").mkdir(parents=True)
        (self.md / "Languages").mkdir()
        self.baseline = Path(self.tmp.name) / "baseline.txt"
        (self.md / "LOTRLOME_items" / "harad" / "weapons.xml").write_text(
            '<Items>\n<Item id="a" name="{=k_reg}Registered Blade" />\n'
            '<CraftedItem id="b" name="{=k_new}Fresh &amp; Sharp &quot;Spear&quot;" />\n'
            '<Item id="c" name="Plain name" />\n</Items>\n', encoding="utf-8")
        (self.md / "LOTRLOME_items" / "gondor" / "armors.xml").write_text(
            '<Items><Item id="g" name="{=k_gon}Gondor Helm"/></Items>', encoding="utf-8")
        (self.md / "LOTRLOME_crafting_pieces.xml").write_text(
            '<Pieces><CraftingPiece id="p" name="{=k_piece}Guard &lt;Iron&gt;"/></Pieces>', encoding="utf-8")
        self.harad = self.md / "Languages" / "loc_harad.xml"
        self.gondor = self.md / "Languages" / "loc_gondor.xml"
        self.pieces = self.md / "Languages" / "loc_LOTRLOME_crafting_pieces.xml"
        self.harad.write_bytes(lang_file([("k_reg", "Registered Blade")]))
        self.gondor.write_bytes(lang_file([]))
        self.pieces.write_bytes(lang_file([]))
        self.baseline.write_text("", encoding="utf-8")

    def run_tool(self, *args):
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            try:
                code = ran.main(["--modules", str(self.modules), "--baseline", str(self.baseline), *args])
            except SystemExit as e:
                code = e.code
        return code, out.getvalue() + err.getvalue()


class CheckTests(Armory):
    def test_unregistered_key_outside_baseline_fails(self):
        code, out = self.run_tool("--check")
        self.assertEqual(code, 1)
        for k in ("k_new", "k_gon", "k_piece"):
            self.assertIn(k, out)
        self.assertNotIn("k_reg", out)

    def test_baselined_keys_pass(self):
        self.baseline.write_text("# note\nk_gon\nk_new\nk_piece\n", encoding="utf-8")
        code, _ = self.run_tool("--check")
        self.assertEqual(code, 0)

    def test_baseline_entry_now_registered_fails(self):
        self.baseline.write_text("k_gon\nk_new\nk_piece\nk_reg\n", encoding="utf-8")
        code, out = self.run_tool("--check")
        self.assertEqual(code, 1)
        self.assertIn("k_reg", out)
        self.assertIn("remove", out.lower())

    def test_nested_language_file_does_not_register(self):
        sub = self.md / "Languages" / "DE"
        sub.mkdir()
        (sub / "loc_de.xml").write_bytes(lang_file([("k_gon", "Gondor Helm")]))
        self.baseline.write_text("k_gon\nk_new\nk_piece\n", encoding="utf-8")
        self.assertEqual(self.run_tool("--check")[0], 0)

    def test_missing_armory_exits_2(self):
        code, _ = self.run_tool("--check", "--modules", str(Path(self.tmp.name) / "nowhere"))
        self.assertEqual(code, 2)

    def test_check_writes_nothing(self):
        before = self.harad.read_bytes()
        self.run_tool("--check")
        self.assertEqual(self.harad.read_bytes(), before)

    def test_update_baseline_writes_sorted_set(self):
        code, _ = self.run_tool("--update-baseline")
        self.assertEqual(code, 0)
        self.assertEqual(self.baseline.read_text(encoding="utf-8").split(), ["k_gon", "k_new", "k_piece"])
        self.assertEqual(self.run_tool("--check")[0], 0)


class ApplyTests(Armory):
    def test_dry_run_writes_nothing(self):
        before = [p.read_bytes() for p in (self.harad, self.gondor, self.pieces)]
        code, out = self.run_tool()
        self.assertEqual(code, 0)
        self.assertIn("k_new", out)
        self.assertEqual(before, [p.read_bytes() for p in (self.harad, self.gondor, self.pieces)])
        self.assertEqual(list((self.md / "Languages").glob("*.bak*")), [])

    def test_apply_routes_rows_to_the_right_files(self):
        code, _ = self.run_tool("--apply")
        self.assertEqual(code, 0)
        harad = self.harad.read_text(encoding="utf-8")
        self.assertIn('id="k_new" text="Fresh &amp; Sharp &quot;Spear&quot;"', harad)
        self.assertNotIn("k_gon", harad)
        self.assertIn('id="k_gon" text="Gondor Helm"', self.gondor.read_text(encoding="utf-8"))
        self.assertIn('id="k_piece" text="Guard &lt;Iron&gt;"', self.pieces.read_text(encoding="utf-8"))
        self.assertIn("register_armory_names.py", harad)
        self.assertEqual(self.harad.read_text(encoding="utf-8").count('id="k_reg"'), 1)

    def test_apply_is_idempotent_and_makes_check_pass(self):
        self.run_tool("--apply")
        snap = [p.read_bytes() for p in (self.harad, self.gondor, self.pieces)]
        baks = len(list((self.md / "Languages").glob("*.bak-armory-names-*")))
        code, _ = self.run_tool("--apply")
        self.assertEqual(code, 0)
        self.assertEqual(snap, [p.read_bytes() for p in (self.harad, self.gondor, self.pieces)])
        self.assertEqual(baks, len(list((self.md / "Languages").glob("*.bak-armory-names-*"))))
        self.assertEqual(self.run_tool("--check")[0], 0)

    def test_backup_has_non_xml_suffix_and_original_bytes(self):
        before = self.harad.read_bytes()
        self.run_tool("--apply")
        baks = list((self.md / "Languages").glob("loc_harad.xml.bak-armory-names-*"))
        self.assertEqual(len(baks), 1)
        self.assertFalse(baks[0].name.endswith(".xml"))
        self.assertEqual(baks[0].read_bytes(), before)

    def test_crlf_and_bom_preserved(self):
        self.harad.write_bytes(lang_file([("k_reg", "Registered Blade")], nl="\r\n", bom=True))
        self.run_tool("--apply")
        raw = self.harad.read_bytes()
        self.assertTrue(raw.startswith(b"\xef\xbb\xbf"))
        self.assertEqual(raw.count(b"\n"), raw.count(b"\r\n"))

    def test_lf_file_stays_lf(self):
        self.gondor.write_bytes(lang_file([], nl="\n"))
        self.run_tool("--apply")
        raw = self.gondor.read_bytes()
        self.assertNotIn(b"\r", raw)
        self.assertIn(b"k_gon", raw)

    def test_missing_target_file_is_reported_and_not_created(self):
        self.gondor.unlink()
        code, out = self.run_tool("--apply")
        self.assertFalse(self.gondor.exists())
        self.assertIn("loc_gondor.xml", out)
        self.assertIn("k_gon", out)
        self.assertIn("k_new", self.harad.read_text(encoding="utf-8"))
        self.assertEqual(code, 0)

    def test_unparseable_result_is_refused(self):
        self.pieces.write_bytes(b"<base><strings>\r\n  <string id='x' text='<'/>\r\n  </strings></base>")
        before = self.pieces.read_bytes()
        code, out = self.run_tool("--apply")
        self.assertEqual(self.pieces.read_bytes(), before)
        self.assertEqual(code, 1)
        self.assertIn("loc_LOTRLOME_crafting_pieces.xml", out)

    def test_loose_item_file_routes_to_its_own_loc_file(self):
        (self.md / "LOTRLOME_items" / "LOTRAOM_shields.xml").write_text(
            '<Items><Item id="s" name="{=k_shield}Shield"/></Items>', encoding="utf-8")
        loose = self.md / "Languages" / "loc_LOTRAOM_shields.xml"
        loose.write_bytes(lang_file([]))
        self.run_tool("--apply")
        self.assertIn("k_shield", loose.read_text(encoding="utf-8"))

    def test_prefix_filter(self):
        self.run_tool("--apply", "--prefix", "^k_gon$")
        self.assertIn("k_gon", self.gondor.read_text(encoding="utf-8"))
        self.assertNotIn("k_new", self.harad.read_text(encoding="utf-8"))
        self.assertNotIn("k_piece", self.pieces.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
