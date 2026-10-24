# The Red Arrow

## Overview

In the base game the Army Management screen stops working the moment the player's army begins a siege: no party can be called in, none can be released, and every row shows "You cannot perform this action while you are in a siege." The Red Arrow lifts that block for a siege the player leads, so a commander at Minas Tirith or Helm's Deep can send the war-arrow out and bring up more hosts exactly when they are needed. One MCM switch ("War of the Ring/Armies", "Call armies during your siege", default on). Singleplayer only.

## Why This Exists

- **Vanilla behavior:** `ArmyManagementItemVM` refuses a call twice during any siege: the model's first check (`PlayerSiege.PlayerSiegeEvent != null`) and the map-screen check (`CampaignUIHelper.GetMapScreenActionIsEnabledWithReason`), and its constructor sets `IsTransferDisabled = IsMainHero || PlayerSiege.PlayerSiegeEvent != null`, which also switches off removing a member.
- **TAOM requirement:** the War of the Ring is built on long sieges. Reinforcing the besiegers is the natural move, and the base game forbids it for exactly the period it is useful.
- **Without this feature:** a commander must lift the siege, call the army up, and start again.

## What the player sees

While leading an army that is besieging, open Army Management as usual. Every party row can be added to the army or released from it; the player's own row stays fixed. A called party is charged influence as always, then marches to the siege camp and joins it. Refusals show the base game's own texts: a party that is fighting, too small, disbanding, far away, at sea or already in an army is still refused, and so is a party that is already at the siege outside the army. The Disband Army button keeps its siege block. Nothing new is shown, so there is no new text to localize: every refusal reuses a base game string under its own key.

## Architecture

### Design Challenge

- The refusal lives in three vanilla places: the model method `CheckPartyEligibility` (branch 1), the static `CampaignUIHelper.GetMapScreenActionIsEnabledWithReason` (two siege blocks), and the item view model constructor (`IsTransferDisabled`). The model's only caller is `ArmyManagementItemVM.UpdateEligibility`, so no model override is needed: the model slot (already filled by `TaomArmyManagementModel`, CulturalFeats) stays exactly as it is, and `SubModule.cs` gains nothing.
- Two Harmony postfixes on `ArmyManagementItemVM` (no transpiler) do the whole job. The `UpdateEligibility` postfix redoes the party check and the map check over vanilla's refusal and writes the private `_eligibilityReason`, so it needs a field reference.
- A defender also has a siege event, so the test for "the besieger side" is `MobileParty.MainParty.BesiegerCamp != null`, not the siege event.

### Solution Approach

`RedArrowService` is pure over primitive snapshots (ADR-007) and answers three questions, each with null for "use vanilla's answer". `Applies` is the gate for all three:

| Condition | Required |
|---|---|
| MCM switch | on |
| Co-op | no session (`IsSessionActive`), not deferring to a host (`ShouldDeferToHost`), not a dedicated server; each read live on every call |
| Siege side | the player's party is in a besieger camp |
| Army | the player's party is its army's leader (no army is created mid-siege) |

**`CheckParty`** reproduces branches 2 to 12 of `DefaultArmyManagementCalculationModel.CheckPartyEligibility` (v1.5.5 :405-479) in vanilla's order, so the first failing check names the reason exactly as vanilla would:

| # | Check | Reason | Vanilla key |
|---|---|---|---|
| 2 | no party | `NoParty` | `f6vTzVar` |
| 3 | `party.LeaderHero == Hero.MainHero.MapFaction?.Leader` | `RulersParty` | `ipLqVv1f` |
| 4 | in an army other than the player's | `InAnotherArmy` | `aROohsat` |
| 5 | in the player's army | `InThisArmy` | `Vq8yavES` |
| 6 | map event, siege event, or standing in a besieged settlement | `Fighting` | `pkbUiKFJ` |
| 7 | size score not above `PlayerMobilePartySizeRatioToCallToArmy` (0.4) | `TooSmall` | `SVJlOYCB` |
| 8 | disbanding, or waiting for the disband behavior | `Disbanding` | `tFGM0yav` |
| 9 | player's party at sea: no naval capability / has it | `NoShips` / `AtSea` | `nqq84Dzq` / `gFixGQsr` |
| 10 | candidate in a raft | `LostAtSea` | `TbXDmh3t` |
| 11 | distance above `MaximumDistanceToCallToArmy` | `TooFar` | `UINgZDN5` |

Branch 1, the player's siege, is the one the feature lifts. The snapshot (`ArmyCallCandidate`) carries the sea, raft and distance facts as plain fields; the adapter reads the naval capability only while the player is at sea and measures the distance only when neither the sea nor the raft check applies (otherwise `false` and NaN), as vanilla does. It measures that distance for every such row, including one an earlier check refuses, where vanilla would stop first. Two comparisons are written as positive requirements (`size > ratio`, `distance <= limit`) so that a NaN refuses the party where vanilla's `size <= ratio` and `distance > limit` are both false and let it in. A party already at the siege outside the army has a siege event, so check 6 refuses it: allied parties at the siege keep the base game block.

