# No-Mount Cultures (Character Creation)

## Overview

Certain TAOM cultures intentionally omit horses from character creation equipment. This requires four Harmony patches to prevent vanilla's CC narrative stage from crashing when no horse is present in a culture's battle equipment roster.

**Shipped no-mount cultures:** Ered Luin (the Blue Mountains dwarves, [ered-luin.md](ered-luin.md)), Goblin-town, the Misty Mountain orcs and Blue Craig: none of their `player_char_creation_{culture}_{title}_{m|f}` rosters has a Horse slot, and `tools/generate_char_creation_equipment.py` marks each `no_mount: True`. **Erebor is no longer one:** commit `440d65342` (2026-03-31) took the horse out of its rosters and `d80cd7645` (2026-04-07) put `sumpter_horse` and `light_harness` back into all 16, where they still are. Removing them again is open for Mike's decision.

> **Updated 2026-08-28 (issue #515): "no-mount" now means "no HORSE".** Erebor fields a rideable war ram, so `troops_erebor.xml` carries Horse-slot entries for the first time and dwarves are no longer `CanRide="false"`. Nothing in this document's crash analysis changes: the patches key on whether a *character-creation* roster has a Horse slot (Erebor's have had one since `d80cd7645`; see above). The `MOUNTED_DWARF` validator rule likewise still fires for a dwarf on a horse; it allowlists only the two war-ram item ids. See [war-ram.md](war-ram.md).

## Why This Exists

- **Vanilla behavior:** Six `Get*NarrativeMenuCharacterArgs` private methods in `CharacterCreationCampaignBehavior` drive each CC stage. Three of them (youth, adult, age selection) unconditionally read `DefaultEquipment[Horse].Item.StringId` and spawn a horse actor. All vanilla culture CC rosters include horse + harness slots.
- **TAOM requirement:** Dwarves don't ride horses (lore). Ered Luin's, Goblin-town's, the Misty Mountain orcs' and Blue Craig's CC equipment rosters have no horse slots. (They do ride war rams as of #515, but that is a battle-roster mount and never appears in a CC roster, so this patch family is unaffected.)
- **Without this fix:** Two cascading crashes per horse-reading CC stage:
  1. `NullReferenceException` in `Get{Youth|Adult|AgeSelection}MenuNarrativeMenuCharacterArgs` — `DefaultEquipment[Horse].Item` is null.
  2. `ArgumentNullException("key")` in `SpawnNonHumanNarrativeMenuCharacter` — horse scene character has uninitialized (null) item ID because `ModifyMenuCharacters` never set it.

## Architecture

### Design Challenge

`MBEquipmentRoster.DefaultEquipment` returns the first Battle-type EquipmentSet after `OrderEquipments()` sorts Battle sets to the front. Adding horse to only civilian sets doesn't help — `DefaultEquipment` always returns a Battle set. The vanilla narrative scene machinery has two separate crash points per horse-reading stage: the `Get*NarrativeMenuCharacterArgs` method (sets the horse `NarrativeMenuCharacterArgs`) and `SpawnNonHumanNarrativeMenuCharacter` (spawns the horse 3D actor), which runs after `ModifyMenuCharacters`. There are exactly 3 horse-reading methods and 1 shared spawn method — 4 patches total.

### Solution Approach

Four thin Harmony patches in `Patch20_NarrativeHorseGuard`:

1. **Prefix on `GetYouthMenuNarrativeMenuCharacterArgs`**: Looks up the culture's CC equipment roster. If `DefaultEquipment[Horse].Item == null`, returns a `__result` containing only the player character entry (`characterId: "player_youth_character"`, age 17) — skipping the horse `NarrativeMenuCharacterArgs` entirely. Vanilla runs for horse-enabled cultures (returns `true`).

2. **Prefix on `GetAdultMenuNarrativeMenuCharacterArgs`**: Identical logic. Returns only the player character entry (`characterId: "player_adulthood_character"`, age 20) when no horse is present.

3. **Prefix on `GetAgeSelectionMenuNarrativeMenuCharacterArgs`**: Identical logic. Returns only the player character entry (`characterId: "player_age_selection_character"`, age = `StartingAge`) when no horse is present.

4. **Finalizer on `SpawnNonHumanNarrativeMenuCharacter`** (`SandBox.GauntletUI.CharacterCreation.CharacterCreationNarrativeStageView`): Suppresses `ArgumentNullException("key")` that occurs when the horse scene character's item ID was never set (because the horse entry was skipped by any of the three Prefixes). The horse actor is simply not spawned.

### Component Diagram

```
taom_char_creation_equipment.xml
  └── Ered Luin, Goblin-town, Misty Mountain orc and Blue Craig rosters: no Horse/HorseHarness slots

Patch20_NarrativeHorseGuard
  ├── CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch [Prefix]
  │     Checks DefaultEquipment[Horse] → null → skip horse NarrativeMenuCharacterArgs
  │     → returns "player_youth_character" entry only (age 17)
  │     → vanilla handles horse-enabled cultures unchanged
  │
  ├── CharacterCreationCampaignBehavior_GetAdultMenuArgs_Patch [Prefix]
  │     Same null check → returns "player_adulthood_character" only (age 20)
  │
  ├── CharacterCreationCampaignBehavior_GetAgeSelectionMenuArgs_Patch [Prefix]
  │     Same null check → returns "player_age_selection_character" only (age = StartingAge)
  │
  └── CharacterCreationNarrativeStageView_SpawnNonHuman_Patch [Finalizer]
        Catches ArgumentNullException("key") from null horse item ID
        → horse actor simply not spawned (covers all three prefix paths)
```

### Crash Flow (Without Patches)

Each horse-reading CC stage shares the same crash flow:

```
OnNextStage
  → TrySwitchToNextMenu
      → ModifyMenuCharacters
          → GetYouthMenuNarrativeMenuCharacterArgs        ← Crash 1a: NRE (null horse item)
          → GetAdultMenuNarrativeMenuCharacterArgs        ← Crash 1b: NRE (null horse item)
          → GetAgeSelectionMenuNarrativeMenuCharacterArgs ← Crash 1c: NRE (null horse item)
  → RefreshMenu
      → OnMenuChanged
          → RefreshAgentVisuals
              → SpawnNonHumanNarrativeMenuCharacter       ← Crash 2: ArgumentNullException("key")
                  → MBObjectManager.GetObject<T>(null)
```

The three non-horse methods (`GetParentMenu`, `GetChildhoodMenu`, `GetEducationMenu`) are safe — they don't include a horse entry.

## Configuration

### Removing horse from a culture's CC rosters

In `Main/_Module/ModuleData/equipmentsets/taom_char_creation_equipment.xml`, ensure all non-civilian `<EquipmentSet>` blocks for `player_char_creation_{culture}_*` do **not** contain:

```xml
<Equipment slot="Horse" id="Item.sumpter_horse" />
<Equipment slot="HorseHarness" id="Item.light_harness" />
```

The patches detect horse absence at runtime — no code change needed to make a culture no-mount.

### Cultures with horses (must retain Horse/HorseHarness slots)

| Culture | Reason |
|---------|--------|
| Gondor | Riders of Gondor, mounted knights |
| Mordor | Nazgul steeds, mounted wargs |
| Rivendell | Elven horses |
| All others | Vanilla-compatible, horse-enabled |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs` | All four patch classes (three Prefixes + Finalizer) |
| `Main/SubModule.cs` | `PatchCategory("Patch20_NarrativeHorseGuard")` registration |
| `Main/_Module/ModuleData/equipmentsets/taom_char_creation_equipment.xml` | CC equipment rosters; the no-mount cultures' have no Horse slots |
| `tools/generate_char_creation_equipment.py` | Writes a culture's CC rosters (`--append <culture>`); a `CULTURES` row with `no_mount: True` gets no Horse or HorseHarness |

## Dependencies

- `HarmonyLib` — Harmony patching framework
- `TaleWorlds.CampaignSystem.CharacterCreationContent.NarrativeMenuCharacterArgs` — struct for scene character setup
- `SandBox.GauntletUI.CharacterCreation.CharacterCreationNarrativeStageView` — accessed via `AccessTools.TypeByName` (runtime, no compile-time reference needed)

## Tests

No unit tests — all patches are thin Harmony entry points with no extractable service logic. The entire logic is a null check (`DefaultEquipment[Horse].Item != null`) and an exception type/parameter check (`ArgumentNullException { ParamName == "key" }`).

**Trailer:** `Not-tested: Harmony patch invocation (requires live game)`

## How to Add a No-Mount Culture

1. For a new culture, set `no_mount: True` on its `CULTURES` row in `tools/generate_char_creation_equipment.py` before `--append <culture> --apply` (Ered Luin: `dict(CULTURES["erebor"], no_mount=True)`). For a culture already in the file, remove `<Equipment slot="Horse" ...>` and `<Equipment slot="HorseHarness" ...>` from all non-civilian `<EquipmentSet>` blocks in `taom_char_creation_equipment.xml` for the culture.
2. No C# changes needed — the patches detect horse absence from `DefaultEquipment[Horse].Item` at runtime.
3. Verify: `grep -c 'slot="Horse"' taom_char_creation_equipment.xml` — confirm only horse-enabled cultures have the slot.

## Changelog

- 2026-10-27: Ered Luin joins as a no-mount culture (`no_mount=True`, 0 Horse slots in its 55 CC rosters). Recorded that Erebor's 16 adult CC rosters have carried `sumpter_horse` and `light_harness` since `d80cd7645` (2026-04-07), so Erebor is no longer a no-mount culture here.
- 2026-03-31 — Completed `Patch20_NarrativeHorseGuard` to 4 patches (3 Prefixes + 1 Finalizer) covering the youth, adult, and age-selection CC stages plus the shared `SpawnNonHumanNarrativeMenuCharacter` spawn; removed Horse/HorseHarness slots from Erebor CC rosters so dwarves no longer crash during character creation.

## GitHub Issues

- **Issue #49** — [Arena practice crash for all 13 TAOM cultures](https://github.com/haterade22/TAOM/issues/49) — Closed
- **Issue #50** — [Dwarf character creation crashes and horse removal](https://github.com/haterade22/TAOM/issues/50) — Closed

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/culture-playability-wiring.md](./culture-playability-wiring.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/equipment-rosters.md](../modding/equipment-rosters.md)

<!-- backlinks-end -->
