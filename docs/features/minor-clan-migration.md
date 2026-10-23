# Minor Clan Migration

## Overview

On every save load, the four minor clans whose data the #957 update changed are brought to the shipped values: the Grey Company's clan culture becomes `arthedain`, the Corsair Blades' becomes `umbar`, and each of the Grey Company, Corsair Blades, Beornings and Dunlending Reavers swaps its vanilla leader templates (`spc_*`) for the TAOM clones (`taom_spc_*`). A new campaign already has those values, so the migration changes nothing there. It is part of [minor-factions.md](minor-factions.md).

## Why This Exists

- **Vanilla behavior:** a save stores `Clan.Culture` (a `SaveableProperty`) and the clan's template list `Clan._minorFactionCharacterTemplates` (a `SaveableField`). On a load `SandBoxManager.InitializeSandboxXMLs(isSavedCampaign: true)` skips the Kingdoms and Factions files, so the clans are never rebuilt from `spclans.xml`, while the `NPCCharacters` file is always loaded again.
- **TAOM requirement:** #957 retags the four clans' troops to the culture of the clan's side (`lords.xslt`) and gives two clans a new culture and four clans cloned leader templates (`spclans.xslt`). An old save would keep the vanilla clan culture and templates next to the retagged troops.
- **Without this feature:** the Grey Company (clan culture `empire`, Dunland, Evil) would prefer Dunland contracts while its `arthedain` troops desert under Dunland (`AlignmentDesertion` reads the troop's culture), and its new leaders would still be spawned from the vanilla templates.

## Architecture

### Design Challenge

Both fields are saved engine state, written by the host's load from the shipped data; a joining co-op client receives the host's already migrated campaign as an in-memory snapshot and writes nothing itself. Both are public members of the engine type (`Clan.Culture { get; set; }`, `Clan.MinorFactionCharacterTemplates`, an `IList` over the saved list), so no reflection is needed. The decision must be deterministic: no RNG, no hero state.

### Solution Approach

`MinorClanMigrationService` is pure: it asks `IMinorClanMigrationAdapter.Snapshot` for the raw facts of the four clans (clan id, culture id, template ids), decides, and applies through the adapter. `MinorClanMigrationBehavior` is the thin boundary: it listens to `OnGameLoadedEvent` and nothing else, catches and logs any fault, and persists nothing. `MinorClanMigrationModule` registers the adapter and the service and declares the behavior (`OwnsSaveData` is false, because the engine saves the clan fields).

Entity state matrix (the migration writes data fields of a clan only, never a hero):

| Clan state | Mutates? |
|---|---|
| Not in the campaign, or not one of the four | No |
| Culture already the shipped one, templates already clones | No (a second load logs nothing) |
| Vanilla culture, vanilla templates (an old save) | Yes: set the culture, swap the templates |
| Clone not registered for a template | That template keeps its original; the others swap |
| Eliminated, serving a kingdom as mercenaries, or independent | Yes, the same way: the fields do not depend on the clan's state |

Heroes already spawned keep the culture they were created with (a hero's culture is saved with the hero).

### Component Diagram

```
OnGameLoadedEvent
        |
MinorClanMigrationBehavior (guard, log)
        |
MinorClanMigrationService (decide)
        |
IMinorClanMigrationAdapter -> Clan.Culture, Clan.MinorFactionCharacterTemplates
```

## Configuration

None. The four clan ids, the two target cultures and the clone naming (`taom_` + the original id) are constants of the service. `ShippedClanSideCoverageTests` fails when they stop matching the shipped data.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/MinorClanMigration/MinorClanMigrationService.cs` | The decision |
| `Main/Features/MinorClanMigration/MinorClanFacts.cs` | The raw facts of one clan |
| `Main/Features/MinorClanMigration/Hooks/MinorClanMigrationBehavior.cs` | `OnGameLoaded` boundary |
| `Main/Features/MinorClanMigration/MinorClanMigrationModule.cs` | Registration and the behavior declaration |
| `Main/Adapters/IMinorClanMigrationAdapter.cs`, `MinorClanMigrationAdapter.cs` | Reads and writes the two clan fields |
| `Main/Composition/FeatureModules.cs` | The module line |

## Dependencies

- `IModLogger` (Core): one INFO line per clan changed, a warning when the adapter refuses
- The data it follows: `spclans.xslt` (clan cultures, template names) and `lords.xslt` (the clones)

## Tests

- `TAOM.Tests/Features/MinorClanMigration/MinorClanMigrationServiceTests.cs`: old culture to new for each clan, already new, the two clans that keep their culture, each of the four clans' templates swapped in order, a missing clone, no clone at all, templates already clones, a template that is not an original leader, a clan outside the four, a fact for an unasked clan, the four ids asked, the log lines, a refusing adapter, and a second run changing nothing.
- `TAOM.Tests/Features/MinorClanMigration/MinorClanMigrationWiringTests.cs`: the module line, no hand wiring in `IoC.cs` or `SubModule.cs`, the container graph, the load-only listener, the guarded handler.
- `TAOM.Tests/Features/Execution/ShippedClanSideCoverageTests.cs` (`LiveInstall`): the migration's target cultures and clones equal the shipped data.
- The co-op row is in [coop-interop.md](coop-interop.md) ("Deliberately NOT gated").

## How to Add a Clan to the Migration

1. Add its id to `MinorClanMigrationService.ClanIds` and, when its own culture changed, to `ClanCultures`.
2. Add the matching rows to `MinorClanMigrationServiceTests` and run `ShippedClanSideCoverageTests`.

## Changelog

- 2026-10-21 (#957): added, after the deep review found that an old save kept the vanilla clan culture and leader templates beside the retagged troops.

## GitHub Issue

- **Issue:** [#957](https://github.com/haterade22/TAOM/issues/957)
- **Status:** In progress (part 2 is uncommitted on feat/relations-politics)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/minor-factions.md](./minor-factions.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
