# Help Unlooked-For

## Overview

AI lords scatter at the sight of an army besieging a settlement, even when they are strong enough to pass by or to gather near it. Help Unlooked-For lets a lord that is strong enough stop fleeing a siege, so lords can move past or gather toward a besieged Minas Tirith or Helm's Deep. One switch in MCM ("World/Help Unlooked-For", "Lords stop fleeing sieges", default on) and one tuning number in a small JSON file.

## Why This Exists

- **Vanilla behavior:** every think tick a lord scores the parties near it. When the party scoring comes out locally weaker than the party it meets, `ShouldConsiderAvoiding` is asked and the lord runs. A besieger counts as its whole camp (`DefaultMobilePartyAIModel.cs:216-224`), so a lord weighs itself against the entire siege, not against the one party in front of it.
- **TAOM requirement:** the War of the Ring is built on sieges. A lord that is a fair match for the camp should be able to ride past it or assemble near it instead of scattering at the first sight of the banners.
- **Without this feature:** every lord weaker than the camp (and the guard is only "locally weaker") bolts, so a siege empties the roads around it.

## Architecture

### Design Challenge

- The only caller of `ShouldConsiderAvoiding` is `CalculateInitiativeScoresForEnemy` (v1.5.5 `DefaultMobilePartyAIModel.cs:518`). It is a plain virtual on the AI model, so no Harmony patch is needed.
- The call runs inside `Campaign.PartiesThink` on a native async worker, overlapping the main thread, and also on the main thread (`MobilePartyAi.CheckPartyNeedsUpdate` runs `Tick(0)`). The override may read only party and camp fields and strengths, as vanilla does at the same call. No IoC resolve, no MCM instance walk, no logging, no random number.
- Besiegers never take initiative while a siege lasts (`DefaultMobilePartyAIModel.cs:94` requires `BesiegedSettlement == null`), and a party shut inside a besieged settlement skips every target. So the siege's own parties do not chase a passer through this path. The hourly `AiEngagePartyBehavior` can still send a lone besieger after a passing lord, and a weak lord standing adjacent when the siege lifts is at risk, which is why a strength guard remains.
- The player's own party never reaches `ShouldConsiderAvoiding`: `ShouldPartyCheckInitiativeBehavior` refuses `MobileParty.MainParty` (`DefaultMobilePartyAIModel.cs:94`), and that check gates the only path (`MobilePartyAi.cs:483-485`, then `DefaultMobilePartyAIModel.cs:382` and `:518`).

### Solution Approach

`TaomMobilePartyAIModel : DefaultMobilePartyAIModel` overrides `ShouldConsiderAvoiding` for this feature: vanilla's answer first, then `UnlookedForAidService.Decide(facts)` (`base.ShouldConsiderAvoiding(...) && _service.Decide(...)`, so the reader only runs when vanilla already says avoid). `SiegeAvoidanceFactsReader` (the ADR-007 boundary) turns the two parties into a readonly struct of primitives. The feature module registers the model through `GameModelDecl.Of<MobilePartyAIModel, TaomMobilePartyAIModel>(ModelTarget.Campaign, ...)`, one line in `Main/Composition/FeatureModules.cs`; `Main/IoC.cs` and `Main/SubModule.cs` are untouched.

**The rules** (all in `UnlookedForAidService`, pure over the facts):

| Case | Verdict |
|---|---|
| Vanilla says no avoid | no avoid (the service is not asked) |
| Evaluator not a lord party, or the siege involved is led by the player's party (as the target's camp, or as the camp besieging the settlement the target stands in) | vanilla's answer |
| Switch off | vanilla's answer |
| Target is a besieger, own or camp strength is NaN (not read, or a torn read) or infinite | vanilla's answer |
| Target is a besieger, camp strength is 0 or less | no avoid |
| Target is a besieger, own strength / camp strength at least `MinStrengthRatio` | no avoid |
| Target is a besieger, ratio below `MinStrengthRatio` | vanilla's answer (avoid) |
| Target stands inside a besieged settlement | no avoid, no guard (such a party cannot take initiative) |
| Anything else | vanilla's answer |

