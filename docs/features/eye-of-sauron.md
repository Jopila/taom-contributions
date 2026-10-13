# Eye of Sauron

## Overview

Great deeds draw a hunter. A player whose battles against enemy kingdoms build up a deed score is hunted by a
named hero with a formidable host: one of the Nine with high-tier Mordor troops (Dol Guldur troops for Khamûl) when
the player fights for the Free Peoples, or Glorfindel with a host of the Eldar, sent by Elrond, when the player
fights for the Dark. The hunter closes in while out of sight and, once in sight, moves faster than the player, so a
hunt all but forces a battle. Milestone M8 of #765 (War Chronicle).

## Why This Exists

- **Vanilla behavior:** no lord ever singles the player out. An AI party engages the player only when its own
  initiative score says so, and drops the chase as soon as the player outruns it.
- **TAOM requirement:** the war should notice the player. Sauron's attention, or Elrond's, is lore the campaign
  never showed; a player who keeps winning should meet the enemy's champions, not only its levies.
- **Without this feature:** a strong player never meets the Nine or Glorfindel in the field unless he seeks them out.

## The maintainer's decisions (final)

1. A **Free-side** player whose deeds (won battles and sieges) draw the Eye is hunted by the Witch-king, Khamûl or
   another of the Nine with a formidable army of high-tier Mordor troops (Dol Guldur troops for Khamûl).
2. An **Evil-side** player is hunted by Glorfindel (`lord_R2_1`), sent by Elrond, with high-tier Elven troops.
3. Only a player whose kingdom (ruler, vassal or mercenary) is **at war with the hunter's kingdom** is hunted. A
   kingdomless player is never hunted.
4. A hunter who already leads a party has **that party** re-tasked and topped up; a free hunter gets a new party
   spawned out of sight near the player. Never a second party for one hero.
5. The hunter moves closer in steps, hour by hour, while out of sight; once within the player's visual range he
   moves faster than the player.
6. No reward beyond normal battle loot. **Single-player only** in v1.

The player is told once when a hunt starts and once when it ends (orchestrator decision: no warning phase).

## Architecture

### Design Challenge

The engine has no "hunt the player" order that survives the AI's own decisions, the speed model runs on the
parallel party tick, a party-size limit is cached per roster version, and a topped-up host is trimmed by the daily
TroopWeight shed unless its limit covers it. Every piece of state must survive a save, and a pinned party must never
be left pinned.

### Solution Approach

