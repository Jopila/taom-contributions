#!/usr/bin/env python3
"""Tests for tools/merge_live_file.py: a three-way merge of a live (CRLF, unversioned)
file with an artist's LF versions.

Run:  python -m unittest discover -s tools/tests -p "test_merge_live_file.py"
"""
import io
import os
import subprocess
import sys
import tempfile
import unittest
from contextlib import redirect_stdout, redirect_stderr
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import merge_live_file  # noqa: E402

BASE = ["<Items>", '  <Item id="a" v="1"/>', '  <Item id="b" v="1"/>', '  <Item id="c" v="1"/>', "</Items>"]


def _join(lines, eol, final=True):
    return (eol.join(lines) + (eol if final else "")).encode("utf-8")


class _Tmp(unittest.TestCase):
    def setUp(self):
        self._td = tempfile.TemporaryDirectory()
        self.addCleanup(self._td.cleanup)
        self.dir = Path(self._td.name)

    def put(self, name, data):
        p = self.dir / name
        p.write_bytes(data)
        return p

    def run_tool(self, *args):
        out, err = io.StringIO(), io.StringIO()
        with redirect_stdout(out), redirect_stderr(err):
            code = merge_live_file.main([str(a) for a in args])
        return code, out.getvalue() + err.getvalue()


class MergeTests(_Tmp):
    def test_clean_crlf_merge_keeps_crlf_and_both_edits(self):
        live_lines = list(BASE)
        live_lines[1] = '  <Item id="a" v="LIVE"/>'
        theirs_lines = list(BASE)
        theirs_lines[3] = '  <Item id="c" v="THEIRS"/>'
        live = self.put("live.xml", _join(live_lines, "\r\n"))
        base = self.put("base.xml", _join(BASE, "\n"))
        theirs = self.put("theirs.xml", _join(theirs_lines, "\n"))
        out = self.dir / "out.xml"

        code, text = self.run_tool(live, "--base", base, "--theirs", theirs, "--out", out)

        self.assertEqual(code, 0, text)
        got = out.read_bytes()
        expected = _join(
            ["<Items>", '  <Item id="a" v="LIVE"/>', '  <Item id="b" v="1"/>', '  <Item id="c" v="THEIRS"/>', "</Items>"],
            "\r\n",
        )
        self.assertEqual(got, expected)
        self.assertNotIn(b"\n", got.replace(b"\r\n", b""))

    def test_overlapping_edit_exits_1_and_writes_nothing(self):
        live_lines = list(BASE)
        live_lines[2] = '  <Item id="b" v="LIVE"/>'
        theirs_lines = list(BASE)
        theirs_lines[2] = '  <Item id="b" v="THEIRS"/>'
        live_bytes = _join(live_lines, "\r\n")
        live = self.put("live.xml", live_bytes)
        base = self.put("base.xml", _join(BASE, "\n"))
        theirs = self.put("theirs.xml", _join(theirs_lines, "\n"))

        code, text = self.run_tool(live, "--base", base, "--theirs", theirs, "--apply", "--tag", "t")

        self.assertEqual(code, 1, text)
        self.assertIn("v=\"LIVE\"", text)
        self.assertIn("v=\"THEIRS\"", text)
        self.assertEqual(live.read_bytes(), live_bytes)
        self.assertEqual([p.name for p in self.dir.glob("live.xml.bak*")], [])

    def test_bom_and_missing_final_newline_preserved(self):
        bom = b"\xef\xbb\xbf"
        theirs_lines = list(BASE)
        theirs_lines[3] = '  <Item id="c" v="THEIRS"/>'
        live = self.put("live.xml", bom + _join(BASE, "\r\n", final=False))
        base = self.put("base.xml", _join(BASE, "\n"))
        theirs = self.put("theirs.xml", _join(theirs_lines, "\n"))
        out = self.dir / "out.xml"

        code, text = self.run_tool(live, "--base", base, "--theirs", theirs, "--out", out)

        self.assertEqual(code, 0, text)
        got = out.read_bytes()
        self.assertTrue(got.startswith(bom))
        self.assertFalse(got.startswith(bom + bom))
        self.assertFalse(got.endswith(b"\n"))
        self.assertIn(b'v="THEIRS"', got)

    def test_malformed_xml_result_is_refused(self):
        theirs_lines = list(BASE)
        theirs_lines[4] = "</Wrong>"
        live_bytes = _join(BASE, "\r\n")
        live = self.put("live.xml", live_bytes)
        base = self.put("base.xml", _join(BASE, "\n"))
        theirs = self.put("theirs.xml", _join(theirs_lines, "\n"))

        code, text = self.run_tool(live, "--base", base, "--theirs", theirs, "--apply")

        self.assertEqual(code, 2, text)
        self.assertEqual(live.read_bytes(), live_bytes)
        self.assertEqual([p.name for p in self.dir.glob("live.xml.bak*")], [])

    def test_apply_backs_up_then_writes(self):
        theirs_lines = list(BASE)
        theirs_lines[3] = '  <Item id="c" v="THEIRS"/>'
        live_bytes = _join(BASE, "\r\n")
        live = self.put("live.xml", live_bytes)
        base = self.put("base.xml", _join(BASE, "\n"))
        theirs = self.put("theirs.xml", _join(theirs_lines, "\n"))

        dry, _ = self.run_tool(live, "--base", base, "--theirs", theirs)
        self.assertEqual(dry, 0)
        self.assertEqual(live.read_bytes(), live_bytes)

        code, text = self.run_tool(live, "--base", base, "--theirs", theirs, "--apply", "--tag", "unit")

        self.assertEqual(code, 0, text)
        baks = list(self.dir.glob("live.xml.bak-unit-*"))
        self.assertEqual(len(baks), 1)
        self.assertEqual(baks[0].read_bytes(), live_bytes)
        self.assertIn(b'v="THEIRS"', live.read_bytes())

    def test_detect_style_picks_the_dominant_terminator(self):
        self.assertEqual(merge_live_file.detect_style(b"a\r\r\nb\r\r\nc\n")[1], "\r\r\n")
        self.assertEqual(merge_live_file.detect_style(b"a\r\nb\r\nc\n")[1], "\r\n")
        self.assertEqual(merge_live_file.detect_style(b"a\nb\nc\r\n")[1], "\n")


