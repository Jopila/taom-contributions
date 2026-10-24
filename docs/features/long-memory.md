# Long Memory

## Overview

A village remembers who burned it. When the player raids a village another clan owns, or forces it to hand over goods or recruits, the owner's clan leader and every notable of the village lose relation with the player, on top of what vanilla already takes. How much depends on who the player is: a Gondor or Rohan player who raids free folk villages pays the full price, and an Evil warband raiding a Free village pays a tenth of it, because Evil factions raid constantly. Every amount and every multiplier is an MCM setting under "Long Memory".

## Why This Exists

- **Vanilla behavior:** a finished raid costs the raiding leader `ceil(6 x RaidDamage)` with the owner's clan leader and `ceil(3 x RaidDamage)` with each notable, and only when the raiders win a village outside their own faction (`CharacterRelationCampaignBehavior.OnRaidCompleted`). Starting a hostile action at peace costs `-4 x value` with the owner and every notable (value 1 for forcing supplies with no resistance, 2 for volunteers, 6 for a raid or a resisted fight) and, for a raid or a resisted fight, a declared war (`BeHostileAction`). At war, forcing a village costs no relation at all. At war, burning a village to the ground costs 6 with the owner's clan leader and 3 with each notable.
- **TAOM requirement:** relation is the currency of the political game (marriages, fiefs, kingdom votes, gifts). Raiding and coercing the people of a realm should be remembered, and the memory should follow alignment, as every other TAOM relation rule does.
- **Without this feature:** a player can burn Rohirrim villages with little lasting consequence.

## Architecture

### Design Challenge

- Three completed events carry the map event's component: `CampaignEvents.RaidCompletedEvent`, `ForceSuppliesCompletedEvent` and `ForceVolunteersCompletedEvent` (`IMbEvent<BattleSideEnum, ...Component>`). All three fire from `OnBeforeMapEventFinalize`, and the side is `Attacker` only on `BattleState.AttackerVictory`.
- **A raid is not "the attacker won".** `RaidDamage` is the hit points this raid event took on the village's 0 to 1 health scale (capped at 1: `RaidEventComponent.cs:361-362`), so it can exceed the health the village had when the raid began by at most about one hit (0.05): a hit applies whole once past 0.05, and the per-event rounding adds up to a point per event (`RaidEventComponent.cs:333-334`, `:360-362`). It resets when the map event initializes and rises only once the defenders are gone. The fight against a militia is itself a raid map event, and winning it starts a second one (`RaidEventComponent.TryHandlePlayerEncounterEnd`), so an attacker win alone would charge the player twice. The rule is the damage alone: the event pays the raid amount times `RaidDamage`, with no floor, and an event that took no hit points pays 0. A raid that is interrupted pays per event, and the events of one raid add up to about one full raid, a few points over at most. A village weakened by an earlier forcing costs less, because that forcing was already charged.
- **A village that gives way fires no event at all.** `force_supplies_no_resist_continue` and `force_volunteers_no_resist_continue` run vanilla's coercion action directly (`VillageHostileActionCampaignBehavior.cs:145,151, 490-512`). `CampaignEvents.GameMenuOptionSelectedEvent` is raised after the option's consequence (`GameMenu.RunMenuOptionConsequence`), so it is the signal, with no Harmony patch.
- The engine lands a relation change on the relation owner, not on the hero named: the clan leader unless Personal Relations is engaged ([relations.md](relations.md)). Notables have no clan, so each notable is its own relation owner and the shared per-owner call (`GrantStrongestPerOwner`) only guards.
- The player must lead the act. The attacker side's leader party must be the player's own party, which excludes an AI lord's raid, a raid inside another lord's army, and every raid or battle an enlisted player joins under the commander. An act the enlisted player starts personally (for example on shore leave) counts.

### Solution Approach

One service (`LongMemoryService`), one behavior, no Harmony patch, no game model, nothing saved. `LongMemoryModule` (one line in `FeatureModules.All`, no edit to `IoC.cs` or `SubModule.cs`) registers the services and declares the campaign behavior.

