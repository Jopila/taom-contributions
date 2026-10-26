#!/usr/bin/env python3
"""Unit tests for the in-game ledger (tools/ingame_ledger.py).

Run:  python -m unittest discover -s tools/tests -p "test_ingame_ledger.py"

A fake runner returns canned (returncode, stdout) pairs, so no test calls the real git or gh.
"""
import io
import json
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import ingame_ledger as il  # noqa: E402

TAG_DATE = "2026-10-20T12:00:00+02:00"  # 10:00 UTC


def issue(n, closed, title=None, url=None):
    return {"number": n, "title": title or f"issue {n}", "closedAt": closed,
            "url": url or f"https://github.com/o/r/issues/{n}"}


class FakeRunner:
    def __init__(self, describe=(0, "v2.0.35\n"), log=(0, TAG_DATE + "\n"), gh=None):
        self.describe = describe
        self.log = log
        self.gh = gh if gh is not None else (0, "[]")
        self.calls = []

    def __call__(self, args):
        self.calls.append(list(args))
        if args[0] == "git" and args[1] == "describe":
            return self.describe
        if args[0] == "git" and args[1] == "log":
            return self.log
        if args[0] == "gh":
            return self.gh
        raise AssertionError(f"unexpected command {args}")


def gh_ok(*issues):
    return (0, json.dumps(list(issues)))


def run(argv, runner):
    out, err = io.StringIO(), io.StringIO()
    code = il.main(argv, runner=runner, out=out, err=err)
    return code, out.getvalue(), err.getvalue()


