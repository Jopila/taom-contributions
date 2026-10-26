# RCA: Smith's Measure (#910), deep review of 2026-10-26

## Top line

The tier 1 review of Smith's Measure (Codex at ultra; Lenses 5 Data flow, 2 Engine and 7 XML) found no CRITICAL or HIGH.
The smelt cap was traced through every reader of `GetSmeltingOutputForItem` (the smelting list, its preview, the smelt,
the co-op host's smelt and bulk smelting in #911) and found sound, as was the model's DI timing. The review confirmed
4 MEDIUM and 8 LOW findings, all in the Make Tools card, its text and its docs. All are fixed. Two proposals were left out
on the maintainer's decision (N1, N2). A new gate, `PrefabHintParentGateTests`, now catches the repeat behind finding 1.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The card's hint never showed: its parent carried `DoNotAcceptEvents="true"`, and a `HintWidget` relays only its parent's hover (`HintWidget.cs:18`, `EventManager.cs:897`). Codex found it; Lens 2 showed that the planned fix (move the hint into the button) fails for a disabled button, which hit-testing skips (`:882`, `:891`); Lens 7 showed the enabled state needs a hint inside the button too | Gauntlet hover routing | Third occurrence (Reeve's Ledger #857, Messengers #898): the lesson and the `gui-ui.md` rule existed, the builder copied a decorative-widget shape, and the structural test pinned one hint without asking which widget gets the hover | Root accepts events; one hint beside the button (disabled), one inside it (enabled), as vanilla's crafting buttons do; structural tests; **gate** `PrefabHintParentGateTests` over every in-code prefab extension, proved RED on the old card; recurrence on the lesson |
| 2 | MEDIUM | Make Tools allowed a press at stamina 10 or less, where vanilla greys every smithy action (`CraftingVM.cs:1034-1041`) and the co-op host refuses (Lenses 5 and 2) | A TAOM action beside vanilla's skipped vanilla's gate | The plan wrote "stamina at least the cost" from the cost model, without reading the screen's own enable rule | Stamina above 10 and at least the cost; tests at 10 and 11; lesson (campaign-mechanics) |
| 3 | MEDIUM | Player text used internal names: "Crafting experience" (the game shows Smithing), "the Refinement tab" (Refine), "one iron" (the recipe takes the grade named Iron) (Lens 7) | Text from code ids, not from what the player sees | The strings were written from `DefaultSkills.Crafting`, `IsInRefinementMode` and `CraftingMaterials.Iron3` | Each name taken from the game's own text; fixed in all 13 rows before the paid run; lesson (localization-ui) |
| 4 | MEDIUM | `taom_sm_too_tired` repeated vanilla's translated line for the same rule on the same screen (Lens 7) | Duplicate of a vanilla string | No check for an existing vanilla line before adding one | Uses `{=PRE5RKpp}`; the TAOM row is deleted from all 13 files |
| 5 | LOW | The card overlapped the crafting-hero toggle by 11 px (`Crafting.xml:492-494`) (Lens 2) | Layout | The x was chosen from the refinement panel, not from the widgets beside it | `MarginLeft` 200; a test pins it |
| 6 | LOW | The Make Tools switch counted as simulation-relevant in the co-op fingerprint, though co-op suppresses the card (Lens 5) | Settings classification | The default classification was left in place | `PlayerLocal`; covered count 324 |
| 7 | LOW | The doc did not say that under co-op the host's cap decides the yield a client receives (Lens 5) | Missing co-op note | Written for single player | One sentence in the co-op text |
| 8 | LOW | The registry row named a career passive that does not exist (`EnchantmentCostReduction`) and two of the five smithing feats (Lenses 5 and 2) | Wrong names in docs | Copied from an older row | `SmithingCostReduction` and all five feats |
| 9 | LOW | The doc said a press resets the refinement choice; `ExecuteSwitchToRefinement` keeps it (`RefinementVM.cs:149-159`) (Lens 2) | Wrong engine claim | Written without reading the refresh | Corrected, and the test comment too |
| 10 | LOW | The mixin comment said a property change cannot rebind a child view model; UIExtenderEx's `SetField` raises one that Gauntlet rebinds (Lens 2) | Wrong reason in a comment | Written from intuition | Comment gives the real reason (one tooltip is simpler) |
| 11 | LOW | No test kept the 12 seeded language rows equal to the English, so an English edit could ship stale (Lens 7) | Missing wording test | The feature had no `*WordingTests` language check | `EveryLanguageFile_HoldsOneRow_OfTheCurrentEnglish_NoneStale`, proved RED |
| 12 | LOW | The prefab comment gave a wrong reason for the card's x (Lens 7) | Wrong comment | As 5 | Rewritten |

**Not applied, on the maintainer's decision (2026-10-26, on my recommendation).**
- N1 (Codex LOW): no ceiling on Make Tools XP. Vanilla's own refine XP has none (`DefaultSmithingModel.cs:120-123`), and with
  the game's item values a press earns about 50; only an absurd item value makes it large.
- N2 (Lens 2 LOW): no guard around the `IoC.Resolve` in the mixin's constructor. The wiring and binding tests pin the
  resolve, and the repo's mixins are split on the pattern.

## Root-cause pattern

**A TAOM control beside a vanilla one did not copy what vanilla does for that control (1, 2, 4).** The card sits next
to vanilla's main action on the same screen. Vanilla's button already shows how to route a hint for both states, which
stamina rule applies, and which translated line explains a refusal. The card re-derived all three and got each one
wrong. Before you add a control next to a vanilla one, read the vanilla control's prefab, its enable rule and its
strings, and copy them unless there is a reason not to.

## Why each reviewer missed what it missed

- **Codex (ultra):** found 1 and N1, traced DI timing and every smelt caller; it did not check text or layout.
- **Lens 5 (Data flow):** found 2, 6, 7 and 8; it did not render the prefab (1, 5).
- **Lens 2 (Engine):** found 1's correct fix, 2, 5, 8, 9, 10 and N2.
- **Lens 7 (XML):** found the complete fix for 1, and 3, 4, 11 and 12.

## Not run

No game session has run this build: the card's look, its hover in both states and its place at 1080p and another
resolution are owed in game (`docs/features/smiths-measure.md`). The reference-assembly gate was not run on this machine.