| Piece | What it does |
|---|---|
| Deeds | `MapEventEnded`: a player victory over a side whose faction is a `Kingdom` scores 1 (field), 2 (sally out or a battle outside a siege) or 3 (siege assault) against that kingdom's side (`AlignmentService.GetKingdomSide`; Neutral never scores). Two scores, against Evil and against Free, decay with a half-life when read. Deeds score even while kingdomless. |
| Start (daily) | Single-player (no co-op session, no dedicated server, read live); the player clan has a kingdom; its side is Free or Evil; the score against the other side reaches the threshold; no cooldown. |
| Hunter | Free player: the Nine in order, Witch-king (`lord_1_15`) first, Khamûl (`lord_1_48`) last. Evil player: Glorfindel only. The first who is alive, not a fugitive or prisoner, not marked for death, and whose live faction is at war with the player's kingdom is chosen. |
| Busy hunter | His party is re-tasked only when it is not in a battle, an army, a siege or a settlement, not disbanding and not pinned by anyone else. Otherwise the hunt waits for tomorrow; it never spawns instead. |
| Free hunter | Spawned with `MobilePartyHelper.SpawnLordParty` at a reachable point 1.6 to 2.5 of the player's sight away (straight line beyond 1.5), after removing him as governor and re-checking that he belongs to no party. |
| Order | `SetPartyAiAction.GetActionForEngagingParty(hunter, MainParty)`, `CheckPartyNeedsUpdate`, `SetDoNotMakeNewDecisions(true)`. Re-issued hourly only when lost, since every call resets the party's movement state. |
| Host | Registered as the hunter's party first, then topped up to `HunterStrengthRatio` times the player's strength (his army's when he leads one) with the host's troops, `ceil(deficit * share / power)` each, capped at `HunterMaxTroops` men. Three days of grain, topped up daily. |
| Steps (hourly) | Out of sight only: rebuild the path to the player, walk `min(pathGap - 1.5 S - 3, StepCap)` along it, accept the point only on the player's island with no enemy of the hunter within 3, and at least 1.5 S (straight line) from the player; then move the party and rebuild its path. S is the player's seeing range, re-read each hour. |
| Speed | A main-thread frame tick publishes `{hunter party, in sight, player speed, multiplier}`. `TaomPartySpeedModel` raises the hunter to `player speed * HunterSpeedMultiplier` (never under 1) while in sight. The hunter's speed cache is refreshed when the in-sight state flips or the player's speed moves more than 5%. |
| Size | `TaomPartySizeModel` lifts the hunter's limit to the larger of its raw and weighted head count, before the TroopWeight line, so the daily shed never trims the host. |
| End | The first of: any battle involving the hunter's party; the party destroyed, missing or under another leader; the hunter dead, captured or a fugitive; the war ending; the player kingdomless; a co-op session or server; `HuntTimeoutDays`; the switch off; `taom.eye_end_hunt`. Every end releases the pin (only when the hunt set it), clears the snapshot, refreshes the speed and starts the cooldown. |

### Component Diagram

```
CampaignEvents (daily, hourly party, frame, MapEventEnded, MobilePartyDestroyed, session launch)
        |
  EyeOfSauronBehavior (thin router, SyncData)          TaomPartySpeedModel / TaomPartySizeModel
        |                                                         |  IEyeOfSauronHook (snapshot only)
  EyeOfSauronService (scores, gates, hunter choice, host plan, steps, snapshot, ends)
        |
  IHunterPartyAdapter -> HunterPartyAdapter (Hero, MobileParty, NavigationHelper, popups)
```

`EyeOfSauronModule` registers the services in `IoC.Configure` and builds the behavior with `new` in its declaration's
factory, once per campaign start, so the behavior's constructor resets the singleton service before any event or
SyncData (`EyeOfSauronWiringTests` pins the shape and rejects a cached, static or resolved construction).

### Persistence

Eight `SyncData` keys (`_taom_eye_*`): both scores, the last decay day, the cooldown end, the hunter's hero and party
StringIds, whether the hunt set the pin, and the start day. Ids are resolved against the live campaign on every use.
A load drops the in-sight state and re-registers the host; on session launch a hunt whose party is gone ends.

**Uninstalling mid-hunt** leaves the hunter's saved AI pin with nothing to clear it. The timeout bounds the window in
a game that keeps TAOM; remove TAOM only between hunts.

### Threading

The speed and size models run on the parallel party tick as well as the main thread. They read only the immutable
snapshot (`Volatile.Read`); the multiplier is captured into it on the main thread, so neither model reads MCM,
`MainParty.Speed` or a position.

## Configuration

MCM group **War of the Ring/Eye of Sauron**. Defaults live in `EyeOfSauronSettingsProvider`; the provider reverts a
non-finite float to its default before clamping it into the slider range, and clamps the ints.

| Setting | Default | Range | Meaning |
|---|---|---|---|
| `EyeOfSauronEnabled` | on | | Master switch; turning it off ends a running hunt at the next daily tick |
| `EyeThreshold` | 10 | 1 to 100 | Decayed deed score that draws a hunt |
| `EyeHalfLifeDays` | 30 | 1 to 365 | Days for a score to halve |
| `EyeCooldownDays` | 60 | 0 to 365 | Days after a hunt ends before another can start |
| `HuntTimeoutDays` | 20 | 1 to 120 | Days before the hunter gives up |
| `HunterSpeedMultiplier` | 1.3 | 1.1 to 2.0 | In sight, the hunter moves at least this many times the player's speed |
| `HunterStrengthRatio` | 1.3 | 0.5 to 3.0 | Host strength as a multiple of the player's |
| `HunterMaxTroops` | 250 | 20 to 1000 | Most men in the hunter's party after the top-up |
| `HunterStepCap` | 15 | 1 to 50 | Longest single out-of-sight step, in map units |

MCM persists each property, so a changed compiled default reaches fresh installs only.

### Hosts (`HuntTroopLists`)

| Host | Troops (share) |
|---|---|
| Mordor | `mordor_num_temple_knight` (0.25), `mordor_num_temple_guard` (0.25), `mordor_num_shadowbow` (0.20), `mordor_num_knight` (0.15), `mordor_uruk_baraddurguard` (0.15) |
| Dol Guldur (a hunter whose kingdom is `dolguldur`) | `dg_khamul_shadow_knight` (0.30), `dg_khamul_shadow_guard` (0.25), `dg_khamul_shadow_reaper` (0.20), `dg_khamul_shadow_bowman` (0.25) |
| Rivendell (Glorfindel) | `rivendell_glorfindel_guard` (0.30), `rivendell_knight_golden_flower` (0.25), `rivendell_royal_knight` (0.25), `rivendell_gondolin_battlemaster` (0.20) |

`HuntTroopListsDataTests` proves every id is a level 36 or higher soldier of its culture in `Main/_Module/ModuleData`.

## Console commands

| Command | Tier | Does |
|---|---|---|
| `taom.eye_status` | A | Scores, threshold, cooldown, the player's kingdom and side, and the running hunt (hunter, party, pin, day, in sight, speeds) |
| `taom.eye_add_score <evil\|free> <n>` | B | Adds `n` (between -1000 and 1000) to the score against Evil or against Free; never below zero |
| `taom.eye_start_hunt` | B | Starts a hunt now, ignoring the score and the cooldown; the switch, single-player, kingdom and war gates still hold |
| `taom.eye_end_hunt` | B | Ends the hunt and releases the hunter; a spawned hunter stays on the map as an ordinary lord party |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/EyeOfSauron/EyeOfSauronService.cs` | All logic; implements `IEyeOfSauronHook` |
| `Main/Features/EyeOfSauron/IEyeOfSauronHook.cs` | The models' seam |
| `Main/Features/EyeOfSauron/EyeOfSauronBehavior.cs` | Event router and SyncData; constructor resets the service |
| `Main/Features/EyeOfSauron/EyeOfSauronModule.cs`, `EyeOfSauronIoC.cs` | Feature module and registration |
| `Main/Features/EyeOfSauron/HuntTroopLists.cs` | Hunter order and hosts |
| `Main/Features/EyeOfSauron/HuntBattleReader.cs` | `MapEvent` to a plain battle summary |
| `Main/Features/EyeOfSauron/EyeOfSauronSettingsProvider.cs` | Validated MCM values |
| `Main/Features/EyeOfSauron/EyeOfSauronConsole.cs` | The four console commands |
| `Main/Adapters/IHunterPartyAdapter.cs`, `HunterPartyAdapter.cs`, `HuntSnapshots.cs` | Engine boundary |
| `Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs`, `TaomPartySizeModel.cs` | One seam line each |

## Dependencies

- `IAlignmentService` (Execution): the side of a kingdom.
- `ICoopSessionProvider`, `IDedicatedServerProvider` (CoopInterop): the single-player gate.
- `AiPartySizeService.AddResultFrameBonus`: adds the host bonus in the result frame.

## Tests

- `TAOM.Tests/Features/EyeOfSauron/EyeOfSauronServiceTests.cs`: decay and NaN scores, deed weights and the Neutral,
  non-kingdom and lost-battle cases, every start gate, hunter order, the busy guard and spawn guards, host sizing
  and the head cap, food, step size and landing, the speed floor and size bonus with their NaN gates, every end
  releasing the pin only when the hunt set it, reset and persistence.
- `EyeOfSauronWiringTests.cs`: the module, the per-campaign construction pin and its negative rows, SubModule's hook,
  the speed seam's order. `AiPartySizeOrderingTests` pins the size seam before the TroopWeight line.
- `EyeOfSauronBehaviorSessionResetTests.cs` (RequiresGame), `EyeOfSauronSettingsProviderTests.cs`,
  `HuntTroopListsDataTests.cs`.

## In-game test checklist

Owed before the issue closes (`triage-needs-ingame`). Use a Free kingdom (Gondor) unless stated.

1. **Won:** `taom.eye_add_score evil 20`, wait a day. The start popup names the hunter. Find and beat him; the
   "is beaten" line shows and `taom.eye_status` reports no hunt and a cooldown.
2. **Lost:** start a hunt, let the hunter catch and beat you. The hunt ends with "The hunt is over".
3. **Timed out:** set Hunt Timeout to 1, start a hunt, stay in a town for two days. The hunt ends; the hunter resumes
   his own decisions.
4. **Re-tasked hunter:** with the Witch-king leading his own party outside an army, `taom.eye_start_hunt`. His
   existing party turns toward you, grows to the strength ratio and no second party appears.
5. **Spawned hunter:** with the Witch-king without a party, `taom.eye_start_hunt`. A new party appears out of sight
   and closes in hour by hour; in sight it outpaces you.
6. **Save and reload mid-hunt:** save while hunted, reload. `taom.eye_status` shows the same hunter, party and day;
   the hunter still chases and is not shed back to its old size.
7. **Glorfindel:** join an Evil kingdom at war with Rivendell, `taom.eye_add_score free 20`. Glorfindel rides with an
   Elven host and the Elrond line shows.
8. Kingdomless: leave your kingdom with a high score; no hunt starts, and a running one ends.

## Changelog

- 2026-10-08: built (#765 M8): deeds, hunter choice, re-task or spawn, host top-up, out-of-sight steps, in-sight
  speed floor, host size seam, console, MCM.

## GitHub Issue

- **Issue:** #765 (War Chronicle), milestone M8
- **Status:** Open
