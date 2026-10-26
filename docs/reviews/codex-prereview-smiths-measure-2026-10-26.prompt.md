# Codex pre-review: Smith's Measure (#910), 2026-10-26

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` and `.ai/review-reference.md` first. Scope is the uncommitted working tree of this
worktree on top of `069962aa4` (`git diff` and `git status`; new files are untracked:
`Main/Features/SmithsMeasure/`, `Client/Features/SmithsMeasure/`, `Main/Adapters/*CraftingWorkshopAdapter.cs`,
`TAOM.Tests/Features/SmithsMeasure/`, `docs/features/smiths-measure.md`).
Engine source for v1.5.5 is in `C:\Users\mikew\.taom-src\v1.5.5\` (for example
`TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel.cs`,
`TaleWorlds.CampaignSystem.CampaignBehaviors.CraftingCampaignBehavior.cs`,
`TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.CraftingVM.cs`; list the directory
for exact names). The vanilla prefab is `Modules/SandBox/GUI/Prefabs/Crafting/Crafting.xml` under the
game install `E:\Steam\steamapps\common\Mount & Blade II Bannerlord`. Mark anything you cannot prove
from code as UNVERIFIED.

## The change

1. **Smelt cap.** `TaomSmithingModel` (`Main/Features/CulturalFeats/Models/TaomSmithingModel.cs`) now
   overrides `GetSmeltingOutputForItem`: the base output, then `SmeltingYieldCap.Apply` when the MCM
   switch is on. Slots 0 to 6 (ore, six iron grades) are lowered to the cap (default 1); hardwood
   (slot 7) becomes 0 when any metal comes out, else is lowered to the cap; charcoal (slot 8) and
   every negative value are untouched. The model's public constructor now resolves
   `ISmithsMeasureSettingsProvider` from `IoC` (the model is built in `Main/SubModule.cs` about :1195,
   which this change does not edit; the provider is registered through
   `Main/Features/SmithsMeasure/SmithsMeasureModule.cs` and one line in `Main/Composition/FeatureModules.cs`).
2. **Make Tools.** A UIExtenderEx ViewModel mixin on `CraftingVM` (hooked to private
   `RefreshEnableMainAction`) and a prefab insert (`Client/Features/SmithsMeasure/UI/`) add a card on
   the refinement tab. A press takes 1 Iron3 and 1 hardwood from the main party, gives 1
   `DefaultItems.Tools`, spends stamina equal to the model's refining cost for a formula of those two
   inputs (floored at 1), and grants Crafting XP of 30 percent of the value added. Logic in
   `Main/Features/SmithsMeasure/MakeToolsService.cs`; engine side in `Main/Adapters/CraftingWorkshopAdapter.cs`.
3. Three MCM settings (cap switch, cap size 1 to 10, Make Tools switch).

## Questions to answer with evidence

- DI timing: is `ISmithsMeasureSettingsProvider` always registered before `TaomSmithingModel` is
  built, in every load path (new campaign, load, sandbox, custom battle, a dedicated server, a co-op
  client)? What happens if `IoC.Resolve` fails there? Compare with `TaomPartySizeModel`.
- Every caller of `GetSmeltingOutputForItem` in v1.5.5 (the smelt itself, the list, the preview, AI or
  other behaviors, TAOM code): does any depend on the uncapped value, or on the array being the same
  instance? Can the cap produce a smelt that vanilla's UI gate then mis-prices (charcoal check)?
- Make Tools: can a press run with too little stamina, without the inputs, in the wrong game mode, for
  a hero other than the crafting hero, or twice from one click? Does it bypass anything vanilla does
  for refining (XP model, research, events)? Is the refining-cost formula with a placeholder output
  (IronOre x0) read correctly by `DefaultSmithingModel.GetEnergyCostForRefining` and by TAOM's
  overrides? Is the XP formula bounded (never negative, never huge)?
- UI: is the mixin binding sound (private method name, property change notification, tooltip), is
  the XPath right for v1.5.5 Crafting.xml, does the card name any sprite or brush that does not exist,
  and is it suppressed under co-op? Any client-only assembly named in Main (ADR-013,
  ServerJitGateTests, ClientIsolationGateTests)?
- Settings, registration and counts: does any count gate, classification or test break? Are the
  settings validated (range, sign)?

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact, and the
proving code or a reproduction. Say PASS for a question you checked and found sound.
