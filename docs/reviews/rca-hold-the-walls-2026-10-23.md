# RCA: Hold the Walls deep review, #871 (2026-10-23)

**Summary.** Hold the Walls extends the 10-hour AI pause vanilla gives a capturing army so that a Free-side army holds a fortress it just took. Six `/deep-review` lenses read the uncommitted tree before its first commit. They found no CRITICAL or HIGH defect, four MEDIUM and a set of LOW findings, re-read against the code and the v1.5.5 decompile. All were fixed before the first commit. The maintainer changed one approved number: the hold range is now 12 to 30 hours (base 22, code cap 32) instead of 12 to 40.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The config list `Sides` could never drop "Free": Json.NET's default `ObjectCreationHandling.Auto` appends a file's list to a non-empty default, so `["Evil"]` loaded as Free plus Evil. | Config loader semantics | It was the first army-targeting list with a non-empty default; every other config provider in this batch already used `Replace`. | `ObjectCreationHandling.Replace`; a test through the real provider. Lesson below. |
| 2 | MEDIUM | An army that dispersed or was pushed out of the fortress mid-hold kept TAOM's longer pause, because the record was dropped without restoring vanilla's remainder. | Engine state outlives the record | Drop and release were treated as the same thing. | Gone and Moved now release; only an expired hold drops with no write (a hold whose hourly step throws is also dropped with no write and logged, so that leader keeps TAOM's pause until the hold's end). |
| 3 | MEDIUM | The hold counts toward vanilla's 48-hour inactivity disband and release does not reset it, so a 40-hour hold left the army 5 to 8 hours to march out; the code claimed "four hours of margin". | Engine threshold read halfway | The plan read where the counter fires, not that it carries past the hold. | Maintainer's decision: 12 to 30 hours, cap 32; the text states the real reasoning. |
| 4 | MEDIUM | The owed in-game check ("no inactivity disbands in the log") had nothing to read: vanilla logs a disband only for the player's own army. | Unmeasurable check | The check was written from intent, not from where the evidence would come from. | An `ArmyDispersed` listener counts AI inactivity disbands in the daily line, with the feature on or off. |
| 5 | LOW | An army that walked in after the capture (vanilla never paused it) could be held; the release loop had no per-hold isolation; a second release path duplicated the hourly one; the switch had its own settings provider beside the group's; doc order, numbers and one file name were off; two error paths were untested. | Assorted | Built ahead of the review's simplicity pass. | `AiPaused` at start, isolation, one release path, the switch on the group provider, docs and tests corrected. |

## Root-cause pattern

Findings 2 and 3 share a cause: the feature borrowed an engine mechanism (the AI pause) and an engine limit (the inactivity counter) and reasoned about them only at the moment it touched them, not over their whole life (what happens to the pause when the army goes, what the counter does after release).

## Lessons

- `docs/reviews/lessons/state-lifecycle-save.md`: a config list with a non-empty default needs `ObjectCreationHandling.Replace`.
