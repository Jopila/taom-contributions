# RCA: Game Pass removal, Visual Studio deploy guard, commit-manifest gate, 2026-10-22

**Summary.** On Mike's decisions (Game Pass is not supported; he builds in Visual Studio; the release gate
reads the release commit's manifest; fix #969), one batch changed the deploy targets and the release gate.
Risk tier 2 under the tiered review: Codex (gpt-6-astra, xhigh) plus the Data flow and Tooling lenses, one
wave. The reviewers found one HIGH (Tooling) that Codex also raised: the #969 fix would have made four stub
modules visible that name a DLL the engine cannot find from their folder, so every launch would show four
"Cannot find" boxes. That hunk was taken out, so the batch needed no second wave; #969 holds the evidence
and the options. Five MEDIUM and several LOW findings were fixed in the same commit.

Sources: Codex output `docs/reviews/raw/codex-adversarial-deploy-gamepass-vsguard-2026-10-22.md` (local,
ignored), prompt `docs/reviews/codex-adversarial-deploy-gamepass-vsguard-2026-10-22.prompt.md`.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | Moving the stubs to `Modules/<ID>/SubModule.xml` makes them visible and ticked by default; each names `TAOM.Dependencies.dll`, which the engine looks for only in the stub's own `bin/` | Unchecked consequence | The fix was scoped to the path; nobody read what a visible stub then loads | Hunk removed; #969 updated with the engine lines and three options; a fix needs a launcher check |
| 2 | MEDIUM | Excluding the Game Pass bin in the packager meant "not gated", because /release ships the editor package from the channel folders, and all three still hold that bin (one with the retired `BehaviorTreeWrapper.dll`) | Release flow | The packager was treated as the shipping path | The gate now refuses a Game Pass bin in any planned module, dry runs included |
| 3 | MEDIUM | `git show` output decoded as cp1252: a BOM or a non-cp1252 byte turned the new manifest check off or crashed the gate | Encoding | `_git` used `text=True` | Manifest read as bytes; `_git` uses UTF-8; tests with a temp repo |
| 4 | MEDIUM | The hash-guard test passed for a condition that compared a hash with itself | Test cannot fail | Pinned the presence of tasks, not the compared values | Pins the exact built-versus-installed condition |
| 5 | MEDIUM | An old checkout's staged `_Module/bin/Gaming.Desktop...` still deploys through the additive `CopyModule` | Additive destination | Removing the writer does not remove old output | `FailOnGamePassStagingInModule` in Main and Dependencies |
| 6 | LOW | The guard skipped a missing installed core and did not check the editor bin's core | One member of a set | Designed from the Steam bin | Both now fail the deploy |
| 7 | LOW | An installed manifest that does not parse passed; the Game Pass write test missed writes through an item; stale counts and comments | Gaps | | Fixed with tests |

## Lesson

The third repeat today of one pattern: a change checked against one member of a set (one bin, one assembly,
one shipping path) and not the rest. Here the set was "what ships": the packager's copy list versus the
editor package Mike uploads. When a change narrows what ships, trace the real release path in
`.claude/skills/release/SKILL.md`, not the tool that dry-runs it.