**`CheckMapAction`** is `GetMapScreenActionIsEnabledWithReason` (:2440-2497) in vanilla's order (prisoner, raft, ferry, mission, encounter without a settlement, raid, siege, map event). Both siege blocks are lifted for the player's own besieged settlement and nothing else: a foreign besieged settlement still blocks (`Siege`), and a raid, a map event (an assault in progress), a prisoner, raft or ferry state, a mission and a bare encounter all still block.

**`TransferDisabled`** returns `IsMainHero`, so the player's own row stays fixed and every other row can add and remove.

**Wiring.**

- No model seam: `TaomArmyManagementModel`, `SubModule.cs` and `Main/IoC.cs` are untouched, and `RedArrowModule` registers the services. `RedArrowWiringTests` pins that the model declares only its two trunk overrides and that neither single-owner file names the feature.
- `ArmyManagementItemVM_Constructor_RedArrow_Patch`: a postfix on the constructor that sets `IsTransferDisabled` from the service.
- `ArmyManagementItemVM_UpdateEligibility_RedArrow_Patch`: a postfix on `UpdateEligibility`. It returns at once when vanilla's own skip branch decided the row (`CanJoinBackWithoutCost`, or `IsInCart && !IsAlreadyWithPlayer`). Otherwise, when the feature applies, `RedArrowArmyBoundary.TryRecheck` reads the candidate through the adapter and asks the service for the party check (vanilla's text on a refusal) and, if that passed, for the map-action check, then the postfix writes `_eligibilityReason` (a field reference bound in `Prepare`) and `IsEligible`.
- Fail safe: `Prepare` returns false, with one logged error, when `_eligibilityReason` is missing, which skips both patches (the constructor patch defers to that `Prepare`). That coupling covers only the missing field: if one target alone moved, the other postfix would still apply, and `HarmonyPatchBindingTests` is the guard for a moved target. A fault inside a postfix or the boundary is caught and logged once, and vanilla's value (the block) stays.

```
MCM switch ─> RedArrowSettingsProvider
co-op state ─> ICoopSessionProvider, IDedicatedServerProvider
                         │
 IPlayerSiegeStateAdapter ──> RedArrowService <── IArmyCallCandidateAdapter
 (siege side, map action)     (Applies, CheckParty,   (one party, in vanilla's order)
                               CheckMapAction,
                               TransferDisabled)
                                    ^
                        RedArrowArmyBoundary ── RedArrowTexts (vanilla keys)
                                    ^
                    two postfixes on ArmyManagementItemVM
                       (constructor, UpdateEligibility)
```

### Engine path (why the screen change is enough)

Confirming the screen sets `item.Party.Army = MobileParty.MainParty.Army`. The called party escorts the besieging leader, `Army.Tick` merges it, and attaching copies the leader's besieger camp, so it joins the siege camp by the path a late member of an AI army takes. Releasing a member leaves the camp the same way (`BesiegerCamp.OnPartyLeftSiege`), and a released member of ten or more men becomes disorganized (`CanGetDisorganized`, `BesiegerCamp.cs:273-276`). The screen's other controls are vanilla's. One consequence: after every member is released, Done is greyed out with the hint "You need more than 1 party to create an army" (`ArmyManagementVM.UpdateCanConfirm`, `ArmyManagementVM.cs:1122-1137`), because only the player's row is left and the disband path it falls to is blocked during a siege (`GetCanDisbandArmyWithReason`, `:1240`). Keep at least one member.

### Callers

`ArmyManagementItemVM.UpdateEligibility` runs from the constructor, from the row's add and remove commands (`OnAddToCart` twice, `OnRemove` once) and from `ArmyManagementVM.ExecuteReset` (v1.5.5 `ArmyManagementItemVM.cs:427,461,467,472`, `ArmyManagementVM.cs:1354`). `CheckPartyEligibility` has that one caller. The constructor is built by `ArmyManagementVM` for every lord party of the player's faction plus the player's own row (`ArmyManagementVM.cs:994,997`). All on the main thread.

### Co-op

Singleplayer only. `Applies` reads the co-op state live on every call, so a live session, a client deferring to a host, a co-op mod TAOM cannot probe and a dedicated server all keep vanilla's block. See [coop-interop.md](coop-interop.md).

### Rejected

A transpiler on the model (not needed in v1.5.5: the model method is a plain virtual). Creating a new army mid-siege (kept vanilla). Calling allied parties already at the siege into the army (kept vanilla).

## Configuration

