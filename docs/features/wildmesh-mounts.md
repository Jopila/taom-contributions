# WildMesh3D mounts

## Overview

Five animals from the WildMesh3D "Realistic Animals" pack become horse-skeleton mounts with their own
animation: a boar, a warthog and a mountain goat for the Dwarves, a deer and a moose for the Elves. Each is a
second variant beside the existing mounts (the war ram, the great elk, the Animalia elk and moose); nothing
is replaced. The route is the one the Animalia elk and moose proved (#646):
[quadruped-pack-to-horse-skeleton-workflow.md](../ai-includes/quadruped-pack-to-horse-skeleton-workflow.md).

**Status (2026-10-14):** stages 1 to 11 are done. The meshes (LOD0 to LOD5), 1K textures, 74 clip masters and 96
clips are in the live Armory with their RuntimeDataCache entries; the Monsters, action sets and Horse items are
written (`tools/apply_wildmesh_armory.py`, ledger [lotrlome-wildmesh-changes.md](../reference/lotrlome-wildmesh-changes.md));
five Custom Battle test riders ride them. Still owed: the in-game checks, the attacks (stage 12), the sizes after
the first battle, and the campaign riders. No campaign troop rides one yet.

**In-game test (Custom Battle, cheat mode, console):**
`taom.spawn_troops taom_test_wildmesh_<animal>_rider 5 ally` for `boar`, `warthog`, `mountaingoat`, `deer`, `moose`.
Check the gaits (hoof slide at each speed), idles, grazing, hits, vanilla falls, the dwarf's seat on the boars and
the goat (light and heavy ram bardings alternate across the riders' rosters), the elk saddle on the stag and
moose, and the sizes.

## Why This Exists

- **Vanilla behavior:** a mount is a horse, or a creature on its own skeleton built through the custom
  creature phases.
- **TAOM requirement:** the Dwarves have one mount (the war ram), and the Elves' elk and moose had one look
  each. The pack, bought under a commercial licence, adds rigged and animated animals that fit both peoples.
- **Without this feature:** the pack's rigs (74 to 134 bones, mostly face bones and `_end` leaves) cannot
  ship on their own skeletons against the engine's 64-bone cap, and a custom skeleton would lose the horse
  usage set, reins and flags that a horse-skeleton reskin inherits.

Decisions (Mike, 2026-10-14): boars and goats to the Dwarves, deer, elk and moose to the Elves; every one a
second variant; the pack's own clips retargeted for every animal.

## Architecture

### Design Challenge

- **Short necks.** Every WildMesh neck is far shorter than the horse's (0.18 m on the boar, 0.48 m on the
  deer, against about 0.9 m). Fitting it to the horse joints stretched it 2 to 3.8 times.
- **Short legs.** After one uniform scale the deer, moose and goat leg segments stretch 0.69 to 1.25 times;
  the boar and warthog lower legs 1.3 to 2.0 times, so those two come out leggier. Accepted on the renders.
- **Bone-parented parts.** Antlers and tusks ship parented to a bone with no vertex groups, some with a
  negative scale; the reskin moves an unweighted vertex with the global fit only.
- **In-place clips at 24 or 25 fps.** Every forward gait plays in place (no root motion), and the clip
  generator counts 30 fps.
- **Broken vendor gaits.** Several goat and warthog clips named `_F` (forward) do not loop or curve sideways
  (below).

### Solution Approach

No C#. The pack goes through the horse-skeleton pipeline: one map for the shared rig with a `keepneck`
profile (the neck is moved, not stretched, as for the Animalia moose; the head then pivots 0.54 to 0.73 m
behind the head, a throat crease in strong bends), and these tool changes:

| Stage | Tool | What changed for this pack |
|---|---|---|
| 1 | `tools/blender/export_blend_pack.py` (new) | Replaces the Unreal export: opens each `.blend` read-only, writes one FBX per mesh part and per clip, bakes bone-parented parts onto a weight-1 group on their bone in the rest pose, resamples clips to 30 fps |
| 2, 3 | `reskin_animalia_to_horse.py`, `retarget_animalia_to_horse.py` with `tools/blender/wildmesh_to_horse_map.json --profile keepneck` | Unchanged tools; the new map maps the hoof bones directly (no `hoof_split`) |
| 2 | `tools/blender/add_mesh_lods.py` | LOD1 to LOD5 at 70, 30, 15, 7 and 3 per cent (the pack ships none) |
| 4 | `tools/oneoff/convert_tripo_prop_textures.py` | Colour maps only: copied under `<material>_basecolor.png` so the converter finds them; no normal maps exist, so no `_n` |
| 5 | `tools/blender/measure_animalia_clips.py` | Travel from the planted hoof's slide for in-place clips; bone names from the map's `measure` block |
| 7 | `tools/wire_anim_master_skeletons.ps1` | Unchanged: 74 masters patched to `horse_skeleton` |
| 8 | `tools/gen_animalia_anim_clips.ps1 -Pack wildmesh` | The pack's plan beside the Animalia one |

### The clip plan

| Slot | Boar | Warthog | Mountain goat | Deer | Moose |
|---|---|---|---|---|---|
| Walk | WalkMedium_F | WalkMedium_F | WalkMedium_F | WalkMedium_F | WalkMedium_F |
| Trot | TrotFast_F | TrotFast_F_2 | TrotSlow_F_2 | TrotFast_F | TrotSlow_F |
| Canter | RunSlow_F | boar RunSlow_F | HoppingSlow_F | RunSlow_F | RunSlow_F |
| Gallop | RunFast_F | boar RunFast_F | deer RunFast_F | RunFast_F | RunFast_F |
| Movement idle | Idle_Breathing | Idle_Breathing | Idle_Breathing | Idle_Breathing | Idle_Breathing |
| Idles | Idle, Idle_SecondaryMoveCalm_2 to 5 | Idle, Idle_SecondaryMoveCalm_2 to 5 | Idle_SecondaryMoveCalm_1 to 5, Cue_Bleat | Idle, Idle_1 to 4 | Idle, GrazingStepsLookat |
| Riderless idles | GrazingOnSpotA_1, A_2, B_1, B_2 | same | same | GrazingOnSpotB_1 to 3, GrazingSteps | GrazingOnSpotA_1, A_2, B_1, B_2 |
| Hit front, back | HitLegFL2RunSlow_F, HitLegBL2RunSlow_F | same | HitInRunHead_F, vanilla | HitInRunHead_F, HitLegBL2RunSlow_F | HitLegFL2RunSlow_F, HitLegBL2RunSlow_F |
| Falls, rear, kick, walk back | vanilla horse | vanilla horse | vanilla horse | vanilla horse | vanilla horse |
| Attack (stage 12) | war ram head-butt | war ram head-butt | war ram head-butt | Animalia elk antler | Animalia moose head |

A borrowed gait gets no clip of its own: the action set binds the lender's clip, which already carries the
travel, because every reskin sits on the same horse joints. The deer's Bouncing and Hopping clips are leaping
gaits (0.5 to 0.7 m of pelvis travel each stride), so the deer gallops on `RunFast_F`; its 30 degree loop
error sits on two chest helper bones that drive nothing. The pack's only fall is a slow walk and lie-down that
turns the body about 100 degrees, so falls stay vanilla.

### Broken vendor gaits

Found by checking each gait's loop per mapped bone and its Root bone path, not its name:

| Clip | Mountain goat | Warthog |
|---|---|---|
| WalkFast_F, RunSlow_F | the same data; front legs never return (no frame pair closer than 22 deg) | |
| TrotSlow_F | front leg misses the loop by 63 deg | drifts 5.5 m sideways, turns 55 deg |
| TrotFast_F, RunFast_F | curve: Root 0.8 to 1.2 m sideways, 21 to 27 deg | TrotFast_F, RunSlow_F, RunFast_F hold the same data and miss the loop by 49 to 67 deg |
| HoppingFast_F, all seven A_F_* | curve: Root 1.1 to 3.4 m sideways, 20 to 145 deg | |
| _2 versions | TrotSlow_F_2 is clean | TrotFast_F_2 is clean; the _2 runs are broken |

Measured travel per loop at horse size, metres (walk, trot, canter, gallop): boar 1.78, 4.23, 4.76, 6.59;
warthog 1.69, 2.03, then the boar's; goat 1.84, 2.37, 4.09, then the deer's 7.46; deer 1.56, 5.08, 4.72,
7.46; moose 1.45, 2.84, 3.75, 4.99. The Animalia elk for scale: 2.02, 6.32, 7.40, 8.75.

## Configuration

### Armory data (Mike's decisions, 2026-10-14)

| Item | Name | Monster size | Harness | Stats from |
|---|---|---|---|---|
| `taom_wildmesh_boar_a` | War Boar | 100 | ram bardings | the war ram |
| `taom_wildmesh_warthog_a` | Bristleback Boar | 100 | ram bardings | the war ram |
| `taom_wildmesh_mountaingoat_a` | Iron Hills Goat | 100 | ram bardings | the war ram |
| `taom_wildmesh_deer_a` | Mirkwood Stag | 100 | `taom_elk_saddle_a` | the Animalia elk |
| `taom_wildmesh_moose_a` | Greenwood Moose | 150 | `taom_elk_saddle_a` | the Animalia moose |

The three dwarf mounts are in `WAR_RAM_MOUNT_IDS` (`tools/taom_schema.py`), so `MOUNTED_DWARF` allows a dwarf on
them; their Monsters keep the horse's rider seat, as the war ram does, until the in-game seat check. Monsters
`taom_wildmesh_<animal>`, action sets `as_wildmesh_<animal>` (children of `as_horse`, with `_town_and_village` and
`_map` twins). Test riders: `Main/_Module/ModuleData/troops/troops_wildmesh_test.xml`, CustomGame only, clones of
`ironpass_ram_rider` and `mirkwood_rochenlas`; their names are not in `taom_troop_name_strings.xml` yet, because
that generated file is stale on the branch for other troops and a regeneration would also drop 25 Harad keys.

### Live files

The data lives in the external, unversioned `LOTRLOME_Armory`:

- Sources: `AssetSources/creature/creatures/` (5 mesh FBX, `animations/<animal>/`, `textures/`).
- Kit packages: `Assets/creature/creatures/`: `wildmesh_<animal>_geo.tpac`, ten materials
  `wildmesh_{boar,warthog}_{body,tusk}`, `wildmesh_mountaingoat_{body,horn}`, `wildmesh_{deer,moose}_{body,antler}`
  with their `_d` / `_s` textures, 74 masters `animations/<animal>/wildmesh_<animal>_<stem>_geo.tpac`
  (skeleton patched 2026-10-14, write-once `.bak-preskel` beside each), and 96 clips
  `anim_wildmesh_<animal>_<stem>[_stand|_movement]_anm.tpac`.

## Key Files

| File | Purpose |
|---|---|
| `tools/blender/export_blend_pack.py` | `.blend` pack to part and clip FBX, bone-parented parts skinned, clips at 30 fps |
| `tools/blender/wildmesh_to_horse_map.json` | Joints, chains, weights, `keepneck` profile and `measure` bone names for the shared WildMesh rig |
| `tools/blender/wildmesh_<animal>_clip_measure.json` | Measured travel and hoof plants of each animal's gaits |
| `tools/gen_animalia_anim_clips.ps1` | `-Pack wildmesh` plan: the 96 clips |
| `tools/blender/measure_animalia_clips.py` | Stride travel for in-place clips |
| `tools/apply_wildmesh_armory.py` | Monsters, action sets and Horse items in the live Armory (tests: `tools/tests/test_apply_wildmesh_armory.py`) |
| `Main/_Module/ModuleData/troops/troops_wildmesh_test.xml` | The five Custom Battle test riders |
| `docs/reference/lotrlome-wildmesh-changes.md` | Ledger of every change outside the repo, replay and removal |

## Dependencies

- `horse_skeleton` and `as_horse` (Native): the rig and the parent action set.
- TaleWorlds' `horse.fbx` mesh export (`C:\Users\mikew\Downloads\horse.fbx`, outside the repo): the export
  armature.
- The war ram's and the Animalia attack clips, reused in stage 12.

## Tests

No unit tests: the stages are gated by their own reports. Export round trip: bone drift 0. Reskin: no
unweighted vertex, at most four influences, re-import drift 0.00001 m, bend checks near the Animalia elk
except the tail lift (p99 2.4 to 3.2 against 1.3). Retarget: 155 of 155 candidate clips export with the engine
bone order and frame 0 at rest. Resample: length kept, pose within 0.07 deg. Masters: census ok=74.
Clips: `gen_animalia_anim_clips.ps1 -Pack wildmesh -Verify` ok=96; the Animalia default still ok=54.

## How to add another WildMesh animal

1. Check the rig against the map's joints, then export: `export_blend_pack.py --pack <key>=<file.blend> --out <dir> --fps 30`.
2. Check every candidate gait's loop per mapped bone and its Root path before picking it; borrow a clean
   `horse_skeleton` clip where the pack's is broken.
3. Reskin and retarget with `--profile keepneck`, add LODs, convert the textures, measure the gaits.
4. After the Kit import: `wire_anim_master_skeletons.ps1`, then add the animal to `$PLANS.wildmesh` in
   `gen_animalia_anim_clips.ps1` and run `-Pack wildmesh -Apply`.

## Changelog

- 2026-10-14: stages 9 to 11: RuntimeDataCache entries, the Armory data (`tools/apply_wildmesh_armory.py`) and
  five Custom Battle test riders.
- 2026-10-14: stages 1 to 8 for the boar, warthog, mountain goat, deer and moose (#794): exporter, map, LODs,
  textures, 30 fps clips, stride measurement, 74 masters wired, 96 clips generated.

## GitHub Issue

- **Issue:** #794 [WildMesh3D boars, goats, deer and moose as second-variant mounts](https://github.com/haterade22/TAOM/issues/794)
- **Status:** Open
