#!/usr/bin/env python3
"""Tests for `--seed-only` in tools/translate_with_claude.py (#769 C1 follow-up).

Run:  python -m unittest tools.tests.test_translate_seed_only

`--sync-ids` only writes with `--apply`, and `--apply` goes on to call the paid API in the same run.
`--seed-only` is the free half: it writes the English rows a language file lacks (the same
sync_missing_ids code path, line terminators included) and returns before any provider is resolved,
any key is read or any client is built. These tests make every way of reaching a client fail the
test, then assert the seed still landed.

No test here touches the network or the real language files: the language dir and the English source
list are pointed at a temp dir.
"""
import contextlib
import io
import os
import shutil
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import _gamedir  # noqa: E402
import translate_with_claude as twc  # noqa: E402

SOURCE = (b'<?xml version="1.0" encoding="utf-8"?>\n<strings>\n'
          b'  <string id="taom_a" text="{=taom_a}Alpha" />\n'
          b'  <string id="taom_b" text="{=taom_b}Beta &amp; more" />\n'
          b'</strings>\n')
TARGET_HEAD = (b'<?xml version="1.0" encoding="utf-8"?>\r\r\n<strings>\r\r\n'
               b'  <string id="taom_a" text="Alfa" />\r\r\n')
TARGET_TAIL = b'</strings>\r\r\n'