| Entry point | What it does |
|---|---|
| `LongMemoryBehavior.OnRaidCompleted` (`Hooks/`) | Hands `LongMemoryService.OnRaidCompleted` a reader over `ILongMemoryVillageAdapter.ReadMapEvent` and the raid's component. The winner is ignored |
| `OnForceSuppliesCompleted`, `OnForceVolunteersCompleted` | `OnCoercionCompleted(attackerWon, read)`: the same kind of reader, called only when the side is `Attacker` |
| `OnGameMenuOptionSelected` | Passes `option?.IdString` and a reader over `ReadCurrentVillage` to `OnMenuOptionSelected`, which calls the reader only for the two pinned no-resist ids |

The service calls a reader only after its gates (the attacker win or the option id, the master and per-act switches, the dedicated-server and co-op tests), so a switch that is off or any other menu option never touches the engine. The service takes no village adapter.

Every body is inside a try that logs: an `MbEvent` invokes its listeners with no try, so a fault would reach the engine's raid or menu code.

**The rules** (all in `LongMemoryService` and `LongMemoryRules`, pure over raw facts):

- **Who counts:** the village must be a village, the player's own party must lead the act, and the player's clan must not own the village. The owner's clan leader (if there is one) and every living notable are asked; a dead hero and the player's own hero are skipped.
- **One pairing per act.** The side of the village owner's map faction against the player's side, through `SideMultipliers.For` (`Features/Relations`) over `IAlignmentService.ResolveSide`, scales the owner's clan leader and every notable alike. The sides are built by `FactionSides` alone (`SidePairScanTests` stays green). "Neutral" means either side is Neutral.
- **The raid scale:** `RaidDamage` itself, with no floor. `Penalty` already refuses a scale that is not above 0 or not finite, so a zero or non-finite damage pays 0.
- **Rounding:** `Penalty(magnitude, multiplier, scale)` is `-round(magnitude x multiplier x scale)`, half away from zero, computed in `decimal` (0.7 x 50 is 35, not 34.99999) and capped at -100, the engine's relation floor. A magnitude, multiplier or scale that is not above 0 or not finite costs nothing, so no input can become a gain. A loss that rounds to 0 is dropped.
- **One loss per relation owner.** The adapter reads each hero's relation owner with `DiplomacyModel.GetHeroesForEffectiveRelation` (`RelationTargets.Of`); the service lists a `RelationAsk` for the owner's clan leader and for each notable, and `GrantStrongestPerOwner` keeps the strongest (largest `|delta|`) per owner, the first listed on a tie, and asks it through `IRelationGrantService.Grant`.
- **Counted by what landed.** `Grant` returns the change that landed; the one message-log line (red) is shown only when at least one landed.
- **Authority:** a co-op peer that defers to the host (`ICoopSessionProvider.ShouldDeferToHost`) and a dedicated server lose nothing. The service keeps no state, so it needs no per-campaign reset.

**How it stacks on vanilla** (Free player, village of a Free realm, a whole raid; the Long Memory defaults):

| Act | Vanilla | Long Memory |
|---|---|---|
| Raid, when it finishes | `-ceil(6 x damage)` with the owner's clan leader, `-ceil(3 x damage)` with each notable, only if the raiders won | -50 with the owner's clan leader and with each notable, times the damage (no floor) |
| Raid, when it starts at peace | `-24` with the owner and each notable, `-10` with the realm's leader, war declared | nothing (Long Memory acts when the raid finishes) |
| Forced supplies, villagers give way | `-4` with the owner and each notable, only at peace | -20 with the owner's clan leader, -25 with each notable |
| Forced volunteers, villagers give way | `-8` with the owner and each notable, only at peace | the same -20 and -25 |
| Forced supplies or volunteers, villagers resist and lose | `-24` with the owner and each notable, `-10` with the realm's leader (`ApplyEncounterHostileAction`, `BeHostileAction.cs:192-195`), only at peace, war declared | the same -20 and -25 |
| Any of the above, the player's clan owns the village, or another lord leads | as vanilla | nothing |

At peace, every hostile action above also runs vanilla's `ApplyGeneralConsequencesOnPeace` (`BeHostileAction.cs:95-149`), which Long Memory does not touch. With the action's value 1 for supplies, 2 for volunteers and 6 for a raid or a resisted fight:

| Who | Vanilla's peace-time consequence |
|---|---|
| A vassal player | `-2 x value` relation with the raided realm's ruler and `-50 x value` influence |
| A kingdom ruler | `-50 x value` influence |
| A mercenary | `-2 x value` relation with the liege, and expulsion from the kingdom at value 6 |
| The player (any of the above) | `+10 x value` crime rating with the raided realm and a trait hit; both also reach an independent clan (`BeHostileAction.cs:105-115` run before the clan return at `:116-118`) |

