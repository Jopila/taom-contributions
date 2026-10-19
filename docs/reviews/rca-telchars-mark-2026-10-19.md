# RCA: Telchar's Mark deep review (#875, 2026-10-19)

## Top-line

The first build of Telchar's Mark (inventory upgrade tints and a "Show only better items" filter) went
through a 7-lens deep review with no HIGH, eight MEDIUM findings and a set of LOW ones, all confirmed against the v1.5.5
decompile. Mike decided four behaviour questions (weapon target, what the filter keeps, Take all, the
adapter shape) and added a second MCM setting. The fix pass restructured the engine half around a
per-screen adapter owned by the screen mixin, moved the patch category to Patch109 (trunk took Patch105
for Hidden Hosts), and applied every finding below.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The checkbox was inserted as the FIRST child of `TopMenu`, under the tab-bar art, while the comment and doc said last. UIExtenderEx `InsertType.Child` inserts before the child at `Index`, which defaults to 0. | Library default assumed | The builder read "Child" as "append" and nobody opened `PrefabComponent.InsertAsChild`. Already recorded once (`codex-adversarial-career-ux-arc-2026-08-05.prompt.md:29`), never promoted to a rule. | `Index => int.MaxValue`, pinned by `Patch109WiringTests`. Lesson in `lessons/localization-ui.md`. |
| 2 | MEDIUM | The `AfterTransfer` postfix rescored once per transfer command: Take all over N stacks ran N+1 full rescores, a town sell-all one per unit. | Event cardinality | The build treated `AfterTransfer` as once per player action. `InventoryLogic` raises it once per command (`InventoryLogic.cs:849-871`). | Return early while `IsRefreshed` is false, which every vanilla handler sets around its commands. Lesson in `lessons/harmony-il.md`. |
| 3 | MEDIUM | Weapons were compared with the weakest equipped weapon of the same type, while vanilla's one-click equip replaces the first empty slot, else the first same-type slot (`SPInventoryVM.cs:4053-4088`), and shields retarget onto the first equipped shield (`:4151-4166`). A green row could replace the stronger weapon. | Rule taken from a spec, not from the engine | The scoring rule came from the design input; nobody read what a click on the row actually does. | Mike chose vanilla's target. Lesson in `lessons/gamemodels-services.md`. |
| 4 | MEDIUM | With the box ticked, vanilla Take all and Give all move only the visible rows (`TransferAll` skips filtered rows, `:4754-4759`). Not documented, and the hint did not say yellow rows stay. | Undocumented side effect | The hide reuses vanilla's filter flag, so it inherits every consumer of that flag; the consumers were not listed. | Mike kept the behaviour; the hint and doc now say it. |
| 5 | MEDIUM | An untagged test called `UpgradeStatsReader.Read(default(EquipmentElement))`, which throws on the reference assemblies (`EquipmentElement.Item` is `throw null`), so the no-game unit run would fail. | Repeat (`lessons/testing-qa.md`) | The builder ran only the full game-loaded suite. | Moved to a `RequiresGame` class; the fix pass ran the RefAsm unit filter (57 passed). |
| 6 | MEDIUM | Nothing pinned the patch category's three registration points; deleting the SubModule line left every test green. | Repeat (Patch39, `lessons/harmony-il.md`) | The sibling pattern (`Patch85...PatchClass_IsRegisteredInAllThreePlaces`) was not in the build brief. | `Patch109WiringTests`. |
| 7 | MEDIUM | The hooks and the toggle downcast `IoC.Resolve<IInventoryVMAdapter>() as InventoryVMAdapter`, re-captured QuickActions' active VM on every refresh, and resolved services outside the mixin constructor. | Borrowed ownership | The plan said "use the existing IInventoryVMAdapter", which pushed the feature onto another feature's captured state. | Per-screen `InventoryUpgradeScreen` owned by `UpgradeScreenMixin` (Mike overrode the spec's adapter decision); `IInventoryVMAdapter` is untouched. |
| 8 | MEDIUM | The feature doc lacked the scoring rules, values, How-To and several owed checks. | Doc completeness | Written before the review settled the rules. | Rewritten from the final code. |
| 9 | LOW | A Horse-typed item without horse data would throw out of the whole rescore (the engine's mount getters dereference `HorseComponent` unguarded); the first-rescore timing latched on an empty civilian-mode rescore; weights carried a validator for compiled constants and a `Scale` that changed no verdict; the equipment mode was magic numbers; the adapter interfaces sat in the feature folder; notifications were untyped; a systematic fault would log once per row per keystroke. | Assorted | Normal review catch. | All fixed; `UpgradeStatsReaderTests` pins the horse case. |

## Root-cause pattern

Findings 1, 2 and 3 share one shape: an engine or library contract was assumed from a name ("Child",
"AfterTransfer", "the weakest weapon is what you replace") instead of read from the code that runs. Each
was one decompile read away. The lessons below make each read a named step.

## Why each lens saw what it saw

- **Engine compatibility (2)** and **XML (7)** both found finding 1 by decompiling UIExtenderEx.
- **Data flow (5)** found findings 3 and 4 by tracing what a click and Take all do with the verdict and
  the filter flag; **efficiency (3)** and **engine (2)** found finding 2 by counting `OnAfterTransfer`.
- **Completeness (4)** found 5, 6 and 8 from the house test and doc rules; **standards (1)** and
  **design (6)** found 7 and the simplifications in 9.

## Follow-ups

- Telchar's Mark rescores once per command for TAOM's own batches (Unequip All, EquipPresets: up to 24
  small passes per click) and once per sold stack for Quick Actions batch sells, with a filter re-run each
  time while the box is ticked. Accepted and documented; the cost is owed as an in-game measurement.
- The convergence pass proposed loosening the horse guard to HorseComponent only; rejected, because
  `GetModifiedMountHitPoints` reads `HorseComponent.HitPoints`, which is `Monster.HitPoints`
  (`HorseComponent.cs:35`), so a null Monster would still throw.
- Companion plus non-transferable rows can show green although vanilla refuses the equip: no public
  member names the shown character cleanly; documented as a known limitation.
- `tools/snapshot_api_surface.ps1 -Check` reports drift on any snapshot file that carries the backlinks
  block, trunk included: the generator and `build_backlinks.py` disagree about that block.
