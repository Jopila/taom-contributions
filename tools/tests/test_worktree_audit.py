#!/usr/bin/env python3
"""Unit tests for the read-only worktree audit (tools/worktree_audit.py).

Run:  python -m unittest discover -s tools/tests -p "test_worktree_audit.py"

Each test builds a throwaway git repo in a temp dir (local user config, base branch
bannerlord-1.5.x) and adds worktrees inside it. No network, no TAOM repo access.
"""
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import worktree_audit as wa  # noqa: E402


def git(cwd, *args):
    subprocess.run(["git", "-C", str(cwd), *args], check=True, capture_output=True, text=True)


class WorktreeAuditTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        root = Path(self.tmp.name)
        self.repo = root / "main"
        self.repo.mkdir()
        git(self.repo, "init", "-b", "bannerlord-1.5.x")
        git(self.repo, "config", "user.name", "Test")
        git(self.repo, "config", "user.email", "t@example.com")
        git(self.repo, "config", "commit.gpgsign", "false")
        (self.repo / "a.txt").write_text("one\n")
        git(self.repo, "add", "a.txt")
        git(self.repo, "commit", "-m", "base")
        self.root = root

    def tearDown(self):
        # git marks worktree files read-only on some platforms; ignore cleanup errors
        try:
            self.tmp.cleanup()
        except OSError:
            pass

    def add_wt(self, name):
        path = self.root / name
        git(self.repo, "worktree", "add", "-b", name, str(path))
        return path

    def buckets(self):
        rows = wa.audit(str(self.repo), "bannerlord-1.5.x")
        return {Path(r["path"]).name: r["bucket"] for r in rows}

    def test_buckets(self):
        self.add_wt("merged-clean")
        ahead = self.add_wt("ahead")
        (ahead / "b.txt").write_text("b\n")
        git(ahead, "add", "b.txt")
        git(ahead, "commit", "-m", "extra")
        wip = self.add_wt("wip")
        (wip / "a.txt").write_text("changed\n")
        untracked = self.add_wt("untracked")
        (untracked / "new.txt").write_text("x\n")
        b = self.buckets()
        self.assertEqual(b["main"], "main")
        self.assertEqual(b["merged-clean"], "safe")
        self.assertEqual(b["ahead"], "hold-unmerged")
        self.assertEqual(b["wip"], "hold-wip")
        self.assertEqual(b["untracked"], "review")

    def test_parse_detached_and_prunable(self):
        text = (
            "worktree /r/main\nHEAD aaaaaaaaaaaaaaaa\nbranch refs/heads/bannerlord-1.5.x\n\n"
            "worktree /r/det\nHEAD bbbbbbbbbbbbbbbb\ndetached\n\n"
            "worktree /r/gone\nHEAD cccccccccccccccc\nbranch refs/heads/gone\n"
            "locked why\nprunable gitdir file points to non-existent location\n"
        )
        blocks = wa.parse_porcelain(text)
        self.assertEqual(len(blocks), 3)
        self.assertEqual(blocks[0]["branch"], "bannerlord-1.5.x")
        self.assertEqual(blocks[1]["branch"], "(detached)")
        self.assertTrue(blocks[2]["prunable"])
        self.assertTrue(blocks[2]["locked"])

    def test_missing_base_exits_2(self):
        r = subprocess.run(
            [sys.executable, wa.__file__, "--repo", str(self.repo), "--base", "no-such-branch"],
            capture_output=True, text=True)
        self.assertEqual(r.returncode, 2)
        self.assertTrue(r.stderr.strip())

    def test_not_a_repo_exits_2(self):
        r = subprocess.run([sys.executable, wa.__file__, "--repo", str(self.root)],
                           capture_output=True, text=True)
        self.assertEqual(r.returncode, 2)


if __name__ == "__main__":
    unittest.main()
