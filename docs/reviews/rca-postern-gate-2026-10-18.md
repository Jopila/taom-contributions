# RCA: Postern Gate and the shared building pipeline, deep review, #855 (2026-10-18)

**Summary.** Postern Gate (#855) is TAOM's first custom building: a town and castle project, named by
culture, that cuts the troops lost breaking into or out of a siege there. It also builds the shared
pipeline later buildings reuse (type registration at campaign start, a culture gate in a
`BuildingModel`, seeding into loaded saves, and one Harmony category for the icon and the effect line).
A seven-lens deep review of the uncommitted tree on `feat/settlements-buildings` (base `3fe86e22b`)
confirmed two HIGH findings: seeding skipped every castle in an existing save, and the MCM switch was
read through `TaomSettings.Instance` on a path that runs every frame. It also confirmed six MEDIUM and
about twenty LOW findings. The orchestrator added two MEDIUM findings while checking the reports: the
seeding handler could loop the loading screen on a throw, and it gated on the wrong co-op predicate.
Every confirmed finding is fixed in the review's fix pass unless listed under "Not applied".

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | `TownBuildingsAdapter.Fortifications` walked `Town.AllTowns`, which holds towns only (`Campaign.OnDataLoadFinished` puts castles in `_castles`). Every castle in a save made before the feature never got the building; 151 of the live map's 234 fortifications are castles. New games were fine because the engine's own pass walks `Settlement.All`. | Wrong collection | The loop was copied from `ArmouryTownAdapter.AllTownIds`, which is towns-only by design, and its comment was rewritten to claim "every town and castle" without reading the list's fill site. The brief named that adapter as the pattern to copy. The service tests feed fiefs through a fake `Fortifications()`, so the adapter's enumeration was never exercised. | `Town.AllFiefs`, a source pin that `Fortifications()` reads it, and a castle check in the in-game list. Lesson appended to `lessons/adapters-taleworlds-api.md`. |
| 2 | HIGH | `PosternGateSettingsProvider.CutEnabled` resolved `TaomSettings.Instance` on every read. The defender's `menu_siege_strategies` is a wait menu whose tick re-runs every option condition each frame, and the break-out conditions call the loss model. | Hot-path settings read | The brief described the six menu callers as "menu texts", so nobody looked for a wait menu among them. Rule 9 in `csharp-architecture.md` names this case, but it only triggers once a path is known to be hot. | The cached lazy `Settings` accessor and the provider's rows in `CampaignHotPathSettingsProvidersTests`. |
| 3 | MED | The cut looked up the variant's name, allocated a closure, an `Action`, LINQ iterators and a `TextObject` per call, and passed the name to `AddFactor`, but `DefaultTroopSacrificeModel` builds its number with no explainer, so the name was dropped. Twice per frame in the siege menu. | Dead output | The brief asked for a labelled factor by analogy with other models; nobody read the base model's `ExplainedNumber` constructor. | `CutFor` returns a float; `FindTypeId` is deleted. |
| 4 | MED | Both Patch105 postfixes swallowed every exception with an empty catch, and the lazy hook factory re-threw on every widget update. | Silent catch | The builder treated "keep the screen working" as enough. `lessons/harmony-il.md` already requires a log line for such a catch; the rule existed and was not applied. | Log once per postfix behind a latch (the Patch96 shape); a failed factory is not retried. |
| 5 | MED | Neither module had a wiring test, so `OwnsSaveData => true`, the container link from the definition to the catalog, both model slots and the GameInit category were unpinned. | Missing test | The brief listed service tests only; the module-era sibling (`SiegeForcesWiringTests`) was named as a pattern for code, not for tests. | `TaomBuildingsWiringTests` and `PosternGateWiringTests`. |
| 6 | MED | The six variant ids are saved with each town, yet nothing pinned them. Renaming or removing one makes `Town.AfterLoad` drop the building and clear the town's whole build queue. | Save contract | A code-registered `BuildingType` id did not look like save data, because no `SaveableTypeDefiner` is involved. | A test listing the six ids (the gate; a trap-index line was tried and dropped, because the index is at its 45-row cap and the test enforces the rule), a warning on the definition and on `OwnsSaveData`, and a lesson in `lessons/state-lifecycle-save.md`. |
| 7 | MED | `TaomBuildingSeedingService.Apply` was unguarded inside an `OnGameLoaded` handler. A throw there does not crash: the loading state re-runs every tick, an endless loading screen. | Load-loop | `lessons/campaign-mechanics.md` records this exact class (the 2026-07-07 tester crash); it was not consulted because the brief framed seeding as "idempotent", which is about repeat runs, not about throws. | Seeding fails open per fief with one warning; a test drives a throwing `Add`. |
| 8 | MED | The seeding behavior gated on `CoopSessionPolicy.MayWriteSaveBackedState`, which is for TAOM `SyncData` keys. Adding buildings mutates shared world state, and `ICoopSessionProvider.IsAuthority` is the documented gate for that. | Wrong predicate | The brief named `MayWriteSaveBackedState` as the co-op gate; the two predicates read alike. | `IsAuthority`, matching `CastleRecruitmentBehavior` and `CultureConversionBehavior`. |
| 9 | LOW | The 13 string keys took the `taom_bld_` prefix, which BattleLoadDiagnostics already owns (`taom_bld_stall_*`). | Key ownership | The prefix was chosen in the brief without a grep; `lessons/localization-ui.md` already says a prefix is an ownership claim. | Renamed to `taom_postern_*` before the paid translation run. |
| 10 | LOW | With the switch off, the project card still advertised the cut. | Inconsistent UI | The switch was wired to the cut only. | The effect line reads the switch at display time (Mike's decision). |
| 11 | LOW | The 0.95 reduction ceiling let a small break-in round to 0 losses, which sends vanilla down its zero-loss shortcut and skips the break-in step. | Range | The ceiling was picked as "below 1" without checking the engine's zero branch. | Ceiling 0.85 (Mike's decision). |
| 12 | LOW | Descriptions ended "this besieged fief" (shown at all times) and the elf text did not fit Lindon. | Wording | Written per variant without reading them against every culture they cover. | Reworded before translation (Mike's decision). |
| 13 | LOW | Smaller items: the MCM hint hard-coded the config values; a dead `IsFortification` field; a `Lazy` that deferred nothing; an iterator on a per-frame path; `ITaomBuildingCatalog` with one implementer and no fake; adapters outside `Main/Adapters`; a hand-written float check instead of `FiniteFloatValidator`; a computed getter guarded on its result; stale contract comments (`OwnsSaveData`, the composition order, a false module-order rule, a false reason for the lazy hook); `gamemodels.md` 24 bytes over its cap; missing doc sections, INDEX lines, the state matrix and the API snapshot; a misplaced registry section; config edge tests. | Hygiene | Each was a local slip by the builder; no shared cause beyond finding 1's "copy the pattern without its reason". | Fixed in the fix pass. |
| 14 | LOW | Found by a RED test in the fix pass, and its twin by the convergence review: a JSON number above the `long` range made `Value<long>` (costs) and `Value<double>` (reductions) throw `InvalidCastException`, not the `OverflowException` the provider caught, so the outer catch reverted both fields. | Exception type | The overflow branch had no test, so the exception type it expected was never observed, and the first fix covered only the field its test named. | Each field reverts on its own with the "out of range" warning; `Cost_BeyondLongRange_Reverts_NeverThrows` and `Reduction_BeyondLongRange_RevertsOnlyReductions_NeverThrows`, each seen failing first. |

## Root-cause pattern

Findings 1, 3, 7 and 8 share one cause: the brief named a TAOM or engine pattern to copy (an adapter, a
labelled factor, "idempotent" seeding, a co-op predicate) without the reason the pattern holds, and the
builder copied the shape. Each pattern was right where it came from and wrong here. The reviewers
caught every one by reading the engine or the sibling's fill site, which the brief should have done.

Finding 1 is also a repeat of the castle-omission class: War Chronicle B03 (`rca-war-chronicle-batch-a-2026-10-09.md`)
missed castles on the volunteer path, and two data lessons record `castle_village_*` ids missed by a
filter. Each time, "town" in a name was read as "fortification".

## Why each lens caught or missed these

- Lenses 2 (engine) and 5 (data flow) both found finding 1 independently, by reading `Campaign.cs`.
- Lens 3 (efficiency) found finding 2 by tracing the menu tick; the brief had told every lens the callers were plain menus.
- Lens 1 (standards) found 3, 4, 5 and most hygiene items; lens 7 (XML) found 9; lens 4 (completeness) found the doc gaps.
- No lens found 7 or 8. Lens 5 traced seeding's data but not its failure mode, and every lens accepted the brief's co-op predicate. The orchestrator found both while checking the lens 6 pointer about fail-closed loads.

## Not applied

- **Decision 48, the end state of a fail-closed campaign start** (`plans/_audit/2026-09-23-opus/DECISIONS.md`):
  `TaomBuildingsModule` owns save data, so a registration failure throws out of campaign start. Whether
  the player then sees a crash report or an endless loading screen is unsettled for every save-owning
  module (WarChronicle, EyeOfSauron and ArmourAcquisition share it). The docs now mark it UNVERIFIED.
  A follow-up issue is owed on Mike's word.

## Lessons appended

- `lessons/adapters-taleworlds-api.md`: `Town.AllTowns` holds towns only; read a campaign collection's fill site before trusting its name.
- `lessons/state-lifecycle-save.md`: a code-registered `MBObjectBase` id that a save references is a save contract.