TAOM's same-side war veto (`DeclareWarAction_ApplyInternal_Patch`, `WarOfTheRingService.cs:103`) means a same-side raid begun at peace is refused its war declaration and ends with no damage, so it pays 0; a resisted forcing at peace may stay at peace.

### Component Diagram

```
RaidCompletedEvent ────────────────┐
ForceSuppliesCompletedEvent ───────┤
ForceVolunteersCompletedEvent ─────┼─> LongMemoryBehavior ──> ILongMemoryVillageAdapter (raw facts)
GameMenuOptionSelectedEvent ───────┘          |
                                              v
MCM "Long Memory" ─> LongMemorySettingsProvider ──> LongMemoryService ──> ILongMemoryNoticeAdapter (one red line)
                                                     |        |
                                      IAlignmentService       IRelationGrantService ─> IHeroRelationAdapter
                                      ICoopSessionProvider, IDedicatedServerProvider
```

## Configuration

### MCM: "Long Memory" (`GroupOrder = 67`)

Defaults and ranges live in `LongMemorySettingsProvider`, and the provider clamps through `SettingClamp` (a non-finite float reverts to its default; a hand-edited json is clamped; a multiplier is rounded to two decimals). The sliders hold magnitudes, never negative numbers: the service negates them.

| Setting | Default | Range |
|---|---|---|
| Enable Long Memory (master) | on | |
| Remember Raids, Remember Forced Takings | on each | |
| Raid: Relation Lost With The Owner's Clan Leader, With Each Notable | 50, 50 | 0 to 100 |
| Forced Taking: Relation Lost With The Owner's Clan Leader, With Each Notable | 20, 25 | 0 to 50 |
| Same Side, Neutral, Opposite Side Multiplier | 1.00, 0.50, 0.10 | 0.00 to 5.00 |

The multipliers apply to the village owner's side against the player's, for a raid and a forced taking alike. The group sits at `GroupOrder` 67, after Fire and Tribute at 66. Every value is read when the wrong happens, none per frame, so none needs a restart. The settings are simulation-relevant for co-op (the fingerprint counts them: [coop-interop.md](coop-interop.md)). The labels and hints are gender-neutral and each hint quotes its compiled default and range (`LongMemorySettingsProviderTests` compares them with the constants).

### Localization

Two keys, shown as red message-log lines: `{=taom_longmemory_raid}The people of {VILLAGE} will long remember the raid.` and `{=taom_longmemory_coercion}The people of {VILLAGE} will long remember what was taken from them by force.` The villagers are the subject of both lines and neither names the player, so no verb or adjective can carry the player's gender and every translation fits any player. The twelve language files carry the seeded English rows until the paid translation run (backlog #763); `LongMemoryWordingTests` pins the English rows to the C# literals and each language row to the current English.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/LongMemory/LongMemoryService.cs` | The rules, the side scaling, the asks, the gates before each read |
| `Main/Features/LongMemory/LongMemoryRules.cs` | The penalty arithmetic and the two pinned option ids |
| `Main/Features/LongMemory/LongMemorySettings.cs`, `LongMemorySettingsProvider.cs`, `ILongMemorySettingsProvider.cs` | The MCM group, clamped |
| `Main/Features/TaomSettings.cs` | The "Long Memory" MCM group (labels, hints, ranges) |
| `Main/Features/LongMemory/Hooks/LongMemoryBehavior.cs` | The four listeners |
| `Main/Features/LongMemory/Models/VillageHostileActionFacts.cs` | The raw-fact record the adapter returns |
| `Main/Features/LongMemory/LongMemoryModule.cs` | Services and behavior declaration |
| `Main/Adapters/ILongMemoryVillageAdapter.cs`, `LongMemoryVillageAdapter.cs` | Village, map event and hero facts from the engine |
| `Main/Adapters/ILongMemoryNoticeAdapter.cs`, `LongMemoryNoticeAdapter.cs` | The two lines |
| `Main/_Module/ModuleData/taom_module_strings.xml` and the 12 `Main/_Module/ModuleData/Languages/<L>/std_taom_module_strings_*.xml` | The two `taom_longmemory_*` rows, English and seeded in each language |

