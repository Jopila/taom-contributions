# Bonds of Battle

## Overview

Bonds of Battle makes the player's relations with lords and notables something earned on campaign. Four small rules share one MCM group: a relation bonus with every lord who fights on the player's side, gratitude from nearby notables after the player clears bandits, goodwill from a lord who is handed troops, and a higher cap on what a barter overpay can buy. An optional rule, off by default, takes relation from each lord who fought against the player.

## Why This Exists

- **Vanilla behavior:** Vanilla pays the side leader the player came to help (the thank-you conversation after a win, `BattleRewardModel.GetPlayerGainedRelationAmount`, never inside the player's army), a rescued villager party's owner and notables and a caravan's owner, +5 to a notable after a raid defence, and +2 to one notable per nearby village after a hideout. Other lords who fought beside the player earn nothing, and a barter overpay buys at most 3 points.
- **TAOM requirement:** A campaign that lasts hundreds of days should let a player who keeps riding with the same lords, and keeps the roads clear, be known for it. Relation is also what the later political features read, so earning it from play matters.
- **Without this feature:** Relation stays a number the player raises by clicking through barter screens, and the 3 point cap makes the overpay almost useless late in a game.

## Architecture

### Design Challenge

Three unrelated engine seams (a campaign event, a party screen call, a game model) all end in the same effect, a change of the player's relation with one hero. The new relation code goes through one shared seam so the rules (clamp, skip zero, ignore the dead) are written once and later features can reuse them.

### Solution Approach

- **Shared seam.** `IRelationGrantService.Grant(heroId, delta, reasonForLog)` in `Main/Features/Relations/` is the shared seam for new relation code. It clamps the change to -100..100, skips a zero, a blank id, an unknown or a dead hero and a dedicated server, and logs the reason. It calls `IHeroRelationAdapter`, which wraps `ChangeRelationAction.ApplyPlayerRelation` with the quick notification off. The seam has its own page, [relations.md](relations.md), and `RelationsModule` registers it (Bonds of Battle does not).
- **Battle.** `BondsOfBattleBehavior` listens to `CampaignEvents.OnPlayerBattleEndEvent`, which `PlayerEncounter.DoApplyMapEventResults` raises before the map event commits, so sides and casualties are still readable. `IBattleRelationAdapter` turns the map event into an engine-free `BattleSnapshot`. `BattleRelationService` decides the grants and tells the player through `IBondsNoticeAdapter`.
- **Hand-over.** Patch106 is a prefix and a postfix on `PartyScreenLogic.DoneLogic(bool)`. The prefix keeps the left party's leader and `CurrentData.PartyInfluenceChangeAmount.Item2` in Harmony's `__state`, because vanilla resets that number on success. The postfix acts only when the call returned true. That influence is non-zero only on a donating screen (Donate Troops to another clan's party, or donate to a garrison); only Donate Troops has a left owner party, the receiving party's own, so the garrison screens capture no lord. The patch reads the lord's id, name and whether he is the player or of the player's clan; `TroopHandoverService` decides. The only caller of the Donate Troops screen is `GameMenuOverlay.ExecuteTroopAction`.
- **Barter.** `TaomBarterModel` overrides only `CalculateOverpayRelationIncreaseCosts`. It reads the hero's relation and the Charm Tribute perk at its own boundary and hands plain numbers to `BarterOverpayService`, which runs `OverpayRelationCalculator`. The calculator is the engine's loop with the cap as an argument; at a cap of 3 it matches the engine for the same rolls. When the feature is off or the player is enlisted, the service answers null and the model calls `base`. The loop buys at most 3 points in vanilla (the setting here), before the Tribute perk (x1.2, rounded up) and the Charm relation bonus the engine adds when it applies the change.

### Component Diagram

```
TaomSettings (MCM "Bonds of Battle")
        |
BondsOfBattleSettingsProvider (clamps)
        |
 +------+------------------+------------------+
 |                         |                  |
BattleRelationService   TroopHandoverService  BarterOverpayService
 |  (snapshot in)          |  (id, influence)    |  (relation, overpay, Tribute)
 |                         |                     |
BondsOfBattleBehavior   Patch106 prefix/postfix  TaomBarterModel
 |                         |                     |
 +------------+------------+                     |
              |                                  |
     IRelationGrantService                (relation and perk read
              |                        in the model)
     IHeroRelationAdapter -> ChangeRelationAction
```

## Rules

### Battle

- Nothing happens when the feature is off, while the player is enlisted, or on a co-op client that defers to the host. A dedicated server grants nothing (the grant service refuses it).
- Each lord who led a party on the player's side gains `Relation With Allied Lords` (default 1), once per battle however many parties the lord brought.
- Each lord who led a party against the player loses `Relation Lost With Enemy Lords` (default 5), only when `Lose Relation With Enemy Lords` is on (default off).
- Never counted: parties of the player's clan (the main party and the player hero included), parties in the player's army, parties with no leader, a settlement's own party, bandit leaders, and leaders who are not lords.
- Gratitude: in the field, against bandits alone (cleared hideouts are thanked by vanilla, whose `HideoutCampaignBehavior.SetCleanHideoutRelations` gives +2 to one random notable per nearby village), when the player's side won and every party on the other side is a bandit party, one random living notable of each settlement within `Bandit Gratitude Distance` map units of the battle gains `max(1, bandit casualties / 10)`, capped at 10. The casualties are the bandits killed, wounded or routed. A settlement without a living notable is skipped. In a hideout battle an allied lord still earns the ally bonus.

### Hand-over

A lord handed troops on the Donate Troops screen gains `clamp(influence / 10, 1, 10)` plus a random -1, 0 or +1, never less than 1, where `influence` is what the gift is worth in the screen's own influence number. Nothing is granted for a gift worth no influence, to a lord of the player's own clan or the player hero, while enlisted, or on a co-op client that defers to the host.

### Barter

Each point from the current relation `r` costs `1000 + 100 r^2`. Whole points are paid while the overpay covers them, the last point is a chance of (what is left / its cost), and the Tribute perk multiplies the total, rounded up. The loop buys at most 3 points in vanilla (the setting here, `Most Relation From One Barter Overpay`, default 15), before the Tribute perk (x1.2, rounded up) and the Charm relation bonus the engine adds when it applies the change. The model changes nothing while the player is enlisted or the feature is off. The barter path needs no co-op gate: the installed co-op module routes a client's barter to the server and never calls the model.

## Configuration

MCM group `Bonds of Battle` (GroupOrder 57). Every value is read where it is used, so nothing needs a restart. Out-of-range values from a hand-edited `TAOM.json` are clamped, and a non-finite distance reverts to its default.

| Setting | Type | Default | Range |
|---------|------|---------|-------|
| Enable Bonds of Battle | bool | on | |
| Relation With Allied Lords | int | 1 | 0 to 5 |
| Lose Relation With Enemy Lords | bool | off | |
| Relation Lost With Enemy Lords | int | 5 | 0 to 10 |
| Notables Thank You For Clearing Bandits | bool | on | |
| Bandit Gratitude Distance | float, map units | 25 | 5 to 60 |
| Troop Gifts Win Lords' Favour | bool | on | |
| Most Relation From One Barter Overpay | int | 15 | 3 to 30 |

### Why 25 map units

TAOM_Map's settlements sit a median 17 units from their nearest neighbour, and a median of 3 settlements lie within 30 units (measured on the live `settlements.xml`, 2026-10-18). 25 reaches the one to three settlements around a battle. It can reach another realm's settlements: the closest fortifications of different kingdoms are 14 units apart, and 53 town or village pairs of different kingdoms lie within 25 units of each other (measured the same day). Whether enemy-realm notables should thank the player is an open decision on #861.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/BondsOfBattle/BondsOfBattleModule.cs` | Feature module: services, behavior, Patch106 category, barter model |
| `Main/Features/BondsOfBattle/BattleRelationService.cs` | Who earns what from a finished battle |
| `Main/Features/BondsOfBattle/TroopHandoverService.cs` | The hand-over gain |
| `Main/Features/BondsOfBattle/OverpayRelationCalculator.cs` | The overpay loop, with a cap argument |
| `Main/Features/BondsOfBattle/BarterOverpayService.cs` | Gates (feature, enlisted) and runs the calculator |
| `Main/Features/BondsOfBattle/BondsOfBattleSettingsProvider.cs` | Clamped MCM values and the defaults |
| `Main/Features/BondsOfBattle/Hooks/BondsOfBattleBehavior.cs` | Battle end listener |
| `Main/Features/BondsOfBattle/Hooks/PartyScreenLogic_DoneLogic_Patch.cs` | Patch106 |
| `Main/Features/BondsOfBattle/Models/TaomBarterModel.cs` | Barter overpay override |
| `Main/Features/BondsOfBattle/BondsOfBattleSettings.cs` | The clamped values every service reads |
| `Main/Features/Relations/RelationGrantService.cs` | The shared seam for new relation code |
| `Main/Adapters/HeroRelationAdapter.cs`, `BattleRelationAdapter.cs`, `BondsNoticeAdapter.cs` | Engine boundary |

## Dependencies

- `IEnlistmentStateQuery` (Enlistment): relation bonuses pause while the player serves a lord.
- `IRandomSource` (InitialChildGeneration): the notable pick, the hand-over spread and the last barter point.
- `ICoopSessionProvider` (CoopInterop): the battle listener and the hand-over yield to the host.
- `IDedicatedServerProvider` (CoopInterop): the grant service refuses a dedicated server.

## Localization

New lines, all in `taom_module_strings.xml`: `taom_bonds_together`, `taom_bonds_against`, `taom_bonds_bandits`, `taom_bonds_handover`. The two gratitude lines are worded without a gender ("thanks you"). The twelve languages hold the English text until the paid translation run.

## Tests

- `TAOM.Tests/Features/BondsOfBattle/BattleRelationServiceTests.cs`: ally, enemy toggle, enlisted, feature off, each exclusion, one grant per lord, the bandit math and its gates, hideout battles.
- `TAOM.Tests/Features/BondsOfBattle/TroopHandoverServiceTests.cs`: gain bounds, spread (in and out of range), zero influence, each gate (feature, switch, enlisted, co-op, player clan, player hero).
- `TAOM.Tests/Features/BondsOfBattle/OverpayRelationCalculatorTests.cs`: parity with the engine at cap 3, cap 15, the +100 clamp, the Tribute perk, non-finite inputs.
- `TAOM.Tests/Features/BondsOfBattle/BarterOverpayServiceTests.cs`: the cap, the roll, the Tribute bonus, feature off, enlisted.
- `TAOM.Tests/Features/BondsOfBattle/BondsOfBattleSettingsProviderTests.cs`: defaults, clamps, non-finite distance.
- `TAOM.Tests/Features/BondsOfBattle/BondsOfBattleBehaviorTests.cs`: co-op client, feature off, the radius asked for, an unreadable battle, a throwing service.
- `TAOM.Tests/Features/BondsOfBattle/BondsOfBattleWiringTests.cs`: the module line, category, model slot, DI graph, Patch106 names and fault handling.
- `TAOM.Tests/Features/Relations/RelationGrantServiceTests.cs`: clamp, zero, blank id, unknown hero, dedicated server.

## How to Change a Rule

1. A number the player should tune: add an MCM property in the `Bonds of Battle` group and a clamped field in `BondsOfBattleSettingsProvider`.
2. A new source of relation: call `IRelationGrantService.Grant`, the shared seam for new relation code; do not call `ChangeRelationAction` directly.
3. A new exclusion for battle lords: add a flag to `BattleParticipant`, set it in `BattleRelationAdapter`, and test it in `BattleRelationServiceTests`.

## Known Limits

- The engine runs every change through `DiplomacyModel.GetEffectiveRelationChange`, which can scale or zero the amount, so a grant is logged as made even when the relation moved less (see [relations.md](relations.md)).
- The hand-over rule fires only where vanilla reports influence for the gift, which is the Donate Troops screen to a lord outside the player's clan. In a co-op session the installed co-op module replaces `DoneLogic` with its own prefix, so the captured influence may read 0 and nothing is granted.
- With [personal relations](personal-relations.md) on, a grant lands on the lord named, so the once-per-lord rule is per lord; with it off, a grant lands on the clan leader and two lords of one clan share one number.
- A lord who fought beside the player earns the ally bonus once per battle, and a lord vanilla already thanked (the side leader the player came to help) earns both.

## Changelog

- 2026-10-18: first version.

## GitHub Issue

- **Issue:** #861
- **Status:** Open

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/relations.md](./relations.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/harmony-patch-registry.md](../reference/harmony-patch-registry.md)

<!-- backlinks-end -->
