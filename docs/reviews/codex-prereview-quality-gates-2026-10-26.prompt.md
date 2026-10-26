# Codex adversarial pre-review: quality gates (#1020)

You are reviewing an uncommitted change in this repository (a Lord of the Rings mod for Mount &
Blade II Bannerlord: C# on .NET Framework 4.7.2, a large Python tool suite under `tools/`, bash
hooks under `.claude/hooks/`). Read `AGENTS.md` first. This is a review only: change no file, run
no command that writes, and do not run `./build.ps1` (it deploys into the game).

## The change

Run `git status --short` and `git diff`; the untracked files are part of the change. It adds five
quality gates (issue #1020):

1. `ruff.toml` and `tools/tests/test_python_lint_gate.py`: Ruff on bug-class rules (E9, F63, F7,
   F82, F811) over tracked Python, with one per-file ignore; a missing `import json` fixed in
   `tools/oneoff/generate_rivendell_materials.py`.
2. `TAOM.Tests/Infrastructure/BannedConstructsGateTests.cs`: one test for ADR-003 (`#region`),
   ADR-004 (`[Obsolete]`) and ADR-005 (conditional compilation outside `Main/IoC.cs`).
3. `tools/tests/test_claude_config_lint.py`: frontmatter of `.claude/skills/*/SKILL.md` and
   `.claude/agents/*.md` (30-word description cap) and the hook script paths in
   `.claude/settings.json`.
4. `Directory.Build.props`: `NuGetAuditMode` `all`, NU1903 and NU1904 as errors.
5. `.shellcheckrc` and `tools/tests/test_shell_lint_gate.py` (ShellCheck at warning level over
   tracked `.sh`, with inline suppressions in `tools/test_hooks.sh` and small edits in
   `tools/context_budget_scan.sh`, `tools/oneoff/translate_all_remaining.sh` and
   `.claude/hooks/check-doc-config-drift.sh`); `.gitleaksignore` and a Gitleaks history-scan step in
   `.claude/skills/security-scan/SKILL.md`.

Tools on this machine: Ruff 0.15.5 as `python -m ruff`; ShellCheck 0.11.0 at
`E:\Tools\shellcheck-0.11.0\shellcheck.exe`; Gitleaks 8.30.1 at `E:\Tools\gitleaks-8.30.1\gitleaks.exe`
(neither on PATH). Windows with Git Bash; `core.autocrlf=true` system-wide; see `.gitattributes`.

## What to look for

- A gate that passes when it should fail: an input set that silently drops files (untracked,
  deleted, new folders), a tool crash or missing tool read as a pass, an exit code misread, a
  config file that stops parsing after a Git checkout on Windows.
- A gate that fails when it should pass on a clean checkout (line endings, paths, encoding).
- Behaviour changes in the edited shell scripts and the hook (`set -f` and `set +f` pairing, early
  exits, `set -e` interactions).
- The NuGet Audit settings: whether they reach every project and every build entry point, offline
  behaviour, no-op restores.
- The C# scanner: false negatives and false positives (comments, strings, verbatim and raw strings,
  attribute lists, path matching of the exemption).
- Anything a later maintainer will trip on.

## Output

For each finding: severity (P1 blocks, P2 should fix, P3 minor), file:line, the claim, the evidence
(a command and its output, or quoted code), and a concrete fix. Try to refute each finding before
you report it. End with the line `END OF CODEX REVIEW`.
