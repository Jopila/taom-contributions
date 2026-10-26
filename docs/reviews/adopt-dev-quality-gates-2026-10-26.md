# dev-quality-gates adoption review (2026-10-26)

Review of [mokevnin/dev-quality-gates](https://github.com/mokevnin/dev-quality-gates) against TAOM,
through `/adopt-external`. Mike thought it looked very promising and chose all five proposals,
including the two that change the build and his machine. Issue
[#1020](https://github.com/haterade22/TAOM/issues/1020).

## Coverage

- **Repository:** branch `main` at `35697a2f` (CC0-1.0, 62 files, created 2026-10-23). Read as data
  through `gh api`: `README.md`, `tools.json` (all 67 entries), `docs/patterns.md`, `docs/adopt.md`,
  `docs/not-adopted.md`, `research/stack-go-rust-jvm-dotnet.md`, `research/cross-cutting.md`,
  `research/custom-rules.md`, `research/stack-python-ruby-php.md`, `research/github-actions.md`,
  `package.json`, `mise.toml`, `hk.pkl`. Nothing was run.
- **TAOM:** a read-only agent mapped thirteen quality concerns (analyzers, banned constructs,
  suppressions, Python, shell, spelling and links, secrets, dependencies, generated files, coverage,
  contention, one aggregate command, agent-config lint); the main session checked the load-bearing
  claims and measured each baseline itself.

## What it is

An opinionated list of one recommended tool per job, mostly for polyglot web projects gated in CI
(Go, Rust, JS, Ruby, PHP, Python, infrastructure), with research notes and a page of setup patterns.
It holds no plugin, hook or skill, so there is nothing to install by accident. Its strongest part for
TAOM is `docs/patterns.md`: "An instruction without a check is advice. Prefer adding the check."

## Security

- **Safe to learn from:** yes. No install path, no lifecycle scripts (`package.json` holds only the
  list's own build tooling), CC0.
- **Tools installed on the desktop** (Mike's decision, item 5), each from its GitHub release with
  the zip's SHA-256 checked against the digest GitHub publishes for the asset:
  - ShellCheck 0.11.0 in `E:\Tools\shellcheck-0.11.0` (`8a4e35ab...e740e`).
  - Gitleaks 8.30.1 in `E:\Tools\gitleaks-8.30.1` (`d29144de...afc4e`, also matching the release's
    `checksums.txt`). The list recommends Betterleaks instead; Gitleaks was chosen because it is the
    established project (about 30k stars against 2.3k for Betterleaks, created February 2026), and
    Betterleaks' live validation sends found credentials to outside services, which a history scan
    has no need for. The list notes that Betterleaks reads Gitleaks' rules.
  - Neither was added to PATH or any setting. The shell gate looks for ShellCheck through
    `TAOM_SHELLCHECK`, then PATH, then the `E:\Tools` default; the Gitleaks step reads
    `TAOM_GITLEAKS`, then its default.
  - Ruff went from 0.15.5 to 0.17.2 and pip from 25.3 to 26.2.2, from PyPI into the user site of
    the system Python, on Mike's direction to keep every tool at its latest release. PyYAML 6.0.3
    was already the latest.

## Baselines measured

- **Python:** `python -m ruff check tools --select E9,F` found 132 F541, 50 F401, 31 F841, 9 F821
  and 1 F402. Of the nine undefined names, seven are in `tools/blender/spider_cfg.py`, which runs
  inside Blender with injected globals; two are a real defect: `tools/oneoff/generate_rivendell_materials.py`
  used `json` without importing it.
- **Shell:** ShellCheck at warning level over the 34 tracked scripts: 26 findings, no errors. 14 are
  SC2206, TAOM's required `set -f` split. Of the other 12, four were fixed: three SC2155 (two
  `local x=$(...)` lines and one `export x=$(...)`) and an unused retry variable (SC2034, now `for _`).
  Eight take a suppression with its reason, under five directives: four SC1112 (curly quotes that are
  the push gate's test data, one directive), an SC2069 (`2>&1 >/dev/null` captures only stderr on
  purpose), an SC2207 (a word split under `set -f`), an SC2010 and an SC2043 in the hook test suite.
- **Git history:** Gitleaks over 2,735 commits (337 MB, 97 s) found 19 `generic-api-key` matches, all
  false positives: save-data key constants, localization ids that contain "key", and a variable in
  vendored BCL code removed since. No real secret.
- **NuGet:** a restore of all four projects with `NuGetAuditMode=all` and level `low` reported no
  advisory. A net472 probe referencing Newtonsoft.Json 12.0.1 failed restore with
  `error NU1903 ... GHSA-5crp-9r3c-p9vr` under the new setting, so the gate works on TAOM's target.
- **Agent config:** all 20 skills and 9 agents already had parseable frontmatter and descriptions of
  at most 30 words.

## Decisions

| # | Item | Decision |
|---|---|---|
| A1 | Python lint (Ruff) | Adopted: `ruff.toml` with bug classes only (E9, F63, F7, F82, F811), target Python 3.14 (Mike's call), and `tools/tests/test_python_lint_gate.py` over the whole repo; the missing `json` import fixed; the Blender snippet carries a file-level `# ruff: noqa: F821` with its reason. Style classes stay off: 213 hits of churn for no runtime defect |
| A2 | Banned constructs as one check | Adopted: `TAOM.Tests/Infrastructure/BannedConstructsGateTests.cs`. `[Obsolete]` (ADR-004) is found by reflection over the four compiled assemblies; `#region` (ADR-003) and every conditional-compilation directive outside `Main/IoC.cs` (ADR-005 as written, not only `#if DEBUG`) by a line match in Main, Client, Dependencies and TAOM.Tests. Five per-feature checks did this before. AGENTS.md names the gate |
| A3 | Lint the agent instruction files (agnix) | Adopted as `tools/tests/test_claude_config_lint.py`: frontmatter, the 30-word cap including folded descriptions, skill keys read from the `harness-facts.md` allowlist row, and every hook script that settings name exists. agnix itself was not installed |
| A4 | NuGet Audit as errors | Adopted in `Directory.Build.props`: `NuGetAuditMode` `all`, NU1903 and NU1904 as errors; offline, NU1900 stays a warning. Edited through the session's config-protection override on Mike's word. NuGet skips the audit on a no-op restore, so `/verify` Step 1b runs `dotnet restore TAOM.sln --force-evaluate` |
| A5 | Shell lint and a history secret scan | Adopted: `tools/tests/test_shell_lint_gate.py` runs ShellCheck with no rc file over every `.sh` git lists and lets SC2206 pass only on a line that starts `set -f;`; Gitleaks as a `/security-scan` step with `.gitleaksignore` holding the 19 reviewed fingerprints |
| X1 | `TreatWarningsAsErrors`, `AnalysisMode`, Meziantou or Roslynator | Declined: a solution build reports 2,633 warnings, almost all nullable warnings in the test project, and a mod that follows game updates should not fail its build on a new SDK rule. The nullable ratchet already raises clean folders to errors |
| X2 | ArchUnitNET for layer rules | Declined: pre-1.0, and TAOM's adapter rule is checked per feature where the seam is known |
| X3 | Coverage ratchet (diff-cover, coverlet floors) | Declined: TAOM works test first, and a floor adds test runtime for little signal |
| X4 | Machine-wide slots for heavy commands | Declined: no recorded collision; each worktree builds into its own `obj/` |
| X5 | One aggregate check command | Declined: `/verify` and `/ship` run the lanes, and `.ai/verification.md` lists them |
| X6 | Review dates on suppressions | Not built: TAOM has no security suppression today. The `Directory.Build.props` comment asks for a reason and a review date on any future `NuGetAuditSuppress`; a check comes with the first one |
| X7 | jactionlint, Dependabot, Renovate, commitlint, mise and hk, lychee, typos | Declined: no CI to run them, a hook already gates the commit subject, the hooks are Claude hooks rather than Git hooks, external links rot slowly in these docs, and a spell checker would flag every Tolkien name |
| X8 | Every other stack's tools | Out of domain |

## Review

`/deep-review` at tier 2 (gates, tests, the build's restore): Data flow and Tooling, Codex at
`xhigh` on Mike's word ([prompt](codex-prereview-quality-gates-2026-10-26.prompt.md)), then
Standards, Completeness and Design in a second wave after a HIGH. RCA:
[rca-quality-gates-2026-10-26.md](rca-quality-gates-2026-10-26.md).

- **HIGH, found by Data flow and Codex independently:** the first ShellCheck rc file was LF only
  because a tool wrote it, and a checkout would make it CRLF, which ShellCheck rejects. The rc file
  is gone; the test passes ShellCheck's options itself.
- **MEDIUM:** shell discovery read only the index; Ruff linted a fixed folder list; NuGet skips the
  audit on a no-op restore; the C# scanner misread interpolated strings and attribute lists; the
  Gitleaks step could pass a scan that read no commits; a third copy of the skill-key list; no
  provenance rows for this adoption or the two before it (#996, #1007); no setup record for a second
  machine.
- **LOW:** weak assertions, floors at today's exact counts, separators in hook paths, cp1252
  decoding, `python3` shebangs, rule text narrower than the gate, review routing and the
  completeness lens's own grep.

All were fixed except three, each with its reason in the RCA: protecting the gate configs (Mike
declined), a ShellCheck version check, and a release trigger older than this change.
