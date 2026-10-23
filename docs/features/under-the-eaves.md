# Under the Eaves

## Overview

While your own party travels through forest on the campaign map, it sees 30% of its base sight less far, and half of that when the party's scout has the Keen Sight perk. Inside a settlement the penalty does not apply. One MCM toggle (default on), no Harmony patch, no new model, nothing saved. Only the player's main party is affected. Tracked in [#849](https://github.com/haterade22/TAOM/issues/849), the forest half of a larger spec.

## Why This Exists

- **Vanilla behavior:** forest already works against the party that stands in it, but never hides it. `DefaultMapVisibilityModel.GetPartySpottingRatioForMainPartySeeingRange(target)` multiplies the ratio of a target standing on forest by 0.7, so OTHER parties in forest are harder for the player to see (less with the Keen Sight perk). `DefaultPartySpeedCalculatingModel` takes 20% off the party's speed in forest. Nothing in vanilla hides the player's party from anyone because it is in a wood. And `GetPartySpottingRange` gives a holder of the Battanian forest speed feat +0.15 sight in forest, which is the only forest effect a spotter gets.
- **TAOM requirement:** a Middle-earth wood should feel like a place you cannot see far out of. Vanilla's forest costs speed and hides other parties from you; this adds the shorter sight of the party under the trees.
- **Without this feature:** the party in the wood sees as far as on open ground.

The reference spec also cut sight 30% everywhere, trimmed it below Scouting 200, halved hideout spotting, doubled townsfolk and villager spawns and changed the map camera. The binding decision keeps only the forest penalty on the player's own sight (the feature hides no one). The flat cut, the Scouting trim and the rest are not built (see "What it does not do").

## Architecture

### Design Challenge

The engine has one `MapVisibilityModel` slot and the career model owns it, so a feature that changes sight cannot register a second model. The seam for that already exists: `IPartySpottingContributor`, whose factors the composer sums and whose penalties it floors (see [far-watch.md](far-watch.md) "The seam"). The questions were where to read the terrain from and how often, since the engine's visibility pass reads the main party's sight every frame.

### Solution Approach

A feature module (`UnderTheEavesModule`, one line in `FeatureModules.cs`, order-free because contributors sum). No `IoC.cs` or `SubModule.cs` edit: `SubModule` already builds `TaomMapVisibilityModel` with every registered `IPartySpottingContributor`.

- `UnderTheEavesRule`: pure and static. `Factor(inForest, inSettlement, keenSight)` is 0 unless the party is in forest and not inside a settlement, then `-0.3`, or `-0.15` with Keen Sight. Its eight-row truth table is the decision, and every row is reachable.
- `ForestSightContributor`: the boundary of the seam. After the toggle, null-party and main-party returns it computes three facts, cheapest first, and hands them to the rule: `inSettlement` (`CurrentSettlement`), `inForest` (only outside a settlement, the adapter read) and `keenSight` (only in forest, `HasPerk`, the call vanilla's target ratio makes). The rule's truth table pins the decision; the contributor test pins the source of those four lines (whitespace-normalised), so a flipped polarity or a dropped fact fails it. The cap-bonus member is a constant 0: a penalty never widens the engine's per-frame search.
- `IMapTerrainAdapter.IsInForest(MobileParty)`: a new member on the adapter Realm Borders registered. It is false for a null party, with no campaign map or on an invalid face, and otherwise whether the face group index of `party.CurrentNavigationFace` is `TerrainType.Forest`.
- `IUnderTheEavesSettings` and `UnderTheEavesSettingsProvider`: the toggle, through one cached `TaomSettings` reference (the Hidden Hosts provider shape).

### Component Diagram

```
MCM toggle (TaomSettings.ForestSightPenaltyEnabled)
        |
UnderTheEavesSettingsProvider
        |
ForestSightContributor --(party)--> IMapTerrainAdapter.IsInForest --> party.CurrentNavigationFace
        |  facts: inSettlement (CurrentSettlement), inForest (IsInForest, outside a settlement), keenSight (HasPerk, in forest)
        |  pinned as source by ForestSightContributorTests
UnderTheEavesRule.Factor(inForest, inSettlement, keenSight)   <- the truth table pins the decision
        |
SpottingContributorComposer (one sum, penalties floored at -0.5) --> TaomMapVisibilityModel.GetPartySpottingRange
```

### The terrain read and its cost

`MobileParty.CurrentNavigationFace` is `Position.Face`, and `CampaignVec2.Face` returns the face cached in the stored position when that cache is valid and calls the native scene lookup only when it is not. So a read is a struct copy while the party's stored position has a warm face cache; while it is cold (after a load, until the party moves) each read repeats vanilla's native face lookup. `DefaultMapVisibilityModel.GetPartySpottingRange` reads that same member (for the party's effective scout, as the Battanian feat's terrain test), and so does the target ratio, so the adapter reads the one face vanilla already reads and adds no kind of lookup vanilla does not make. `TerrainTypeAt(x, y)` would have built a new `CampaignVec2` and paid the native lookup on every read, which is why it is not used. The terrain type is the face group index: `MapScene.GetFaceTerrainType` returns exactly that after an `IsValid` check, and raises a `Debug.FailedAssert` on an invalid face, which the adapter avoids by checking first. The getter dereferences `Campaign.Current` when the cache is cold, so the adapter returns false for no campaign map before it touches the face.

Engine facts, each pinned by name in `UnderTheEavesEngineFactsTests` (read from v1.5.5 when written): `GetPartySpottingRange` reads `CurrentNavigationFace` and then asks the map scene for its terrain type; `CurrentNavigationFace` is the position's own face; `MapScene.GetFaceTerrainType` is the face group index behind an `IsValid` check; the target ratio compares a target's terrain with Forest and loads `-0.3`; Keen Sight is the scout's perk with a primary bonus of `-0.5`, which is where the rule's half comes from; and the Battanian feat's `+0.15` is in `GetPartySpottingRange`.

## Configuration

Compiled constants and one MCM bool.

| Item | Value | Why |
|---|---|---|
| `UnderTheEavesRule.ForestPenalty` | `-0.3` of base sight | Keeps the gap the reference had between forest (0.5) and open ground (0.7), about 29%, without its flat cut. It equals vanilla's own forest constant for targets, and it leaves 0.2 of the composer's `-0.5` penalty budget to a Far Watch settlement ride. A compiled constant: no float reaches config, so none needs validating. It is pinned finite and strictly between `MaxPenalty` and 0. |
| `UnderTheEavesRule.KeenSightShare` | `0.5` | Vanilla's Keen Sight applies `penalty + penalty x PrimaryBonus` with a bonus of `-0.5`; the rule keeps that half. |
| MCM `Map UI/Hidden Hosts/Shorter Sight Under Trees` (`ForestSightPenaltyEnabled`) | on | `Order = 1` beside Hidden Hosts' toggle; a new setting id, so no persisted default can contradict it. Live, no restart. With no MCM instance the provider answers on. |

No slider: one toggle per feature is the batch's precedent, and a strength nobody has tuned is a number to defend, not a player knob. If a slider is ever wanted it is a positive strength in `[0, 0.5]`, negated inside the rule, with NaN and infinity reverting to the compiled default.

**Localization.** The setting's label and hint are plain English, as the other settings in `TaomSettings` are (all but two of its hints are plain; those two use `{=key}` ids), so no localization key was added.

### Worked examples

Day, no perks (base sight is 12 plus 0.06 per Scouting point; at night 6 plus the same): Scouting 0, 12 becomes 8.4. Scouting 100 (base 18), 18 becomes 12.6. Night at Scouting 0, 6 becomes 4.2. A raised lookout (`+0.2 + Scouting/200` for the leader) joins the same sum: at Scouting 100, +0.7 and -0.3 net +0.4, so 18 becomes 25.2. Keen Sight is a tier-9 perk that needs Scouting 225, and the base bonus comes from the same scout (0.06 per point), so a Keen Sight scout's day base is at least 12 + 13.5 = 25.5. In forest that becomes 21.7 (25.5 x 0.85). With a Far Watch settlement ride too, Keen Sight (-0.15) and the ride (-0.35) sum exactly to the `-0.5` floor: 12.75.

## Interactions

- **Vanilla's target-side rule is untouched.** A party in forest already has its ratio multiplied by 0.7 for the player, so a host in the wood seen from the wood has about 0.49 of the open-ground reach (0.7 for the spotter's cut, 0.7 for the target). The seam is keyed on the spotter and cannot express a target rule; a stronger one would belong in `TaomMapVisibilityModel.GetPartySpottingRatioForMainPartySeeingRange`, which TAOM already overrides.
- **Settlements.** A party inside a settlement is not travelling the wood, and 111 settlement gates stand on forest faces, so the penalty is skipped while `CurrentSettlement` is set.
- **Keen Sight.** The scout's perk halves the penalty (see Configuration).
- **Dunland and Khand.** TAOM gives both the Battanian forest speed feat, so vanilla's `+0.15` forest sight stays and nets against the penalty: `-0.15` for them (with Keen Sight too the penalty is `-0.15` and the feat `+0.15`, which is 0).
- **Hidden Hosts.** Digit thresholds and the horizon are fractions of the main party's `SeeingRange` ([hidden-hosts.md](hidden-hosts.md)), so in forest every threshold pulls in by the same share: counts blur closer. That is intended and changes no Hidden Hosts code. Its intel adapter caches the range for half a second, so the number lags at a forest edge by at most that.
- **Far Watch.** Factors add. A surroundings ride (+0.5) in forest nets +0.2 (18 becomes 21.6). A settlement ride (-0.35) plus forest (-0.3) is -0.65, and the composer floors the penalties together at -0.5 (18 becomes 9), both pinned in `SpottingContributorComposerTests`. The scout's cap bonus is untouched because forest asks for none. Far Watch's sweep radius is `main.SeeingRange`, so a camp in forest sweeps a smaller area; accepted, the report covers what the sight covers.
- **Lookout.** Its `+0.2 + Scouting/200` joins the same sum (see the worked examples).
- **Field Camp ambush.** The ambush camp's reach and its chance read the main party's sight, so both change in forest. From the review: by day at Scouting 0 the reach goes from 10 to 8.4 and the chance from 0.50 to 0.548; at night the reach goes from 6 to 4.2 and the chance from 0.62 to 0.674. See [field-camp.md](field-camp.md).
- **Eye of Sauron.** The hunter's rings shrink with the sight but never fall inside the open-ground sight (the ratios are 1.05 and 1.12, both above 1), so a hunt in forest does not start inside what the player could see on open ground.
- **Spawns and chases.** Creature bands (`CreatureBandParties.OutOfPlayerSight`) and Fire and Tribute raid hosts (`RaidHostAdapter`) spawn out of the player's sight, so in forest they can spawn closer. Chases break sooner, because the player's engage order needs the target visible. Hideouts are spotted from closer (1.2 times the range), bandit and deserter spawn points can fall closer to you, parley reach shrinks, and the map bar view distance shows the shorter number. It fits the theme.
- **Strong scouts.** Vanilla ends the number with `LimitMax(60)`, and the composer writes the cap again after the factor. Where perks and buffs push the sum past the cap, the cap absorbs part or all of the forest cut: 30 x (1 + 0.65 + 1.0 - 0.3) = 70.5 stays 60.
- **AI parties are untouched.** The contributor tests `IsMainParty`. In the v1.5.5 decompile every reader of `MobileParty.SeeingRange` reads `MobileParty.MainParty` except two that read another party's own range: the party tooltip in a menu and the deserter party of the Extortion by Deserters issue. Both keep vanilla's number. The player's clan parties get no rule, since no reader asks about them.
- **Co-op.** The rule writes nothing; each peer computes its own party's sight, as with the lookout. BannerlordCoop reads the local `MobileParty.MainParty.SeeingRange` through the model (checked against its `bannerlord-1.5.x` branch); no co-op patch on `GetPartySpottingRange` was found. On a dedicated server `TaomSettings.Instance` is the instance loaded from `taom-server-settings.json`, so an operator can turn the rule off there. The server's main party is removed when the campaign is ready, so the rule reads that removed party's last face, which affects no player. The setting sits in the co-op fingerprint like any gameplay knob (`CoopSettingsRelevance` includes every setting it does not name), because bandit and deserter spawns read the range.
- **Save.** No state and no keys; toggling off takes effect on the next read.

### No line in the view-distance tooltip

The composer adds the summed factor to the number with no description (the lookout and the scout have none either), so the map bar's view-distance tooltip shows the shorter number without a line explaining it. Accepted for this change and said in the MCM hint; a label member on the seam would touch all three contributors and the floor wording. A line could come later with that member.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/UnderTheEaves/UnderTheEavesRule.cs` | The penalty, Keen Sight's share and the three-fact rule |
| `Main/Features/UnderTheEaves/ForestSightContributor.cs` | The `IPartySpottingContributor`: three returns, three facts, the rule |
| `Main/Features/UnderTheEaves/IUnderTheEavesSettings.cs`, `UnderTheEavesSettingsProvider.cs` | The toggle |
| `Main/Features/UnderTheEaves/UnderTheEavesModule.cs` | Registration, nothing else |
| `Main/Adapters/IMapTerrainAdapter.cs`, `MapTerrainAdapter.cs` | `IsInForest(MobileParty)` |
| `Main/Features/TaomSettings.cs` | `ForestSightPenaltyEnabled` |
| `Main/Composition/FeatureModules.cs` | The module line |
| `Main/Core/Domain/SpottingContributorComposer.cs` | The sum and the `-0.5` floor (Far Watch's, unchanged) |

## Dependencies

- `IPartySpottingContributor` and `SpottingContributorComposer` (Core/Domain), the seam of [far-watch.md](far-watch.md).
- `IMapTerrainAdapter` (Adapters), registered by the Realm Borders module. The module runner registers every module before anything resolves, parked ones included, so the dependency holds; `UnderTheEavesWiringTests` resolves it from a real container.

## Tests

| File | What it pins |
|---|---|
| `TAOM.Tests/Features/UnderTheEaves/UnderTheEavesRuleTests.cs` | The full eight-row truth table, the penalty finite and strictly between the composer's floor and 0, Keen Sight's share |
| `.../ForestSightContributorTests.cs` | Toggle off and a null party answer 0 and ask no terrain; the cap member is 0; the toggle, null-party and main-party returns and the three facts pinned as exact whitespace-normalised source, so a polarity flip or a dropped fact fails; the decision itself is the rule's truth table |
| `.../UnderTheEavesSettingsProviderTests.cs` | No MCM falls back to the compiled default, the attribute group, order and no-restart, a distinct order in the group, the hint's promises |
| `TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs` | The provider joins the read-through and lazy-accessor gate (it is read per sight query) |
| `TAOM.Tests/Adapters/MapTerrainAdapterTests.cs` | False for a null party and for no campaign; the source reads the party's own face, with no fresh lookup, behind the null and campaign checks |
| `.../UnderTheEavesWiringTests.cs` | Listed once, no `IoC.cs` or `SubModule.cs` mention, declares only registration, resolves as a contributor beside the lookout, `IMapTerrainAdapter` resolves, every file under 150 lines |
| `TAOM.Tests/Core/Domain/SpottingContributorComposerTests.cs` | Forest and Keen Sight with the lookout, Far Watch's two rides and the cap: the numbers in "Interactions" |
| `TAOM.Tests/Migration/UnderTheEavesEngineFactsTests.cs` | The six engine facts above (`BindingVerification`, all also `RequiresGameIL`) |

The main-party branches (settlement, forest, perk) need a campaign (`IsMainParty` reads `Campaign.Current`), so no unit test drives them: the rule's truth table and the gate order in the source cover them, and the in-game checklist below runs them.

## How to change the strength

Edit `UnderTheEavesRule.ForestPenalty` (it must stay negative and above `SpottingContributorComposer.MaxPenalty`; `UnderTheEavesRuleTests` and the composer rows pin it), then update the worked examples here.

## Performance

`MobileParty.SeeingRange` recomputes through the model on every read, and the engine's per-frame visibility pass reads it for the main party, so the contributor is on a per-frame path. The cheap toggle read comes first; a null or non-main party, and one inside a settlement, never reach the terrain; the terrain read is the party's face, a struct copy while the stored position has a warm face cache and vanilla's own native face lookup while it is cold (after a load, until the party moves), the read vanilla makes in the same method; the perk is read only in forest; nothing is allocated, no IoC, no LINQ. A shorter range shrinks the pass's locator-grid search radius, which should be equal or better, not worse; measuring it is on the checklist.

## What it does not do

- The flat 30% cut everywhere, the trim below Scouting 200, halved hideout spotting, doubled townsfolk and villager spawns and the map camera change of the reference.
- A woodland bonus for elves or woodmen (holders of the Mirkwood, Lothlorien and Rivendell forest-speed feats): not in this change. A follow-up keyed on those feats is possible if play shows the need.
- A change to how well others spot a party in forest (vanilla's 0.7 stays).
- A tooltip line, a strength slider, a save key or a Harmony patch.

## Which woods are painted Forest

The rule fires on faces of terrain type Forest in the navmesh. Decoding `navmesh.bin` for the review found 634 forest faces, and the woods painted as Forest on TAOM_Map are Mirkwood, Lorien, Dol Guldur, Rivendell's vale and a wood between Orthanc and Lorien. The Old Forest and the other Eriador woods are painted Plain, so the rule does nothing there. Repainting them is map work and not part of this change.

## Changelog

- 2026-10-23: first version (#849): the forest rule through the spotting-contributor seam, the terrain read on `IMapTerrainAdapter`, the MCM toggle. Review fixes: the setting is named `ForestSightPenaltyEnabled`; the adapter answers `IsInForest`; the penalty is skipped inside a settlement and halved by Keen Sight.

## In-game checklist (desktop, owed)

Toggle on unless stated.

1. Day: ride into Mirkwood, Lorien, Dol Guldur and Rivendell's vale and read the map bar view distance beside each wood: it drops to 70% of the open value with the same perks. Then the Old Forest: it should show no change, because it is painted Plain.
2. Inside a settlement whose gate stands on forest, the view distance equals the value with the toggle off.
3. A scout with Keen Sight in forest reads 85% of the open value.
4. A Dunland or Khand party in forest reads 85%.
5. The toggle: off in MCM on the map, the number returns at once.
6. **Frame time**, because the per-frame pass reads sight: turn on "Enable Map Profiler" (`BattleLoadDiagnosticsSettings`, needs a restart) and compare `realTickMs` in the `[MapProfile]` lines ([map-perf-profiler.md](map-perf-profiler.md)), standing in forest after a load without moving, with the toggle on and then off.

## GitHub Issue

- **Issue:** #849 [Forest concealment](https://github.com/haterade22/TAOM/issues/849)
- **Status:** Open (code done; the in-game checklist is owed).