- **Own strength** follows vanilla's expression (`DefaultMobilePartyAIModel.cs:208`): the army's `EstimatedStrength` when the lord leads that army or is attached to it, else its own party's. The reader reads `party.Army` once.
- **Camp strength** is the sum of `EstimatedStrength` over `BesiegerCamp.GetInvolvedPartiesForEventType()`, the sum vanilla's second pass uses (`DefaultMobilePartyAIModel.cs:325-327`). Vanilla's own flee input at `:220-222` is the terrain-adjusted `GetCustomStrength`, so the two differ a little; the guard is a ratio test, not a replica. Both strengths are read only when the evaluator is a lord party and the target has a besieger camp; otherwise they are NaN. If the camp list or the army changes under the read on the main thread, the read may throw (the reader catches `InvalidOperationException` and `NullReferenceException` and returns NaN, so vanilla's answer stands) or may return a short or repeated sum; that is harmless, because the next check recomputes.
- **Player-led sieges:** when the camp leader is `MobileParty.MainParty`, the fact `PlayerLedSiege` is set and the service returns vanilla's answer, so AI lords keep vanilla behaviour at the player's own siege. It covers both the besieger case and a target inside the settlement the player besieges.
- **Who:** AI lord parties only, at sieges the player does not lead. Caravans, villagers, bandits, patrols and garrisons keep vanilla behaviour. The player's own party never reaches this call.

### Threading

The service reads no MCM per call. `UnlookedForAidSettingsProvider` takes the `TaomSettings` reference once, in its constructor, on the main thread (the model is built in `OnGameStart`, where the factory resolves the service and with it the provider), and `Enabled` is then one bool read through the cached object. MCM edits its one registered settings object in place, so a change applies at once. If MCM was not up when the provider was built, the lazy accessor takes the instance on the first read instead, and until then the switch reads on. The ratio is loaded and validated once, in the service constructor. `Decide` allocates nothing and logs nothing.

### Limit

Vanilla already exempts some relief from this flight. An army whose leader has a defend-settlement order skips the besiegers of that settlement for its leader and its unattached members (`DefaultMobilePartyAIModel.cs:187-193`). Attached army members take no initiative at all (`:96-98`). A party on a defend-settlement order also ignores the initiative result against the settlement's last attacker (`MobilePartyAi.cs:488`). That last attacker is normally the party that opened the siege (`BesiegerCamp.cs:215-217`), so a lone lord on a defend order already does not flee the camp that party leads; it still flees a camp led by any other party. The gain is therefore mainly for lone lords passing by or gathering near a siege, and for relief that vanilla does not exempt. If relief armies still hang back, the cause is vanilla's defend-settlement logic, a separate issue.

### Component Diagram

```
unlooked_for_aid.json ─> UnlookedForAidConfigProvider (validated once)
MCM "World/Help Unlooked-For" ─> UnlookedForAidSettingsProvider (captured once, main thread)
                                        │
MobilePartyAIModel slot ─> TaomMobilePartyAIModel ──> UnlookedForAidService.Decide
   (AI + main thread)        base.ShouldConsiderAvoiding        ^
                             SiegeAvoidanceFactsReader ─────────┘ (facts: primitives)
                             IRaidHostOwnSideQuery (Fire and Tribute, checked first)
```

### Co-op

No gate. The decision is a calculation inside the engine's party AI tick, which the co-op mod already limits to the host. It reads the two parties, the besieger camp's parties and the lord's army for their strength, and writes nothing. The player-led exclusion keys on `MobileParty.MainParty`, so on a co-op host only the host's own siege is excluded: the server has no main party for a client, and an AI lord can still be lifted at a client's siege. A model is not a campaign behavior, so `CoopGateDocCoverageTests` asks for no row; the note is in [coop-interop.md](coop-interop.md) under "Deliberately NOT gated". Co-op patches other methods of the default AI model, not this one, and they still run, because the TAOM model extends the default.

### Rejected

A bare postfix with no strength check (a weak lord would walk into a siege that lifts beside it), and a naval copy (TAOM declares the Naval DLC incompatible).

## Configuration

### Config File: `Main/_Module/ModuleData/unlooked_for_aid/unlooked_for_aid.json`

| Field | Type | Description |
|-------|------|-------------|
| `MinStrengthRatio` | number | How strong a lord must be, against the whole siege camp, to stop fleeing it. Finite, at least 0 and below 1. |

### Current Values

`MinStrengthRatio` is 0.5: a lord at least half as strong as the whole camp (its army counted if it leads one or is attached to it) walks past. A bad value reverts to 0.5 with a warning and one summary warning; a missing file reverts with a warning, a malformed one with an error. The file loads once per process: an edit needs a full game restart.

### MCM

"World/Help Unlooked-For" (`GroupOrder = 68`), "Lords stop fleeing sieges" (`EnableUnlookedForAid`, default on, `RequireRestart = false`).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/UnlookedForAid/UnlookedForAidModule.cs` | feature module, the model declaration, the registrations |
| `Main/Features/UnlookedForAid/Models/TaomMobilePartyAIModel.cs` | the `ShouldConsiderAvoiding` override; it also carries Fire and Tribute's own-side rule on `ShouldConsiderAttacking` and `ShouldConsiderAvoiding` ([fire-and-tribute.md](fire-and-tribute.md)) |
| `Main/Features/UnlookedForAid/Models/SiegeAvoidanceFactsReader.cs` | boundary reader, engine types to primitives |
| `Main/Features/UnlookedForAid/UnlookedForAidService.cs` | the decision (a concrete class, no interface: nothing fakes it) |
| `Main/Features/UnlookedForAid/UnlookedForAidConfigProvider.cs` | validating JSON loader |
| `Main/Features/UnlookedForAid/UnlookedForAidSettingsProvider.cs` | the MCM switch, captured once |
| `Main/_Module/ModuleData/unlooked_for_aid/unlooked_for_aid.json` | tuning |

## Dependencies

- `IPathService`, `IModLogger` (kernel) for the config load.
- `TaomSettings` (MCM) for the switch.
- `IRaidHostOwnSideQuery`, registered by Fire and Tribute's module: this feature's model also carries that feature's own-side rule ([fire-and-tribute.md](fire-and-tribute.md), "Own side").

## Tests

- `TAOM.Tests/Features/UnlookedForAid/UnlookedForAidServiceTests.cs`: the decision matrix (switch off, non-lord, player-led siege in the besieger and the inside-settlement case, ratio above, at and below, zero and negative camp, NaN and infinite strengths, besieged-settlement target, unrelated target, ratio read once).
- `TAOM.Tests/Features/UnlookedForAid/UnlookedForAidConfigProviderTests.cs`: one test per validation rule and the shipped JSON.
- `TAOM.Tests/Features/UnlookedForAid/UnlookedForAidWiringTests.cs`: module listed once, the model on the `MobilePartyAIModel` slot, IL checks that the override calls base and the service, reader and service read no `TaomSettings.Instance`, IoC or logger, the model declares only `ShouldConsiderAttacking` and `ShouldConsiderAvoiding`, both run the own-side check, its helpers and the alignment read path read no settings, IoC or logger, a source scan that no other TAOM class fills the `MobilePartyAIModel` slot, the model factory builds the model, a reader test for a target outside any siege (unread strengths are NaN), and that `IoC.cs` and `SubModule.cs` name nothing of the feature. A reader test with an army, an attachment or a besieger camp needs a running campaign (their setters call game code, and the player-led check reads `Campaign.Current`), so those paths are covered by the in-game check.
- `TAOM.Tests/Features/UnlookedForAid/UnlookedForAidSettingsProviderTests.cs` and the rows in `CampaignHotPathSettingsProvidersTests.cs`.

## How to Retune

1. Edit `MinStrengthRatio` in `unlooked_for_aid.json` (0 up to below 1; 0 means any lord stops fleeing any siege).
2. Restart the game fully; the file is cached for the process.

## Changelog

- 2026-10-23: first version (#872). Deep-review fixes: sieges the player leads keep vanilla, reader and model efficiency and NaN handling, corrected engine citations.

## In-game checks owed

- A lord at least half as strong as a siege camp passes by it instead of fleeing; a weak lord still flees.
- Watch whether lords are caught when a siege lifts beside them.
- AI lords keep vanilla behaviour at your own siege: besiege a settlement yourself and watch that nearby lords flee as in the base game, even when strong.
- Switch off in MCM mid-campaign: lords flee again at once.

## GitHub Issue

- **Issue:** #872, [Help Unlooked-For: lords strong enough stop fleeing from sieges](https://github.com/haterade22/TAOM/issues/872)
- **Status:** Shipped on `bannerlord-1.5.x` in `d07ea836b` (v2.0.35). Issue open: the in-game checks are owed.