No config file. MCM: "War of the Ring/Armies" (`GroupOrder = 76`), "Call armies during your siege" (`EnableRedArrow`, default on, `RequireRestart = false`). The switch is read each time the screen builds a row and on each click, so a change applies the next time the screen opens. The group is shared with the next army feature.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/RedArrow/RedArrowModule.cs` | feature module: registrations, patch category `Patch115_RedArrow` at GameInit |
| `Main/Features/RedArrow/RedArrowService.cs` | the decisions (pure; a concrete class, nothing fakes it) |
| `Main/Features/RedArrow/ArmyCallCandidate.cs`, `MapActionState.cs`, `PlayerSiegeState.cs` | the snapshots |
| `Main/Features/RedArrow/ArmyCallReason.cs`, `MapActionReason.cs`, `CandidateArmy.cs` | reasons in vanilla's order |
| `Main/Features/RedArrow/RedArrowSettingsProvider.cs` | the MCM switch |
| `Main/Features/RedArrow/RedArrowArmyBoundary.cs` | engine boundary for the patches |
| `Main/Features/RedArrow/RedArrowTexts.cs` | vanilla's refusal texts under vanilla's keys |
| `Main/Features/RedArrow/Hooks/` | the two postfixes and their shared statics |
| `Main/Adapters/PlayerSiegeStateAdapter.cs`, `ArmyCallCandidateAdapter.cs` (+ interfaces) | engine to snapshot |

## Dependencies

- `ICoopSessionProvider`, `IDedicatedServerProvider` (CoopInterop) for the singleplayer gate.
- `IModLogger` for the first-fault log line.
- `TaomSettings` (MCM) for the switch.

## Tests

- `TAOM.Tests/Features/RedArrow/RedArrowServiceTests.cs`: the decision matrix. Inactive cases (switch off, each co-op flag, dedicated server, defending, no army, besieging without leading), each check alone, every pair in vanilla's order, size and distance boundaries, NaN handling, the map-action blocks alone and in pairs, a foreign besieged settlement blocked and the player's own allowed, and the main hero row staying fixed.
- `TAOM.Tests/Features/RedArrow/RedArrowArmyBoundaryTests.cs` (`RequiresGame`): `TryRecheck` refuses with the candidate's own vanilla text, takes the map verdict for an eligible party (clear, and a map event), stands down when the feature does not apply, a fault logged once, vanilla's keyed texts.
- `TAOM.Tests/Features/RedArrow/RedArrowPostfixTests.cs` (`RequiresGame`): both postfixes on a bare `ArmyManagementItemVM`: what each writes, and that each skip state (vanilla's skip branch, the feature off, no boundary) writes nothing.
- `TAOM.Tests/Features/RedArrow/RedArrowBindingTests.cs` (`BindingVerification`, `RequiresGameIL`): the IL fingerprint of vanilla's `CheckPartyEligibility` and `GetMapScreenActionIsEnabledWithReason`, including every `{=key}` text and the order of the checks.
- `TAOM.Tests/Features/RedArrow/RedArrowSettingsProviderTests.cs`: default on, the MCM attributes, the unique group order.
- `TAOM.Tests/Features/RedArrow/RedArrowWiringTests.cs`: module listed once, category and phase, services resolve, the army-management model untouched, both patch targets and the `_eligibilityReason` binding resolve against the installed engine, `Prepare` true, postfix parameter names, single-owner files.
- Binding gate: `HarmonyPatchBindingTests` (both targets), `ReflectionSiteBindingTests` (`_eligibilityReason`), and the regenerated `taleworlds-api-snapshot` files.

## How to Retune

Nothing to tune: the rules are vanilla's. To restore the base game block, turn the switch off.

## Engine bump

On every engine bump re-diff, against the previous version: `DefaultArmyManagementCalculationModel.CheckPartyEligibility` (the eleven reproduced branches and their order), `CampaignUIHelper.GetMapScreenActionIsEnabledWithReason`, the `ArmyManagementItemVM` constructor line `IsTransferDisabled = ...`, and the skip branch of `UpdateEligibility` (`CanJoinBackWithoutCost`, `IsInCart && !IsAlreadyWithPlayer`) that the postfix mirrors. A new branch in any of them is a branch the feature does not know. `RedArrowBindingTests` pins the IL of the first two and of `UpdateEligibility` itself (an engine change fails it with the new sequence in the message); the constructor line is left to this re-diff, and `RedArrowServiceTests` pins the order of the reasons and the boundaries.

## Changelog

- 2026-10-23: first version (#873).

## In-game checks owed

- As army leader, start a siege, open Army Management, call a party: influence is charged, it marches in, merges, shows in the camp and fights in the assault.
- Release a member: it leaves the camp, and a member of ten or more men shows as disorganized.
- Release a member from the army overlay AND from the Kingdom screen route, then confirm the siege menu refreshes and the time controls stop (`OnPartyLeftSiege` refreshes the menu and stops time; the Kingdom screen route is new ground).
- Release every member but the player: Done is greyed out with "You need more than 1 party to create an army".
- Save and load with a called party still marching.
- Defend a siege, then switch the setting off: both restore the base game block.
- No exceptions in the logs (`[RedArrow]` lines).

## GitHub Issue

- **Issue:** #873, [The Red Arrow: call parties into your army during your own siege](https://github.com/haterade22/TAOM/issues/873)
- **Status:** Built on `feat/war-armies`; open until the in-game checks are done.
