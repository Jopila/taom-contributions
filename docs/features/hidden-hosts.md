# Hidden Hosts

## Overview

On the campaign map, another party's troop count is blurred by its distance from the player's main party, measured
against the main party's seeing range: `1??`, `12?`, `???`, and exact once the party is close. Past nine tenths of that
party's own spotting reach (the horizon) the count is `...`, the plate's name is "Unknown party", and the encyclopedia
hotkey opens nothing for it. Wounded and prisoner text shows only when the count is exact. It is local UI only: the AI,
saved engine fields and the map visibility model are untouched.

Phase 1 (nameplates) is built. Phase 2 is the hover tooltips. Phase 3 is the memory fade and the `~` symbol (a count
remembered from an earlier sighting), which gets its own popup text under a new key with its own saved flag; the Phase 1
popup does not mention `~`. Tracked in [#846](https://github.com/haterade22/TAOM/issues/846).

What the player sees in vanilla, and so here: the nameplate count is always drawn, but the party's name and its wounded
and prisoner text only while the ShowIndicators key (Left Alt) is held. The mask therefore shows on the count at all times
and on the name and extra text only while that key is down.

The encyclopedia is opened by a hotkey, not a click: game key 67 (`MapGoToEncylopedia`), pressed while the cursor is on a
party (`MapScreen.cs:1649-1651`). The keyboard default is unbound; the controller default is Left Option. A left click
engages or escorts as in vanilla and is not touched.

## Why This Exists

- **Vanilla behavior:** every visible party shows its exact healthy troop count, however far away it is; its name and
  wounded and prisoner counts show with the indicators key.
- **TAOM requirement:** scouting should matter. A host seen at the edge of the horizon is a shape on the hill, not a
  muster roll; a closer look, a good scout and a party of your own realm tell you more.
- **Without this feature:** the map gives away the strength of every enemy army at the first sighting.

## Architecture

### Design Challenge

`PartyNameplateVM.RefreshDynamicProperties` writes the bind fields `_countBind`, `_extraInfoTextBind` and `_fullNameBind`
from int caches and a cached name object, never from the field text, and it may run on `TWParallel` workers at 32 or
more plates. `RefreshBinding` runs on the main thread afterwards and is the only reader of those fields (v1.5.5; pinned
by `HiddenHostsBindingTests`): it copies each to its property, and every property setter returns early on an equal value.
A hook that overwrites a field for good would leave the mask in place after the party comes close, because vanilla does
not rewrite the field until its cache says the value changed. `_fullNameBind` in particular is rewritten when the name
changes, on every forced refresh and when a pooled plate is reused (`_latestNameTextObject = null`), not every frame.

### Solution Approach

A void prefix on `PartyNameplateVM.RefreshBinding` (Patch105) lays the mask over the three fields for that one call and
hands vanilla's values back as Harmony's `__state` (`VanillaBinds`); a finalizer on the same method puts them back, even
when the original throws. No mask outlives the call that copies it to the properties, so there is nothing to track
between frames, no per-plate table and no "lift" when the party comes close or the toggle goes off: the next frame simply
lays no mask. A bool prefix on `MobilePartyVisual.OnOpenEncyclopedia` blocks the hotkey at the horizon.

`HiddenHostsService` decides the masks from a `PartyIntelSnapshot` of raw facts:

- **Digits:** the digit count of the party's healthy troops picks a threshold row; digit `k` is known when
  `distance / seeingRange < row[k] * multiplier`. Known digits are always a prefix.
- **Multiplier:** `(1 + 0.12 * Scouting / 50) * (same realm ? 2 : 1)`, capped at 2.25. Scouting is the skill of the main
  party's `EffectiveScout`. Same realm means the party's map faction, or its army leader's, is the main party's.
- **Latch:** a digit already known stays known to 1.1 times its threshold, per party, so a party at the edge does not
  flicker. The latch clears when a party reaches the horizon and for every new campaign.
- **Horizon:** `distance / seeingRange > 0.9 * GetPartySpottingRatioForMainPartySeeingRange(party)`: past nine tenths of
  the party's own spotting reach. A besieger or raider of a settlement the player can see stays visible farther out than
  that ratio says, so for those parties the horizon band is wider than a tenth of the range.
- **Exempt (never masked):** the main party, parties of the player's clan, parties of the player's army, parties whose
  info the engine already hides (`IsInfoHidden`), everything while `Campaign.TrueSight` is on, and any snapshot with
  missing or non-finite data.

The adapter reports the facts (`IsMainParty`, `InPlayerClan`, `InPlayerArmy`, `TrueSight`, `SameFaction`,
`ArmyLeaderSameFaction`); the service composes them into exempt and same realm. The mask text is cached per party by
(count, known digits), so a steady plate allocates nothing.

**Errors.** A fault in a hook shows vanilla's text for that frame (the prefix writes nothing it cannot take back). After
32 faults (`HiddenHostsCalls.FaultBudget`) the hooks stop for the rest of the process and one log line says so; a fault is
logged once per hook and exception type. A missing engine field leaves its bind null: the nameplate hook reports it once
and does nothing. Toggle off: every hook returns at once with no work.

One-time popup: the first campaign tick on a free map with the feature on shows one inquiry explaining `1??` and `...`; the
flag `_taomHiddenHostsHintShown` is saved with the campaign. It waits while the hooks are stopped by the fault budget. A popup that throws counts as shown (saved,
never retried in that campaign); only a throw from the free-map test gives the hint up for the session alone, logged once.

### Component Diagram

```
MobileParty
    |
PartyIntelAdapter (cached spotting ratio, seeing range, scout skill)
    |  PartyIntelSnapshot (raw facts)
HiddenHostsService  --  HiddenHostsMath (thresholds, digits, horizon)
    |  NameplateIntel
Patch105 prefix -> VanillaBinds (__state) -> RefreshBinding -> finalizer restores
```

## Configuration

MCM `Map UI/Hidden Hosts`: "Hide Exact Troop Counts" (`HiddenHostsEnabled`), default on, no restart, applies at once. The
thresholds are constants in `HiddenHostsMath`.

### Thresholds

Digit thresholds are fractions of the main party's seeing range, left digit first, by the digit count of the party's
healthy troops (a count of 0 is one digit). Six or more digits use the five-digit row; extra digits take its last value.

| Digits | Digit 1 | Digit 2 | Digit 3 | Digit 4 | Digit 5 |
|--------|---------|---------|---------|---------|---------|
| 1 | 0.28 | | | | |
| 2 | 0.50 | 0.26 | | | |
| 3 | 0.62 | 0.32 | 0.20 | | |
| 4 | 0.70 | 0.45 | 0.28 | 0.16 | |
| 5 or more | 0.75 | 0.50 | 0.32 | 0.20 | 0.14 |

| Constant | Value | Meaning |
|----------|-------|---------|
| Scouting bonus | +0.12 per 50 skill | Multiplies every threshold |
| Same-realm factor | x2 | Party or its army leader in the main party's faction |
| `MaxScoutMultiplier` | 2.25 | Cap on the product of the two |
| `Hysteresis` | 1.1 | A known digit holds to 1.1 times its threshold |
| `HorizonFraction` | 0.9 | Horizon starts past this share of the party's spotting reach |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/HiddenHosts/HiddenHostsMath.cs` | Threshold rows, scout multiplier, known digits, horizon, mask format |
| `Main/Features/HiddenHosts/HiddenHostsService.cs` | The decision per snapshot, the latch and the string cache |
| `Main/Features/HiddenHosts/NameplateIntel.cs` | What a plate shows: masked, count text, horizon |
| `Main/Features/HiddenHosts/VanillaBinds.cs` | Lays the mask over the three bind fields and restores vanilla's values (`__state`) |
| `Main/Features/HiddenHosts/Hooks/PartyNameplateVM_RefreshBinding_HiddenHosts_Patch.cs` | Patch105 prefix and finalizer on `RefreshBinding` |
| `Main/Features/HiddenHosts/Hooks/MobilePartyVisual_OnOpenEncyclopedia_HiddenHosts_Patch.cs` | Patch105 bool prefix on the encyclopedia hotkey |
| `Main/Features/HiddenHosts/HiddenHostsCalls.cs` | Statics the hooks reach, the fault budget |
| `Main/Features/HiddenHosts/HiddenHostsTexts.cs` | "Unknown party", rendered once per language |
| `Main/Features/HiddenHosts/HiddenHostsBehavior.cs` | Per-campaign reset, one-time popup, saved flag |
| `Main/Features/HiddenHosts/HiddenHostsModule.cs` | Module wiring, service registration, patch category, statics |
| `Main/Features/HiddenHosts/IHiddenHostsSettings.cs`, `HiddenHostsSettingsProvider.cs` | The MCM toggle, read through a cached settings object |
| `Main/Features/TaomSettings.cs` | The `HiddenHostsEnabled` property |
| `Main/Adapters/IPartyIntelAdapter.cs`, `PartyIntelAdapter.cs`, `PartyIntelSnapshot.cs` | `MobileParty` to raw facts |
| `Main/Adapters/IHiddenHostsHintAdapter.cs`, `HiddenHostsHintAdapter.cs` | The popup and the free-map test |
| `Main/_Module/ModuleData/taom_module_strings.xml` | `taom_hhost_*` keys |
| `docs/reference/harmony-patch-registry.md` | `Patch105_HiddenHosts` entry |
| `docs/reference/taleworlds-api-snapshot/reflection-sites.md` | The four reflected `PartyNameplateVM` fields |

## Dependencies

- `IHiddenHostsSettings` (HiddenHosts) over MCM `TaomSettings`.
- `IPartyIntelAdapter` (Adapters) over the party, faction, skill and `MapVisibilityModel` data.
- `IHiddenHostsHintAdapter` (Adapters) over `InformationManager.ShowInquiry`.

## Tests

- `TAOM.Tests/Features/HiddenHosts/HiddenHostsMathTests.cs`: every threshold row boundary, the cap, hysteresis, count 0, six digits, mask format and a NaN row per gate.
- `HiddenHostsServiceTests.cs`: the gates, one row per exempt and same-realm fact, the horizon, the Scouting reach, the latch, the string instance cache, the party cap, the infinite-ratio gate and reset.
- `VanillaBindsTests.cs`: mask, restore, nulls, the unset no-op.
- `HiddenHostsHookTests.cs`: the prefixes and the finalizer run against bare engine objects (`FormatterServices.GetUninitializedObject`): mask then restore, horizon rename, toggle off, invisible plate, faults and the fault budget, and the patch classes applied and JIT-compiled against the installed engine.
- `HiddenHostsBindingTests.cs`: the engine facts the finalizer design rests on (the only reader of the three fields, the three setters).
- `HiddenHostsFaultBudgetTests.cs`, `HiddenHostsWiringTests.cs`, `HiddenHostsBehaviorSessionResetTests.cs`, `HiddenHostsSettingsProviderTests.cs`, `PartyIntelAdapterTests.cs`: the budget, the module and the per-campaign reset, the saved flag, the toggle, and that the adapter keeps no campaign object.

`PartyIntelAdapter` and `HiddenHostsHintAdapter` need a live campaign, so the in-game check below is their coverage.

## How to retune the thresholds

1. Edit the `Rows` table or the constants (`HiddenHostsMath.cs`: `Hysteresis`, `HorizonFraction`, `MaxScoutMultiplier`, the 0.12 per 50 skill and the same-realm factor in `ScoutMultiplier`).
2. Update `HiddenHostsMathTests` (each row is pinned just inside and just outside) and the table above.
3. Rebuild and restart the game; there are no sliders yet, so nothing is read at run time.

## Performance

The prefix and finalizer run once per visible party nameplate per map frame. Plates that are not on the map, and every
call while the toggle is off or the hooks are stopped, return before any work. For an evaluated plate the cost is two
dictionary lookups (the adapter's spotting cache and the service's party state), a second `GetPartyHealthyCount` (vanilla
computes it too; it loops the attached parties for an army leader), a distance, and the cached reads: the main party's
seeing range and Scouting skill and each party's spotting ratio (a terrain lookup) are kept for half a second of real
time in `PartyIntelAdapter`. The mask text is cached per party, so a steady plate allocates nothing.

## Not in Phase 1

- The folder is `HiddenHosts`, not `PartyIntel`; the adapter and snapshot keep the neutral name `PartyIntel` because later phases read more of the party.
- No MCM tuning sliders yet: the thresholds are constants.
- No prefab widget: the mask is text in the existing nameplate fields only.
- Vantage Point is not a separate x1.25: vanilla adds it to the spotting range and the fog reads `SeeingRange` (`DefaultMapVisibilityModel.cs:61-63`), so it already widens the reach.
- The scout-away rule and the scout report move to [#847](https://github.com/haterade22/TAOM/issues/847).
- Hover tooltips still show exact numbers and the name until Phase 2.
- The memory symbol `~` and its popup text arrive with Phase 3.
- Translation: the three `taom_hhost_*` keys are seeded as English in all 12 languages; the paid translation run is owed.

## Changelog

- 2026-10-18: phase 1 (nameplates) built; the mask moved from a remembered-value table to a prefix and finalizer pair.

## In-game check owed

Bind the encyclopedia key first (it is unbound on the keyboard by default). Approach a lord army and a small party with
Left Alt held, so the name and extra text show: counts sharpen with distance; step back at a boundary and the digit holds
for a tenth of the threshold; past nine tenths of the party's spotting reach the plate reads `...` and "Unknown party" and
the encyclopedia key does nothing, while a left click still engages; turn the toggle off mid-chase and the exact numbers
return on the next frame; the popup appears once per campaign.

## GitHub Issue

- **Issue:** #846 [Hidden Hosts](https://github.com/haterade22/TAOM/issues/846)
- **Status:** Open (phase 1 of 3)