class GitSpecTests(_Tmp):
    def _git(self, *args):
        subprocess.run(["git", "-C", str(self.dir / "repo"), *args], check=True, capture_output=True)

    def _commit(self, rel, lines, msg):
        (self.dir / "repo" / rel).write_bytes(_join(lines, "\n"))
        self._git("add", rel)
        self._git("-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", msg)
        return subprocess.run(
            ["git", "-C", str(self.dir / "repo"), "rev-parse", "HEAD"], check=True, capture_output=True, text=True
        ).stdout.strip()

    def test_git_specs_merge_and_base_search_ranks_smallest_diff_first(self):
        (self.dir / "repo").mkdir()
        subprocess.run(["git", "init", "-q", str(self.dir / "repo")], check=True, capture_output=True)
        self._git("config", "core.autocrlf", "false")
        far = ["<Items>", '  <Item id="a" v="9"/>', '  <Item id="b" v="9"/>', '  <Item id="c" v="9"/>', "</Items>"]
        rev_far = self._commit("w.xml", far, "far")
        rev_base = self._commit("w.xml", BASE, "base")
        theirs_lines = list(BASE)
        theirs_lines[3] = '  <Item id="c" v="THEIRS"/>'
        rev_theirs = self._commit("w.xml", theirs_lines, "theirs")
        live_lines = list(BASE)
        live_lines[1] = '  <Item id="a" v="LIVE"/>'
        live = self.put("live.xml", _join(live_lines, "\r\n"))
        out = self.dir / "out.xml"
        repo = self.dir / "repo"

        code, text = self.run_tool(
            live, "--base", f"{rev_base}:w.xml", "--theirs", f"{rev_theirs}:w.xml", "--repo", repo, "--out", out
        )
        self.assertEqual(code, 0, text)
        self.assertIn(b'v="LIVE"', out.read_bytes())
        self.assertIn(b'v="THEIRS"', out.read_bytes())

        code, text = self.run_tool(
            "--base-search", "w.xml", "--theirs", f"{rev_theirs}:w.xml", "--repo", repo
        )
        self.assertEqual(code, 0, text)
        body = [ln for ln in text.splitlines() if ln.strip()]
        order = [i for r in (rev_theirs, rev_base, rev_far) for i, ln in enumerate(body) if ln.startswith(r[:9])]
        self.assertEqual(order, sorted(order), text)
        self.assertEqual(len(order), 3, text)


if __name__ == "__main__":
    unittest.main()
