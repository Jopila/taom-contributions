# Fire and Tribute

## Overview

Once the War of the Ring is in Full War, raiding hosts ride out of their homelands to burn the villages of the strongest enemy realm. Orcs of Gundabad, Easterlings of Rhûn, Haradrim, Dunlendings and the Blacklocks of Erebor (a dwarf clan fallen into shadow, so a Shadow host) raid the Free Peoples; outlaw bands of Gondor and Mirkwood strike back at the Shadow; the Corsair Host of Umbar raids either side, never a Neutral realm. A host engages weaker parties of its target realm it meets, raids villages until its quota is done, and goes home. This is slice 1 of #870: the hosts only. The tribute envoy that asks the player to pay for their villages is slice 2 and is not built.

## Why This Exists

- **Vanilla behavior:** bandits are small, scattered and local. Nothing sends a war host at a realm.
- **TAOM requirement:** the scripted wars of the War of the Ring need a threat that is not a kingdom declaring war, and that bites the strongest realm of the opposed side.
- **Without this feature:** the late campaign has no pressure beyond the scripted wars.

## What the player sees

- A red rumour with the attack horn when a host rides: "Riders bring word: {HOST} has taken the field near {TOWN}, and villages will burn." (`taom_fat_spawned`).
- Neutral lines when a host turns home (`taom_fat_going_home`), when it reaches home and scatters (`taom_fat_went_home`), and when it is broken (`taom_fat_broken`).
- A map party named for its host (for example "The Host of Gundabad") that raids villages of one kingdom. Looted villages follow vanilla's raid rules. Every host name is a singular collective, so the messages read right ("The Free Company of Anórien is broken"), and none repeats the clan's ordinary bandit name.
- Nothing else in this slice. The player's party and villages get no special treatment yet.

## The eight hosts

| Host | Side | Clan | Name | Anchors (towns and castles of its own side) |
|---|---|---|---|---|
| `gundabad` | Evil | `gundabad_raiders` | The Host of Gundabad | `town_G1`, `castle_G4`, `town_G2` |
| `rhun` | Evil | `rhun_raiders` | The Easterling Host of Rhûn | `town_RU1`, `town_RU3`, `town_RU5` |
| `harad` | Evil | `harad_raiders` | The Warhost of Harad | `town_A3`, `town_A5`, `town_A4` |
| `dunland` | Evil | `dunland_raiders` | The Dunlending Host | `town_EN3`, `town_EN1`, `town_EN2` |
| `erebor` | Evil | `erebor_warriors` | The Blacklock Warband | `town_RU4`, `town_RU5`, `town_RU7` |
| `umbar` | Neutral | `umbar_corsairs` | The Corsair Host of Umbar | `town_U1`, `town_U2`, `castle_U7` |
| `gondor` | Free | `gondor_soldiers` | The Free Company of Anórien | `town_EW1`, `castle_EW4`, `town_EW7` |
| `mirkwood` | Free | `mirkwood_stalkers` | The Stalker Band of Mirkwood | `town_M1`, `town_M2`, `castle_M3` |

The Blacklocks muster beside Rhûn's fiefs (their hideouts lie there), not in the halls of Erebor, which belong to the Free realm they raid; the Free Company musters on the Ithilien front. The data test `EveryFreeAndEvilHosts_AnchorsLieInRealmsOfItsOwnSide` (LiveInstall) resolves each anchor's authored culture through `execution/alignment.json` and fails if a Free or Shadow host would appear in an enemy realm.

## Rules

