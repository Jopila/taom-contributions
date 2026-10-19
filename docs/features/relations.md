# Relations

## Overview

The shared seam for new relation code. `IRelationGrantService.Grant` clamps and filters the request, and `IHeroRelationAdapter` makes the engine call. [Bonds of Battle](bonds-of-battle.md) is the first user.

## Why This Exists

- **Vanilla behavior:** `ChangeRelationAction.ApplyPlayerRelation` takes whatever it is given, and each caller decides for itself whether a zero, a dead hero or a huge number is sensible.
- **TAOM requirement:** Several features will earn or spend relation. Writing the rules once keeps them identical for the code that uses the seam, and puts its log line in one place.
- **Without this seam:** Each feature repeats the same guards, and one of them eventually forgets one.

## Architecture

### Solution Approach

`RelationGrantService.Grant(heroId, delta, reasonForLog)`:

1. A blank hero id, a zero delta or a dedicated server returns 0 and touches nothing.
2. The delta is clamped to -100..100.
3. `IHeroRelationAdapter.ApplyPlayerRelation` applies it with the on-screen quick notification off. It returns what landed, the relation with the player after the change minus before (`player.GetRelation(hero)` read before and after the engine call), and 0 for an unknown hero, a dead hero, the player hero, a zero delta, or an engine fault (logged by the adapter). The call's own `affectRelatives` argument is not forwarded by the v1.5.5 engine, so the seam does not offer it.
4. `Grant` returns that number. A non-zero change is written to the TAOM log as `[Relations] <hero id> asked +N, landed +M (<reason>)`, and nothing is logged when nothing landed. A caller shows its notice only for a non-zero result.

### What the engine does with a change

- Every change runs through `DiplomacyModel.GetEffectiveRelationChange` first. For a positive change the default model adds the Charm skill bonus and the Charm and trait perks (and a personality factor), so the amount that lands can be larger than the one granted.
- The change lands on the clan leaders (`GetHeroesForEffectiveRelation`) under the default model, not necessarily on the hero named, unless both are the same person or one is the player's companion.
- The result is clamped to -100..100.
- Because of this the amount that lands can differ from the ask, which is why the seam returns what landed.

### Personal relations and the grant

With personal relations engaged, a player grant lands on the lord named: `GetHeroesForEffectiveRelation` keeps the other side of a pair that includes the player. With it off, the grant lands on the clan leader. So Bonds of Battle's battle grants count once per recipient (one per lord with Personal Relations engaged, one per clan without it), and its hand-overs count per relation owner, the hero the relation lands on, so kinsmen share one day's cap without it.

### Callers outside the seam

Three older callers still change relation directly and are migration candidates: `CommanderLordAdapter.ApplyPlayerRelation`, `ArmouryPlayerAdapter.ChangeRelationWith` and `SiegeDefenseService.GrantReward`. An amount that a game model returns (the barter overpay) is applied by vanilla, not by the seam.

### One registration only

Never register a second `IRelationGrantService` or `IHeroRelationAdapter`. A second unkeyed default makes every resolve of the interface throw (see the note above the UncapturableHeroes registration in `Main/IoC.cs`).

### Component Diagram

```
Feature service
      |
IRelationGrantService (RelationGrantService)
      |
IHeroRelationAdapter (HeroRelationAdapter)
      |
ChangeRelationAction
```

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/Relations/IRelationGrantService.cs` | The seam |
| `Main/Features/Relations/RelationGrantService.cs` | Clamp, filter, log |
| `Main/Adapters/IHeroRelationAdapter.cs`, `HeroRelationAdapter.cs` | Engine boundary |

## Dependencies

- `IHeroRelationAdapter` (Adapters): the engine call.
- `IDedicatedServerProvider` (CoopInterop): a dedicated server grants nothing.
- `IModLogger` (Core): the log line.

## Registration

`RelationsModule` (`Main/Features/Relations/RelationsModule.cs`) registers both services, and also Personal Relations ([personal-relations.md](personal-relations.md)). Bonds of Battle used to carry the registration; it no longer does, and a feature module must not register the seam again.

## Tests

- `TAOM.Tests/Features/Relations/RelationGrantServiceTests.cs`: the landed change returned, zero, dedicated server, clamp at both ends and at `int.MinValue` and `int.MaxValue`, blank ids, unknown hero, log line.
- `TAOM.Tests/Features/Relations/PersonalRelationServiceTests.cs`, `PersonalRelationSettingsProviderTests.cs`, `PersonalRelationSeedServiceTests.cs`, `PersonalRelationSeedBehaviorTests.cs`, `PersonalRelationNoticeTests.cs`, `PersonalRelationNoticeBindingTests.cs`, `RelationsWiringTests.cs`, `TaomDiplomacyModelPersonalRelationsTests.cs`: Personal Relations, listed in [personal-relations.md](personal-relations.md).

## Known Limits

The seam reports the change that landed, so a diplomacy model that scaled or zeroed the amount is visible to the caller. The seam has three direct callers it does not cover, and it does not handle amounts a game model returns.

## Changelog

- 2026-10-18: first version, with Bonds of Battle.
- 2026-10-19: the seam returns what landed (an int, the relation the player sees after the change minus before, read with `Hero.GetRelation` around the engine call), 0 for any refusal, and logs the asked and the landed amount only when something landed.

## GitHub Issue

- **Issue:** #861
- **Status:** Open

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/bonds-of-battle.md](./bonds-of-battle.md)

<!-- backlinks-end -->
