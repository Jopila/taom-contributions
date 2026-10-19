# RCA: From the Ashes, deep review, #856 (2026-10-19)

**Summary.** From the Ashes (#856) lets the player rebuild a looted village of his own realm in a timed wait
menu (Evil players set their thralls to it), and help the current construction project of a town or castle
he owns. A seven-lens deep review of the uncommitted tree on `feat/settlements-buildings` (base `8806f33c3`)
confirmed one HIGH finding: every forced exit called `GameMenu.ExitToLast` from inside the settlement, which
leaves a party in a village with no menu and no way to move, and a save keeps that state. It also confirmed
five MEDIUM findings and about twenty LOW ones. Mike decided four behaviour points in the review (refund
forced cancels, leave the old gates out, remove the unused gold cost, count only regular healthy captives as
thralls). Every confirmed finding is fixed in the review's fix pass unless listed under "Not applied".

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | Forced exits (init with no running job, the fault guard, the landing fallback) called `ExitToLast` while the main party was inside the settlement. In a village this closes the menu but keeps `CurrentSettlement` and the player encounter; `GetGenericStateMenu` has no village case, so nothing reopens a menu and the party cannot move. | Menu exit | The approved brief prescribed "forced exits use ExitToLast or a switch to the settlement menu", copied from FieldCamp, whose `ExitToLast` calls run from camp menus on the open map. The reason the pattern was safe there (no settlement) did not travel with it. | One landing helper: the generic state menu, then the settlement menu, then `IEncounterAdapter.Finish(true)`; `ExitToLast` only outside a settlement. A source pin forbids a bare `ExitToLast` inside a settlement. Lesson appended to `lessons/campaign-mechanics.md`. |
| 2 | MED | A cancel caused by a siege landed on the plain `town` or `castle` menu, whose Leave has no siege check, so the party could walk out of a besieged fief without a break-out. | Menu exit | The landing used the settlement menu without asking the engine which menu the current state needs; vanilla's own wait menus route through `GetGenericStateMenu` every tick. | The landing helper consults `GetGenericStateMenu` first; a pin keeps that order. |
| 3 | MED | Early project completion (the queue emptied, usually by this feature's own credit) reported "hastens the ." because the building name came from a snapshot taken after the head was gone. | Stale snapshot | The completion text read the name from the same snapshot that proved the queue empty. | The job keeps the last non-empty project name in memory. |
| 4 | MED | The menu glue class was 201 lines, every member an engine callback, over ADR-002's 150-line cap; the line pin measured only the behavior. | Entry point size | Splitting the behavior moved the code out from under the only line-count check. | Split into a start-options class and a wait-menu class; every `Hooks/*.cs` file is pinned under 150 lines. |
| 5 | MED | The behavior had no row in `coop-interop.md`'s gated table, and a running job checked only `IsAuthority`, not the full single-player predicate. | Co-op gate | The co-op table is updated by hand and the brief did not list it; #921 tracks a machine check. | Row added; `Validate` cancels a running job outside single-player. |
| 6 | MED | Project materials were lost on every cancel, including ones the player did not choose. | Design gap | The brief said "paid at start" without saying what a forced cancel does. | Refund on a forced cancel; Stop by choice keeps them spent (Mike's decision). |
| 7 | LOW | A start while a job was stored showed a red "You need ." (the Busy block fell through to the item text); co-op peers saw disabled options where single-player hides them; the stash gate missed `IsOwnerUnassigned`; the status line went stale when the head project changed; four config fields were validated but never read; thralls counted captured lords and wounded; the alignment decision sat in the adapter untested; map-event pre-emption was not detected; one fault latch for every callback, a dead inner catch and several false comments; per-frame name renders, a closure per frame and fresh empty arrays on the wait tick; two end messages that misreported the outcome. | Hygiene | Local slips; the per-frame items came from not tracing which callback runs every frame. | Fixed in the fix pass, each with a test or a source pin. |

## Root-cause pattern

Findings 1 and 2 share the cause that the Postern Gate RCA (`rca-postern-gate-2026-10-18.md`) recorded the day
before: a pattern was copied with its shape and without the condition that made it safe. FieldCamp's
`ExitToLast` is safe because a camp menu has no settlement behind it; the settlement menu is a safe landing
only when no siege or encounter is pending. The brief (written by the orchestrator) prescribed both shapes.
This is the second feature in two days where the root cause sits in the brief, not in the build.

## Why each lens caught or missed these

- Lens 2 (engine) found finding 1 by following `ExitToLast` into `MapState` and `GetGenericStateMenu`; lenses 2 and 5 found finding 2 independently.
- Lens 5 (data flow) found finding 3 and most chain-level LOW items; lens 1 (standards) found 4; lens 4 (completeness) found 5 and the doc gaps; lens 3 (efficiency) found the per-frame allocations; lens 6 (design) found the duplicated site rules, the twin wait menus and the inert config fields.
- No lens missed a finding another lens confirmed.

## Not applied

- `PosternGateConfigProvider`'s redundant inner try/catch (the same shape as the one removed here) is committed code outside this change; it is a follow-up.
- Lens 3's alternative (validate hourly instead of every frame) was not applied: vanilla's own settlement wait menus check state every frame, and the per-frame cost is now small.

## Feedback to codify

When a brief names a pattern to copy, it must also name the condition that makes the pattern safe, so the
builder can check that the condition holds in the new place. Recorded in `lessons/campaign-mechanics.md`.