class SeedOnlyTests(unittest.TestCase):
    def setUp(self):
        self.root = Path(tempfile.mkdtemp(prefix="twc-seed-"))
        self.addCleanup(shutil.rmtree, self.root, ignore_errors=True)
        self.src = self.root / "taom_module_strings.xml"
        self.src.write_bytes(SOURCE)
        self.lang_dir = self.root / "Languages"
        (self.lang_dir / "DE").mkdir(parents=True)
        self.target = self.lang_dir / "DE" / "std_taom_module_strings_deu-DE.xml"
        self.target.write_bytes(TARGET_HEAD + TARGET_TAIL)
        self.client_calls = []

        patches = [
            mock.patch.object(twc, "TAOM_LANG_DIR", self.lang_dir),
            mock.patch.object(twc, "english_source_files",
                              lambda module: [("TAOM", self.src, "std_taom_module_strings_{locale}.xml")]),
            mock.patch.object(twc, "resolve_provider", self._boom("resolve_provider")),
            mock.patch.object(twc, "require_key", self._boom("require_key")),
            mock.patch.object(twc, "call_model", self._boom("call_model")),
            mock.patch.object(twc, "call_claude", self._boom("call_claude")),
            mock.patch.object(twc, "call_claude_batched", self._boom("call_claude_batched")),
            mock.patch.object(twc, "load_overrides", self._boom("load_overrides")),
            mock.patch.object(twc, "load_cache", self._boom("load_cache")),
            mock.patch.object(twc, "discover_entries", self._boom("discover_entries")),
        ]
        for patch in patches:
            patch.start()
            self.addCleanup(patch.stop)
        self.fake_anthropic = mock.MagicMock()
        self.fake_anthropic.Anthropic.side_effect = lambda *a, **k: self.client_calls.append("Anthropic()")
        sys_modules = mock.patch.dict(sys.modules, {"anthropic": self.fake_anthropic})
        sys_modules.start()
        self.addCleanup(sys_modules.stop)

    def _boom(self, name):
        def fail(*args, **kwargs):
            self.client_calls.append(name)
            raise AssertionError(f"--seed-only reached {name}")
        return fail

    def run_main(self, *argv):
        out, err = io.StringIO(), io.StringIO()
        code = None
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            try:
                code = twc.main(list(argv))
            except SystemExit as exc:
                code = exc.code
        return code, out.getvalue() + err.getvalue()

    def test_seed_only_writes_the_missing_row_and_keeps_the_line_terminators(self):
        code, out = self.run_main("--lang", "DE", "--module", "TAOM", "--seed-only")
        self.assertEqual(code, 0, out)
        raw = self.target.read_bytes()
        self.assertIn(b'<string id="taom_b" text="Beta &amp; more" />\r\r\n', raw)
        self.assertTrue(raw.startswith(TARGET_HEAD), "an existing row or its terminator moved")
        self.assertNotIn(b"\n\n", raw.replace(b"\r\r\n", b""))
        self.assertIn("Ids seeded: 1", out)

    def test_seed_only_builds_no_client_and_reads_no_key(self):
        code, out = self.run_main("--lang", "DE", "--module", "TAOM", "--seed-only")
        self.assertEqual(code, 0, out)
        self.assertEqual(self.client_calls, [])
        self.fake_anthropic.Anthropic.assert_not_called()

    def test_seed_only_is_idempotent(self):
        self.assertEqual(self.run_main("--lang", "DE", "--module", "TAOM", "--seed-only")[0], 0)
        once = self.target.read_bytes()
        code, out = self.run_main("--lang", "DE", "--module", "TAOM", "--seed-only")
        self.assertEqual(code, 0, out)
        self.assertEqual(self.target.read_bytes(), once)
        self.assertIn("Ids seeded: 0", out)

    def test_seed_only_with_dry_run_previews_and_writes_nothing(self):
        before = self.target.read_bytes()
        code, out = self.run_main("--lang", "DE", "--module", "TAOM", "--seed-only", "--dry-run")
        self.assertEqual(code, 0, out)
        self.assertEqual(self.target.read_bytes(), before)
        self.assertEqual(self.client_calls, [])
        self.assertIn("Ids would-be-seeded: 1", out)
        self.assertEqual(out.count("Ids would-be-seeded:"), 1)

    def test_seed_only_refuses_every_option_that_would_translate(self):
        before = self.target.read_bytes()
        for extra in (["--apply"], ["--batch"], ["--max-entries", "5"], ["--model", "x"],
                      ["--provider", "deepseek"], ["--price-in", "1"], ["--price-out", "1"]):
            with self.subTest(option=extra):
                code, out = self.run_main("--lang", "DE", "--module", "TAOM", "--seed-only", *extra)
                self.assertEqual(code, 2, out)
                self.assertIn("error:", out)
                refused = extra[0]
                self.assertIn("drop ", out.split("error:", 1)[1])
                self.assertIn(refused, out.split("error:", 1)[1].split("drop ", 1)[1])
                self.assertEqual(self.target.read_bytes(), before, "a refused run wrote a file")
                self.assertEqual(self.client_calls, [])

    def test_seed_only_all_with_a_missing_install_exits_2_and_writes_nothing(self):
        before = self.target.read_bytes()
        missing = self.root / "no-such-install"
        with mock.patch.dict(os.environ, {_gamedir.ENV_VAR: str(missing)}):
            os.environ.pop(_gamedir.MODULES_ENV_VAR, None)
            for module in ("all", "Armory"):
                with self.subTest(module=module):
                    code, out = self.run_main("--lang", "DE", "--module", module, "--seed-only")
                    self.assertEqual(code, 2, out)
                    self.assertIn("the Bannerlord Modules folder", out)
                    self.assertEqual(self.target.read_bytes(), before)
                    self.assertEqual(self.client_calls, [])

    def test_seed_only_taom_needs_no_install(self):
        with mock.patch.dict(os.environ, {_gamedir.ENV_VAR: str(self.root / "no-such-install")}):
            os.environ.pop(_gamedir.MODULES_ENV_VAR, None)
            code, out = self.run_main("--lang", "DE", "--module", "TAOM", "--seed-only")
        self.assertEqual(code, 0, out)

    def test_seed_only_taom_map_is_refused(self):
        before = self.target.read_bytes()
        code, out = self.run_main("--lang", "DE", "--module", "TAOM_Map", "--seed-only")
        self.assertEqual(code, 2, out)
        self.assertIn("no TAOM_Map pass", out)
        self.assertEqual(self.target.read_bytes(), before)

    def test_sync_ids_dry_run_previews_the_seed_without_a_client(self):
        provider = {"name": "fake", "model": "m", "price_in": 0, "price_out": 0, "batch": False, "api": "openai"}
        before = self.target.read_bytes()
        with mock.patch.object(twc, "resolve_provider", lambda *a, **k: provider),                 mock.patch.object(twc, "load_overrides", lambda lang: {}),                 mock.patch.object(twc, "load_cache", lambda lang: {}),                 mock.patch.object(twc, "discover_entries", lambda lang, module: []):
            code, out = self.run_main("--lang", "DE", "--module", "TAOM", "--sync-ids", "--dry-run")
        self.assertEqual(code, 0, out)
        self.assertIn("ids to seed into std_taom_module_strings_deu-DE.xml", out)
        self.assertEqual(self.target.read_bytes(), before)
        self.assertEqual(self.client_calls, [])

    def test_seed_only_still_needs_a_language(self):
        code, _ = self.run_main("--module", "TAOM", "--seed-only")
        self.assertEqual(code, 2)

    def test_the_normal_modes_still_demand_dry_run_or_apply(self):
        code, out = self.run_main("--lang", "DE", "--module", "TAOM")
        self.assertEqual(code, 2, out)
        self.assertIn("--dry-run or --apply", out)


if __name__ == "__main__":
    unittest.main()