class LedgerTests(unittest.TestCase):
    def test_default_tag_comes_from_describe(self):
        r = FakeRunner(gh=gh_ok())
        code, out, _ = run([], r)
        self.assertEqual(code, 0)
        self.assertEqual(r.calls[0], ["git", "describe", "--tags", "--abbrev=0",
                                      "--match", "v[0-9]*", "HEAD"])
        self.assertEqual(r.calls[1], ["git", "log", "-1", "--format=%cI", "v2.0.35"])
        self.assertIn("since v2.0.35 (2026-10-20)", out)

    def test_explicit_since_skips_describe(self):
        r = FakeRunner(gh=gh_ok())
        code, _, _ = run(["--since", "v2.0.30"], r)
        self.assertEqual(code, 0)
        self.assertFalse(any(c[1] == "describe" for c in r.calls))
        self.assertEqual(r.calls[0], ["git", "log", "-1", "--format=%cI", "v2.0.30"])

    def test_gh_query_shape(self):
        r = FakeRunner(gh=gh_ok())
        run([], r)
        gh_call = [c for c in r.calls if c[0] == "gh"][0]
        self.assertEqual(gh_call, ["gh", "issue", "list", "--state", "closed", "--label",
                                   "triage-needs-ingame", "--limit", "1000", "--json",
                                   "number,title,closedAt,url"])

    def test_cutoff_is_strict_and_timezone_aware(self):
        gh = gh_ok(
            issue(1, "2026-10-20T10:00:00Z"),        # exactly at the tag: older
            issue(2, "2026-10-20T10:00:01Z"),        # one second after: new
            issue(3, "2026-10-20T09:59:59Z"),        # before: older
            issue(4, "2026-10-20T11:30:00+02:00"),   # 09:30 UTC, before the tag: older
            issue(5, "2026-10-20T13:00:00+02:00"),   # 11:00 UTC, after: new
        )
        code, out, _ = run([], FakeRunner(gh=gh))
        self.assertEqual(code, 0)
        self.assertTrue(out.startswith(
            "in-game ledger: 2 issues closed since v2.0.35 (2026-10-20) still owe an in-game "
            "check; 3 older ones are in the backlog\n"))
        self.assertIn("#5 issue 5", out)
        self.assertIn("#2 issue 2", out)
        self.assertNotIn("#1 ", out)
        self.assertNotIn("#4 ", out)

    def test_sorted_by_number_descending(self):
        gh = gh_ok(issue(3, "2026-10-25T00:00:00Z"), issue(9, "2026-10-24T00:00:00Z"),
                   issue(5, "2026-10-26T00:00:00Z"))
        _, out, _ = run([], FakeRunner(gh=gh))
        lines = out.splitlines()
        self.assertEqual(lines[1:], ["#9 issue 9", "#5 issue 5", "#3 issue 3"])

    def test_text_keeps_titles_raw(self):
        gh = gh_ok(issue(7, "2026-10-25T00:00:00Z", title="[tools] #odd"))
        _, out, _ = run([], FakeRunner(gh=gh))
        self.assertIn("#7 [tools] #odd", out)

    def test_md_format(self):
        gh = gh_ok(issue(8, "2026-10-25T00:00:00Z", url="https://x/8"),
                   issue(2, "2026-10-01T00:00:00Z"))
        code, out, _ = run(["--format", "md"], FakeRunner(gh=gh))
        self.assertEqual(code, 0)
        self.assertEqual(out.splitlines(), [
            "## Please check in game",
            "- [ ] [#8](https://x/8) issue 8",
            "Older owed checks: 1 (label triage-needs-ingame).",
        ])

    def test_md_escapes_brackets_and_leading_hash(self):
        gh = gh_ok(issue(8, "2026-10-25T00:00:00Z", title="[ui] fix #5 [x]"),
                   issue(9, "2026-10-25T00:00:00Z", title="# heading"))
        _, out, _ = run(["--format", "md"], FakeRunner(gh=gh))
        self.assertIn(r"- [ ] [#8](https://github.com/o/r/issues/8) \[ui\] fix #5 \[x\]", out)
        self.assertIn(r"- [ ] [#9](https://github.com/o/r/issues/9) \# heading", out)

    def test_empty_text(self):
        code, out, _ = run([], FakeRunner(gh=gh_ok()))
        self.assertEqual(code, 0)
        self.assertEqual(out.splitlines(), [
            "in-game ledger: 0 issues closed since v2.0.35 (2026-10-20) still owe an in-game "
            "check; 0 older ones are in the backlog"])

    def test_empty_md(self):
        code, out, _ = run(["--format", "md"], FakeRunner(gh=gh_ok()))
        self.assertEqual(code, 0)
        self.assertEqual(out.splitlines(), [
            "## Please check in game",
            "Nothing closed since v2.0.35 owes an in-game check.",
            "Older owed checks: 0 (label triage-needs-ingame).",
        ])

    def test_count_prints_only_the_number(self):
        gh = gh_ok(issue(1, "2026-10-25T00:00:00Z"), issue(2, "2026-10-26T00:00:00Z"),
                   issue(3, "2026-01-01T00:00:00Z"))
        code, out, _ = run(["--count"], FakeRunner(gh=gh))
        self.assertEqual((code, out), (0, "2\n"))

    def test_exactly_1000_rows_warns(self):
        rows = [issue(n, "2026-01-01T00:00:00Z") for n in range(1000)]
        code, _, err = run([], FakeRunner(gh=gh_ok(*rows)))
        self.assertEqual(code, 0)
        self.assertIn("truncated", err)

    def test_under_1000_rows_is_quiet(self):
        rows = [issue(n, "2026-01-01T00:00:00Z") for n in range(999)]
        _, _, err = run([], FakeRunner(gh=gh_ok(*rows)))
        self.assertEqual(err, "")

    def test_gh_failure_exits_3(self):
        code, out, err = run([], FakeRunner(gh=(1, "")))
        self.assertEqual(code, 3)
        self.assertEqual(out, "")
        self.assertIn("ERROR:", err)
        self.assertIn("gh auth status", err)

    def test_gh_bad_json_exits_3(self):
        code, _, err = run([], FakeRunner(gh=(0, "not json")))
        self.assertEqual(code, 3)
        self.assertIn("ERROR:", err)

    def test_describe_failure_exits_3(self):
        code, _, err = run([], FakeRunner(describe=(128, "")))
        self.assertEqual(code, 3)
        self.assertIn("ERROR:", err)

    def test_unknown_explicit_tag_exits_2(self):
        code, _, err = run(["--since", "v9.9.9"], FakeRunner(log=(128, "")))
        self.assertEqual(code, 2)
        self.assertIn("v9.9.9", err)

    def test_missing_tool_exits_3(self):
        code, _, err = run([], FakeRunner(describe=(127, "")))
        self.assertEqual(code, 3)
        self.assertIn("ERROR:", err)

    def test_bad_format_is_an_argparse_error(self):
        with self.assertRaises(SystemExit) as cm:
            il.main(["--format", "html"], runner=FakeRunner(), out=io.StringIO(),
                    err=io.StringIO())
        self.assertEqual(cm.exception.code, 2)


if __name__ == "__main__":
    unittest.main()
