# RCA: Reeve's Ledger, deep review, #857 (2026-10-19)

**Summary.** Reeve's Ledger (#857) adds to the vanilla town management window: the fief's name in the title, a
Profit row with a breakdown tooltip, a Patrol row for towns, stash and prison tooltips, and +10K / -10K buttons
on the building reserve. It uses two UIExtenderEx mixins and two insert-only prefab patches, no Harmony and no
reflection. A seven-lens deep review of the uncommitted tree on `feat/settlements-buildings` (base `c4e4b4d43`)
found no HIGH finding and no defect in the gold path. It confirmed two MEDIUM findings (the "why disabled" hints
on the step buttons could never show; a settings hint promised more than the button does), one MEDIUM test gap,
and about fifteen LOW items. Every confirmed finding is fixed in the review's fix pass unless listed under
"Not applied".

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | Each step button's HintWidget sat inside the ButtonWidget whose `IsEnabled` is bound. Gauntlet's hover test (`EventManager.CollectEnableWidgetsAt`) returns early for a disabled widget and skips disabled children, and a HintWidget only relays its parent's hover, so the two "why disabled" hints ("You have no gold to add.", "The reserve is empty.") could never appear. | UI behaviour | The builder copied the shape of an existing button and its comment claimed that shape "shows a hint on a disabled button"; nobody read the hover path. That precedent was TAOM's own Messenger encyclopedia button, which has the same defect. | Vanilla's governor-button shape: a fixed, always-enabled wrapper with its own HintWidget, the button inside it. A structural test pins that each step hint has a non-button parent. Lesson appended to `lessons/localization-ui.md`. |
| 2 | MED | The `EnableReserveSteps` hint said the button takes "gold back out in full"; a press takes at most one step. | Over-claim | The hint was written from the refund rule, not from the step cap. | Reworded; the hints carry no numbers. |
| 3 | MED | The hint fix had no regression test, and the fix breaks the prefab test's top-level brush assert. | Missing test | Found by the completeness lens while planning the fix. | The structural pin above; the assert follows the new shape. |
| 4 | LOW | A constructor fault before the fault log existed went unlogged; the step's mismatch log shared a latch key with its exception log; the log message overstated what the latch skips. | Logging | The latch helper was built after two resolves that can throw. | `_faults` first, distinct keys, accurate text. |
| 5 | LOW | Eight tests constructed engine types (`TextObject`, `DataSourceProperty` through `GetCustomAttributes`) without the `RequiresGame` tag, so they would fail in the reference-assembly run. | Test category | The tests ran green on the desktop, where the game is installed. | Tag the text tests; the binding walk reads `CustomAttributeData`, which constructs nothing. |
| 6 | LOW | The reserve adapter wrote the gold, then vanilla's confirm wrote the same total again. | Duplication | The plan kept an adapter "for safety" without noticing vanilla's confirm already moves the gold from `CurrentReserveAmount + CurrentGivenAmount`. | The adapter is deleted; the step sets the VM's two fields and calls vanilla's confirm. The order is pinned. |
| 7 | LOW | Smaller items: an interface with one implementer and no fake; tooltip and hint view models rebuilt on every refresh; three new keys whose English equals translated vanilla keys ("Profit", "Projects", "Total"); untested saturation and zero-count branches; unpinned co-op reads; a misleading gate comment; doc and comment errors (row counts, "empties the trade tax" (it takes a fifth per call), the garrison line, the tax rounding, the -10K remainder, no How-To, checklist gaps, the co-op trade-off, the FiefManagement dependency, the issue section). | Hygiene | Local slips. | Fixed in the fix pass. |

## Root-cause pattern

Finding 1 is the third time in three days, on this one branch, that a precedent was copied for a property
nobody checked: Postern Gate's towns-only adapter (`rca-postern-gate-2026-10-18.md` finding 1), From the Ashes'
`ExitToLast` (`rca-from-the-ashes-2026-10-19.md` finding 1), and here a button shape whose comment claimed a hint
behaviour the engine does not provide; the precedent, TAOM's own Messenger button, carries the same defect. The
repeat-offender clause applies: the briefs for the remaining B10 features name the
engine fact each copied pattern depends on, and a reviewer checks that fact before the build.

## Why each lens caught or missed these

- Lenses 2 (engine) and 7 (XML) both found finding 1 by reading the Gauntlet hover path.
- Lens 5 (data flow) found finding 2 and the logging and pin gaps; lens 4 (completeness) found finding 3 and the doc gaps; lens 6 (design) found finding 6 and the vanilla keys; lens 3 (efficiency) found the per-refresh view models; lens 1 (standards) found the untagged tests and the interface.

## Not applied

- A mixin on `TownManagementReserveControlVM`, which declares no `OnFinalize`, makes UIExtenderEx stack one more hook on the base `ViewModel.OnFinalize` (the third such hook in TAOM). Removing it would need cross-scope bindings; the overhead is small. Recorded as a lesson in `lessons/harmony-il.md`.
- TAOM's Messenger encyclopedia button has the same disabled-hint shape, and `FromTheAshesTextsTests` constructs a `TextObject` untagged. Both are committed code outside this change; follow-up issues are owed on Mike's word.

## Lessons appended

- `lessons/localization-ui.md`: a hint on a disabled button needs an enabled parent.
- `lessons/harmony-il.md`: a mixin whose target declares no `OnFinalize` stacks a hook on every view model's `OnFinalize`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/namegiving.md](../features/namegiving.md)

<!-- backlinks-end -->
