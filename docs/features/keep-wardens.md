# Keep Wardens

## Overview

A castle with a built Guard House fields one patrol party of its owner's culture at a time, the way a town already does. The patrol grows stronger with the Guard House level (levels 1, 2 and 3 use the culture's weak, moderate and strong patrol template) and its size limit is 15, 20 or 25. A lost patrol is replaced after 10, 8 or 6 days. Since #888. The MCM switch is "Fief Management/Keep Wardens".

## Why This Exists

- **Vanilla behavior:** `DefaultSettlementPatrolModel` allows patrol parties for towns only and looks only for `DefaultBuildingTypes.SettlementGuardHouse`. The castle Guard House (`building_castle_guard_house`) raises militia and the prisoner limit and nothing else. Vanilla's `GetPatrolPartySpawnDuration` also reads the town Guard House without a null check, so asking it about a castle throws. `DefaultPartySizeLimitModel.CalculatePatrolPartySizeLimit` reads only the town Guard House too, so a patrol whose home is a castle gets a size limit of 0.
- **TAOM requirement:** castles are the frontier (Helm's Deep, Henneth Annun, Cirith Ungol). A castle that builds its Guard House should field riders or rangers.
- **Without this feature:** a castle can build its Guard House and still get no patrol.

## Architecture

### Design Challenge

- Vanilla's `PatrolPartiesCampaignBehavior` already queues, spawns and refills patrols for any settlement the campaign `SettlementPatrolModel` allows, so no behavior and no patch is needed. The model is the whole seam.
- The castle branch has to answer before the base model, because vanilla's spawn-duration method dereferences a null Guard House for a castle, and its template method returns null for every castle.
- A patrol that spawns with a size limit of 0 would show "25/0" and never refill, so the party size model needs a second seam.

### Solution Approach

- `TaomSettlementPatrolModel : DefaultSettlementPatrolModel` (campaign slot `SettlementPatrolModel`, declared by `KeepWardensModule` through `GameModelDecl`, so `SubModule.cs` and `IoC.cs` are untouched). Each override is one expression: when the castle plan has an answer it gives it, otherwise the base model (`DefaultSettlementPatrolModel`) does. Towns and naval requests keep vanilla's answer.
- `CastlePatrolService` (pure) turns an engine-free `CastlePatrolSnapshot` into a plan. The template answers any castle patrol: a castle that is not naval, whose owner culture has the patrol template for the level clamped to 1 to 3, gets that strength whatever the switch, the level or the owner says. Vanilla's hourly refill (`HourlyTickParty`, then `ReplenishParty`) asks for the template of a live patrol without asking whether the settlement may have one, and the base model returns null for every castle, which would throw. Only a NEW patrol honours the switch, the owner and the level: `Allowed` needs the setting on, an owner clan that is not a rebel clan and a Guard House of level 1 or more. A castle owned by no clan has no culture and so no template.
- `ICastlePatrolAdapter` reads the settlement into the snapshot. It finds the castle Guard House in `Town.Buildings` by building type; it does not use `TownBuildingsAdapter.GetLevel`, which looks the settlement up on every call.
- `ICastlePatrolSizeHook` is the second seam. `TaomPartySizeModel.GetPartyMemberSizeLimit` calls it right after the base call and before the culture feats. For a patrol party whose home is a castle, when the base limit is 0, it replaces the number with the configured limit for the Guard House level (a fresh `ExplainedNumber`, as vanilla's own town code returns). The hook ignores the MCM switch on purpose: a live patrol must never drop to a limit of 0. The model resolves the hook once in its constructor (`IoC.Resolve<ICastlePatrolSizeHook>()`), because a constructor parameter would need an edit to `SubModule.cs`. `CastlePatrolSizeHookTests` pins the source order.
- No new templates: every culture that owns a town or castle already points at patrol templates (`CulturePartyTemplateTests`), and the cultures without templates are bandit cultures that own no town or castle (hideout-only cultures own hideouts).

### Component Diagram

```
castle_patrols.json
        |
CastlePatrolConfigProvider   TaomSettings.EnableCastlePatrols
        |                          |
        +-------- CastlePatrolService --------+
                       |                      |
      TaomSettlementPatrolModel        CastlePatrolSizeHook
      (SettlementPatrolModel slot)     (TaomPartySizeModel, after base)
                       |                      |
                  ICastlePatrolAdapter (Settlement, MobileParty to snapshot)
```

## Configuration

### Config File: `Main/_Module/ModuleData/configs/castle_patrols.json`

| Field | Type | Description |
|-------|------|-------------|
| `spawnDays` | three numbers | Days between spawns at Guard House level 1, 2 and 3. Finite, above 0, at most 60. |
| `sizeLimits` | three whole numbers | The castle patrol's size limit at level 1, 2 and 3. 1 to 500. |

Each field reverts to its default on its own with one summary warning (a missing field, a wrong length, NaN, infinity, zero or negative, over the maximum, a fractional size). A missing or malformed file reverts both. The provider is a singleton, so an edit needs a full game restart.

### Current Values

| Level | Template | Spawn days | Size limit |
|---|---|---|---|
| 1 | weak | 10 | 15 |
| 2 | moderate | 8 | 20 |
| 3 | strong | 6 | 25 |

These are vanilla's own town numbers (`10 - (level - 1) * 2` days, `10 + 5 * level` troops), before the culture and feat factors that `TaomPartySizeModel` applies afterwards.

### MCM

`EnableCastlePatrols`, default on, group "Fief Management/Keep Wardens" (GroupOrder 4). Label key `taom_keep_wardens_label`, hint key `taom_keep_wardens_hint`. The setting is simulation-relevant and counts toward the co-op settings fingerprint. The settings provider reads through a cached accessor (`CampaignHotPathSettingsProvidersTests`).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/KeepWardens/KeepWardensModule.cs` | Module: the model declaration and the registrations |
| `Main/Features/KeepWardens/Models/TaomSettlementPatrolModel.cs` | The three overrides, castle branch first |
| `Main/Features/KeepWardens/CastlePatrolService.cs` | Plan and size-limit decisions |
| `Main/Features/KeepWardens/CastlePatrolConfigProvider.cs` | Validating loader for the config |
| `Main/Features/KeepWardens/CastlePatrolSettingsProvider.cs` | MCM switch, cached accessor |
| `Main/Features/KeepWardens/CastlePatrolSizeHook.cs` | The `TaomPartySizeModel` seam |
| `Main/Adapters/CastlePatrolAdapter.cs` | Settlement and party to snapshot, template lookup |
| `Main/_Module/ModuleData/configs/castle_patrols.json` | Spawn days and size limits |

## Dependencies

- `ICastlePatrolAdapter` (Adapters): the settlement, its owner culture's patrol templates and the castle Guard House.
- `TaomPartySizeModel` (CulturalFeats): carries the one hook line.

## Tests

- `TAOM.Tests/Features/KeepWardens/CastlePatrolServiceTests.cs`: 24 tests, the plan matrix (town, castle levels 0 to 3 and above, rebel, setting off, naval, missing template, clamp; a refused castle keeps its template strength) and the size override, NaN included.
- `CastlePatrolConfigProviderTests.cs`: 34 tests, one per validation rule, values beyond the long range, and a path service that throws. `ShippedCastlePatrolConfigTests.cs`: 2 tests on the shipped file.
- `CastlePatrolSizeHookTests.cs`: 7 tests, the boundary (ignores the setting), the source-order pin in `TaomPartySizeModel` and the one constructor resolution.
- `KeepWardensWiringTests.cs`: 8 tests, module listed once, one campaign model of the right slot, no behavior, no patch, not parked, no save data, no `if` in any override body, castle branch before base, the template named by strength, and the container validating.
- `CastlePatrolAdapterTests.cs`: 3 tests, the shared not-a-castle-patrol constant and a source pin on the template mapping and the castle Guard House.
- `CastlePatrolSettingsProviderTests.cs`: 2 tests, plus the rows in `CampaignHotPathSettingsProvidersTests.cs`.
- `ShippedCastleOwnerCultureTests.cs`: 1 test (`LiveInstall`), no castle owner clan has a hideout-only or settlementless culture. Inconclusive without the install.

## Co-op

Models only, nothing to gate. BannerlordCoop already runs patrol spawning on the host only; the clients receive the parties it creates. The setting is simulation-relevant and part of the fingerprint. BannerlordTogether: UNVERIFIED, nobody has checked how it handles these parties.

## Save Compatibility

No save data. The patrols are vanilla `PatrolPartyComponent` parties, saved by the engine. Turning the feature off removes no building. Parking Keep Wardens removes the model but not the size hook in `TaomPartySizeModel`, and with castle patrols alive that reopens the null-template crash on the hourly refill; parking it also means removing the hook line from `TaomPartySizeModel`.

## How to

**Change the spawn days or the size limits.**

1. Edit `Main/_Module/ModuleData/configs/castle_patrols.json`: `spawnDays` and `sizeLimits` each hold three values, for Guard House level 1, 2 and 3.
2. Restart the game, because the provider loads once per process. A value that fails validation reverts that field to its default with one warning in the log.
3. No code change.

**Turn castle patrols off.** In MCM, "Fief Management/Keep Wardens", untick "Castle Patrols". No new castle patrol is queued, and vanilla's daily check (`DailyTickSettlement`, then `UpdateSettlementParties`) removes the castle patrols already out.

## Performance

About 28 castles start with a Guard House, so a new game gains about 28 patrol parties, and at most 156 if every castle builds one (about 1 to 8 percent of the 2,000 to 2,500 parties on the map). The model reads a castle's buildings only for a castle. The size hook costs two property reads for a party that is not a patrol.

## Known Limits

- The size limit is read when the roster is next checked, so after a Guard House upgrade a patrol keeps its old limit until its roster changes.
- Vanilla queues castle spawns on its daily queue and its `OnBuildingLevelChanged` reacts only to the town Guard House, so a castle's first patrol waits for the queue (about 10 days at level 1).
- Turning the setting off lets vanilla remove the live castle patrols at the next daily tick. Keeping them would need reflection into private behavior methods, which this feature does not use.
- Castle patrols add no security until #889. They are parties on the map, and nothing yet reads them as a castle's protection.
- A new game spawns the 28 seeded castle patrols at once. A castle that builds its Guard House later waits for the daily queue.
- The town-window Patrol row is TAOM's Reeve's Ledger, gated on `IsTown` (`Client/Features/ReevesLedger/UI/TownManagementLedgerMixin.cs:103`). It is not added for castles, by choice.
- Patrol templates: vanilla spawns a patrol at each stack's maximum, and the stay-home gate is a size ratio below 0.9 (patrol size over the limit). Head counts at levels 1, 2 and 3 against the limits 15, 20 and 25:
  - Arthedain, Cardolan, Rhudaur: 11/14/14, under 90 percent at every level, so their 12 castles' patrols stay home once they return.
  - Goblin family: 15/17/21, under at levels 2 and 3.
  - Erebor: 17/22/27, over at every level.
  - Mordor: 23/21/17.
  - Dale: 24/36/24.
  - Dunland, Harad, Isengard, Rhun, Rohan: 24/26/24, over at levels 1 and 2.

  This predates Keep Wardens (towns use the same templates); a follow-up is pending the maintainer.
- `patrol_party_mordor` is TAOM data, not vanilla.

## Changelog

- 2026-10-23: castle patrols, the size hook and the MCM switch (#888).

## GitHub Issue

- **Issue:** #888, [Castle Guard House patrols](https://github.com/haterade22/TAOM/issues/888)
- **Status:** Open