Shared with Good Deeds and kept in `Features/Relations`, not copied: `SideMultipliers`, `RelationTarget` and `RelationAsk`, `GrantStrongestPerOwner` (`RelationGrantServiceExtensions.cs`) and `RelationTargets.Of` (`Main/Adapters`). Also reused: `FactionSides` and `SideIds`.

## Dependencies

- `IRelationGrantService` and `GrantStrongestPerOwner` (Relations): the clamp, the strongest ask once per relation owner, the landed count and the log line; `RelationsModule` owns the registration.
- `IAlignmentService` (Execution): the side of each party.
- `ICoopSessionProvider`, `IDedicatedServerProvider` (CoopInterop): the host and server gates.
- `IModLogger` (Core): the listeners' fault lines and the adapter faults.

## Tests

- `LongMemoryRulesTests` (36): the penalty's sign, rounding, ceiling and every refusal (zero, negative, non-finite, overflow), and the two pinned option ids against every other id.
- `LongMemoryServiceRaidTests` (37): a whole raid, the leader and ownership gates, a militia fight that did no damage, a negative or non-finite damage, a proportional small damage with no floor, the events of one interrupted raid adding up to one raid, every side pairing, the amounts, one route-level per-owner test, dead and player heroes, the landed count and the line, and the switches and the co-op and server skips, each also counting that the reader never ran.
- `LongMemoryServiceCoercionTests` (38): the attacker win and the defender win, the gates, the scaling, one route-level per-owner test, the menu route (the two ids pay once, every other id pays nothing and reads nothing), "Remember Raids" off still charging a forced taking, and every gate coming before the read.
- `LongMemorySettingsProviderTests` (45): defaults, pass-through, every clamp, a negative amount as 0, every non-finite fallback, the two-decimal rounding, the group declared once at `GroupOrder = 67` with every property in it and no restart, the slider ranges, each hint quoting its default and range, and gender-neutral labels with no dash.
- `LongMemoryWordingTests` (8): the English rows equal the C# literals and each of the twelve language rows equals the current English.
- `LongMemoryBehaviorTests` (18, needs the game): the hand-offs, the attacker-only read, the catches, and the three completed-event listeners passing the winning side.
- `LongMemoryWiringTests` (15): the module listed once, the single-owner files untouched, nothing saved or patched, a fresh behavior per campaign, no second registration of a shared seam, no Harmony patch, no side built outside `FactionSides`, the adapter's two early-outs and leads rule pinned from the source, the four registrations and the option id expression, and every service call inside a try.
- `LongMemoryBindingTests` (2, `BindingVerification`, needs the game): the installed engine's `AddGameMenus` registers each pinned option id on its no-resist menu.
- `RelationGrantServiceExtensionsTests` (15) and `SideMultipliersTests` (10): the shared strongest-per-owner rule and the nine side pairings, in [relations.md](relations.md).
- `CampaignHotPathSettingsProvidersTests`: the provider takes the MCM instance only in its lazy accessor, resolves from a container and reads through the cached settings.
- A campaign behavior declared by a feature module also needs its co-op row in [coop-interop.md](coop-interop.md); `CoopGateDocCoverageTests` enforces it, and the settings-fingerprint counts in that doc and `SettingsFingerprintTests` count this group's ten settings.

## How to Change the Penalties

1. Amounts and multipliers: MCM, "Long Memory". No code change.
2. A new default: change the constant in `LongMemorySettingsProvider` (the property initializers and the fallback share it), and the test that pins it. A persisted MCM value keeps the old default: rename the setting (and say why in a comment) to change what existing players see.
3. A new act: add a method to `LongMemoryService` that takes a reader, checks `Allowed`, calls the reader and then `Charge` (which lists the `RelationAsk`s and calls `GrantStrongestPerOwner`), a raw-fact read on the adapter, and one listener. Never call the engine from the service.

## Known Limits

