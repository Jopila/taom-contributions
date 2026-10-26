# RCA: Changed setting defaults that never reached saved settings (#1017, deep-review, 2026-10-26)

## Top-line

#1017 added a gate for a known trap (the mod options keep a saved value per setting name, so a changed default
under an unchanged name reaches nobody who saved before) and renamed the settings it caught. `/deep-review`
(a tooling lens, a gameplay lens on the renames, then a convergence pass) found that the **first gate missed
most real cases** (HIGH): it compared against one release tag, so every flip that had already shipped was
invisible, and it compared defaults as text, so a change in a constant's value passed silently. Rebuilt to
compare against every release tag reachable from HEAD and to resolve constants, it found more settings than the first run. Each was renamed or allowlisted with its commit and reason. The renames keep running
campaigns safe: the war phase is saved and only moves forward, hideout counts apply at world generation, and
nothing else is persisted.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | HIGH (Tooling) | One base tag hid every flip that had shipped in an earlier release; replaying the parent commit, the gate passed while all seven original flips were live. | A gate checked against the case it was written for | The base was chosen for speed, not for what the trap needs. | Every reachable tag; each finding names the oldest tag that differs. |
| F2 | HIGH (Tooling) | A default written as a constant was compared as text, so a changed constant value passed (two AI party size defaults did). | Silent skip | The docstring promised nothing is skipped; no test had a constant. | Constants resolved at each ref; a literal moved into a same-value constant reports nothing. |
| F3 | HIGH (Tooling) | A bad base, a wrong repository path or a missing tag printed "OK". | Silent clean | No negative tests. | Each exits 2; every run prints how many settings and tags it compared. |
| F4 | HIGH (Standards) | The new trap row was longer than the doc gate's cap and pushed the entry docs over their budget, which would block every later docs commit. | Budget | The row was edited without running the drift check. | A 177-character row; the drift check passes. |
| F5 | MEDIUM (Tooling, Data flow) | Nothing ran the gate. | Unwired gate | The issue asked for wiring; it was documented, not wired. | In the managed review lane and in the release skill before the tag. |
| F6 | MEDIUM (Data flow) | A project rule and the options doc still told builders to change a default and explain it in the hint. | A rule contradicting the trap | Written before the trap was understood. | Both say: rename the setting, never change its default; the gate checks it. |
| F7 | LOW (Data flow, Engine compat) | Postponed War Chronicle events in running campaigns were not mentioned; hint texts named old setting names players never saw; a few doc lines became false. | Accuracy | Routine. | Release note says it; one standard hint sentence; docs corrected. |

## Convergence pass

A second tooling and data-flow pass found three more gaps, all fixed: a constant used inside an expression
default (a dropdown) was still compared as text; the allowlist reason for the five AI party size settings cited
an acceptance their feature doc does not contain, and these were the largest deviations players kept, so they
were renamed too; and three docs still described the first gate. It also hardened the gate's degenerate paths
(a coloured git output, a base class on the next line, a partial settings class, no tag yielding a setting all
fail loudly) and cut its run from about a minute to two seconds by searching only C# files. Fourteen settings
are renamed in all.

Allowlisted, not renamed (reasons in `tools/mcm_default_flips_allowlist.json`): shield penetration (its lists
ship empty) and the culture conversion hold (saved only on v2.0.7 to v2.0.10; a rename would convert every
pending fief at once).

## Feedback memories to codify

None; the gate is the preventive action.
