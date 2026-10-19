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

1. A blank hero id, a zero delta or a dedicated server returns false and touches nothing.
2. The delta is clamped to -100..100.
3. `IHeroRelationAdapter.ApplyPlayerRelation` applies it with the on-screen quick notification off. It returns false for an unknown hero, a dead hero, the player hero, or an engine fault (logged by the adapter). The call's own `affectRelatives` argument is not forwarded by the v1.5.5 engine, so the seam does not offer it.
4. A change that was applied is written to the TAOM log as `[Relations] <hero id> +N (<reason>)`.

### What the engine does with a change

- Every change runs through `DiplomacyModel.GetEffectiveRelationChange` first. For a positive change the default model adds the Charm skill bonus and the Charm and trait perks (and a personality factor), so the amount that lands can be larger than the one granted.
- The change lands on the clan leaders (`GetHeroesForEffectiveRelation`) under the default model, not necessarily on the hero named, unless both are the same person or one is the player's companion.
- The result is clamped to -100..100.
- Because of this, a grant is logged as made even when the relation moved by a different amount.

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

The Bonds of Battle module registers both services. A later feature that needs them without Bonds of Battle should move the registration into a module of its own.

## Tests

- `TAOM.Tests/Features/Relations/RelationGrantServiceTests.cs`: apply, zero, dedicated server, clamp at both ends and at `int.MinValue` and `int.MaxValue`, blank ids, unknown hero, log line.

## Known Limits

The grant is logged as made when the adapter calls the engine, even if the diplomacy model scaled or zeroed the amount (see above). The seam has three direct callers it does not cover, and it does not handle amounts a game model returns.

## Changelog

- 2026-10-18: first version, with Bonds of Battle.

## GitHub Issue

- **Issue:** #861
- **Status:** Open

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/bonds-of-battle.md](./bonds-of-battle.md)

<!-- backlinks-end -->