- **It stacks on vanilla, by design.** The hit of the raid's start (at peace: -24 and a war declaration) and vanilla's own finish hit are separate from this feature's loss. A player at peace who raids pays all three.
- **A raid against a village defended by a militia is two map events.** Vanilla starts a second raid event after the militia fight is won. The loss applies only to an event that did damage, so the fight against the militia pays only for the damage its own event did. This is the one point that needs an in-game check (below).
- **Sides are read when the act finishes.** The menu does not offer a raid on a village of the player's own kingdom, so only a faction change in the middle of an act (the player's clan joins or leaves the owner's realm while it runs) can make the pairing differ from the one the act began with.
- **A raid is judged when it finishes.** Leaving the raid early pays for the damage done so far, and the events of one raid add up to about one full raid, a few points over at most.
- **The two no-resist ids are pinned from vanilla's menu code** (`VillageHostileActionCampaignBehavior.cs:145,151`, engine v1.5.5). A game update that renames them would silently stop the no-resist route; `LongMemoryBindingTests` (the binding gate, `TestCategory=BindingVerification`) reads the installed engine's `AddGameMenus` and fails when an id, or its no-resist menu, is gone.
- **No notable is spared.** A notable who lives in the village counts whatever the notable's own realm; the pairing is the village owner's.
- **Nothing is refunded.** A release, a gift or a tournament win ([Good Deeds](good-deeds.md)) earns relation separately and does not cancel this loss.

## In-game Checklist

Setup once: a campaign with cheat mode on. Read the `[Relations]` lines in the TAOM log after each step (`long memory: raid` and `long memory: forced taking` are the reasons).

1. **Raid, same side.** As a Free player whose clan is an independent clan (or once War of the Ring is at WarEnded, so a same-side war is allowed), raid a Free village at full health to the end. An independent clan raiding at peace pays -24 at the start with the owner and each notable and -10 with the ruler, then vanilla's -6 and -3 at the finish; on top of those, Long Memory's loss lands: the owner's clan leader and each notable lose about 50, and one red line "The people of <village> will long remember the raid." shows.
2. **Raid, opposite side.** As an Evil player raid a Free village to the end: about 5 with each, not 50.
3. **A short raid.** Start a raid and end it with some damage done: the loss is about 50 times the damage the raid took (a raid that took a fifth of the village's health: about 10), with no floor. With no damage (the militia fight only, then leaving): nothing.
4. **A militia-defended village.** Win the fight against the militia, then raid: the log shows one set of `long memory: raid` lines for the raid. If a set also appears for the militia fight alone, report it. Interrupt a raid and resume it: two sets of lines are expected, because each event pays for its own damage, and together they add up to about one full raid, a few points over at most. Repeat the interrupted raid after saving and reloading in the middle of it: the same result.
5. **Your own village, someone else's raid.** Raid a village your clan owns, or ride in another lord's army that raids: nothing.
6. **Forced supplies, villagers give way.** At peace with a Free village's realm, force supplies from a village with no militia and no resistance: -20 with the owner's clan leader, -25 with each notable, and one red line. Forget it, and no line.
7. **Forced volunteers, villagers resist.** Use step 1's setup (an independent clan, an existing war, or WarEnded), since a same-side kingdom at peace keeps the peace under the veto and nothing is paid. Force volunteers from a defended village and win the fight: the same -20 and -25. Lose the fight: nothing.
8. **Switches.** Turn "Remember Raids" off: a raid costs only vanilla's, and a forced taking still costs. Turn the master off: nothing from either.
9. **Co-op.** On a co-op client nothing is lost; the host's own acts cost as usual.
10. **MCM live.** Change an amount or a multiplier mid-campaign: the next act uses it, no restart.

## Changelog

- 2026-10-21: fix pass after review. The raid scale is the raid's damage alone (no floor, no looting rule), the forced-taking line no longer says "you", the service reads the village through a reader only after its gates, the per-owner grant, the side pairing and the hero record are shared with Good Deeds in `Features/Relations`, the adapter leaves early for an act the player does not lead, and the two option ids have a binding test.
- 2026-10-21: first version (#865). Raid, forced supplies and forced volunteers, scaled by the village owner's side, de-duplicated per relation owner, MCM group at `GroupOrder = 67`.

## GitHub Issue

- **Issue:** #865, [Heavier relation loss for raiding and coercing villages, scaled by alignment](https://github.com/haterade22/TAOM/issues/865)
- **Status:** Shipped on `bannerlord-1.5.x` in `30134a9d9` (v2.0.35). Closed with `triage-needs-ingame`: the in-game checks are owed.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/features/good-deeds.md](./good-deeds.md)
- [docs/features/relations.md](./relations.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
