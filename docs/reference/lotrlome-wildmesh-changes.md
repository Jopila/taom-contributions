# LOTRLOME_Armory changes: the WildMesh3D mounts (#794)

The live `LOTRLOME_Armory` and the installed `TAOM` module are not versioned, so this ledger records every
change the WildMesh3D mounts made outside the repo, how to replay it after a reinstall, and how to remove it.
Feature: [wildmesh-mounts.md](../features/wildmesh-mounts.md).

## Assets (2026-10-14)

| What | Where | By |
|---|---|---|
| Sources: 5 mesh FBX, `animations/<animal>/` (74 clip FBX), `textures/` (20 PNG) | `AssetSources/creature/creatures/` | Mike, from `E:\LOTRAOMAssets\_kit_ready\wildmesh` |
| Kit packages: `wildmesh_<animal>_geo.tpac`, 10 materials and their `_d` / `_s` textures, 74 masters | `Assets/creature/creatures/` | Mike, Modding Kit import |
| Masters' skeleton reference set to `horse_skeleton` (74, write-once `.bak-preskel` beside each) | `Assets/creature/creatures/animations/<animal>/` | `tools/wire_anim_master_skeletons.ps1 -Apply` |
| 96 clips `anim_wildmesh_<animal>_<stem>[_stand\|_movement]_anm.tpac` | beside their masters | `tools/gen_animalia_anim_clips.ps1 -Pack wildmesh -Apply` |
| RuntimeDataCache entries for the clips | `RuntimeDataCache/` | one Kit open and close (Mike) |

## Data (2026-10-14, `tools/apply_wildmesh_armory.py --apply`, backups `*.bak-wildmesh-20261014`)

| File | Change |
|---|---|
| `ModuleData/Monsters/LOTR/lotr_monster_wildmesh.xml` | New: `taom_wildmesh_{boar,warthog,mountaingoat,deer,moose}`, `base_monster="horse"`, own action set, `taom_body_length` 100 (the moose 150); the dwarf mounts take the war ram's weight, hit points, jump acceleration and charge speed limit |
| `SubModule.xml` | Monsters registration after the Animalia one |
| `ModuleData/action_sets.xml` | `as_wildmesh_<animal>` (children of `as_horse`, 24 or 25 overrides each) with `_town_and_village` and `_map` twins, before the `/WARG` marker |
| `ModuleData/LOTRLOME_items/LOTRAOM_horses.xml` | `taom_wildmesh_<animal>_a` after `taom_animalia_moose_a`: War Boar, Bristleback Boar, Iron Hills Goat (Erebor, the war ram's stats), Mirkwood Stag, Greenwood Moose (Mirkwood, the Animalia elk's and moose's stats); not merchandise |

## Installed TAOM module (2026-10-14, test riders only)

The installed `Modules/TAOM/SubModule.xml` was an older build than the repo's, so it was not replaced: the
test riders' registration block alone was inserted after `troops_mirkwood` (backup
`SubModule.xml.bak-wildmesh-20261014`), and `ModuleData/troops/troops_wildmesh_test.xml` was copied from the
repo. A normal build and deploy carries both from the repo afterwards.

## Replay after a reinstall

1. Restore the sources and Kit packages from a backup of `creature/creatures` (or re-import from
   `E:\LOTRAOMAssets\_kit_ready\wildmesh`).
2. `powershell.exe -File tools\wire_anim_master_skeletons.ps1 -Masters <Armory>\Assets\creature\creatures\animations -Apply`
3. `powershell.exe -File tools\gen_animalia_anim_clips.ps1 -Pack wildmesh -Apply`, then one Kit open and close.
4. `python tools/apply_wildmesh_armory.py --apply` (Kit and game closed).
5. Gates: `python tools/check_rdc_entries.py --under creature/creatures`, `gen_animalia_anim_clips.ps1 -Pack wildmesh -Verify`,
   `python tools/audit_action_set_parity.py`, `python tools/validate_moduledata.py`. The writer's
   `LiveRecipeParityTests` fail when a live hand edit (a resize) drifts from its recipe: update the recipe too.

## Removing the test riders

Delete `Main/_Module/ModuleData/troops/troops_wildmesh_test.xml` and its block in `Main/_Module/SubModule.xml`,
the five `taom_test_wildmesh_*` entries in `tools/taom_schema.py` (`_ARMOUR_LADDER_EXEMPT`) and
`tools/melee_ladders.json` (`exempt_troops`), and the `taom_test_` prefix in `VolunteerRecruitmentServiceTests`
when no other test rider remains. The three dwarf mounts stay in `WAR_RAM_MOUNT_IDS` once real dwarf troops
ride them.
