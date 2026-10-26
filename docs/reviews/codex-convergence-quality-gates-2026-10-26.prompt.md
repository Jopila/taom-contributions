# Codex convergence review: quality gates (#1020)

Read `AGENTS.md` first. Review only: change no file, run no command that writes, never run
`./build.ps1`. Same uncommitted change as your pre-review (`git status --short`, `git diff`; untracked
files count). Your pre-review output is `docs/reviews/raw/codex-prereview-quality-gates-2026-10-26.md`
(read its final section). The first-round findings and their fixes are in
`docs/reviews/rca-quality-gates-2026-10-26.md`.

Defects only, no new design round. For each first-round finding, say whether the fix closes it, with
evidence. Then report any defect the fixes introduced. The main changes since your first pass:

- `.shellcheckrc` deleted; `tools/tests/test_shell_lint_gate.py` runs ShellCheck `--norc -f json1`,
  allows SC2206 only on a line starting `set -f;`, fails when ShellCheck exits above 1, and discovers
  scripts with `git ls-files -z --cached --others --exclude-standard` filtered to existing files.
- `ruff.toml` target py314, no per-file ignores; `tools/blender/spider_cfg.py` carries
  `# ruff: noqa: F821`; `tools/tests/test_python_lint_gate.py` lints `.` and checks every git-listed
  `.py` is in Ruff's file list. Ruff here is 0.17.2.
- `TAOM.Tests/Infrastructure/BannedConstructsGateTests.cs`: `[Obsolete]` found by reflection over
  four assemblies; directives by a per-line match with no comment or string blanking.
- `tools/tests/test_claude_config_lint.py` reads skill keys from `.claude/rules/harness-facts.md`;
  hook paths stop at shell separators; floors added.
- `.claude/skills/verify/SKILL.md` Step 1b (forced restore for the NuGet audit);
  `.claude/skills/security-scan/SKILL.md` Gitleaks step (repo root, `--exit-code 2`, commit count).
- Docs: `AGENTS.md` row 37, `.claude/rules/tests.md`, `.claude/rules/hook-authoring.md`,
  `docs/reference/development-machines.md`, `docs/reference/provenance-register.md`, `.ai/routing.json`.

Report each item as severity (P1, P2, P3), file:line, claim, evidence, fix. Try to refute each before
reporting. End with the line `END OF CODEX REVIEW`.
