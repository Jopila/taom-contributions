# RCA: Harvest Road (#912), deep review of 2026-10-26

## Top line

The tier 1 review of Harvest Road (Codex at ultra; Lenses 5 Data flow, 2 Engine and 7 XML) found no CRITICAL or HIGH and
no runtime defect. Every engine call was verified against the installed v1.5.5 DLLs, and the event's only listener was
found safe for a non-caravan party. It confirmed 2 MEDIUM and 6 LOW findings, all in text, tests and one unused
parameter, and all are fixed. One Codex MEDIUM was disproved (D1). HarvestRoad, localization and the binding gate's
HarvestRoad tests pass after the fixes (135 and 11, plus the two new engine tests by name).

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The MCM hint (and its 13 XML rows, the feature doc, the feature map and the index) promised a "bought" pop-up when a caravan buys food. No caravan reaches `BuyFoodInternal`: `TryBuyingFood` needs `DoesPartyConsumeFood`, which is false for `IsCaravan` (`DefaultMobilePartyFoodConsumptionModel.cs:89`), and TAOM does not override it (Lenses 5, 7 and 2) | Player text about who triggers a feature | The plan read the method's caller (`TryBuyingFood`) and its settlement test, not the food model that caller consults; the builder wrote the hint from the plan | Hint reads "a lord's party"; lesson (campaign-mechanics) |
| 2 | MEDIUM | `TheEngineMembersThePatchBodiesName_StillExist_InInstalledEngine` (`BindingVerification`, no `RequiresGameIL`) executed `EquipmentElement.Equals`; the reference-assembly gate (`.ai/verification.md`) runs it on a `throw null` stub and fails (Codex) | Engine-executing binding test without its category | Repeat: the 2026-10-08 lesson says a builder brief must name `RequiresGameIL` for such a test, and this brief did not | The call moved to its own `RequiresGameIL` test; recurrence line on the lesson (testing-qa) |
| 3 | LOW | The keys `taom_hr_mcm_enable` and `taom_hr_mcm_enable_hint` took Hearth Roll's prefix (`taom_hr_age_*`, #906) (Lens 7) | Localization key prefix clash | Repeat: the 2026-08-23 lesson says grep every strings file before claiming a prefix; the builder chose an acronym of the feature name | Renamed to `taom_hroad_` while the 12 language rows were English; recurrence line on the lesson (localization-ui) |
| 4 | LOW | `coop-interop.md` gave the Harvest Road paragraph a closing sentence that says the switch counts among the simulation-relevant settings; the code excludes it (`CoopSettingsRelevance` Presentation set). The sentence belonged to the Red Arrow paragraph, where it is true (Lens 5) | Doc text moved with an insert | The paragraph was inserted before the sentence's end, so the Red Arrow sentence slid onto the new paragraph | Sentence restored to Red Arrow |
| 5 | LOW | The `ShouldWatch` comment said vanilla reaches both seams in towns only; the food seam also runs in villages, and the `hasTown` gate is what drops them (Lenses 5 and 2) | Wrong engine claim in a comment | Written from the castle finding, without rereading `TryBuyingFood`'s `IsTown || IsVillage` | Comment corrected; the doc's hourly-cost line says "town or village" |
| 6 | LOW | The doc described the pop-up as "a small stack of item icons"; the notice is a portrait and a line such as "Sold Grain x12 Hides x4" (`SandBoxUIHelper.cs:132-152`) (Lens 2) | Wrong UI description | Written without reading the notice's view model | Corrected |
| 7 | LOW | `TradeNoticeDiff.Compute` took an `IEqualityComparer` that no production caller passed; `EquipmentElement` already compares by item and modifier (Lens 5) | Unneeded generality | Added for a test convenience | Parameter and its test removed (simplicity criterion) |
| 8 | LOW | No test pinned the premise that the nameplate listener draws the notice for any party; an engine update adding an `IsCaravan` check would silence the feature with every test green (Lens 2) | Missing premise test | The binding tests pinned the seams and the raise, not the consumer | `TheNameplateListener_NeverAsksIsCaravan_InInstalledEngine` (`RequiresGameIL`) |

**Disproved.** D1 (Codex MEDIUM): a caravan's food notice could merge into its sale notice and show bought grain as sold.
No caravan reaches the food seam (finding 1's evidence), villagers have no leader hero and never buy food on this path,
and lords never raise a sale, so no party gets both signs from this feature. Lens 5 traced the same merge and found it
connected. A "not a caravan" guard would be a guard for a case v1.5.5 cannot produce, so it waits for Mike
(simplicity criterion).

## Root-cause pattern

**A claim about who reaches a seam stopped at the first gate (1, 5).** Both wrong claims came from reading one condition
of the caller and not the rest of the chain: the settlement test and not the food model for 1, the castle test and not
the village branch for 5. The code was right in both cases; only the words were wrong, and the words are what players
and the next engine bump read.

## Why each reviewer missed what it missed

- **Codex (ultra):** found 2 by decompiling the cached reference assembly; it raised D1 because it read the merge code
  and assumed a caravan reaches `BuyFoodInternal`, without the food model. It did not check text (1, 3, 4, 6).
- **Lens 5 (Data flow):** found 1, 4, 5 and 7, and disproved D1 before it was raised. It did not see 2 (a test-category
  question outside data flow) or 3 (key ownership).
- **Lens 2 (Engine):** verified 15 engine claims and found 1, 5, 6 and 8. It did not run the reference-assembly gate (2).
- **Lens 7 (XML):** found 1 and 3 and checked every byte of the 13 XML files.

## Not run

The reference-assembly binding gate (`-p:TaomGameRefs=RefAsm`) was not run on this machine; finding 2's fix removes the
only engine call from the test it flagged. No game session has run this build.

## Follow-ups

- `FOLLOW-UP` key-prefix gate (finding 3 is the second occurrence): no issue: a gate that maps each `taom_<prefix>_` to
  one feature is new scope, and filing an issue waits for Mike's word.
