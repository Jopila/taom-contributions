# impeccable adoption review (2026-10-26)

Review of [pbakaus/impeccable](https://github.com/pbakaus/impeccable) against TAOM, through
`/adopt-external`. Mike asked for the same process as the ralph-claude-code review
([adopt-ralph-2026-10-24.md](adopt-ralph-2026-10-24.md)). Issue
[#1007](https://github.com/haterade22/TAOM/issues/1007).

## Coverage

- **Repository:** branch `main` at `f75ee2a2` (Apache-2.0, 4,359 files, pushed 2026-10-26, plugin
  version 4.7.1). The tree was listed through `gh api`; 110 files were downloaded as data into the
  session scratchpad and nothing was run: the Claude skill and agents, the plugin manifest and
  hooks, `.claude/settings.json`, `package.json`, the hook launcher `scripts/impeccable`, and the
  references `critique.md`, `audit.md`, `craft-floor.md`, `harden.md`, `polish.md`, `routing.md`,
  `component-review.md`, `document.md`, `doctor.md`, `hooks.md`. A read-only agent mapped the
  references against TAOM; the main session read the launcher, the hooks, `craft-floor.md`,
  `harden.md` and the critique checklists itself.
- **Not read:** the Rust engine crates (`crates/*`), the browser live mode, the 1,591 oracle test
  files, and the per-harness copies of the skill (the same 58 files for each of about 20 harnesses).
- **TAOM:** `.claude/rules/gui-ui.md`, `.claude/skills/localize/SKILL.md`, the deep-review lenses,
  `output-style.md`, and the localization tools.

## What impeccable is

A design skill pack for AI harnesses building web front ends: one skill with 24 commands
(`critique`, `audit`, `polish`, `harden`, `document` and others), one reference file per command, a
router that suggests the next command, a "craft floor" of checks and refusals loaded before any UI
edit, 62 detector rules over HTML and CSS, and four agents (a finish reviewer, a documenter, an asset
producer, an edit applier). Product and design state live in `PRODUCT.md` and `DESIGN.md`.

## Security

- **Safe to learn from:** yes.
- **Safe to install:** no.
  - The plugin registers SessionStart, PostToolUse (Edit and Write) and Stop hooks, and each runs
    `scripts/impeccable hook`, which starts a native engine binary. The PostToolUse hook pushes a
    reminder into the agent's context after each edit (`reference/hooks.md`).
  - The launcher runs, in order: the binary that `IMPECCABLE_BIN` names, a binary bundled under
    `scripts/bin/`, `~/.impeccable/bin/impeccable`, and any `impeccable` on PATH, all without a
    checksum. Only the version-pinned cache and the download from the project's GitHub releases
    are checked against `engine-checksums.txt`, and the download fails closed on a mismatch.
  - `package.json` has one lifecycle script, `prepack`, which only copies README files.
- **Automated vet:** `python tools/audit_claude_config.py --root <download> --external` scanned
  the 110 files and reported one INFO row (the secret sweep needs a git work tree). It did not
  flag the launcher's download-then-run path; the manual read above did.

## Decisions

| # | Item | Decision |
|---|---|---|
| A1 | Translations that may not fit their widget (`harden.md`: "German is often 30% longer", a 30 to 40% space budget) | Adopted: `tools/loc_length_report.py` and a `/localize` Validate step. TAOM ships 12 AI-drafted languages and nothing checked fit |
| A2 | Screen states, flow and consistency (`craft-floor.md` "States", `critique.md` cognitive-load checklist, `harden.md` extreme inputs) | Adopted: [gauntlet-screen-checklist.md](../reference/gauntlet-screen-checklist.md), with one pointer line in `gui-ui.md`. `gui-ui.md` covers the mechanics and the sprite render check, not what a screen must handle |
| X1 | Install the plugin, its hooks or the engine | Declined: see Security |
| X2 | The 62-rule detector and the browser live mode | Declined: they read HTML and CSS; Gauntlet prefabs are neither |
| X3 | One router skill with per-command references | Declined: TAOM keeps many thin skills with short descriptions under a context budget (`external-skill-ports.md`); one large router body works against it |
| X4 | The visual "AI slop" tells for HTML pages | Declined: TAOM's HTML reports go through the Artifact tool, which loads the `artifact-design` skill for page design |
| X5 | The finish-reviewer agent (reviews screenshots of a built page) | Declined: Claude cannot capture in-game screenshots; the in-game check stays Mike's, tracked by `triage-needs-ingame` and the `Not-tested:` trailer |
| X6 | Audit scores (0 to 4 per dimension, P0 to P3) | Declined: `/deep-review` severities cover it, and the dimensions (contrast ratios, responsive layout, theming) are web standards |
| X7 | A design-system document generated from the shipped UI (`document.md`, the documenter agent) | Declined for now: no TAOM consumer asked for one. Its rule "record a defect as a defect, never as the house style" is worth keeping in mind if one is written |

## The fit report on today's data

At the defaults (`--ratio 1.4`, `--min-extra 8`, CJK characters counted double as an assumed
glyph width, a gender conditional measured by its longer branch), on trunk `2838b25c2`:

- **All keys:** `4588 of 18772 keys flagged (ratio >= 1.4); 1681 untranslated or identical to
  English (names included), so their fit is unknown`. FR gives the worst language for 1,006 flagged
  keys, SP 766, IT 750, BR 638, DE 496, RU 402. The top rows are real: "Armorer" (7 characters) is
  "Fabricante de Armaduras" (23) in BR. An all-keys run is too broad to act on: the tool cannot tell
  which widget shows a key, or whether that widget wraps.
- **One feature's keys,** the way `/localize` runs it: `--since HEAD~40` gives `0 of 97 keys flagged
  (ratio >= 1.4); 97 untranslated or identical to English (names included), so their fit is
  unknown`. Their language rows hold the seeded English (for example `taom_keep_wardens_hint` in DE
  and FR), waiting for a paid translation run. The first version printed only "0 of 97 keys
  flagged", which read as "everything fits"; the untranslated count was added for that reason.

## Review

One adversarial read of the branch found no HIGH finding, two MED and seven LOW, each checked
against the files before the fix. MED: the checklist told builders to put a disabled control's
reason in its own hover hint, which Gauntlet never shows (TAOM's #944 found this); and it capped a
decision at four choices, which vanilla menus and TAOM's own five-option tribute popup do not
follow. LOW: gender conditionals counted both branches, two CJK ranges were wrong, `--since` missed
untracked files, an unused `--module` flag, the untranslated wording, the exit codes in the docs,
a weak smoke test, and the `/localize` step's placement. All were fixed.
