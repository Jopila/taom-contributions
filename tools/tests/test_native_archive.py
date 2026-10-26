#!/usr/bin/env python
"""Unit tests for tools/native_archive.py, which archives each installed native engine build
(#1004).

Run:  python -m unittest discover -s tools/tests -p "test_native_archive.py"

Hermetic: a fake install (Version.xml plus fake DLL bytes) and a fake archive live in a tempdir,
the engine-method map and the matcher index are mocked, and Popen is mocked, so no case reads
the real install, runs ilspycmd or capstone, or starts a process.
"""
import contextlib
import hashlib
import io
import json
import os
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import native_archive as na  # noqa: E402

CLIENT_BYTES = b"client-dll-bytes" * 64
EDITOR_BYTES = b"editor-dll-bytes" * 64


def sha16(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()[:16]


class ArchiveCase(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.base = Path(self._tmp.name)
        self.game = self.base / "game"
        self.archive = self.base / "archive"
        self.client = self._install("Win64_Shipping_Client", CLIENT_BYTES)
        self.editor = self._install("Win64_Shipping_wEditor", EDITOR_BYTES)
        maps = mock.patch("native_engine_methods.load_or_build",
                          return_value=mock.Mock(problems=[]))
        index = mock.patch("native_build_match.load_index", return_value=mock.Mock())
        self.load_or_build = maps.start()
        self.load_index = index.start()
        self.addCleanup(mock.patch.stopall)
        self.addCleanup(self._tmp.cleanup)

    def _install(self, folder: str, data: bytes, version: str = "v1.5.5") -> Path:
        d = self.game / "bin" / folder
        d.mkdir(parents=True, exist_ok=True)
        (d / "Version.xml").write_text(f'<Version>\n\t<Singleplayer Value="{version}"/>\n</Version>',
                                       encoding="utf-8")
        dll = d / "TaleWorlds.Native.dll"
        dll.write_bytes(data)
        return dll

    def run_main(self, *extra):
        buf = io.StringIO()
        with contextlib.redirect_stdout(buf):
            rc = na.main(["--game-dir", str(self.game), "--archive-dir", str(self.archive), *extra])
        return rc, buf.getvalue()

    def state(self) -> dict:
        return json.loads((self.archive / na.STATE_NAME).read_text(encoding="utf-8"))

    def names(self) -> list:
        return sorted(p.name for p in self.archive.glob("TaleWorlds.Native.*.dll"))


class ForegroundTests(ArchiveCase):
    def test_new_dlls_are_copied_once_and_the_second_run_prints_nothing(self):
        rc, out = self.run_main()
        self.assertEqual(rc, 0)
        self.assertEqual(self.names(), ["TaleWorlds.Native.v1.5.5.client.dll",
                                        "TaleWorlds.Native.v1.5.5.wEditor.dll"])
        self.assertEqual((self.archive / "TaleWorlds.Native.v1.5.5.client.dll").read_bytes(),
                         CLIENT_BYTES)
        entry = self.state()[self.client.as_posix()]
        self.assertEqual(entry["sha16"], sha16(CLIENT_BYTES))
        self.assertEqual(entry["size"], len(CLIENT_BYTES))
        self.assertEqual(entry["archived_as"], "TaleWorlds.Native.v1.5.5.client.dll")
        self.assertEqual((entry["map"], entry["index"]), ("ok", "ok"))
        calls = (self.load_or_build.call_count, self.load_index.call_count)
        rc, out = self.run_main()
        self.assertEqual((rc, out), (0, ""))
        self.assertEqual((self.load_or_build.call_count, self.load_index.call_count), calls)

    def test_map_is_built_on_the_installed_dll_and_index_on_the_archive_copy(self):
        self.run_main()
        installed = {Path(c.args[0]) for c in self.load_or_build.call_args_list}
        self.assertEqual(installed, {self.client, self.editor})
        indexed = {Path(c.args[0]) for c in self.load_index.call_args_list}
        self.assertEqual(indexed, {self.archive / "TaleWorlds.Native.v1.5.5.client.dll",
                                   self.archive / "TaleWorlds.Native.v1.5.5.wEditor.dll"})

    def test_same_version_other_sha_gets_the_sha_suffix_and_the_old_file_is_untouched(self):
        self.archive.mkdir()
        old = self.archive / "TaleWorlds.Native.v1.5.5.client.dll"
        old.write_bytes(b"an older build")
        stamp = os.stat(old).st_mtime_ns
        self.run_main()
        self.assertEqual(old.read_bytes(), b"an older build")
        self.assertEqual(os.stat(old).st_mtime_ns, stamp)
        suffixed = self.archive / f"TaleWorlds.Native.v1.5.5.client.{sha16(CLIENT_BYTES)}.dll"
        self.assertEqual(suffixed.read_bytes(), CLIENT_BYTES)
        self.assertEqual(self.state()[self.client.as_posix()]["archived_as"], suffixed.name)
        self.assertEqual(list(self.archive.glob("*.tmp")), [])

    def test_an_sha_already_archived_under_another_name_is_not_copied_again(self):
        self.archive.mkdir()
        (self.archive / "TaleWorlds.Native.v1.5.4.client.dll").write_bytes(CLIENT_BYTES)
        self.run_main()
        self.assertEqual(self.names(), ["TaleWorlds.Native.v1.5.4.client.dll",
                                        "TaleWorlds.Native.v1.5.5.wEditor.dll"])
        self.assertEqual(self.state()[self.client.as_posix()]["archived_as"],
                         "TaleWorlds.Native.v1.5.4.client.dll")

    def test_a_changed_dll_is_archived_again_next_run(self):
        self.run_main()
        self._install("Win64_Shipping_Client", CLIENT_BYTES + b"patched", version="v1.5.6")
        self.run_main()
        self.assertIn("TaleWorlds.Native.v1.5.6.client.dll", self.names())

    def test_missing_game_dir_prints_nothing(self):
        self.game = self.base / "nowhere"
        self.assertEqual(self.run_main(), (0, ""))
        self.assertFalse(self.archive.exists())

    def test_missing_dll_is_skipped_silently_and_the_other_build_is_still_archived(self):
        self.editor.unlink()
        rc, out = self.run_main()
        self.assertEqual(rc, 0)
        self.assertNotIn("NOTE", out)
        self.assertNotIn("wEditor", out)
        self.assertEqual(self.names(), ["TaleWorlds.Native.v1.5.5.client.dll"])
        with mock.patch("subprocess.Popen") as popen:
            self.assertEqual(self.run_main("--detach"), (0, ""))
        self.assertEqual(popen.call_count, 0)

    def test_uncreatable_archive_dir_prints_the_note(self):
        blocker = self.base / "blocker"
        blocker.write_text("a file", encoding="utf-8")
        self.archive = blocker / "sub"
        rc, out = self.run_main()
        self.assertEqual(rc, 0)
        self.assertTrue(out.startswith("NOTE: native archive UNCHECKED: "), out)
        self.assertEqual(len(out.strip().splitlines()), 1)

    def test_a_corrupt_state_file_is_rebuilt_not_fatal(self):
        self.archive.mkdir()
        (self.archive / na.STATE_NAME).write_text("{not json", encoding="utf-8")
        rc, out = self.run_main()
        self.assertEqual(rc, 0)
        self.assertEqual(self.state()[self.client.as_posix()]["map"], "ok")


class FailureTests(ArchiveCase):
    def test_a_map_failure_is_recorded_reported_next_run_retried_and_keeps_the_copy(self):
        self.load_or_build.side_effect = RuntimeError("ilspycmd not found")
        self.run_main()
        copy = self.archive / "TaleWorlds.Native.v1.5.5.client.dll"
        self.assertEqual(copy.read_bytes(), CLIENT_BYTES)
        entry = self.state()[self.client.as_posix()]
        self.assertEqual(entry["map"], "failed: ilspycmd not found")
        self.assertEqual(entry["index"], "ok")

        with mock.patch("subprocess.Popen") as popen:
            rc, out = self.run_main("--detach")
        self.assertEqual(rc, 0)
        self.assertIn("NOTE: native archive: v1.5.5 client map failed: ilspycmd not found "
                      "(see the log)", out.splitlines())
        self.assertNotIn("archiving", out)
        self.assertEqual(popen.call_count, 1)

        self.load_or_build.side_effect = None
        self.run_main()
        self.assertEqual(self.state()[self.client.as_posix()]["map"], "ok")
        self.assertTrue(copy.is_file())
        with mock.patch("subprocess.Popen") as popen:
            self.assertEqual(self.run_main("--detach"), (0, ""))
        self.assertEqual(popen.call_count, 0)

    def test_an_index_failure_is_reported_as_an_index_failure(self):
        self.load_index.side_effect = RuntimeError("capstone missing")
        self.run_main()
        with mock.patch("subprocess.Popen"):
            rc, out = self.run_main("--detach")
        self.assertIn("NOTE: native archive: v1.5.5 client index failed: capstone missing "
                      "(see the log)", out.splitlines())

    def test_a_map_with_problems_counts_as_a_failure(self):
        self.load_or_build.return_value = mock.Mock(problems=["gap at 0x10"])
        self.run_main()
        self.assertTrue(self.state()[self.client.as_posix()]["map"].startswith("failed: "))


class LockTests(ArchiveCase):
    def _lock(self, age_seconds: float) -> Path:
        self.archive.mkdir(parents=True, exist_ok=True)
        lock = self.archive / na.LOCK_NAME
        lock.write_text("1 1\n", encoding="utf-8")
        t = time.time() - age_seconds
        os.utime(lock, (t, t))
        return lock

    def test_a_held_fresh_lock_prints_nothing_and_does_nothing(self):
        self._lock(10)
        with mock.patch("subprocess.Popen") as popen:
            self.assertEqual(self.run_main("--detach"), (0, ""))
        self.assertEqual(popen.call_count, 0)
        self.assertEqual(self.run_main(), (0, ""))
        self.assertEqual(self.names(), [])

    def test_a_stale_lock_is_taken_and_released(self):
        lock = self._lock(na.LOCK_STALE_SECONDS + 60)
        self.run_main()
        self.assertEqual(len(self.names()), 2)
        self.assertFalse(lock.exists())

    def test_the_lock_is_released_after_a_normal_run(self):
        self.run_main()
        self.assertFalse((self.archive / na.LOCK_NAME).exists())


class DetachTests(ArchiveCase):
    def test_detach_returns_without_waiting_and_prints_one_line(self):
        with mock.patch("subprocess.Popen") as popen:
            rc, out = self.run_main("--detach")
        self.assertEqual(rc, 0)
        log = self.archive / "native-archive.log"
        self.assertEqual(out, f"native archive: archiving v1.5.5 client, v1.5.5 wEditor in the "
                              f"background (log: {log.as_posix()})\n")
        self.assertEqual(popen.call_count, 1)
        cmd = popen.call_args.args[0]
        kwargs = popen.call_args.kwargs
        self.assertNotIn("--detach", cmd)
        self.assertEqual(cmd[1], str(Path(na.__file__).resolve()))
        self.assertIn(str(self.game), cmd)
        self.assertIn(str(self.archive), cmd)
        self.assertEqual(kwargs["stdin"], subprocess.DEVNULL)
        self.assertEqual(kwargs["stderr"], subprocess.STDOUT)
        if os.name == "nt":
            self.assertTrue(kwargs["creationflags"] & 0x8)    # DETACHED_PROCESS
            self.assertTrue(kwargs["creationflags"] & 0x200)  # CREATE_NEW_PROCESS_GROUP
        else:
            self.assertTrue(kwargs["start_new_session"])
        self.assertEqual(self.names(), [])  # the parent copied nothing

    def test_detach_with_nothing_to_do_starts_nothing(self):
        self.run_main()
        with mock.patch("subprocess.Popen") as popen:
            self.assertEqual(self.run_main("--detach"), (0, ""))
        self.assertEqual(popen.call_count, 0)

    def test_a_failed_spawn_says_unchecked(self):
        with mock.patch("subprocess.Popen", side_effect=OSError("no process for you")):
            rc, out = self.run_main("--detach")
        self.assertEqual(rc, 0)
        self.assertIn("NOTE: native archive UNCHECKED: ", out)

    def test_a_log_past_one_megabyte_is_truncated(self):
        self.archive.mkdir()
        log = self.archive / "native-archive.log"
        log.write_bytes(b"x" * (na.LOG_CAP + 1))
        with mock.patch("subprocess.Popen"):
            self.run_main("--detach")
        self.assertEqual(log.stat().st_size, 0)

    def test_a_small_log_is_kept(self):
        self.archive.mkdir()
        log = self.archive / "native-archive.log"
        log.write_bytes(b"earlier run\n")
        with mock.patch("subprocess.Popen"):
            self.run_main("--detach")
        self.assertEqual(log.read_bytes(), b"earlier run\n")


class StatusTests(ArchiveCase):
    def test_status_prints_one_line_per_archived_build(self):
        self.run_main()
        rc, out = self.run_main("--status")
        self.assertEqual(rc, 0)
        lines = out.strip().splitlines()
        self.assertEqual(len(lines), 2)
        self.assertTrue(any("v1.5.5 client" in l and "TaleWorlds.Native.v1.5.5.client.dll" in l
                            for l in lines), out)

    def test_status_with_no_state_says_so(self):
        rc, out = self.run_main("--status")
        self.assertEqual(rc, 0)
        self.assertIn("nothing archived", out)


if __name__ == "__main__":
    unittest.main()
