# RCA: The Red Arrow deep review, #873 (2026-10-24)

**Summary.** The Red Arrow lets the player call and release parties in Army Management while their own army besieges a settlement. Six `/deep-review` lenses in two waves read the uncommitted tree before its first commit. They found no CRITICAL or HIGH defect, two MEDIUM (rows 2 and 5) and a set of LOW findings, each re-read against the code and the v1.5.5 decompile, plus three design proposals (rows 1, 3 and 4). Everything was applied before the first commit except two LOW efficiency items declined under the simplicity rule (an empty text allocated per row, a per-read closure that the travel flattening then removed anyway), and the items the design changes made moot (a `?? base` model override, an adapter member for the model call, a travel sentinel). The largest change was a deletion: the GameModel override the plan called for had no visible effect, so it was removed, and `Main/SubModule.cs` and `TaomArmyManagementModel` stay as on trunk.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | Design proposal | The `CheckPartyEligibility` override on `TaomArmyManagementModel` changed nothing on screen. Its only engine caller is `ArmyManagementItemVM.UpdateEligibility`, and the feature's postfix on that method overwrites both outputs whenever the feature applies. The seam still cost a single-owner `SubModule.cs` edit, a cross-feature constructor argument and a second party check per row. | Redundant seam | The plan chose the model seam first ("ladder rung 1") and then added the postfix the UI needed, without asking whether the postfix alone decides every case. | Deleted the override; the boundary reads the candidate itself. Lesson below. |
| 2 | MEDIUM | `TryRecheck` called the active engine model directly, so the feature's main eligibility path had no unit test. | Boundary hygiene | The engine call sat between two adapter reads and looked like glue. | Gone with finding 1: `TryRecheck` now reads only faked adapters, with tests for refusal, eligible-and-clear and eligible-with-map-event. |
| 3 | LOW | Nothing failed when vanilla's copied checks change: the "order" test compared TAOM's enum with a list TAOM wrote. | Copied engine body | The copy was checked by hand against the decompile once. | IL fingerprint pins for `CheckPartyEligibility`, `GetMapScreenActionIsEnabledWithReason` and `UpdateEligibility`, each proved to fail on an edited token (standing lesson in `adapters-taleworlds-api.md`). |
| 4 | LOW | The travel facts sat behind a `Func` in a snapshot documented as primitives only. | Simplicity | A small deferral saving was taken without weighing its cost. | Flattened into the snapshot; one file and one test deleted. |
| 5 | MEDIUM (the tag), LOW (the rest) | A test class built `TextObject` without `RequiresGame`; a duplicate test; the postfixes were only wiring-tested; the settings provider was missing from the accessor-pattern gate. | Tests | Written ahead of the review. | Tagged, deleted, postfix tests on a bare view model, DataRow added. |
| 6 | LOW | Docs: a released member becomes disorganized (`BesiegerCamp.cs:273-276`); Done greys out when only the player's row is left; a release runs the engine's siege-menu refresh, which from the Kingdom screen may run in a context vanilla never reaches (unverified, an in-game check); the Prepare coupling was overstated; a misquoted vanilla string, a stale settings count and several wording slips. | Doc accuracy | Engine side effects of the newly reachable states were not traced. | Docs and in-game checks updated. |
| 7 | LOW | `Applies` read the co-op locks before the cheap siege facts. | Ordering | The gate was written in the order the facts were listed, not by cost. | Reordered. |

## Root-cause pattern

Findings 1 and 2 share a cause: a second seam was added to make the feature work, and the first seam was never re-examined once the second existed.

## Lessons

- `docs/reviews/lessons/gamemodels-services.md`: when a patch on a model's only caller already decides every case, drop the model override.
