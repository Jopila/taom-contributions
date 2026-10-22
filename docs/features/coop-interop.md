# CoopInterop

TAOM's side of sharing one campaign with a third-party co-op mod without the two corrupting each
other's saves.

**Status (2026-08-02): co-op CONFIRMED WORKING by a player.** A community member completed a real
TAOM + BannerlordCoop session. That retires the "unverified, do not tell players it works" caveat
this file carried on 2026-08-01.

Be precise about what that confirms, though: it establishes that the pair **runs and is playable**.
It does not individually confirm each gate below — nobody has audited object sets between peers, and
two known-risk paths are still unproven (see [What is NOT done](#what-is-not-done)). Report it as
"working, with known rough edges", not as "fully verified".

The same session found a **frame-rate collapse caused by TAOM's own PatchShield**, now fixed — see
[PatchShield is skipped under co-op](#patchshield-is-skipped-under-co-op).

**Field report (2026-08-03).** A testing group ran TAOM v2.0.16 on Bannerlord 1.4.7.117484 across
three configurations — BannerlordCoop v0.1.1 client-hosted, a BannerlordCoop dedicated server, and
Bannerlord Together a0.5.3.1 — and filed nine sections of findings. Most of what this file gained on
that date traces to it. It does not upgrade the framing above: the group ran a server and two
clients, not a peer-to-peer object-set audit, so "nobody has audited object sets between peers"
still stands.

> **Why this file has content now.** It was a deliberate stub pointing at
> `bannerlord-together-compat.md`, on the sound principle that a second partial copy of the compat
> rules would drift. That held while there was one co-op mod. There are now two, with different
> architectures, and the BT doc is BT-specific — so this file owns what is SHARED plus the
> BannerlordCoop-specific authority layer, and still routes rather than restates.

## Routing

| For | Read |
|---|---|
| BannerlordTogether specifics, its no-decompile policy, its boot matrix | [`bannerlord-together-compat.md`](bannerlord-together-compat.md) |
| BannerlordCoop internals — Harmony ids, tick model, policies, object identity, integration surface | [`../research/bannerlordcoop-internals.md`](../research/bannerlordcoop-internals.md) |
| What happens to the hero a joiner created, and how TAOM's character-creation grants get re-applied | [`player-possession.md`](player-possession.md) |
| Why a TAOM-less host can corrupt every hero's race on a full client | [`hero-race.md`](hero-race.md) — the degenerate-legend capture guard |
| Raw analysis evidence (133 findings, 30 adversarial verdicts) | [`../raw/bannerlordcoop/`](../raw/bannerlordcoop/) |

## The two co-op mods are different projects

| | BannerlordTogether | BannerlordCoop |
|---|---|---|
| Launcher id | `BannerlordTogether` | **`Coop`** |
| Workshop | 3761555398 | 3770450698 |
| TAOM inspection | **No-decompile policy — respected.** Ids learned only at runtime from Harmony's public registry, or from its authors | Public upstream project, ships 893 of its own generated `.cs` files; decompiled 2026-08-01 |
| TAOM support | Detected, shields applied; nothing further verified | Detected + host-authority gating (below) |

`BattleLinkMPClient` is also detected; it is a multiplayer-window mod, not a shared campaign.
**Never enable two co-op mods together** — both transpile the same campaign types and each ships its
own `0Harmony`.

## Where the code lives

| File | Role |
|---|---|
| `ICoopPresenceProvider` / `CoopPresenceProvider` | Test seam over static `CoopPresence`. **Process-constant**: "is a co-op module enabled". |
| `ICoopSessionProvider` / `CoopSessionProvider` | **Session-varying**: "is a session live, and do I own the simulation". Reflection-bound, no compile-time reference to Coop. |
| `IDedicatedServerProvider` / `DedicatedServerProvider` | **Process-constant**: "is there a real player in this process at all". Reads the engine's build constant `GameNetwork.IsDedicatedServer` (the server DLL returns true, the client DLL false), NOT co-op role; `TAOM.Dependencies` reads the same constant directly and `DedicatedServerProvider` is the test seam for the services. |
| `PlayerControl` / `IPlayerControlQuery` / `CoopPlayerControlQuery` | **Per entity**: "is this party, clan or hero a human player's?" (Player, NotPlayer, Unknown). Reflection-bound to Coop's `PlayerManager.TryGetControlledObjectInfo`, answers live, caches only the binding (#966). See [Who is a player](#who-is-a-player-the-fourth-question). |
| `RecruitAttributionScope` and `Hooks/CoopRecruitAttributionPatch` (Patch113) | A thread-static party id set around Coop's server-side `HandleOnRecruitmentDone`, so the party-less `OnUnitRecruited` can be charged to the recruiting guest's party leader. Policy: `SpecialResources/RecruitChargePolicy`. Applied at `Main/SubModule.cs:1866`; notes in [the patch registry](../reference/harmony-patch-registry.md). |
| `CoopSessionPolicy` | The pure authority decision, split out so it is testable without a game — same pattern as `PatchShieldPolicy` / `SaveShieldPolicy`. |
| `Missions/` (`CoopMissionRole`, `CoopMissionRoles`, `CoopMissionRolePolicy`, `ICoopMissionAuthority`, `CoopMissionAuthority`, `CoopMissionOwnership`) | The mission behavior layer (#967 C7): each TAOM behavior's role, the add-time filter, and the reflection-bound live ownership read. See [Mission behaviors](#mission-behaviors-967-c7). |
| `CoopSuppressedUiAttribute` / `CoopUiRegistrationPolicy` | Marks and filters UIExtenderEx types a co-op host has taken ownership of. |
| `SaveDefinerCollisionDetector` / `Guard` / `SaveDefinerRecord` | Base-id preflight and crash attribution. **Heuristic** — the engine keys on `_saveBaseId + saveId`, so a shared base is legal; engine-only groups are dropped and the rest warn rather than assert. [RCA](../reviews/rca-savedefiner-false-positive-2026-08-01.md). |
| `Diagnostics/HarmonyCensus*` | Runtime patch-overlap report — the sanctioned substitute for reading another mod's binary. |

Other half in `Dependencies/Foundation/`: `CoopPresence`, `CoopModuleList`, and the `PatchShield` /
`SaveShield` co-op inversions.

## Layer 1 — detection (process-constant)

`CoopPresence` reads the launcher's active-module list by reflection and matches ids by **exact,
case-insensitive equality** against `CompiledModuleDefaults` ∪ shipped `coop-modules.txt`.

Exact equality is why `Coop` had to be added explicitly: nothing about `BannerlordTogether` matches
it, so from the moment BannerlordCoop shipped until 2026-08-01 **every TAOM shield was inert against
the co-op mod players actually had.** Parsing is union-only — the file can add ids, never remove a
compiled default, because it also feeds PatchShield's protected-owner allowlist.
`coop-force-active.flag` forces presence on when detection fails (renamed build, fork, unknown mod).

Adding an id requires four coupled edits, pinned by `BundledDependencyManifestTests` +
`AssemblyRedirectListTests`: `CompiledModuleDefaults`, `coop-modules.txt`, and
`<ModulesToLoadAfterThis>` in **both** `SubModule.xml` manifests (the engine ignores
`<DependedModuleMetadatas>`).

When a co-op module is active: PatchShield stops unpatching foreign owners (stripping one peer's copy
of a sync patch does not crash — it silently diverges two campaigns, which is worse); SaveShield
**rethrows** `SAVE-LOAD` faults (and only those); the HarmonyCensus writes its report.

**Role probe binds once.** `CoopSessionProvider` binds Coop's `TryGetContainer` and `IsServer` by
reflection on first use and keeps the result, success or failure. Coop loads `GameInterface` (which holds
`ContainerProvider`) and `Common` (which holds `ModInformation`) in its own `OnSubModuleLoad`, so the types exist before any session can be live and
a retry would only rescan the AppDomain for an unchanged answer. A container provider without a
readable `IsServer`, or a type finder that throws, leaves the probe unavailable and TAOM
authoritative; the failure is logged once. `CoopPlayerControlQuery` binds Coop's `PlayerManager` the
same way, once (the `Lazy` field at `CoopPlayerControlQuery.cs:28`, `Bind` at `:101`), and in a live session answers Unknown for any
entity while that bind failed.

## Layer 2 — assembly resolution

The `AssemblyResolve` handler in `Dependencies/SubModule.cs` matches on **simple name only, discarding
the requested version** — safe only while TAOM's copy is newest. BannerlordCoop ships five higher:

| Name | TAOM | Coop |
|---|---|---|
| `Serilog` | 2.0.0.0 | 4.2.0.0 |
| `System.Runtime.CompilerServices.Unsafe` | 4.0.4.1 | 6.0.1.0 |
| `System.Memory` | 4.0.1.1 | 4.0.2.0 |
| `System.Buffers` | 4.0.3.0 | 4.0.4.0 |
| `System.Numerics.Vectors` | 4.1.4.0 | 4.1.5.0 |

All five removed from `RedirectedSimpleNames`; the BUTR stack stays. A version comparison would not
work (Coop calls `Assembly.Load` with a bare partial name — no version to compare) and it cannot be
gated on co-op presence (the handler installs from a static cctor, long before any probe).

## Load order

`TAOM.Dependencies` goes **above the entire vanilla stack, `Native` included** — not merely above the
co-op mod:

```
TAOM.Dependencies → BUTR alias stubs → Native → SandBoxCore → Sandbox → StoryMode → CustomBattle
                  → TAOM → TAOM_Map → LOTRLOME_Armory → Coop
```

**Why it is first, and not "after the vanilla modules like every other mod":** `TAOM.Dependencies`
bundles the whole BUTR stack (0Harmony, ButterLib, UIExtenderEx, MCM) and installs the
`AssemblyResolve` redirect from its **static constructor**. Everything loaded afterwards resolves
those assemblies through that hook. If anything constructs before it, a mod shipping its own bundled
`0Harmony`/`MCMv5` can win the AppDomain slot first — and then TAOM's patches attach to a Harmony
instance nobody else can see, which fails silently rather than loudly. This is pinned by the
`<ModulesToLoadAfterThis>` block in `Dependencies/_Module/SubModule.xml`, which lists `Native`,
`SandBoxCore`, `Sandbox`, `StoryMode` and `CustomBattle` explicitly, plus 20-odd known
BUTR-bundling consumer mods.

**Why `Coop` is last:** it also ships its own `0Harmony` (2.4.2.0, byte-identical to ours) plus
Mono.Cecil/MonoMod/Serilog 4.x. Constructing `TAOM.Dependencies` first lets the redirect collapse
those onto one instance, which is what keeps `Harmony.GetAllPatchedMethods()` — and therefore
PatchShield and the `[HarmonyCensus]` report — able to see across both mods. Ordering TAOM before the
co-op layer additionally matters for the `Priority.High` DeclareWar/MakePeace prefixes: TAOM validates
racial enmity and War of the Ring constraints first, so a blocked action is never synced.

A BUTR/BLSE launcher sorts this from the manifests. The explicit list matters for the vanilla
launcher and for anyone hand-ordering. Same order applies on **every** peer — Coop's join handshake
compares module lists and disconnects on a mismatch.

## Layer 3 — host authority (session-varying)

**BannerlordCoop does not stop a client ticking the campaign.** Its `PartyTickPatch` blocks only the
per-entity tickers, so `DailyTickSettlementEvent` and friends never reach a client — but the **global**
`DailyTickEvent` / `HourlyTickEvent` fire on both peers. Coop's own countermeasure is a hand-written
allowlist of ~127 `RegisterEvents` prefixes over *named vanilla* types; there is no generic hook a
third-party mod can register into, so TAOM authors its own.

**Four questions, not one.** The predicates below all come out of the session/role probe. Two other
facts are process-constant and answer different questions, and a fourth is asked per party or clan
(see [Who is a player](#who-is-a-player-the-fourth-question)). Both directions have cost TAOM a real
bug: presence read where session/role was needed (the diplomacy vetoes, 2026-08-01), and no signal
at all where the dedicated-server question was the one being asked (special-resource earning,
2026-08-03).

| Signal | Question it answers | Constant for the process? | Read it at |
|---|---|---|---|
| `ICoopPresenceProvider.IsCoopActive` | Is a co-op module enabled? | Yes | Patch-application and other one-shot startup decisions — PatchShield install, UI registration |
| `ICoopSessionProvider` (the predicates below) | Is a session live, and do I own the simulation? | No | Anything that mutates shared world state |
| `IDedicatedServerProvider.IsDedicatedServer` | Is this the headless server process, whose `Hero.MainHero` is the idle world-gen hero? | Yes | Anything that credits, charges or rewards `Hero.MainHero` with no session live |
| `IPlayerControlQuery.OfParty` / `OfClan` / `OfHero` | Is this party, clan or hero driven by a human player? | No, per entity | Anything that compares a party, clan or hero against `MobileParty.MainParty` / `Clan.PlayerClan` / `Hero.MainHero`, which under co-op name only this peer, and on the server are temporarily a guest's |

**`IsDedicatedServer` is deliberately NOT derived from co-op role**, and that distinction is the
whole reason the type exists. The co-op "host" is a loopback client of a separate server process
([#967 design comment](https://github.com/haterade22/TAOM/issues/967#issuecomment-6328464305)), so no human plays on the authority process, and the
authority's own hero is nobody's whether or not it is a headless server. What the provider adds is the
no-session case: a headless server with no session live has only the idle world-gen hero the campaign was
created around. So the provider reads the
engine's build constant `GameNetwork.IsDedicatedServer` (the server DLL's getter returns true, the
client's false, and the engine reads it in `Module.Initialize` before it loads any submodule), which is a
fact about the process rather than a probe of another mod's state, and cannot change mid-session. On the
server a false answer is fatal rather than quiet (`TAOM.Dependencies` would run the client UI setup and end
the process), which a build constant cannot give.

**Which predicate to use.** Three, and picking the wrong one has already shipped bugs both ways:

| Use | When |
|---|---|
| `IsAuthority` | A world-mutating handler that only BannerlordCoop can gate. Fails open to singleplayer. |
| `ShouldDeferToHost` | A shared-world DECISION (diplomacy veto, alliance enforcement, global time). Also covers co-op mods TAOM cannot probe. |
| `MayWriteSaveBackedState` | About to write a field that round-trips through a `SyncData` key. |

Do **not** gate on `ICoopPresenceProvider.IsCoopActive` for anything world-mutating. It is
process-constant — true whenever the module is merely *enabled* — so it disabled TAOM's diplomacy
rules for solo players and for the co-op host itself (Codex, 2026-08-01). It is correct only for
one-shot startup decisions such as UI registration.

**One consumer breaks that shape and is not a violation.**
`PlayerPossessionService.TryConsumePossession` gates on `IsCoopActive`, and what follows it is a
hero mutation. Presence is used there as an **heir-succession discriminator**, not as an authority
decision: `Hero.MainHero` also changes in ordinary solo play when the player continues as an heir,
and the presence gate is what keeps a solo heir out of the re-grant path entirely. It is one of
three independent guards, any one of which suffices — presence, single consumption, and a
`SyncData`-persisted per-hero marker (`_taom_possessionReconciledHeroes`) so a reconnect cannot
re-grant. See [player-possession.md](player-possession.md).

`ShouldDeferToHost` keys on whether the ROLE PROBE RESOLVED, which is what makes it safe for
BannerlordTogether: TAOM cannot read host/client there, so it yields on every peer. Gating those
decisions on `IsAuthority` instead would fail open and gate nothing.

`CoopSessionPolicy.IsAuthority = !sessionActive || isServer` — **fails open to singleplayer**. Every
input is best-effort reflection into another mod; a false negative would silently disable TAOM
features for a solo player, which is worse and far less diagnosable than the divergence it guards.

Two traps the provider exists to avoid, both verified in source:

- **`ContainerProvider.Alive` is permanently `false`** — an inline static initialiser evaluated while
  the backing field is still null. Bind `TryGetContainer` instead.
- **`ModInformation.IsServer` defaults `false` and is sticky.** `IsClient` is its negation, so read
  alone it reports "client" in plain singleplayer whenever the Coop module is enabled, and "host"
  forever after hosting once. Only meaningful alongside a live session.

### Who is a player: the fourth question

Under co-op `MobileParty.MainParty` and `Clan.PlayerClan` name only the local peer, so a remote
player's party reads as an AI party. Code that means "a human drives this" asks
`IPlayerControlQuery` instead (#966, `PlayerControl.cs`, `IPlayerControlQuery.cs`,
`CoopPlayerControlQuery.cs`).

| Answer | Means |
|---|---|
| `Player` | With no session, this peer's own main party, `Clan.PlayerClan` or `Hero.MainHero`. In a session, an entity in Coop's player registry, and only that (the local entity included) |
| `NotPlayer` | The AI. With no co-op session this is everything but this peer's own party and clan, so single-player is unchanged. A session TAOM cannot see (BannerlordTogether, or a failed Coop container bind) also answers `NotPlayer`, not `Unknown`, because `IsSessionActive` is false there: remote players are not protected from desertion or the troop weight shed in that case |
| `Unknown` | A session is live but the registry is unreadable: the bind failed or the call threw |

**How it reads.** `OfParty`, `OfClan` and `OfHero` (`CoopPlayerControlQuery.cs:49,55,61`) pass the entity and
its local comparison to `Classify` (`:68`). With no session the local comparison is the answer and nothing
is scanned. In a session the registry alone decides, the local entity included: no human plays on the
authority process, the registry lists joiners only, and on the co-op server `Hero.MainHero`, `MainParty`
and the player faction are temporarily a guest's inside `BarterPlayerContext` and `MainHeroSubstitutionScope`,
so the local comparison is not a stable identity there. The decision is one reflection call to Coop's static
`PlayerManager.TryGetControlledObjectInfo(object, out ControlledObjectInfo)` (`:90-91`). The
`MethodInfo` is bound once through a `Lazy`, success or failure; the answer is never cached because
players join and leave. A clan counts when its `Leader` is registered. Coop registers each player's
hero, main party and clan. It does not register companions or companion-led parties, and an offline
player's party stays registered. The server's own placeholder hero is not in the registry.

**Each feature chooses what `Unknown` means.** The provider never guesses, so each caller picks the
side that costs least:

| Caller | `Unknown` treated as | Why |
|---|---|---|
| Troop weight shed (`PartyUpgraderUpgradeReadyTroopsHook.cs:52`) | Player: nothing is shed | A shed deletes troops, so doubt means skip |
| Alignment desertion (`DesertionOwnerPolicy.ShouldSkip`) | Player: no troops desert | Same reason |
| War of the Ring participation (`WarEventSnapshotAdapter.IsPlayerRelated`) | Not involved | A guess would let a stranger's wars move the shared meter |
| Special resource earning (`SpecialResourceEarnPolicy.MayEarn`) | Not a player: nobody earns | An earn writes a balance, so doubt means no write |

The recruit charge does not use this query: it follows the Patch113 scope instead (below).

**Known limit.** `OfParty` answers for the party only. A companion-led party is not registered, so it
reads `NotPlayer` and the troop weight shed still applies to it; desertion goes by owner clan, which
is registered through its leader.

**Cost.** About 0.3 microseconds and 217 bytes per non-local entity in a live session (reflection
invoke plus the argument array, `IPlayerControlQuery.cs:14-16`). Convert to compiled delegates
before adding an hourly or per-frame caller.

**Recruit attribution (Patch113).** Coop's server applies a guest's recruitment inside
`TroopRosterInterface.HandleOnRecruitmentDone(string mobilePartyId, TroopInfo[])`, run on the game
thread through `GameThread.Run` (`ServerTroopRosterHandler.cs:41-45`), and the target raises
`CampaignEvents.OnUnitRecruitedEvent`, which carries no party, synchronously inside that call.
`CoopRecruitAttributionPatch` sets a `[ThreadStatic]` `RecruitAttributionScope.Current` to the party's
`StringId` around it (Coop's key is `MobileParty_<StringId>`; `ToPartyStringId` strips the prefix).
`SpecialResourceEventAdapter.SnapshotRecruit` reads the scope, and `RecruitChargePolicy.ChoosePayer`
(through `SpecialResourceEarningService.ChargeRecruit`) decides: a client pays nothing; a scoped recruit
charges the scoped party's leader or nobody, never the authority's own hero; a dedicated server or a live
session without a scope charges nobody (no human plays on the authority process, so an unscoped recruit
there is nobody's); otherwise `Hero.MainHero`. A scoped
miss logs one warning, and `Prepare` logs one warning when Coop is active but the target method is
gone. **Known limits, tracked in #967:** the guest's client-side affordability gate (Patch51) reads a
stale balance and the server's charge floors at zero until balances are server-owned, so the charge
has no real effect for a guest yet; and Coop's prisoner and mercenary recruit paths never raise
`OnUnitRecruited`, so their `recruit_cost` is never charged under co-op, the hosting player included.

### Gated (host-only)

`CoopGateDocCoverageTests` fails when a feature module declares a campaign behavior that has no row in this table or in "Deliberately NOT gated" below, so a new behavior gets its co-op row in the same change. Behaviors that predate the test are listed in its `Baseline` and still need classifying; behaviors added by hand in `Main/SubModule.cs` are not checked by the test but still need a row.

**Gate every handler that reaches the mutation, not just the tick.** The first pass gated only the
tick handlers and a deep-review data-flow pass found two HIGH bypasses within hours: sibling handlers
on the *same* behaviour calling the *same* service method with no gate. When adding a gate, grep the
behaviour's whole `RegisterEvents` and follow each handler to the service.

| Behaviour | Gated handlers | Why |
|---|---|---|
| `CultureConversionBehavior` | **`OnSettlementOwnerChanged`**, `OnDailyTick`, `OnGameLoaded` | Replaces notables via `HeroCreator.CreateNotable`. Client's store holds the same pending records — it loaded the host's save. `OnSettlementOwnerChanged` was left ungated on the reasoning that queuing a pending timer is harmless; but the store is `SyncData`-backed and the daily processor that would mature or clear those records is itself gated, so a client accumulated conversions nothing ever services (Codex, 2026-08-01). |
| `RaceAgeBehavior` | `OnDailyTick` | Re-kills locally what the host already replicated. |
| `HiddenHostsMemoryBehavior` | the Scouting XP grant in `OnTick` and `OnBeforeSave` (the same gate: `IsAuthority` and `MayWriteSaveBackedState`) | `Hero.AddSkillXp` writes the scout's saved skill XP; a client's would be per-peer state in the host's save. The pending XP is taken every tick and dropped on a client, so it cannot be banked. The memory rows themselves are not gated (below). |
| `WarOfTheRingBehavior` | `OnDailyTick`, **`OnSessionLaunched`** | Both call the identical `CheckPhaseTransition` → `DeclareWar`, and `OnSessionLaunched` first calls `ReconcileDeclaredWars` → `DeclareWar` (#772), before its phase check. On a save already at Full War, the reconcile declares each Phase 2 or Hostile-pair war whose stored stance is not War and that the model still holds at war (`ShouldBlockPeace`), so a peace made legitimately stays. After the phase check the daily tick (not the session launch, which runs before the map event and siege managers are restored) also calls `EndWarsForbiddenByPhase` (peace for any AI war the Peace or Isengard War phase vetoes), which returns early under `ShouldDeferToHost` because `IsAuthority` alone is true on both peers under BannerlordTogether. Both calls sit behind the one `IsAuthority` return at the top of the handler. `OnSessionLaunchedEvent` fires on every peer, and a co-op join *is* a save-load, so an ungated client issued its own war set on connect. Under BannerlordCoop the host's Phase 2 wars reach clients through Coop's stance sync, and the load-time reconcile through the join snapshot. Under BannerlordTogether `IsAuthority` fails open, so every peer runs the reconcile; it can only declare for a save already at Full War that still has undeclared Phase 2 or Hostile pairs (UNVERIFIED whether that mod's client-side suppression contains it). |
| `WarOfTheRingMomentumBehavior` | `OnDailyTick`, **`OnKingdomDestroyed`, `OnMapEventEnded`, `OnSiegeCompleted`, `OnRaidCompleted`, `OnArmyGathered`, `OnSessionLaunched`** | `MomentumWarState` rides TAOM `SyncData` that nothing replicates. `OnKingdomDestroyed` reached the same `CheckAndApplyVictory` → `EndWar`/`MakePeace` as the tick. Daily gate sits **after** `RefreshMapMeter`: local UI a client still needs drawn. **Participation (#966):** `WarEventSnapshotAdapter` sets `PlayerInvolved` also when any party on either side is a player's (`OfParty == Player`), or the event involves a kingdom holding a player's clan; `Unknown` counts as not involved, and single-player is unchanged. The state itself is still per peer. |
| `MessengerCampaignBehavior` | `OnHourlyTick` | Arrival writes `MobileParty.MainParty.Position` — on a client, *its own* party. |
| `SiegeDefenseBehavior` | `OnHourlyTick` | `Clan.Influence` + global relation off `Hero.MainHero`, which differs per peer. **Known limitation below.** |
| `CastleRecruitmentBehavior` | `OnGameLoaded`, `OnNewGameCreated` | The one castle path a client reaches; creates heroes. |
| `CastleRecruitmentBehavior` (hourly recruit) | `OnHourlyTickParty`, through `CastleAiRecruitDispatcher.TryRecruit` | **Stands down on every peer while a session is live, and always while a co-op mod TAOM cannot probe is enabled** (`IsSessionActive \|\| ShouldDeferToHost`), read live after the castle guards (#920). Not a gold guard: BannerlordCoop already refuses player-controlled parties inside `CheckRecruiting` (its own prefix, which also runs for the dispatcher's delegate call). The stand-down matches BannerlordCoop's shutdown of vanilla's hourly recruit for every party while a session is live (BannerlordCoop keeps that patch after the session ends; TAOM's recruit resumes); `ShouldDeferToHost` also covers BannerlordTogether and any co-op mod TAOM cannot probe, solo play included. Recruiting on entry stays in vanilla. |
| `WarChronicleBehavior` | `OnDailyTick` (`IsAuthority` and `MayWriteSaveBackedState`), `OnKingdomDestroyed` and `OnGameLoaded` (`IsAuthority`) | Escape rolls (`EndCaptivityAction.ApplyByEscape`), effect registry and baseline writes ride TAOM `SyncData` that nothing replicates, and the ledger line is the host's record. Under BannerlordTogether `IsAuthority` fails open, so every peer runs the daily tick with its own escape rolls and rally writes (UNVERIFIED whether that mod's client-side suppression contains it). See [war-chronicle.md](war-chronicle.md). `OnGameLoaded` writes the ledger's `t=load` marker, which `tools/analyze_war_ledger.py` rewinds to (#792), so it is the host's line too. |
| `WarChronicleEventsBehavior` | `OnSessionLaunched`, `OnDailyTick`, `OnSettlementOwnerChanged`, `OnSiegeEventStarted` (`IsAuthority` and `MayWriteSaveBackedState`) | The chronicle's event chain (#765). Opening and resolving events writes timed war effects, the army focus, `Chronicle` momentum awards and the saved event rows, none of which a co-op mod replicates. `OnTick` stays ungated: it only drains the announcement queue, which re-checks `IsAuthority` before it shows a popup. `taom.chronicle_open` refuses on a non-authority peer. Under BannerlordTogether `IsAuthority` fails open, so every peer runs the chain (UNVERIFIED whether that mod's client-side suppression contains it). See [war-chronicle.md](war-chronicle.md). |
| `StuckBattleCampaignBehavior` | `OnHourlyTick`, through `StuckBattleService.Resolve` | **Stands down on every peer while a session is live** (`IsSessionActive \|\| ShouldDeferToHost`), the host included: Coop's `MapEvent.Update` prefix stops vanilla finishing a battle that holds a remote player's party, and the sweep's player-battle test sees only the local player, so an award there would commit and never finish (#748). |
| `BondsOfBattleBehavior` | `OnPlayerBattleEnd` (`ShouldDeferToHost`) | Changes the player's relation with lords and notables, which a client's own `Hero.MainHero` makes differ per peer. Four entry points: this handler; `TroopHandoverService.OnHandover`, called by Patch106's postfix on `PartyScreenLogic.DoneLogic`, which returns false under `ShouldDeferToHost` (the installed co-op module replaces `DoneLogic` with its own bool prefix, so a co-op session may capture an influence of 0 anyway); `TroopHandoverService.OnHandover` again, called by [Lord Courtesies](lord-courtesies.md)' gift settle (its screen is not donating, and the same `ShouldDeferToHost` gate applies); and `TaomBarterModel`, which needs no gate because the installed co-op module routes a client's barter to the server and never calls the model. A dedicated server grants nothing: `RelationGrantService.Grant` refuses it. |
| `PersonalRelationSeedBehavior` | `OnSessionLaunched`, `OnDailyTick`, through `PersonalRelationSeedService.TrySeed` (`ShouldDeferToHost`, `IsDedicatedServer`) and `IsSinglePlayer` (`IsSessionActive`, `ShouldDeferToHost`, `IsDedicatedServer`) | Writes raw relations between the player and each lord and sets one saved flag, and arms `IPersonalRelationService` only in a single player campaign. A client, a host or a dedicated server keeps the loaded flag, seeds nothing and leaves the service disarmed, so the pair rule, the disband cost, the succession rule and Patch107 stay vanilla there. |
| `GiftBehavior` | `GiftConversationFlow.CanOffer`, `CanChoose`, `Ask` and `Pick` (`ShouldDeferToHost`), the methods its dialog lines call | Moves the player's gold, inventory and relation, and saves the cooldown ledger in TAOM `SyncData`. A co-op client (and any peer running a co-op mod TAOM cannot probe) sees no gift line, and `CanChoose`, `Ask` and `Pick` re-check the gate so a line rendered before a session went live does nothing. A co-op host can give. The dedicated-server rule lives in `RelationGrantService.Grant`, which lands nothing there ([Tokens of Esteem](tokens-of-esteem.md)). |
| `GoodDeedsBehavior` | the `HeroPrisonerReleased` and `TournamentFinished` listeners and `TaomPrisonerDonationModel`, through `GoodDeedsService.OnPrisonerReleased`, `OnTournamentFinished` and `DonationRelation` (one private gate, `IsDedicatedServer \|\| ShouldDeferToHost`) | Changes the player's relation with lords and notables, which a client's own `Hero.MainHero` makes differ per peer. Three entry points behind one gate: a release and a tournament win grant nothing on a deferring peer or a dedicated server, and the model returns vanilla's own value there, so a co-op client sees vanilla's donation relation. A co-op host earns as usual ([Good Deeds](good-deeds.md)). |
| `LongMemoryBehavior` | the `RaidCompletedEvent`, `ForceSuppliesCompletedEvent`, `ForceVolunteersCompletedEvent` and `GameMenuOptionSelectedEvent` listeners, through `LongMemoryService.OnRaidCompleted`, `OnCoercionCompleted` and `OnMenuOptionSelected` (one private gate, `IsDedicatedServer \|\| ShouldDeferToHost`) | Takes the player's relation with a village owner's clan leader and the village's notables, which a client's own `Hero.MainHero` makes differ per peer. A raid, a forced taking the villagers resisted and one they did not (only the two no-resist "Continue" options) cost nothing on a deferring peer or a dedicated server; a co-op host loses relation as usual ([Long Memory](long-memory.md)). |
| `OathsOfServiceCampaignBehavior` | `OnDailyTick` raises a flag, `OnHourlyTick` runs `HomeAnchorService` (`IsAuthority`) | The anchor moves heroes and the ledger rides TAOM `SyncData`. Under BannerlordTogether `IsAuthority` fails open, so every peer may run the anchor (UNVERIFIED whether that mod's client-side suppression contains it). See [oaths-of-service.md](oaths-of-service.md). |
| `OathsOfServiceDialogBehavior` | the hire accept, the topic lines and their consequences, through `HireTermsService`, `OathTopicService` and `OathLedger` (`MayWriteSaveBackedState`) | Pays gold and items, hires, changes relation and writes the saved ledger. A co-op client sees the accept and topic lines greyed, and `IOathFlagStore.Set` returns false. |
| `EyeOfSauronBehavior` | `OnDailyTick` (the start and the running hunt's daily check), `OnHourlyTickParty`, `OnSessionLaunched` and the console's `TryStartHunt`, through `EyeOfSauronService.IsSinglePlayer` | **Single-player only** ([Eye of Sauron](eye-of-sauron.md)): stands down on every peer while a session is live, while a co-op mod TAOM cannot probe is loaded, or on a dedicated server (`IsSessionActive \|\| ShouldDeferToHost \|\| IsDedicatedServer`). No hunt starts; a running hunt ends (`SessionLive`) at the daily or hourly end gate, a restored one at session launch, and the end releases its pin. Not gated as handlers: `OnMapEventEnded` ends a hunt its battle decided on any peer, but the deed scoring inside it returns before touching the save-backed score unless single-player; `OnMobilePartyDestroyed` only ends the hunt; `OnTick` publishes the hunt's snapshot, which the two model seams only read, and while a hunt runs it can also stand the hunter off and refresh his speed cache. All three do nothing without a hunt, and a session that goes live mid-hunt ends it at the hunter's next hourly tick. Trade-off (the maintainer's call): a solo player with Bannerlord Together enabled, or `coop-force-active.flag` present, gets no hunt, while a BannerlordCoop-enabled solo player keeps it. |
| `WatchfulHoldsBehavior` | `OnHourlyTickParty` (after the lord filter), `OnDailyTick` | **Stands down** on every peer while a session is live or a co-op mod TAOM cannot probe is loaded (`IsSessionActive \|\| ShouldDeferToHost`): Watchful Holds runs in singleplayer only ([Watchful Holds](watchful-holds.md)). It slows and wounds parties other peers control, and `IsAuthority` fails open under BannerlordTogether. Its province gate reads Realm Borders, so `EnableRealmBorders` stays presentation in `CoopSettingsRelevance`. |
| `FromTheAshesBehavior` | `OnHourlyTick` and `OnMapEventStarted` (through `FromTheAshesReconcile`), the start options and the wait menu's callbacks, through `FromTheAshesService` | **Single-player only** ([From the Ashes](from-the-ashes.md)): the start gate is the Eye of Sauron's predicate plus authority (`IsSessionActive \|\| ShouldDeferToHost \|\| IsDedicatedServer`, or no `IsAuthority`), so a peer sees no option where single-player sees none and no job starts there. `Validate` applies the same predicate to a running job, so a job carried into a co-op session cancels with no penalty at its next look. Every world write (construction points, the village's restoration, materials, relations, Athletics XP, a project's refund) also checks `IsAuthority`; Stop writes nothing without it. Trade-off (the maintainer's call): a solo player with a co-op mod enabled, or `coop-force-active.flag` present, sees the options greyed out with "Not available in a shared campaign." (`FromTheAshesTexts` does not hide `NotSinglePlayer`). |
| `ReserveStepMixin` | `ExecuteAddReserveStep` and `ExecuteWithdrawReserveStep` (the +10K and -10K buttons on the town management window), through `ReevesLedgerService.MayWriteWorld` | **Single-player only** ([Reeve's Ledger](reeves-ledger.md)): the buttons show and work only while `!IsSessionActive && !ShouldDeferToHost && !IsDedicatedServer && IsAuthority`, the Eye of Sauron's predicate plus authority; otherwise the mixin hides them and a click does nothing. The gate is read live at each refresh and again at each click. Both buttons write the world (the player's gold and the town's reserve), always through the engine's `BuildingHelper.BoostBuildingProcessWithGold`, which moves the gold with the reserve. The title, the Profit and Patrol rows and the stash and prison tooltips only read the fief, so `TownManagementLedgerMixin` needs no gate. The trade-off: a solo player with a co-op mod TAOM cannot probe gets no buttons (the same as From the Ashes). |
| `FarWatchBehavior` | `OnHourlyTick`, `OnPartyEncounter` and `OnMapEventStarted` (through `FarWatchReconcile`), the camp option, the pick and the wait menu's callbacks, through `FarWatchService`, and the sight contributor through `FarWatchService.SightFactor` | **Single-player only** ([Far Watch](far-watch.md)): the start gate is the From the Ashes predicate (`IsSessionActive \|\| ShouldDeferToHost \|\| IsDedicatedServer`, or no `IsAuthority`), so a peer sees the option greyed ("not available in a shared campaign") where single-player can ride. `Reconcile` applies the same predicate to a running ride, so a ride carried into a co-op session ends at its next hourly look with a message, no report and no XP, unless its wait menu finishes first: that completion does not check the session, so it reports and raises the sight, and pays the XP only where the next sentence's gate allows. The Scouting XP is the one world write and also needs `IsAuthority` and `MayWriteSaveBackedState(IsCoopClient)`; the ride is cleared from the store before it. A saved kept sight loaded into a session affects only that peer's own map sight (`MobileParty.SeeingRange` of the main party), as the lookout does, and Hidden Hosts' report writes (the raised digits, the scouted settlement) are local presentation like the rest of its memory. |
| `NamegivingBehavior`, `FiefNamingMixin` | The behavior's `OnSettlementOwnerChangedEvent`, `OnClanChangedKingdomEvent` and `OnSessionLaunchedEvent` handlers, through `NamegivingService.ReconcileLore` and `ReconcileAllLore`; the mixin's `ExecuteRenameFief`, the prompts' final apply and the button's refresh, through `NamegivingService.MayWriteWorld` | **Single-player only** ([Namegiving](namegiving.md)): the button shows and works, and the lore names change, only while `!IsSessionActive && !ShouldDeferToHost && !IsDedicatedServer && IsAuthority`, the From the Ashes predicate. Both halves write the world (a settlement's saved custom name, and the player's influence for a rename). `NamegivingService.Check` is read live at each refresh and again inside `Apply`, so the gate holds at the moment of the write; the reconcile passes check it per call, before the switch, so a co-op peer restores nothing either. The switch is counted in the settings fingerprint (`CoopSettingsRelevance` does not exclude it), because the lore half reacts to captures on its own. The trade-off: a solo player with a co-op mod TAOM cannot probe gets no button and no lore names. |
| `SpecialResourcesBehavior` | `OnMapEventEnded`, `OnRaidCompleted`, `OnPrisonerTaken`, `OnHideoutCompleted`, `OnTournamentFinished`, `OnDailyTickHero`, `OnUnitRecruited`, through `SpecialResourceEarningService` and `SpecialResourceEarnPolicy.MayEarn` (`IsSessionActive`, `IsAuthority`, `IsDedicatedServer`, `IPlayerControlQuery.OfHero`) | **A who-is-a-player gate, per player (#967 C1).** With no session, today's rule: the local `MainHero` earns unless this is a dedicated server (one log line). During a session only the authority writes, and it credits each registered remote player: the leaders of the winning or attacking parties for battle, siege, raid and hideout, the winner for a tournament, every living player hero for the daily tick. The registry alone decides who is a player: the authority's own hero earns, pays and counts as nobody's (a guest swapped in as `MainHero` inside a barter or crime scope still earns, with no toast), a client writes nothing, and an `Unknown` registry answer earns nothing. **Prisoner income pays nobody during a session** (the event has no actor; one log line) until C2. Remote upkeep desertion is off during a session until C4 and C6. The balance a client sees is still its own until C4 mirrors the server's. Detail: [special-resources.md](special-resources.md#per-player-earning-under-co-op-967-c1). |
| `AlignmentDesertionBehavior` | `OnDailyTickParty` and `OnDailyTickSettlement`, through `TryResolveOwner` and `DesertionOwnerPolicy.ShouldSkip` (`AlignmentDesertionBehavior.cs:65,86,96-101`) | **A who-is-a-player gate, not an authority gate.** During a session a remote player's clan (Player, but not `Clan.PlayerClan`) and any Unknown owner lose no troops; with no session this peer's own clan follows "Apply To Player", and in a session the authority's own clan, which the registry does not list, follows "Apply To AI Lords" like an AI clan. Before #966 a remote player's party read as an AI clan and shed troops. Skipped, not relayed: a remote player gets no desertion popup (that relay is #967). Single-player is unchanged. See [alignment-desertion.md](alignment-desertion.md). |
| `PartyUpgraderUpgradeReadyTroopsHook` (troop weight shed) | `OnUpgradeReadyTroops`, after the `LeaderHero` exit (`PartyUpgraderUpgradeReadyTroopsHook.cs:52`) | **A who-is-a-player gate.** No shed for any mobile party whose `OfParty` is not `NotPlayer` (Player or Unknown). The `TaomSettings` toggle is still per peer; a server-authoritative setting is open. See [troop-weight-system.md](troop-weight-system.md). |
| `EnlistmentDialogBehavior` | The enlist offer, through `ShowsOfferFor(verdict, deferToHost)` (`EnlistmentDialogBehavior.cs:109,115-116`) | **Hidden for a guest** (`ShouldDeferToHost`); a player host and a solo player keep it. On a dedicated server the host is the placeholder hero, so enlistment cannot be offered per player yet. Cost: under a co-op mod TAOM cannot probe (or a failed Coop role bind) `ShouldDeferToHost` is true on every peer, solo play included, so the offer hides there too. See [enlistment.md](enlistment.md). |
| Field camp overlay (`FieldCampOverlayVM`) | The camp button and `ExecuteOpenCampMenu`, through `ICampMenuActivationQuery.DefersToCoopHost` (`FieldCampOverlayVM.cs:176,224`; `MapScreenCampMenuActivationQuery.cs:38`) | **Hidden for a guest** (`ShouldDeferToHost`): the camp service writes local, save-backed state. A player host and a solo player keep the camp; the same unprobeable-mod cost as the enlist offer applies. See [field-camp.md](field-camp.md). |
| `HardQuestionsCampaignBehavior` | `OnHeroPrisonerReleased` and `OnHeroKilled`, through `HeroDied` and `ReleaseHero` (`!ShouldDeferToHost`) | **Stands down** on every peer while TAOM must defer to a co-op host, including under a co-op mod whose role TAOM cannot read. A released or dead lord's strip record is only cleared (`StripService.OnReleased`, `OnDied`) when the gate passes, because the record rides TAOM `SyncData` that nothing replicates. See [hard-questions.md](hard-questions.md). |
| `HardQuestionsDialogBehavior` | `CanAsk`, `CanStrip`, `PriceLine`, `PriceIsPayable`, `PayBribe`, `Force`, `Strip` and `RepliesAs`, through `InterrogationService.Eligible` (`!ShouldDeferToHost`) | **Gated in the service, not in the behavior.** The behavior's handlers hold no co-op check of their own; every condition and consequence calls `InterrogationService`, whose `Eligible` returns nothing while TAOM must defer, so the options are hidden and a bribe, force or strip cannot start. `OnConversationEnded` stays ungated: it only runs the deed a gated option queued, and the queue is empty otherwise. See [hard-questions.md](hard-questions.md). |
| `FireAndTributeBehavior` | `OnSessionLaunched`, `OnDailyTick`, `OnHourlyTick`, `OnVillageStateChanged` and `OnMobilePartyDestroyed`, through `FireAndTributeService.IsSinglePlayer` | **Single-player only** ([Fire and Tribute](fire-and-tribute.md)): stands down on every peer while a session is live, while a co-op mod TAOM cannot probe is loaded, or on a dedicated server (`IsSessionActive \|\| ShouldDeferToHost \|\| IsDedicatedServer`). No host spawns. A host restored into such a session, or alive when the gate closes, has its AI pin released, its record dropped and its side's cooldown started at the session launch, the next daily tick or the next hourly tick, whichever comes first; it is never despawned there, because removing a party is a world write the other peers would not see. `OnVillageStateChanged` and `OnMobilePartyDestroyed` are not gated as handlers: they look up at most three host records, which a closed gate has already emptied, then count a loot or report a break. The save record is TAOM `SyncData` that nothing replicates, and a peer that stands down never writes a host into it. The tribute envoy (slice 2) stands down behind the same `IsSinglePlayer` gate: nothing is scheduled at a host's creation, no popup opens from the hourly step, the popup's click handler does nothing once a session is live, and the hosts' records (envoys included) are emptied with the rest. The five MCM switches are counted in the settings fingerprint (`CoopSettingsRelevance` does not exclude them). Trade-off: a solo player with a co-op mod TAOM cannot probe, or `coop-force-active.flag` present, gets no hosts and no envoys. |

**What makes any peer earn at all is the participation fix in the same change.** The victory gate
used to ask whether the player IS the winning side's `LeaderParty.LeaderHero`, conflating
participating with commanding. That was never multiplayer-only: in ordinary single-player, joining
any lord's army stops you being the leader party's hero, so every victory you fought paid zero.
Multiplayer made it total rather than different — under a client/server split no player leads the
authoritative side either, and one reported session produced a single `MapEventEnded` out of 33
fought missions, with `state=None`. The gate is now
`SpecialResourceEarnPolicy.IsPlayerVictory(mapEvent.PlayerSide, mapEvent.WinningSide)`, with
`BattleSideEnum.None` on either side failing it, because a client routinely observes an unresolved
battle the server has already decided.

**Siege defence uses a split gate, not a wholesale one (fixed 2026-08-01).** The hourly tick did two
unrelated jobs and the first pass gated both, so a co-op client could defend a siege to completion
and receive no influence, no relation and no message. `ISiegeDefenseService.OnHourlyTick` is now two
methods:

| Half | Method | Gate | Why |
|---|---|---|---|
| Shared | `OnHourlyTickShared()` | **Authority only** | Expires events and prunes `_activeEvents`, which backs the `_taom_siege_active_events` save key. A client pruning a timeline the host owns is the divergence this layer exists to stop. |
| Local | `OnHourlyTickLocalPlayer()` | **Every peer** | Grants the reward to the hero *this* peer is playing, keyed on `MobileParty.MainParty` / `Hero.MainHero`. Same reasoning that leaves `CareerQuestCampaignBehavior` ungated. |

`GrantReward` sets `RewardClaimed` on the local `_activeEvents` entry. That is a write to a
save-backed field, but each peer keeps its own TAOM `SyncData` after join (see "What is NOT done"),
so the flag reads naturally as *this peer's player has claimed it* — which is the intended meaning.
The consequence to know: on a client the shared sweep never runs, so expired entries linger until
`OnSiegeEnded` removes them. Harmless — the local half filters on `!Deadline.IsPast`, so a stale
entry can never pay out. The authority half is pinned by two tests in `CoopAuthorityGateTests`
(`SiegeDefense_OnHourlyTick_CoopClient_DoesNotTick`, `SiegeDefense_OnHourlyTick_Authority_Ticks`); no test calls
`OnHourlyTickLocalPlayer`, so the local half's deadline filter is unpinned.

### Deliberately NOT gated

`CareerQuestCampaignBehavior` (daily) — per-player, so gating it would strip career quests from
clients.

The original justification said "keyed entirely on `Hero.MainHero`", and **that was false when
written**: the one-quest-at-a-time dedup scanned `QuestManager.Quests` globally, and a client loads
the host's save — so the host's active career quest blocked the client from ever being offered one.
Fixed 2026-08-01 by filtering on `CareerQuest.OwnerHeroStringId`; the claim is now true of the scan
as well as the offer. Codex found it after the claim had already been written down twice and
believed once. **Still owed: in-game confirmation that quest start does not trip the `StringId`
suppression** (see the P1 note above).

`HiddenHostsMemoryBehavior` rows (the remembered troop counts, [hidden-hosts.md](hidden-hosts.md)): per-player presentation in this peer's TAOM `SyncData`, the same reading as the siege defence local half, so stamping (done by `HiddenHostsPlainSightBehavior`), fading, the visibility and destroy handlers and the save are ungated. A co-op join is a save load, so a client decodes the host's memory rows at join and holds the host's remembered counts until its own play replaces them. Only the XP grant above is gated.

`MinorClanMigrationBehavior` (the load migration of the minor clans' culture and leader templates, [minor-factions.md](minor-factions.md)) is deliberately ungated: it listens to `OnGameLoadedEvent` only and writes two engine-saved clan fields (`Clan.Culture` and `Clan.MinorFactionCharacterTemplates`) from the shipped data, with no RNG and no TAOM `SyncData`. A joining client does not migrate anything itself: BannerlordCoop sends it the host's current campaign as an in-memory snapshot (`SaveCurrentGame`), which the host's own load already migrated, so the client's run writes nothing, and a new campaign changes nothing. BannerlordCoop auto-syncs `Clan.Culture` (not the template list); what that sync does with the host's write during the load is UNVERIFIED in a live session.

`GaitVarietyMissionBehavior` (orc and goblin step size and gait set, [animation-variety.md](animation-variety.md)) is deliberately ungated on every co-op signal: it
only changes the walk phase and clip choice of agents this peer built, and the managed `Agent.SetActionSet` broadcasts only under `GameNetwork.IsServerOrRecorder`, which
BannerlordCoop does not use. Its three settings are classified as presentation above.

`LordFaceVarietyBehavior` (one face pass per new campaign, [lord-face-variety.md](lord-face-variety.md)) is deliberately ungated: it listens to `OnNewGameCreatedPartialFollowUpEndEvent` only and has no load path, so it runs wherever a campaign is built and never when a save loads. It writes `Hero.StaticBodyProperties` (and weight and build for rule A), which the save already stores. A co-op client with no hero builds a local campaign for character creation, so the pass runs there too, but its rolls are discarded when the client loads the host's save. A dedicated server that builds its own world runs it. A headless server without a face generator may leave faces unchanged (UNVERIFIED). Not confirmed in a live co-op session.

## Mission behaviors (#967 C7)

Every TAOM mission behavior has a co-op role in `CoopMissionRoles` (`Main/Features/CoopInterop/Missions/`), and
`CoopMissionRolePolicyTests` fails when a behavior that SubModule.cs or a feature module adds is missing from it.
This table is the reasoning; the table in code is the contract.

**The model.** No mission runs on the co-op authority process. Each client runs its own battle, spawns only the
troops it owns, and holds every other agent as a puppet (`Controller = None`) that co-op moves from packets. TAOM
asks no "am I the battle host" question: the host is elected after `Mission.AfterStart`, which is where TAOM adds
its behaviors, so an add-time host check has nothing to read, and no behavior needs one (the host API was removed
in the C7 follow-up). Hence two layers:

1. **At add time** (`CoopMissionRolePolicy.ShouldAdd(role, sessionActive)`, read once per mission): an `Unsafe`
   behavior is not added in a session. Nothing else is filtered here.
2. **Live inside the behavior** (`CoopMissionOwnership.IsOwnedHere(agent)`, the only member of
   `ICoopMissionAuthority` is `IsLocallyControlled(Agent?)`): ownership is read at the point of use and never
   cached across frames, because a host handoff moves authority mid-battle (`BattleAuthorityMigrator`). A behavior
   whose agents are owned by an invariant instead (an AI-controller gate, the player team) carries no check, and its
   table row names the invariant.

**How ownership is read** (`CoopMissionAuthority`, bound by reflection, compiled delegates, no `MethodInfo.Invoke`
per call). No session: owned, with no reflection and no engine read. A mission without a `CoopMissionController`
(a plain tournament, a custom battle): owned. Otherwise `INetworkAgentRegistry.TryGetAgentInfo` separates the
three states: registered here (owned), registered to another peer (a puppet), unregistered. An unregistered agent
follows co-op's own fallback in `BattleBlowInterceptPatch`: a human is owned when its engine controller is not
`None`; a mount follows its rider; a masterless mount is owned. The registry is resolved once per mission. **When a
read fails:** any failure to read the registry (a bind failure, a registry resolve that throws, a registry probe that
throws, or a missing probe or registry) falls back to the engine controller (`HasLiveController`); only an unexpected
throw caught in `IsLocallyControlled` answers owned. A bind failure warns once per process (the binding is a `Lazy`);
the other failures warn once per mission, and the latch is re-armed only after a mission's behavior list was read.

**Cost.** `IsOwnedHere` can run every frame for an agent: a creature tree is due every tick, because
`BehaviorTreeAgentComponent.TickOnMissionThread` compares `_rootEvaluationDelay / 1000` in integer math (10 / 1000
is 0) and `BehaviorTree.ShouldRunNextTick` is set by `HandleNotification` and never reset. A skipped (non-owner) tick
zeroes the cadence timer, which throttles only a tree whose delay is 1000 ms or more. Keep the check allocation-free.

**Never decide at build time.** Inside `OnAgentBuild` the registry has not heard of the agent yet, so it answers
"not local" for every agent: co-op registers puppets only after `SpawnAgent` returns. The engine controller is no
better there. A human's controller is already right at build, but `Agent.Build` fixes a non-humanoid's controller to
AI whatever the spawn asked for, so every mount reads "AI". Every check below runs on a tick or an event, after the
registry has the agent.

**Mounts.** "Controller `None` means puppet" holds for humans. An owned mount can read `None` for up to 2 seconds
during co-op's synthetic mount turn (`AgentMovementHandler.cs:1530-1533` sets the controller to `None`; the turn lasts
`SyntheticMountTurnDurationSeconds = 2f`, `:81`), so
no check may treat a mount's `None` as proof it is another peer's. The ownership read follows the rider for that
reason.

| Behavior | Role | What happens in a session |
|---|---|---|
| `ShaderPrecompilePlayerAgentGuard` | Unsafe | Not added. It exists only for the shader-precompile walk's own battle. |
| `SiegeDismountMissionBehavior` | Unsafe | Not added. Unsafe because `ISiegeDismountService.OnMissionStart` clears the main hero's `BattleEquipment` horse slot and deposits the horse into the party inventory, a write to shared campaign state from a peer. |
| `EnlistmentMeritMissionBehavior` | Unsafe | Not added. It scores the enlisted player's battle merit into this save's enlistment state, so a session scores no battle merit. |
| `FieldCommissionMissionLogic` | Unsafe | Not added. It already required `IsAuthority`, which is false on every peer that runs a mission, so a session runs no field commission logic. |
| `FellFoesMissionLogic` | Unsafe | Not added. Mike kept the stand-down on 2026-10-19 (`FellFoesWiringTests`); co-op has no "stopped fleeing" message, so a rally would not replicate. |
| `LostFootingMissionLogic` | Unsafe | Not added. It reuses Fell Foes' gate (`LostFootingModule`). |
| `CultureDoctrineMissionLogic` | Local | Added everywhere, no check. Tactics move only AI-controlled agents, and on a guest the host's AI teams are puppets (`Controller` `None`), so a tactic has no agent of the host's to move there. The per-unit riding and firing orders it sets (`Formation.RidingOrder`, `FiringOrder`) only set values. Not checked in game: see the owed list. |
| `BehaviorTreeMissionLogic` | AgentOwner | The tree seam: `BehaviorTreeAgentComponent.TickOnMissionThread` skips `RunTree` when the agent is not owned, and restarts its cadence, which throttles only a tree whose delay is 1000 ms or more (see Cost above). Every row below marked "tree seam" is covered by it. |
| `AdvancedCombatBehavior` | AgentOwner | `BoneCheck` drops a hit whose attacker is not owned (a bite or slam in flight across an ownership change) and ends the check. |
| `WargMissionBehavior` | AgentOwner | Tree seam; `OnWargDied` acts only when the rider is owned. The other tree listeners (`WargTryToGoRage`, `OnSpiderDied`) only write a blackboard or log, so `NotifyAll` stays ungated. |
| `SpiderMissionBehavior`, `WarRamMissionBehavior`, `ElkMissionBehavior`, `AnimaliaMissionBehavior`, `TrollBruteForceMissionBehavior` | AgentOwner | Tree seam. |
| `ElephantMissionBehavior` | AgentOwner | Tree seam; the howdah crew spawns only when the mahout is owned (`HowdahCrewSpawner.SpawnQueued`), so the owner's peer adds the crew once and co-op replicates it. |
| `MumakilMissionBehavior` | AgentOwner | Tree seam; the platform crew spawns only when the rider is owned (`MumakilCrewSpawner.SpawnQueued`). |
| `CreatureBanditMissionBehavior` | AgentOwner | Tree seam. The routed-removal backstop has no ownership check, by an invariant: a puppet's origin has no troop supplier, so `CoopAgentOrigin.SetRouted` latches nothing and does nothing (`CoopAgentOrigin.cs:139-147`; the puppet origin is built at `PuppetSpawner.cs:439`). Co-op never replicates creature bandits either, because `SpawnMonster` bypasses `SpawnAgent`. Pinned: `CreatureBanditBackstop_StaysUngated`. |
| `MixedFormationsMissionBehavior` | AgentOwner | Player team only, so owned by an invariant: co-op keeps other owners' puppets off `PlayerTeam` (`PuppetSpawner.cs:1031-1054`). No check. |
| `SmartCavalryAIMissionBehavior` | AgentOwner | Player team only, the same invariant (`PuppetSpawner.cs:1031-1054`), so `ApplyCollisionAvoidance` has no check. Pinned for both rows by `PlayerTeamBehaviors_StayUngated`: they read only `Mission.Current?.PlayerTeam` and contain no `CoopMission` call. |
| `BannerBearerAssignmentMissionLogic` | AgentOwner | No check, by an invariant: vanilla promotes a bearer only through `CanAgentBecomeBannerBearer`, which needs `IsAIControlled`, and a puppet is not AI-controlled. Pinned: `TaomBattleBannerBearersModel.CanAgentBecomeBannerBearer` calls the base first in both branches (the TAOM race and formation gates only narrow it). |
| `DreadAuraMissionLogic` | AgentOwner | No check, by an invariant: `DreadAgentGate.CanAffect` needs `IsAIControlled` and a `CommonAIComponent`, which a puppet lacks (co-op builds a human puppet with `Controller` `None`, and `AgentCommonAILogic` removes the component when the controller leaves AI). It also covers `RaceAbilityTicker.PulseAuras`. Pinned: `DreadAgentGate_CanAffect_RequiresAnAIController`. |
| `SignatureStrikesMissionLogic` | AgentOwner | `OnMeleeHit` and `SignatureStrikeRunner.Run` need an owned attacker. The fear drain goes through `DreadAgentGate`, the same AI-controller invariant, so it frightens no puppet. The ring's blow on a puppet reaches the victim's owner through co-op's own victim-side routing. |
| `CareerPerkMissionBehavior` | AgentOwner | `MissionAbilityExecutionContext.ApplyAoeBuff` buffs only owned allies (a live check). The morale burst has none, by the same AI-controller invariant: it reads the ally's `CommonAIComponent`, which a human puppet lacks (puppet mounts are AI at build and keep the component until co-op sets them `None`, so the burst on a horse is inert). |
| `CreatureSiegeRoleMissionBehavior` | AgentOwner | Already safe: it excludes ladder faces only for `agent.IsAIControlled`, and a puppet is `Controller None`. Pinned. Caveat: an owned mount reads `None` for up to 2 s during co-op's synthetic mount turn (see Mounts above), and a non-AI agent releases its role (`RoleReason.NotAIControlled`), so a mount may drop its siege role for that window (not checked in game). |
| `RaceAbilitiesMissionLogic` | AgentOwner | Tree seam. The fear-on-kill loop and the aura pulse frighten through `DreadAgentGate` (the AI-controller invariant). The killer's heal-on-kill runs only when the killer has ability state in the store, and only this peer's own activations create that state, so a puppet killer heals nothing. |
| `AutonomousMovementPlayerController`, `SiegePropDiagnosticsMissionBehavior`, `MissionPerfHeartbeatBehavior`, `MissionTickProfilerBehavior`, `BattleActionBarMissionView`, `MissionDiagnosticBehavior`, `CrashReportDevTriggerMissionBehavior`, `AnimMemoryProbeMissionBehavior`, `SkeletonBufferWatchMissionBehavior`, `CreatureBanditDiagnosticsBehavior` | Local | This peer's own view, log or diagnostics. Unchanged. |
| `MountDespawnMissionBehavior`, `BattleCorpseMissionBehavior`, `AnimVarietyMissionBehavior`, `GaitVarietyMissionBehavior` | Local | Presentation on this peer's own agents (a dead mount fades on each peer). Unchanged. |
| `EnlistmentBattleRoleMissionBehavior`, `EnlistmentBattleFormationMissionBehavior` | Local | They shape this peer's own player slot. Unchanged. |
| `HeroKillCounterMissionLogic` | Local | The consumer is already gated (`LordsLadderBehavior` defers to the host). |
| `BattleLoadPhaseBehavior`, `CustomBattleTeamFixBehavior`, `HostReviewMissionLogic` | Local | Added outside `AddTaomBehavior`; loading, custom battle and host review missions have no co-op controller. |

**Tests and pins.**

| Test class | What it holds |
|---|---|
| `CoopMissionRolePolicyTests` | The truth table, table coverage, `Table_UnsafeSet_IsExactlyTheSixDroppedInASession`, `Table_CultureDoctrine_IsLocalAndNotGated`. |
| `CoopMissionAuthorityTests` | The bind, the three registry states, the mount-to-rider rule, the per-mission resolve, live answers, `Decide_CleanBind_LogsNoWarning`, and the failure paths: `Decide_TypeFinderThrows_FallsBackToControllerAndWarnsOnce`, `Decide_RegistryMemberRenamed_FallsBackToControllerAndWarnsOnce`, registry resolve throws, rider probe throws, a null agent in a session, a registry that throws in two missions (two warnings), and `LinkFor_BehaviorEnumerationAlwaysThrows_DoesNotReArmTheWarning`. |
| `CoopMissionOwnershipTests` | The static face, including a resolver that returns null. |
| `CoopMissionBindingTests` | Loads the installed Coop DLLs (skipped when absent) and asserts the members `CoopMissionAuthority` binds still exist with the shapes it expects, plus the two `CoopSessionProvider` members. |
| `CoopSessionProviderCompiledProbeTests` | The compiled probe answers equal the reflection answers on a fake or no-Coop host. |
| `CoopMissionSeamPinTests` | Each remaining check sits before the action it guards (comments stripped); the `IsLocallyControlled` source has no `() => mission`. |
| Invariant pins | `DreadAgentGate_CanAffect_RequiresAnAIController`, `CreatureBanditBackstop_StaysUngated`, `PlayerTeamBehaviors_StayUngated`, `BannerBearers_StayUngated_BecauseThePromotionModelCallsBaseFirst` (`TaomBattleBannerBearersModel.CanAgentBecomeBannerBearer` calls the base first in both branches), and the pin that `SubModule.cs` resolves `SpecialResourceEarningService` once per campaign. |

The tests for the checks removed in the C7 follow-up (`DreadPulse_DrainsOnlyOwnedTargets`,
`RaceAbilityFear_FrightensOnlyOwnedVictims`, `SmartCavalryAvoidance_SteersOnlyOwnedRiders`,
`BannerBearers_TryAssignBanner_SkipsAFormationThisPeerDoesNotOwn`, `CreatureBanditBackstop_NeverRoutsAPuppet`) are gone
with them.

**Known limits.** The `SubModule.cs` add-time filter is the one edit outside these files. Morale is not replicated by
co-op, so a puppet's morale is whatever its own peer computes. An effect an event starts (a signature strike, a race
ability's fear) reaches only the troops of the peer that ran it; the dread aura is the exception, because every peer
pulses it over its own AI troops.

After a battle-host migration, the new host adopts replicated elephant and mumakil crew as AI with no seat link. The
crew are spawned and seated only on the owning peer (`HowdahCrewSpawner`, `MumakilCrewSpawner`), co-op's spawn message
(`NetworkSpawnBattleAgents`) carries no seat identity, and adoption (`BattleAuthorityMigrator`) converts the crew to AI.
The fix needs a seat relay (#967 C3). Before C7 every peer spawned its own crew.

**Owed by #967 C7 (in-game checks):**

1. One `[CoopMission] skipped` line per Unsafe behavior: five in an ordinary battle, six only when the shader guard
   claims the mission.
2. Creature trees (host-owned creatures, idle non-owner copies, adopted creatures after a handoff) act only on owned
   creatures: a host-owned creature attacks once on a guest.
3. No banner churn, and banners present in a two-guest battle.
4. Dread, race and signature morale lands only on the peer's own troops.
5. Career buffs reach only the peer's own troops.
6. CultureDoctrine host tactics do not move the guests' puppets.
7. Siege dismount is absent in a session.
8. Dead mounts fade on both peers.
9. A guest's signature strike frightens no host AI.
10. Howdah and mumakil crews stand on the deck for puppets, once, on every peer.
11. After a battle-host migration, the adopted elephant and mumakil crew: expect them off their seats (see Known limits).

## Client-side object creation

[Source-verified, runtime-unconfirmed] Constructing any `MBObjectBase` on a client inside
`CampaignState` throws: `MBObjectBasePatches` prefixes the `StringId` **setter** and returns `false`
when the sync policy disallows the write, so `CreateObject<T>` leaves `StringId` null and
`MBObjectManager.cs:215` calls `Dictionary<string,T>.TryGetValue(null, …)` → `ArgumentNullException`.
Full chain in the internals doc. **Not TAOM-specific** — any mod creating an `MBObjectBase` on a
client hits it; TAOM merely had two paths in, both now gated.

## Client-reachable entry points the gate later blocks

These share one shape, and it is the **opposite** of the divergence the gates were built for. The
gate correctly stops a client mutating shared state — but the *entry point* is still
client-reachable, so a client can start something it can never finish, and pay for it. Nobody
desyncs; the client just quietly gets nothing.

| Area | What a client could do | What it never got | Status |
|---|---|---|---|
| Messengers | Pay `MessengerGoldCost` and enqueue a `PendingMessenger` | Hourly processing is authority-only, so it never advanced or arrived — gold simply lost | **Fixed 2026-08-01** — the send refuses on a non-authority peer and logs `[Messengers][coop] send refused on non-authority peer — delivery is host-side` |
| Elite emissary | Pay special resources for an elite troop | The troop landed in a client-side roster the next resync overwrote — pay real, get phantom | **Fixed 2026-08-03** — `ExecutePurchase` refuses before charging when `ShouldDeferToHost` |
| Siege defence | Be prompted, accept, and be tracked | Reward tick is authority-only | Partly addressed by `_locallyClaimed`; the prompt/accept path still needs an owner check |
| Career quests | Be offered and accept | — (fixed: the dedup scan now filters by owner) | **Fixed 2026-08-01** |
| Field camp | Press the camp button and establish a camp through the local camp service | A camp the host knows about: the camp book is this peer's own TAOM `SyncData` | **Switched off for guests 2026-10-21 (#966)**: the button hides and `ExecuteOpenCampMenu` refuses |
| Enlistment | Be offered and take enlistment | Per-player service: `EnlistmentPlayerActionService.cs:96` is host-only, and on a server the host is the placeholder hero | **Switched off for guests 2026-10-21 (#966)**: the offer is hidden |

**Both fixes decline rather than forward, and that is the ceiling here.** Granting either
authoritatively would need TAOM to send a message across the co-op layer, which it cannot do without
a compile-time dependency on one specific co-op mod. Declining is the honest behaviour, not a
placeholder for a forwarding path that is coming.

The emissary decline shows the player `{=taom_emissary_coop_guest}` *"The emissary only deals with
the host of this campaign."* **That string is not yet localized** — it appears only at
`EliteEmissaryInquiryPresenter.cs`, in no localization XML, so `/localize` is owed for it.

**Do not simply ungate the messenger hourly body** if the send is ever re-opened: arrival calls
`target.ChangeState(...)` and `EnterSettlementAction.ApplyForCharacterOnly(...)`, which are
shared-state mutations, and `PendingMessenger` still has no owner field (`TargetHeroId`,
`DispatchTimeDays`, `Position`, `Arrived`) for processing to filter on.

**Also open — `CareerQuest` construction on a client [P1].** `new CareerQuest` on player acceptance
is client-side `MBObjectBase` construction during a live campaign: Coop's `MBObjectBasePatches`
prefixes the `StringId` setter and returns false when the sync policy disallows the write, and
`QuestManager.OnQuestStarted` then adds the object anyway. It does not go through
`MBObjectManager.CreateObject<T>`, so the exact `TryGetValue(null, …)` chain is not proven for this
path — but "per-player, therefore safe" is not a sufficient defence, and this needs a live session
to characterise. The existing try/catch around acceptance is containment, not correctness.

## What is NOT done

- **No MCM settings parity: reported, not yet exchanged.** TAOM ships **453** settings across
  four MCM classes (the 284 here counted `[SettingPropertyGroup]` lines alongside the properties;
  the split is 423 in `TaomSettings`, 19 in `BattleLoadDiagnosticsSettings`, 10 in
  `CrashReportSettings` and 1 in `BlowDiagnosticsSettings`).
  **304 are simulation-relevant**, traced to the feature that consumes each one and kept when that feature
  ships a GameModel, CampaignBehavior, MissionBehavior or Harmony patch; all 304 are in
  `TaomSettings`. The 149 excluded (67 counted 2026-09-22, less the Native Skin Fixes toggle removed with the feature 2026-10-05, the 38 Realm Borders settings added from 2026-09-30 to 2026-10-08, the Crash Report AfterStart dev trigger added 2026-10-01, the Battle Corpses advice toggle and button added 2026-10-01, and the thirteen Menus & Loading Screens settings added 2026-10-01, #704, and the three tick profiler settings added 2026-10-02, and the hitch probe toggle added 2026-10-02, and the Animation Clip Memory probe toggle added 2026-10-02, and the map profiler toggle added 2026-10-03, and the Load-Time Stamps toggle added 2026-10-03, and the Race Abilities war cry, message and debug-log toggles added 2026-10-04, and the Race Abilities glow toggle added 2026-10-04, and the Crash Report mission-start survival toggle added 2026-10-08, and the Crash Report skeleton buffer guard and watch toggles added 2026-10-08, and the Map Performance nameplate cull and map view release settings added 2026-10-08, and the War Chronicle announcements toggle added 2026-10-08, and the three Animation Variety settings and the swing alternative probe toggle added 2026-10-17, and the Hidden Hosts toggle added 2026-10-18, and the Fell Foes debug log toggle added 2026-10-19, the two Telchar's Mark settings added 2026-10-19, #875, the two Reeve's Ledger switches added 2026-10-19, #857, and the Lost Footing census toggle added 2026-10-20, #894) are instrumentation, player-local convenience,
  presentation, two action buttons, and the three time-acceleration knobs whose UI co-op already
  suppresses; the list with its reasons is
  `Main/Features/CoopInterop/CoopSettingsRelevance.cs`.

  Those numbers are pinned per class in `SettingsFingerprintTests`, which is the guard that a new
  setting cannot arrive unclassified: "relevant or excluded" is true of everything by construction
  — classification is include-by-default — so only the count can fail, and adding a setting
  anywhere moves one and forces the decision. Two further tests keep the list itself honest: no
  excluded name may stop matching a real property, and no two settings classes may share a
  property name.

  `SettingsFingerprint` hashes those 304: one code per MCM group plus a global, culture-invariant
  so a comma decimal separator cannot fake a mismatch — and `SettingsFingerprintLog` writes them
  to each peer's log under the co-op gate. It reads all four classes, not just `TaomSettings`:
  every diagnostics property is excluded so this moves no hash today (a test asserts exactly
  that), and it means a simulation-affecting setting added to a diagnostics page later is covered
  the day it lands. That makes the "compare settings manually" workaround a comparison of a few
  short strings instead of 304 values read off two screens.

  **All four classes are `AttributeGlobalSettings<T>`, and the reasoning above depends on it.**
  MCM has three scopes, and only one of them travels with a save. Verified against MCM 5.12.1:
  `MCM.Internal.GameFeatures.PerSaveCampaignBehavior : CampaignBehaviorBase` carries
  `SyncData<Dictionary<string, string>>("_settings", ref _settings)`, so **PerSave** is in the
  savegame. **PerCampaign** is not, despite the name — `PerCampaignSettingsContainer` roots at a
  `PerCampaign` directory, then `GetOrCreateDirectory(RootFolder, GetCurrentCampaignId())`, and
  loads through `ISettingsFormat.Load(…)`: a per-machine file keyed by campaign id. It would NOT
  arrive in parity with the host's save — the campaign id matches while the local file may not
  exist at all, which is worse than global rather than better. TAOM declares neither:
  the only `AttributePerCampaignSettings` reference in the tree is a reflection target string in
  `McmSettingsCollector.cs`, which scans third-party mods. So nothing here is save-inherited and
  nothing arrives guaranteed-equal. **If a PerSave or PerCampaign settings class ever lands in
  TAOM, this paragraph stops being true and the fingerprint's meaning changes with it** — a
  PerSave class would be equal by construction and need not be hashed; a PerCampaign one would
  need hashing more urgently than a global, not less.

  **A matching fingerprint means the MCM settings agree, and nothing wider.** It covers the 304
  values on the four settings pages. It does not cover the `ModuleData` JSON/XML that several
  features fall back to when a setting is unset, the `TAOM.Dependencies` flag files
  (`patchshield-disabled.flag`, `saveshield-swallow-disabled.flag`, `coop-force-active.flag`),
  or any difference outside TAOM. None of those three flags gates a setting in the 304 (each
  governs a Dependencies-side shield with no MCM property), but the general point holds: read the
  log line as "our settings pages match", not as "our installs are equivalent".

  **Still not done:** peers do not exchange them. Putting the fingerprint in save metadata and
  comparing on join is the remaining step, and it cannot be verified without two machines in a
  session — `FingerprintReport.DivergentGroups` is already there for it. BannerlordCoop still
  syncs no setting, so divergent settings continue to defeat everything above; they are now
  visible rather than silent.
- **No ModuleData content hash.** Coop's handshake compares module ids and version strings only, and
  disconnects on mismatch — so identical TAOM versions on all peers are mandatory, and a TAOM version
  bump is a co-op compatibility break.
- **No sync of TAOM's own campaign state.** Clients get the join-time baseline from the host's save,
  then never hear about WotR momentum or culture-conversion deltas.
- **War of the Ring participation: attribution fixed, state still per peer (#966).**
  `WarEventSnapshotAdapter.FromSiege` applies the same `IsPlayerRelated` test as the battle
  snapshot beside it (fixed 2026-08-03). Since #966 the test also counts any party on either side
  whose `OfParty` is `Player`, and any kingdom holding a player's clan, so a remote player in a
  different kingdom is credited when the event is scored on the authority; `Unknown` counts as not
  involved. The old local kingdom check stays, so on a dedicated server the local kingdom clause still names the world-gen hero's kingdom (`OfClan` answers Player only for a registered clan in a session, and that clan is not in the registry). What is still missing: the momentum state is TAOM `SyncData` that nothing replicates, so
  a client never sees the meter move, and the `IsAuthority` gates on the scoring handlers are
  unchanged.
- **Dedicated server: no longer "unsupported", but not supported either.** The 2026-08-03 field
  group ran TAOM on a BannerlordCoop dedicated server by hand-copying the client binaries into
  `bin/Win64_Shipping_Server/`, and that session is the evidence source for the race-capture,
  SpecialResources and `action_sets.xml` findings on this page. The build now does that copy: both
  `Main/TAOM.csproj` and `Dependencies/TAOM.Dependencies.csproj` carry a
  `MirrorWin64ShippingClientToServer` target modelled on the existing `…ToEditor` mirror. It
  mirrors the **assembled** client folder because the vendored natives and NuGet companions only
  exist there after `PostBuildCopyToModules`. TAOM's mirror excludes `TAOM.Client.*` and deletes
  stale copies from its server bin (`Main/TAOM.csproj:220-225`). A verified
  deploying build mirrored 10 files for TAOM and 42 for TAOM.Dependencies. A server that boots also
  needs the `LOTRLOME_Armory` `action_sets.xml` fix: build 117131, which the dedicated-server engine
  ships, throws `KeyNotFoundException` in `MBObjectManager.MergeElements` at schema path
  `/action_sets/action` on data that 1.4.7.117484 tolerates. What is still true: both manifests tag
  `DedicatedServerType=none` (`Main/_Module/SubModule.xml`, six entries in
  `Dependencies/_Module/SubModule.xml`), the appliance's `engine/Modules` ships no TAOM, and its
  `default_new_game.sav` bootstrap is a vanilla world. An earlier revision of this bullet also said
  the appliance runs on `Microsoft.NETCore.App` against TAOM's .NET Framework 4.7.2; that is
  **unverified and now in tension with the field evidence** — treat it as a claim to re-check, not
  as the reason not to try. The listen-host path remains the one that carries TAOM into the spawned
  server process with no copying at all.

## PatchShield is skipped under co-op

`PatchShield.Install()` is suppressed whenever a co-op module is active
(`PatchShieldPolicy.ShouldInstall`). This is a **performance** fix, not a safety one.

Until plan 034 the shield finalizer took `__originalMethod`, so Harmony's generated wrapper paid a
`MethodBase.GetMethodFromHandle` **on every call** (measured at about 63 ns per call, 1,145 ns
contended, plan 034), the same mechanism that turned a millisecond tournament teardown into a measured
104 to 109 s freeze in #331. Co-op amplifies it: Coop's AutoSync transpiles every declared method and
constructor of 43 campaign types, and Coop's `PatchAll` runs on connect, *before* TAOM's
`OnGameInitializationFinished` pass, so pass 2 shielded that entire surface. Those methods are the
campaign hot path, so the symptom was frame rate rather than a single stall. A player profiled it and
traced it here. Plan 034 removed the parameter: the finalizer now takes only `__exception`. Its
benchmark, with a stand-in finalizer of that shape on a trivial patched method, measured 5.4 ns per
call in a Debug build (what players run) against 1.9 ns with the postfix alone, about 3.5 ns more (1.3
against 0.7 ns optimized); the shipped finalizers were not benchmarked themselves. The skip was decided
under the old cost and stands; whether to revisit it is a separate decision.

Extending `ExcludedTargetNamespacePrefixes` would have been the wrong lever: adding
`TaleWorlds.CampaignSystem` there suppresses shielding **in solo play too**, because that list is not
co-op-scoped.

**What this gives up:** the swallow half — surviving `MissingMethodException` /
`MissingFieldException` / `TypeLoadException` from engine drift. Under co-op that is the right trade
and matches SaveShield, which already rethrows save-load faults on the same reasoning: a visible
crash beats a silent divergence between two campaigns. The unpatch half was already withheld.

**Known cost:** a player with the co-op module merely *enabled* but playing solo also loses the
shield, for no benefit — Coop only calls `PatchAll` on connect. Unavoidable here, because install
runs before any session can exist, so there is nothing session-scoped to read. This is the one place
where **module presence is the correct signal** and `ICoopSessionProvider` is not; `CoopPresence`'s
class docs explain why a patch-application site needs a process-constant fact.

**This makes two steps in community dedicated-server recipes redundant.** Those recipes tell
operators to drop `patchshield-disabled.flag` and `saveshield-swallow-disabled.flag` into
`Modules/TAOM.Dependencies/`. Under co-op both are already the default:
`PatchShieldPolicy.ShouldInstall(coopActive, disabledByFlag) => !disabledByFlag && !coopActive`
skips install outright, and `SaveShieldPolicy.ShouldSwallow` returns false for the `SAVE-LOAD`
category only. `MISSION-INIT` keeps swallowing under co-op (see "Why the other rethrows were
reverted" below). The flags still work, and `saveshield-swallow-disabled.flag` still does strictly
more, because it also rethrows the `MISSION-INIT` chain. Do not set it in a co-op session: a rethrow
from a mission tick hangs the next frame.

**PatchShield has no removal path.** On v1.5.5 `Module.Initialize` fills the module list (`Module.cs:301`)
before `LoadSubModules` (`:307`), so pass 1 already sees co-op and skips the install. Commit `6ce20f6ed`
(shipped, then reverted in #965) unpatched shields when pass 2 saw co-op; that code and its tests were
deleted.

**Assembly type loading.** `CollectAssemblyTypesShim` stops swallowing a `ReflectionTypeLoadException`
only when the BannerlordCoop module (id `Coop`) is among `CoopPresence.ActiveCoopModuleIds` and the
assembly is one of Coop's (its project names plus `AutoSync`, which Coop compiles at runtime). The
force flag's `(forced-by-flag)` marker does not count, and neither does BannerlordTogether. The shim
rethrows through `RethrowStackPreserver.PreserveForRethrow`. `SaveDefinerCollisionGuard` catches the
exception itself and keeps `tle.Types`, so its preflight no longer relies on the shim swallowing.

**The tournament guard.** `Patch69_TournamentEndGuard` swallows a `NullReferenceException` in every
mode, co-op included, and returns anything else. The swallowed NRE costs only the local winner panel:
the prize, the leaderboard and `OnTournamentFinished` run before the event
(SandBox `TournamentBehavior.cs:252-264`).

**Why the other rethrows were reverted.** Commit `6ce20f6ed` (shipped, then reverted in #965) rethrew `MISSION-INIT` faults and the
Patch69 NRE under co-op. In v1.5.5 `Mission.OnTick` sets `tickCompleted = false` (`Mission.cs:3749`)
once per frame, before the `OnMissionTick` loop, and only the agent tick sets it true (`:3622`). The next frame's
`OnPreTick` waits in `WaitTickCompletion` (`:3594-3599`), so an exception rethrown out of
`OnMissionTick` skips the agent tick and hangs the game. `SpawnTroop`, `SetMissionMode` and the
tournament-end event can all run inside `OnMissionTick`. A rethrow from `MissionState.OnInitialize`
leaves an inactive state on top of the stack, and the loading-time rethrows leave the mission as
half-initialised as the swallow does. Only `SAVE-LOAD`, which runs outside any mission tick, is
rethrown.

## Verification status

Confirmed by a player (2026-08-02):

- TAOM + BannerlordCoop launches, connects and plays.
- The PatchShield frame-rate collapse is fixed by skipping install.

Still unconfirmed — worth walking if you have two machines:

1. `[CoopPresence] EnsureProbed: co-op module(s) ACTIVE: Coop` in `Modules/TAOM.Dependencies/diag.log`,
   and `[PatchShield] co-op module(s) active … install skipped` alongside it.
2. A `[HarmonyCensus]` block naming `Bannerlord.Coop`; check its `TRANSPILER CONFLICT` rows against
   TAOM's own transpilers (`grep -rl 'IEnumerable<CodeInstruction> Transpiler(' Main`; 18 methods in 16 files
   on 2026-10-14).
3. TAOM behaves identically **solo** with the Coop module merely enabled — what the `IsAuthority`
   fail-open direction protects, and now also where the PatchShield cost above lands.
4. The spawned `/server` process starts TAOM cleanly (no blocking UI).
5. No `ArgumentNullException` from `MBObjectManager` on a client load — the `CultureConversion`
   chain, still source-verified only.
6. Several in-game days, then Coop's `coop.debug.hero audit` / `coop.debug.settlements audit` to
   compare object sets between peers.

Owed by the 2026-08-03 work specifically, none of it run:

7. A dedicated-server boot against the mirrored `Win64_Shipping_Server` binaries **and** the fixed
   `action_sets.xml` — the two together are what a server needs to start with TAOM's simulation.
8. A two-client join where the joiner keeps their character-creation race, culture startup gold,
   career and special-resource seed. The line to look for is
   `[Possession] Controlled hero changed 'X' -> 'Y'`.
9. An emissary purchase attempted on a guest: it must decline, and no resources may be deducted.
10. A dedicated server with no session live logs the one-shot `[SpecRes] Dedicated server —` line and
    credits nobody. Once a session is live the registered players earn on the server, with the hero id
    on the `Co-op <source>: credited hero` lines ([special-resources.md](special-resources.md#per-player-earning-under-co-op-967-c1)).

Owed by the #966 work (Batch B, co-op player attribution), none of it run in game:

11. A guest on a dedicated server recruits a `recruit_cost` volunteer: the server log shows a
    `[SpecRes] RECRUIT:` line (it does not name the hero, so read the guest hero's balance), no
    `Recruit not charged` warning, and nothing charged to the placeholder hero. Expect the stale
    client balance and the zero floor described under Patch113 until #967 lands.
12. A recruit during a session with no Patch113 scope charges nobody: no `[SpecRes] RECRUIT:` line and no
    toast (the rule on the [#967 design comment](https://github.com/haterade22/TAOM/issues/967#issuecomment-6328464305)). A recruit by the hosting player is expected to arrive scoped like a guest's and charge that player's hero, with no toast on the server.
13. A guest sees no camp button and no enlist offer; a player host still sees both.
14. A remote player's troops do not desert for alignment and are not shed for weight across a
    day tick, while an AI lord of the same kingdom still loses opposed troops.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/bannerlord-together-compat.md](./bannerlord-together-compat.md)
- [docs/features/dread-aura.md](./dread-aura.md)
- [docs/features/fief-granting.md](./fief-granting.md)
- [docs/features/hero-race.md](./hero-race.md)
- [docs/features/player-possession.md](./player-possession.md)
- [docs/features/player-switcher.md](./player-switcher.md)
- [docs/features/smart-cavalry-ai.md](./smart-cavalry-ai.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/migration/dr3-maintenance.md](../migration/dr3-maintenance.md)
- [docs/modding/module-dependencies.md](../modding/module-dependencies.md)
- [docs/modding/modules-overview.md](../modding/modules-overview.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/research/bannerlordcoop-internals.md](../research/bannerlordcoop-internals.md)

<!-- backlinks-end -->
