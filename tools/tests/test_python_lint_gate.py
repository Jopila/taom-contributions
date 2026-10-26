#!/usr/bin/env python
"""Python bug-class lint gate (issue #1020).

Runs ruff with the repo's ruff.toml (E9, F63, F7, F82, F811: syntax errors and undefined names)
over the whole repo; ruff honours .gitignore. A finding fails the test with ruff's own output.

ruff is not stdlib. When it is not importable the test FAILS and names the install command, so a
machine without it cannot pass the gate by accident. Set TAOM_ALLOW_NO_RUFF=1 to skip knowingly
(a silent skip here would hide the whole gate).

Run:  python -m unittest tools.tests.test_python_lint_gate
"""
import importlib.util
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
CONFIG = REPO / "ruff.toml"
NO_RUFF = ("ruff is not installed for this interpreter, so the Python bug-class lint gate did not "
           "run. Install it with: python -m pip install --user ruff "
           "(see docs/reference/development-machines.md; set TAOM_ALLOW_NO_RUFF=1 to skip knowingly).")


def run_ruff(cwd, *args):
    """Run `ruff check` with the repo config; return (exit code, output)."""
    cmd = [sys.executable, "-m", "ruff", "check", "--config", str(CONFIG), "--no-cache", *args]
    proc = subprocess.run(cmd, cwd=str(cwd), capture_output=True, text=True,
                          encoding="utf-8", errors="replace", timeout=120)
    return proc.returncode, (proc.stdout + proc.stderr).strip()


def python_files_ruff_misses(root):
    """Git-listed .py files under root that ruff does not lint; also the git file count."""
    root = Path(root).resolve()
    listed = subprocess.run(["git", "ls-files", "-z", "--", "*.py"], cwd=str(root),
                            capture_output=True, encoding="utf-8", errors="replace",
                            timeout=60).stdout.split("\0")
    # A file deleted but not yet staged stays in the index; there is nothing left for ruff to lint.
    listed = {Path(p).as_posix() for p in listed if p and (root / p).is_file()}
    code, out = run_ruff(root, "--show-files", ".")
    if code != 0:
        raise AssertionError(out)
    linted = set()
    for line in out.splitlines():
        try:
            linted.add(Path(line.strip()).resolve().relative_to(root).as_posix())
        except ValueError:
            pass
    return sorted(listed - linted), len(listed)


class PythonLintGateTests(unittest.TestCase):
    def setUp(self):
        if importlib.util.find_spec("ruff") is None:
            if os.environ.get("TAOM_ALLOW_NO_RUFF") == "1":
                self.skipTest(NO_RUFF)
            self.fail(NO_RUFF)

    def test_repo_python_is_clean(self):
        code, out = run_ruff(REPO, "--output-format", "concise", ".")
        self.assertEqual(code, 0, "ruff found bug-class problems:\n" + out)

    def test_every_tracked_python_file_is_linted(self):
        missed, count = python_files_ruff_misses(REPO)
        self.assertGreater(count, 1, "git ls-files returned no Python files")
        self.assertEqual(missed, [], "tracked .py files ruff does not lint")

    def test_a_deleted_unstaged_file_is_not_reported_as_unlinted(self):
        with tempfile.TemporaryDirectory() as tmp:
            git = ["git", "-C", tmp, "-c", "user.name=t", "-c", "user.email=t@t"]
            subprocess.run(git[:3] + ["init", "-q"], check=True)
            for name in ("keep.py", "gone.py"):
                Path(tmp, name).write_text("x = 1\n", encoding="utf-8")
            subprocess.run(git + ["add", "keep.py", "gone.py"], check=True)
            subprocess.run(git + ["commit", "-q", "-m", "seed"], check=True)
            Path(tmp, "gone.py").unlink()  # deleted on disk, still in the index
            missed, _ = python_files_ruff_misses(tmp)
        self.assertEqual(missed, [])

    def test_gate_catches_an_undefined_name(self):
        with tempfile.TemporaryDirectory() as tmp:
            Path(tmp, "bad.py").write_text("def f():\n    return json.dumps({})\n", encoding="utf-8")
            code, out = run_ruff(tmp, "--output-format", "concise", "bad.py")
        self.assertEqual(code, 1, out)
        self.assertIn("F821", out)

    def test_gate_catches_a_syntax_error(self):
        with tempfile.TemporaryDirectory() as tmp:
            Path(tmp, "bad.py").write_text("def f(:\n    pass\n", encoding="utf-8")
            code, out = run_ruff(tmp, "--output-format", "concise", "bad.py")
        self.assertEqual(code, 1, "rc 2 is a ruff crash, not a finding:\n" + out)
        self.assertIn("invalid-syntax", out)


if __name__ == "__main__":
    unittest.main()
