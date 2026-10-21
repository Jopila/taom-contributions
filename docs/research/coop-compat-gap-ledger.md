# Co-op compatibility gap ledger (TAOM v1.5.5)

Status of every TAOM defect and gap that Sternab's private `Sternab/TAOM-Coop` repo shims or
lists, checked against TAOM `bannerlord-1.5.x` on 2026-10-21. His register was written against
TAOM public 2.0.29.5 on Bannerlord 1.4.8, so each row was re-read in our code.

**Sources:** his `docs/coop-taom-compat-register.md`, `docs/current-compatibility-ledger.md`,
`docs/taom-dedicated-server-guide.md` section 2, and the shims under `src/TAOM.CoopCompat/`
(branch `codex/taom-coop`, HEAD `e3196ee3`). Provenance:
[register row](../reference/provenance-register.md#taom-coop-sternab). Background:
[coop-interop.md](../features/coop-interop.md), [bannerlordcoop-internals.md](bannerlordcoop-internals.md).

**Status values:** PRESENT (the defect exists in our code), PARTIAL (gated, but the gate switches
the feature off instead of making it work for each player), FIXED, UNVERIFIED (evidence missing).

**One framing fact:** `Main/_Module/SubModule.xml:76` tags TAOM `DedicatedServerType=none`, so
`TAOM.SubModule` runs headless only when the server forces it. Every `IsDedicatedServer` gate
matters only in that mode, and most of them stand a feature down.

## Remediation gates

| ID | Item | Status | Evidence | Owner | Action |
|---|---|---|---|---|---|
| R1 | A failed role-provider bind is cached forever; a client then reads both `IsAuthority` and `ShouldDeferToHost` as true | PRESENT | `Main/Features/CoopInterop/CoopSessionProvider.cs:132-133` sets `_bound = true` after a failed `BindLocked()`; `:106` then returns no session, so `CoopSessionPolicy.cs:45` gives authority | TAOM | Retry the bind until it succeeds; with co-op active and no role probe, `IsAuthority` is false |
| R1b | A client may read authority before Coop's container is live at `OnSessionLaunched` | UNVERIFIED | `CoopSessionProvider.cs:112-113`; consumers such as `WarOfTheRingBehavior.cs:86-91`. Missing: Coop's container timing on a client | TAOM, Coop | Log the probe result at each TAOM `OnSessionLaunched`, read it in a live join |
| R2 | `CollectAssemblyTypesShim` can give Coop AutoSync a partial type list | PRESENT | `Dependencies/SubModule.cs:228` installs it unconditionally; `Dependencies/Foundation/CollectAssemblyTypesShim.cs:108-114` returns the partial list and swallows | TAOM | Rethrow for Coop assemblies, or skip the install when co-op is active |
| R3 | `SubModuleConstructionGuard` hides a failed Coop constructor | FIXED | Deleted (`docs/migration/v1.5.5-impact.md:26`) | none | Remove the stale mention in `bannerlord-together-compat.md` |
| R4 | SaveShield still swallows `MISSION-INIT` faults under co-op | PRESENT | `Dependencies/Foundation/SaveShieldPolicy.cs:33-35` | TAOM | Under co-op, rethrow `MISSION-INIT` |
| R5 | `Patch69_TournamentEndGuard` swallows an NRE in the tournament completion path | PRESENT (NRE only) | `Patch69_TournamentEndGuard.cs:39,81-84`; applied with no co-op check at `Main/SubModule.cs:2013` | TAOM | Rethrow when co-op is active; keep the diagnostic dump |
| R6 | `Hero._exSpouses` is null on replicated heroes; TAOM's screen finalizer leaves a half-pushed screen | PRESENT | No guard in `Main/`; `Patch37_CrashReport.cs:56-62` swallows on `ScreenManager.Tick` | Coop (field), TAOM (finalizer) | Coop initialises constructor-only collections; TAOM recovers the screen stack |
| R7 | Headless FaceGen exposes only the human race | PRESENT | `RaceManager.cs:70-73` falls back to `human` | DS, CoopCompat | A headless race provider on the server; TAOM logs the race count at startup |
| R8 | `DedicatedServerType=none` | PRESENT | `Main/_Module/SubModule.xml:76` | TAOM | Change only after the headless boot gate passes |
| R9 | PatchShield pass 1 may install before co-op is detected; pass 2 does not remove it | PARTIAL | `Dependencies/SubModule.cs:221-231,287-290`; `PatchShield.cs:149-162` | TAOM | Pass 2 unpatches the pass-1 shields when co-op is active |

## TAOM feature parity

| ID | Item | Status | Evidence | Owner | Action |
|---|---|---|---|---|---|
| T1 | Special-resource victory earning depends on `MapEventEnded`, which co-op clients rarely get | PRESENT | `SpecialResourcesBehavior.cs:60,303`; `coop-interop.md:248-249` | TAOM | Earn on the authority for every player party on the winning side, keyed by hero |
| T2 | Prisoner and raid income credits the world-gen `MainHero` | PARTIAL | `SpecialResourcesBehavior.cs:376-378,557-559`: nobody earns on a server; the comment at `:566` is contradicted by `coop-interop.md:248-249` | TAOM | Credit the acting hero |
| T3 | Recruit cost charges `MainHero`, so players on a server recruit priced troops free | PRESENT | `SpecialResourcesBehavior.cs:433-437` | TAOM | Charge the leader of the recruiting party |
| T4 | Emissary troops are granted into a client-side roster | PARTIAL | `EliteEmissaryInquiryPresenter.cs:199-205` refuses the purchase on a guest, so on a server nobody can buy | TAOM, CoopCompat | An authoritative purchase command: charge and grant on the server |
| T5 | War of the Ring participation counts only the authority's `MainHero` | PRESENT | `WarEventSnapshotAdapter.cs:64-66,96`; `MomentumEventService.cs:75-76` | TAOM | Count any player hero's party |
| T6 | Troop weight sheds over-weight parties of remote players | PARTIAL | `TaomSettings.cs:41` has the toggle; `PartyUpgraderUpgradeReadyTroopsHook.cs:32` exempts only `MainParty` | TAOM | Exempt every player-controlled party; server-authoritative setting |
| T7 | Career choice groups collapse on refresh | FIXED | PR #373 (`b5bd1d280`) is an ancestor of HEAD; the screen was later rebuilt (`29d8ac413`) | none | Tell Sternab his shim can go |
| T8 | Career and special-resource writers act only for `MainHero` | PRESENT | Storage is per hero (`SpecialResourceStorageService.cs:17`), but writers gate on `MainHero` (`SpecialResourcesBehavior.cs:203`, `CareerCampaignBehavior.cs:56,111,118`) | TAOM | A server-side per-hero tick over player heroes, plus a client snapshot |
| B1 | Unreachable path targets assert every tick; TAOM_Map navmesh holes | UNVERIFIED | Not inspected | TAOM_Map, Coop | Get Sternab's coordinates; test the Edoras path on the server |
| X1 | The field camp overlay calls the local camp service on a co-op client | PRESENT | No co-op gate under `Main/Features/FieldCamp/` | TAOM | `[CoopSuppressedUi]` or gate on `MayWriteSaveBackedState` |
| X2 | Wanderer pre-introduction NRE for named heroes without a template | UNVERIFIED | No data scan done | TAOM data | Add a validator rule |
| X3 | Retired Misty Mountain troop ids (2.0.23) have no save alias | PRESENT | No alias in the repo | TAOM | Decide whether pre-2.0.23 saves are supported |
| X4 | Restored Full War saves miss configured wars | FIXED (TAOM half) | `WarOfTheRingBehavior.cs:86-91` | Coop | none |
| X5 | A null TAOM `BodyPropertyRange` aborts Coop's campaign-ready step | UNVERIFIED | Not scanned | Coop, TAOM data | Scan BodyProperties ids |

## Feature support contract

| ID | Feature group | Status | Evidence | Action |
|---|---|---|---|---|
| C1 | Race, culture, possession | PARTIAL | `PlayerPossessionService.cs:39,53` keeps choices in process memory only; a reconnect loses them | Server-held identity snapshot per player |
| C2 | War of the Ring | PARTIAL | `WarOfTheRingMomentumBehavior.cs:139` gated; attribution is T5; state not synced | T5 fix plus momentum transport |
| C3 | Careers and special resources | PRESENT | T1, T2, T3, T8 | Server-owned balances with a command and acknowledgement protocol |
| C4 | Recruiting and emissary purchase | PRESENT | T3, T4 | As T3 and T4 |
| C5a | Recurring castle recruitment | FIXED (stand-down) | `CastleAiRecruitDispatcher.cs:87` | Accept, or design server-side recruitment later |
| C5b | Alignment desertion | PRESENT | `AlignmentDesertionBehavior.cs:53,61-62` treat remote players as AI clans | Classify player-controlled parties, or disable during a session |
| C5c | Culture marketplace | PARTIAL | `CultureMarketplaceBehavior.cs:73` runs on the server; settlement `ItemRoster` sync unverified | Confirm Coop syncs settlement stock |
| C5d | Troop weight | PRESENT | T6 | As T6 |
| C6 | Messenger, siege defence, culture conversion | PARTIAL | `MessengerCampaignBehavior.cs:103,204`, `CultureConversionBehavior.cs:83,103,114` gated; private state not synced | Snapshot, or disable during a session |
| C7 | Enlistment and field commission | PARTIAL | `EnlistmentPlayerActionService.cs:96`, `FieldCommissionBehavior.cs:71` host-only; on a server the host is the placeholder hero | Hide the enlist offer during a session until per-player rules exist |
| C8 | TAOM mission behaviors | PRESENT | `Main/SubModule.cs:2141-2146` adds 33 behaviors with no co-op filter | Classify each one and filter in `AddTaomMissionBehaviors` |
| C9 | Tournaments | PARTIAL | R5; grouping, colours and agent removal are Coop seams | R5 fix, then a two-player lifecycle run |

## Co-op-only rows (upstream candidates, not verified in depth)

| IDs | Note |
|---|---|
| J1, J2, J3 | All-or-nothing join baseline, dangling AI references, silent rejection: Coop join and capture code |
| J4, J5, J6 | Assembly resolve path, `Monster` registration, `ArmyRegistry` keyed by kingdom: Coop |
| B2, B3, B4 | Occupancy pause, non-idempotent roster deltas, native warning dumps: Coop and server build flags |
| S1, S2, S4, S5 | Capture of healthy parties, partial battle verdicts, tournament authority race, no position replication: Coop |
| S3, S11 | Tournament agent removal NRE; `HandleClosingHold` NRE after teardown: Coop, TaleWorlds |
| S12 | Headless banner palette returns `0xDEADBEEF`: send the colour id, not the value |
| K1 | Kingdom decisions addressed by list index: Coop protocol |

## Summary

- 35 TAOM-relevant rows: 18 PRESENT, 10 PARTIAL, 4 FIXED, 3 UNVERIFIED.
- Most existing co-op gates stop corruption by switching the feature off. On a dedicated server
  that means no player gets special resources, emissaries or War of the Ring credit.
- Highest risk first: R1 (authority fork on a client), R2 (silent loss of Coop sync handlers),
  C5b and T6 (silent troop loss for remote players), T3 (free priced troops), R4 and R5 (hidden
  mission and tournament failures).