| Rule | Detail |
|---|---|
| Window | The War of the Ring in Full War or after it ended, from `firstHostDelayDays` (45) after the later of Full War's effective day and the day this save first saw it. With the War of the Ring off: from `warlessStartDay` (89). |
| Roll | Each day, for each side with no host and no cooldown: `spawnChance` (5%). At most one host per side, so at most three. |
| Cooldown | `cooldownDays` (120) per side, from the day its host is broken, goes home or is lost, and from the day a co-op session releases it. Removing a host because a switch went off starts none. |
| Host choice | The side's hosts in a random rotation; the first with a target, a clan, a hideout and an anchor goes. A host that cannot go is skipped with one log line. |
| Target | The first of the host's `preferredTargets` that qualifies, else the strongest, ties by ordinal id. Only living kingdoms of the host's `targetSides` that own a raidable village (`FireAndTributeMath.KingdomsWithRaidable`). |
| Size | `target strength x strengthShare` split over the troops by share with `HuntMath.PlanTopUp`, lifted to `minTroops` and capped at `maxTroops`. |
| Quota | `clamp(round(target strength / strengthPerRaid) + rand(quotaExtraMin..quotaExtraMax), quotaMin, quotaMax)`. |
| Steering | Once an hour, over every live host (one per side); a host whose step throws is logged and the others still steer. A host in a map event is left alone. Otherwise it engages the nearest party of the target kingdom within `engageRadius` that is not in a battle or settlement or attached to an army, has at most `engageStrengthRatio` of its strength and is not faster; else it keeps a still-valid raid order or picks one of the `nearestVillages` closest raidable villages at random (owner is the target kingdom, normal state, finite hearth at least `minHearth`). |
| Engage strength | The party's strength is its army's whole strength when it leads an army (vanilla's own rule, `DefaultMobilePartyAIModel`: a battle with the leader pulls the army in), else its own roster's. Parties the engine ignores (`ShouldBeIgnored`, which `CanPartyInteract` refuses) and any party in a siege camp (vanilla weighs a besieger by its whole camp) are never candidates. |
| Ending | It turns home on its quota, after `maxHostDays`, or when no raidable village is left. A returning host is dismissed within `homeRadius` of its hideout (or where it stands after twice `maxHostDays`). |
| Counting | A raid counts when `VillageStateChanged` reports the Looted state with the host as raider. The event fires inside the finishing raid, so the count is recorded and the next hourly step acts. |
| Switched off | A host whose side (or the master switch) is off is despawned at its next hourly step. While it is in a battle the engine will not remove it, so its record and pin stay and the next hour tries again, as a returning host's dismissal does; only a removed or lost party drops the record. Toggling a side off and on during a battle therefore cannot produce a second host. |
| Sizing | A host is built with no party template, so bandit scaling (Patch39) and the bandit density settings do not size it. It counts as one party in its clan's `WarPartyComponents`, the count vanilla's `SpawnBanditsAroundHideout` subtracts from the clan's quota, so it takes one slot of its clan's vanilla spawn quota. |
| Budget (proposed) | The hourly step is under 1 ms per campaign hour for all hosts together, the budget Watchful Holds keeps. `maxHourMs` in the daily debug line is its measure. The daily roll and the spawn are not timed. |

## Architecture

```
fire_and_tribute.json   MCM (4 switches)
        |                    |
FireAndTributeConfigProvider  FireAndTributeSettingsProvider
        \                   /
         FireAndTributeService --- FireAndTributeMath (pure)
          /          |                \
 FireAndTributeBehavior  IRaidHostAdapter   IFireAndTributeNoticeAdapter
 (5 campaign events,     (spawn, roster, pin, (message, horn)
  one SyncData key)       orders, villages)
```

- A feature module (`FireAndTributeModule`, one line in `Main/Composition/FeatureModules.cs`; `Main/IoC.cs` and `Main/SubModule.cs` are untouched). It owns save data, so a fault fails the campaign closed.
- A host is a party of an outlaw clan from `characters/clans.xml`, created with `BanditPartyComponent.CreateBanditParty` and the nearest hideout of the clan's culture as its home. A null home would crash vanilla's per-hideout bandit count, so a host is never created without one. Its roster is filled after creation and its AI is pinned (`SetDoNotMakeNewDecisions(true)`): vanilla's hourly think never re-plans it, so its orders are the service's, while the engine's initiative still applies around the settlement it targets. It appears at a point the engine finds near the anchor's gate (the gate itself when it finds none), which is then retried 15 times around that point for one out of the player's sight (`CreatureBandParties.OutOfPlayerSight`, shared with the creature bands); it can land up to about twice the spawn radius away, and a point in sight is kept when every retry fails, so a host can appear in sight.
- The service sees parties as ids; the adapter resolves them against the live campaign on every call and keeps no state: no `Campaign`, no `Village` (a village reaches the whole campaign graph). It hands the service the village readings (`ReadVillages`, one `VillageReading` per village with its id, position, owner kingdom, state and hearth), and `FireAndTributeMath` holds the raidable rule and the nearest-village selection. An IL test pins that the adapter holds no `Campaign` or `Village`, directly or through another type.
- Reuse: `HuntMath.PlanTopUp`, `IKingdomWarSnapshotAdapter` (strength, from the War Chronicle module), `IAlignmentService.ResolveSide`, `IWarOfTheRingService`, `IRandomSource`.

## Configuration

### Config file: `Main/_Module/ModuleData/fire_and_tribute/fire_and_tribute.json`

Read once per game session: an edit needs a full restart. A missing file gives the compiled tuning and no hosts with a warning, a parse failure the same with an error. An invalid tuning field reverts alone to its default (an ordered pair reverts together), an unknown key is warned about and ignored, and an invalid host is skipped with a warning. Every float goes through `FiniteFloatValidator` first.

| Field | Shipped | Valid range |
|---|---|---|
| `firstHostDelayDays` | 45 | 0 to 3650 |
| `spawnChance` | 0.05 | 0 to 1 |
| `cooldownDays` | 120 | 0 to 3650 |
| `warlessStartDay` | 89 | 0 to 3650 |
| `minTroops` / `maxTroops` | 80 / 200 | 1 to 2000, min at most max |
| `strengthShare` | 0.012 | above 0, at most 1 |
| `strengthPerRaid` | 2000 | above 0, at most 10000000 |
| `quotaMin` / `quotaMax` | 6 / 15 | 1 to 100, min at most max |
| `quotaExtraMin` / `quotaExtraMax` | 0 / 3 | 0 to 100, min at most max |
| `minHearth` | 150 | 0 to 10000 |
| `nearestVillages` | 3 | 1 to 10 |
| `engageRadius` | 8 | above 0, at most 60 |
| `engageStrengthRatio` | 0.8 | above 0, at most 5 |
| `maxHostDays` | 30 | 1 to 365 |
| `homeRadius` | 4 | above 0, at most 50 |
| `spawnRadiusDays` | 0.25 | above 0, at most 3 |

Each host has `id`, `side` (`Free`, `Neutral` or `Evil`), `clan` (one of the eight outlaw clans), `name` (`{=taom_fat_host_<id>}` plus English text, the key carrying its own id), `anchors` (town or castle ids on the TAOM map, in realms of the host's own side; at most 12), `targetSides` (never `Neutral`, never its own side), optional `preferredTargets` (kingdom ids, at most 12) and `troops` (`id` and `share`, shares summing to 1, at most 12). The file is read as UTF-8. The troops of each shipped host are the stacks of its clan's `*_raider_party_template` in `taom_partyTemplates.xml`.

### Why these defaults

Kingdom strength (`Kingdom.CurrentTotalStrength`, the `str` key of the `[WarLedger]` lines in two real runs to day 592, 17,989 kingdom-days) has the 5th, 25th, 50th, 75th and 95th percentiles 2.8k, 7.2k, 11.2k, 17.3k and 28.3k. At `strengthShare` 0.012 the host's strength is 33, 86, 135, 208 and 340; troop power for tier 2 to 4 is 0.96, 1.30 and 1.68, so a median host is about 100 men and everything below the 25th percentile is lifted to 80, everything past the 75th is capped at 200. At `strengthPerRaid` 2000 the quota is 1, 4, 6, 9 and 14 raids before the floor and the extra, so 6 to 15 in practice. The troop mix and village `hearth` thresholds are not measured: balance runs decide them.

### MCM settings

Group "World/Fire and Tribute" in `Main/Features/TaomSettings.cs`. All four apply at once, no restart, and are counted in the co-op settings fingerprint.

| Setting | Property | Default |
|---|---|---|
| Enable Fire and Tribute | `EnableFireAndTribute` | on |
| Free Peoples Hosts | `FireAndTributeFreeHosts` | on |
| Neutral Hosts | `FireAndTributeNeutralHosts` | on |
| Shadow Hosts | `FireAndTributeEvilHosts` | on |

A host alive when its switch goes off is despawned at its next hourly step, or at the first hour its battle is over if it is fighting then. The Free switch covers the outlaw bands of Gondor and Mirkwood, the Shadow switch Gundabad, Rhûn, Harad, Dunland and the Blacklocks of Erebor, the Neutral switch the Corsair Host of Umbar.

## Co-op, save data and IoC

- **Singleplayer only.** The spawn, the steering and the console spawn stand down while a co-op session is live, while a co-op mod TAOM cannot probe is loaded, or on a dedicated server. The village and destroy handlers are not gated and do nothing there, because the records are emptied when a session goes live. A host restored into such a session has its pin released and its record dropped, never despawned, and its side rests for `cooldownDays` so the session that ends cannot be followed by a second host at once. See the `FireAndTributeBehavior` row in [coop-interop.md](coop-interop.md).
- **Save data:** one `SyncData` key, `_taom_fire_and_tribute_v1`, a JSON string: the first-seen day of Full War, each side's cooldown, and each live host (party id, host id, side, target, start day, raids, quota, returning). Parsing is forgiving per section and per row; a save from before the feature stays reset. Side names parse by name through `EnumNames.TryParse` (any case, never a number). A dropped host row, or a record dropped for an unknown version, leaves its party on the map still pinned (the engine saves `DoNotMakeNewDecisions`); the warning in the log says so, and any future version bump must migrate v1 rows, never reset them.
- **Session reset:** the behavior's constructor calls `ResetForNewSession`, and the module builds the behavior inline for every campaign start.

## Diagnostics and console

A daily debug line: `[FireAndTribute] day N: hosts=..., spawned=..., raids=..., maxHourMs=...`. `maxHourMs` is the slowest hourly step of the day.

| Command | Tier | Use |
|---|---|---|
| `taom.print_fire_and_tribute` | A | Switches, the spawn window (the one the next daily roll would use, noting a Full War not yet seen without recording it), each side's host and cooldown, today's counters |
| `taom.spawn_raid_host <free\|neutral\|evil> [confirm]` | C | Dry run without `confirm` (who rides, from where, against whom, men, quota). With `confirm` it ignores the phase, the chance and the cooldown; the switches, single-player and one host per side still hold |

## Key files

| File | Purpose |
|---|---|
| `Main/Features/FireAndTribute/FireAndTributeService*.cs` | Every rule: lifecycle, spawn, steering, events |
| `Main/Features/FireAndTribute/FireAndTributeMath.cs` | Target picker, raidable-village rule and selection, size, quota, spawn window, cooldown |
| `Main/Features/FireAndTribute/FireAndTributeConfig*.cs` | Config model and validating loader |
| `Main/Features/FireAndTribute/FireAndTributeSaveCodec.cs`, `FireAndTributeState.cs` | The save record |
| `Main/Features/FireAndTribute/Hooks/FireAndTributeBehavior.cs` | Thin router and SyncData |
| `Main/Features/FireAndTribute/Cheats/FireAndTributeCheats.cs` | The two console commands |
| `Main/Adapters/RaidHostAdapter.cs`, `IRaidHostAdapter.cs`, `RaidHostTypes.cs` | Spawn, roster, pin, orders, villages, despawn |
| `Main/Adapters/FireAndTributeNoticeAdapter.cs` | The messages and the horn |
| `Main/_Module/ModuleData/fire_and_tribute/fire_and_tribute.json` | Tuning and the eight hosts |

## Tests

MSTest with NSubstitute and a hand-written engine fake, in `TAOM.Tests/Features/FireAndTribute/`: config validation (one test per rule), the data against ModuleData and the live map, the pure math, the save codec, the service (spawn gates and timers, every steering branch, event counting, every end path, co-op stand-down), the console parsers, the session reset and wiring, and IL pins on the adapter.

## Still owed

- **In game:** force each side from the console, watch a host raid, fight it, save and load mid-raid, switch the feature off mid-host, and check the destroy path of a host killed in a battle.
- **Balance:** two fast-forward runs to day 200 from one save, hosts on and off, compared with `tools/analyze_war_ledger.py`.
- **Budget check from `maxHourMs`:** read the slowest hourly step of the day from the daily debug line over a long run and compare it with the proposed 1 ms budget.
- **Translation (paid):** the five message keys, the nine `taom_fat_host_*` names (eight hosts and the `taom_fat_host_unknown` fallback, "A raiding host") are English only until the localization run.
- **Slice 2:** the tribute envoy, the grudge (state, save section and target preference land with it) and the player-spared logic.

## Changelog

- 2026-10-20: slice 1, the raiding hosts (#870).
- 2026-10-21: review fixes: Blacklocks and the Gondor host re-anchored, singular host names, the `regions` key renamed `anchors`, army leaders weighed by their army, ignored parties and non-leader besiegers skipped, the village cache removed (villages read live, the raidable rule moved to the math), retirement keeps a fighting host tracked, the grudge removed until slice 2.

## GitHub Issue

- **Issue:** #870, Fire and Tribute: raiding hosts for Good, Neutral and Evil, with a tribute envoy
- **Status:** Open (slice 1 built, slice 2 owed)
