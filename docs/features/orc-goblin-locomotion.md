# Orc and Goblin Locomotion

## Overview

Gives orcs (race `orc`) and goblins (race `goblin`) their own walk, run and standing idle clips,
restyled from the vanilla human clips on the unchanged `human_skeleton`. Armour, weapons and every
combat clip keep working, because only the locomotion and idle slots of the two races' action sets
change.

**Status (2026-10-14):** item 1 (forward walk, forward run, standing idle; unarmed, one-handed, one-handed with
shield; both stances) is in the live Armory for both races: 32 masters imported and wired to `human_skeleton`,
32 clips generated and saved by the Kit (RuntimeDataCache entries present), and 42 rows bound in each of
`as_orc_warrior` and `as_goblin_warrior`. Mike's in-game check the same day passed with the first 18 rows
(the animations look good); the other 24 codes play the same clips and still need a look (see Testing).
Next: items 2 to 6 of #791 (directional walks, the other weapon classes, idle fidgets, town idles, an
upright uruk variant on approval).

## Why This Exists

- **Vanilla behavior:** a race on `human_skeleton` uses `as_human_warrior` clips unless its own
  action set overrides them.
- **TAOM data before #791:** `as_orc_warrior` and `as_goblin_warrior` in the Armory's
  `action_sets.xml` were empty sets with `base_set="as_human_warrior"`, so orcs and goblins moved like
  Calradian peasants.
- **TAOM requirement:** Tolkien's orcs are squat, crook-legged and long-armed, with a stooping,
  loping gait; goblins are smaller, quicker and furtive. A battle line should read as orcs from a
  distance.

## Architecture

### Design Challenge

- **Clip definitions are binary.** A clip's flags, priority, usages and stride live in an
  `AnimationClip` item in a `_anm.tpac`, not in XML. `tools/gen_troll_anim_clips.ps1` clones them
  from the vanilla clip.
- **Stride and cadence are one parameter.** The engine's bipedal movement system plays a gait clip
  at agent speed divided by the clip's `BipMovIkUsage.LoopDisplacement` (metres per loop). A shorter
  stride gives a faster cadence at the same speed; stride scale 0.8 gives 1.25 times the cadence.
  The clip must carry the restyled stride as its LoopDisplacement, or the feet slide.
- **The Armory's own XSLT cannot bind these rows.** `MBObjectManager.CreateMergedXmlFile` (v1.5.5
  decompile, lines 962 to 978) applies a module's XSLT to the accumulated document *before* it merges
  that module's own XML. The Armory's `action_sets.xslt` therefore sees only Native's sets: its
  elephant rows reach `as_human_warrior`, but a template matching `as_orc_warrior` never fires,
  because the Armory's own `action_sets.xml` defines that set. A module that loads after the Armory
  does see it (DualWield's `action_sets.xslt` rewrites every `*_warrior` set that way), but TAOM
  declares no load order against the Armory and registers no action-set file in its
  `project.mbproj`. So the rows go into the Armory's `action_sets.xml` itself, beside the clips and
  the race sets, as the cave troll's binder does.
- **No restyle tool existed.** Every clip TAOM shipped on `human_skeleton` (the 52 cave troll
  clips) was retargeted from the Fab Cave Troll Lightweight pack (provenance register row). Restyling
  a vanilla human clip is new.

### Solution Approach

1. Dump the vanilla source masters to JSON (`tools/read_anim_keyframes_tpac.ps1 -ByClip`).
2. Restyle each source master per variant on the engine-frame rig (new
   `tools/blender/restyle_human_clip.py`), with feet pinned by temporary IK and exported through the
   Kit-ready exporter in `tools/blender/retarget_mannequin_to_human.py`.
3. Validate each clip (new `tools/blender/validate_locomotion_clips.py`).
4. Mike imports the FBX in the Modding Kit; the tpac tools wire the masters and clone the clips
   (`-CloneByName`, `-TravelScale` = the variant's stride scale).
5. A generator (new `tools/bind_orc_goblin_locomotion.py`) writes marked `<action>` rows inside the
   two sets in the Armory `action_sets.xml`, the pattern `tools/bind_troll_action_set.py` uses for
   the cave troll. No C#, no Monster change. It binds every `act_walk_*` and `act_run_*` code of
   Native's `as_human_warrior` whose vanilla clip has a restyled package in the race's folder: 42
   codes per race from the 16 clips (the table under Discovery, plus the 24 codes listed below it).

Derived sets take a snapshot of `as_orc_warrior` through `base_set` when the engine parses them, so
they inherit the bound rows. `bind_orc_goblin_locomotion.py --check` groups them (84 per race) and
proves that each set in every chain comes after its own parent:

| Derived sets per race | What they get |
|---|---|
| 6: female warrior, hideout bandit and their female and tavern-free twins | every new clip |
| 72: villager, lord, seller, tavern, child, carry and the other civilian sets | their own unarmed walk and idle (human), and 20 of them their own unarmed running idle too; the armed codes and the runs come from the new clips |
| 2 facegen, 2 map (with banner), 2 carry-axe | their own overrides of some codes, unchanged |

So unarmed orcs in towns keep the human civilian walk and idle until a later step.

## Discovery

### Assignment path

race id (skins.xml, `human_skeleton`) → Monster with the same id (`action_set`) →
`as_<race>_warrior` → `base_set="as_human_warrior"`, copied at parse time.

| Race | Monster `action_set` | Troops in `troops/` | Example |
|---|---|---|---|
| `orc` | `as_orc_warrior` | 50 (Mordor 29, Isengard 13, Dol Guldur 7, goblin file 1) | `mordor_orc_recruit` |
| `goblin` | `as_goblin_warrior` | 34 (goblin file 26, Dol Guldur 8) | `goblin_snaga` |

Out of scope: `uruk`, `uruk_hai`, `pale_uruk` (this includes `gundabad_snaga`), `dg_uruk`,
`berserker`. They keep the human clips until an upright uruk variant is approved.

### Action codes and source clips (Native `as_human_warrior`)

Source ranges come from `clips_index.json`; frame numbers are key times in the master.

| Action code | Vanilla clip | Master | Frames | Clip duration |
|---|---|---|---|---|
| `act_walk_forward_unarmed` (+ `_left_stance`) | `walk_forward_unarmed` | `anim_walk_forward_unarmed` | 1 to 37 | 1.4 s |
| `act_walk_forward_1h` | `walk_forward_1h` | `anim_walk_forward_onehanded` | 1 to 37 | 1.4 s |
| `act_walk_forward_1h_left_stance` | `walk_forward_1h_left_stance` | `anim_walk_forward_onehanded_left_stance` | 1 to 37 | 1.4 s |
| `act_walk_forward_1h_with_shield` | `walk_forward_1h_with_shield` | `anim_walk_forward_onehanded_with_big_shield` | 1 to 37 | 1.4 s |
| `act_walk_forward_1h_with_shield_left_stance` | `walk_forward_1h_with_shield_left_stance` | same master as above | 1 to 37 | 1.4 s |
| `act_run_forward_unarmed` (+ `_left_stance`) | `run_forward_unarmed` | `anim_run_forward_unarmed` | 1 to 25 | 0.8 s |
| `act_run_forward_1h` | `run_forward_1h` | `anim_run_forward_onehanded_without_shield` | 1 to 25 | 0.8 s |
| `act_run_forward_1h_left_stance` | `run_forward_1h_left_stance` | `anim_run_forward_onehanded_without_shield_left_stance` | 1 to 25 | 0.8 s |
| `act_run_forward_1h_with_shield` | `run_forward_1h_with_shield` | `anim_run_forward_onehanded_with_big_shield` | 1 to 25 | 0.8 s |
| `act_run_forward_1h_with_shield_left_stance` | `run_forward_1h_with_shield_left_stance` | `anim_run_forward_1h_with_big_shield_left_stance` | 1 to 25 | 0.8 s |
| `act_walk_idle_unarmed` | `stand_non_combat` | `anim_stand_non_combat` | 1 to 141 | 4.6 s |
| `act_walk_idle_unarmed_left_stance` | `stand_unarmed_left_stance` | `anim_stand_unarmed` | 51 to 99 | 2.2 s |
| `act_walk_idle_1h` | `stand_1h` | `anim_onehanded_guard_idles` | 1 to 134 | 4.5 s |
| `act_walk_idle_1h_left_stance` | `stand_1h_left_stance` | `anim_onehanded_guard_idles_leftstance` | 1 to 134 | 4.5 s |
| `act_walk_idle_1h_with_shield` | `stand_1h_with_shield` | `anim_onehanded_guard_idles` | 805 to 938 | 4.5 s |
| `act_walk_idle_1h_with_shield_left_stance` | `stand_1h_with_shield_left_stance` | `anim_onehanded_guard_idles_leftstance` | 671 to 804 | 4.5 s |

Native plays the same 16 clips on 24 more walk and run codes, and the binder binds them too:

| Codes | Vanilla clips |
|---|---|
| The running-mode idles, `act_run_idle_*` for unarmed, 1h and 1h with shield (both stances) | the six `stand_*` clips above |
| The hand-shield idles and walks, `act_walk_idle_1h_with_h_shld`, `act_run_idle_1h_with_h_shld`, `act_walk_forward_1h_with_h_shld` (both stances each) | `stand_1h_with_shield*`, `walk_forward_1h*` |
| The polearm-with-shield runs and running idles, `act_run_forward_polearm_with_shield` (both stances), `act_run_idle_polearm_with_shield_left_stance`, `act_run_idle_polearm_with_h_shld_left_stance` | `run_forward_1h_with_shield*`, `stand_1h_with_shield_left_stance` |
| The thrown and dagger walks and runs, `act_{walk,run}_forward_{thrown,dagger}` (both stances each) | `walk_forward_1h*`, `run_forward_1h*` |

Facts from the dump (2026-10-14, installed v1.5.5 packages, TpacTool from `E:\Release_v0.5.1\bin`):

- 16 clips resolve to 13 masters, each with the 28 bones of `human_skeleton`.
- The unarmed walk and run reuse one clip for both stances. The 1h and shield codes have their own
  left-stance clips, so binding a right-stance clip to a left-stance code would put the weapon arm
  in the wrong pose.
- The run and idle clips play at 30 frames per second. The walk clips play 36 frames in 1.4 s
  (about 25.7 per second); the clip's own duration, not the frame count, sets that.
- The JSON `duration` field is not the frame span of a long idle master:
  `anim_onehanded_guard_idles` reports 412 but has keys to frame 940. Cut sub-ranges by key time.

### Vanilla clip recipes

From [bannerlord-animation-clip-flags.md](../reference/bannerlord-animation-clip-flags.md):

| Clip | Priority | Flags | Usages | Blend in / out |
|---|---|---|---|---|
| `walk_forward_unarmed` | 0 | `make_walk_sound` | `BipMovIkUsage` (LoopDisplacement 1.8, step points 0.4 and 0.898) | 0.3 / 0 |
| `run_forward_unarmed` | 0 | `make_walk_sound` | `BipMovIkUsage` | 0.3 / 0 |
| `troop_stand_unarmed_1` | 1 | `allow_head_movement` | none | 0.5 / 0 |

`gen_troll_anim_clips.ps1 -CloneByName` copies each new clip from its own vanilla clip, so these
recipes carry over, with two changes: `-TravelScale` scales the LoopDisplacement, and the facial
animation id is cleared on every clone (written for the troll, which has no facial rig). Vanilla
`stand_non_combat` and `stand_unarmed_left_stance` carry `female_custom`; whether orc and goblin heads
used it is unverified.

### Naming

The restyle exports one master per clip, `<prefix>mst_<clip>` (the FBX take name is the master name):
frame 0 is the rest frame and frames 1 to N are the vanilla clip's own range, so a long idle master
such as `anim_onehanded_guard_idles` (940 frames) is never restyled whole. It writes a
`clips_index.json` in the generator's format (master `anim_mst_<clip>`, source 0 to N-1), and
`gen_troll_anim_clips.ps1 -CloneByName` names each clip `<prefix><vanilla clip>` with its range shifted
by one for the rest frame. With prefixes `taom_orc_` and `taom_goblin_`, the longest master is
`taom_goblin_mst_walk_forward_1h_with_shield_left_stance` (55 characters) and the longest clip
`taom_goblin_walk_forward_1h_with_shield_left_stance` (51), under the engine's 63-character limit.

## Motion Spec (approved 2026-10-14)

Mike approved these values; the previews confirmed them before export, and the in-game check passed.

| Parameter | Orc | Goblin |
|---|---|---|
| Spine chain flexion, total over spine, spine1, spine2 | 22° | 28° |
| Neck and head counter-lift (keeps the eyes forward) | -12° | -15° |
| Clavicle drop / protraction | 6° / 8° | 5° / 6° |
| Upper-arm hang (abduction reduced) | 5° | 3° |
| Arm swing scale | 1.3 | 0.9 |
| Elbow bend add | 8° | 12° |
| Pelvis forward tilt | 6° | 10° |
| Knee bend add (contact and passing) | 10° | 16° |
| Stance width per foot | +3 cm | +1.5 cm |
| Foot splay (toe out) | 8° | 4° |
| Stride scale (sets LoopDisplacement) | 0.95 | 0.80 |
| Derived cadence at the same speed | 1.05x | 1.25x |
| Vertical bob scale | 1.2 | 0.8 |
| Lateral sway scale (also scales the pelvis's roll, yaw and pitch about their mean) | 1.6 | 1.1 |
| Idle | slow head turns, hand flex, weight shift | quick glances, hunch pulses, constant weight shift |

Item 1 builds the idle from head turns, a breathing pitch on the upper spine and a pelvis weight shift
(the `idle` block of each variant JSON). The orc's hand flex has no key yet; it belongs with the idle
fidgets of item 5.

**Caps for combat blending:** spine total at most 30°, clavicle at most 10°. Attack and block clips
start from the stance pose, so a larger trunk offset pops on the blend. Values past the caps are
flagged for armour clipping (pauldrons and capes follow the clavicles and spine).

## Build (item 1)

**Restyle:** `tools/blender/restyle_human_clip.py` with `tools/blender/locomotion_variants/{orc,goblin}.json`.
Each clip becomes its own master (`<prefix>mst_<clip>`: frame 0 rest, frames 1 to N the vanilla range), so
the 940-frame guard-idle master is not restyled whole. Per frame: deviation scaling about the cycle mean
(the upper arm by `arm_swing_scale`; the pelvis's whole rotation, roll, yaw and pitch, and its sideways sway by
`sway_scale`; its bob by `bob_scale`), posture turns top-down, pelvis drop for the knee bend, two-bone leg
IK to the vanilla ankle path (widened, stride-scaled, and lowered with the hips while the foot is lifted),
then the Kit's 180° root yaw. Every clip also runs with an identity spec as a self-check (largest
deviation from vanilla: 0.33° to 3.1°, from the leg IK).

**Validation (2026-10-14):** `tools/blender/validate_locomotion_clips.py` re-imports each FBX: 16 of 16 orc
and 16 of 16 goblin clips pass every check.

| Check | Orc | Goblin |
|---|---|---|
| Measured stride ratio, contact frames (spec) | 0.946 to 0.953 (0.95) | 0.791 to 0.813 (0.80) |
| Ankle residual in contact, walk, cm per frame (vanilla 1.66 to 1.71) | 1.56 to 1.61 | 1.34 to 1.43 |
| Ankle residual in contact, run, cm per frame (vanilla 3.45 to 5.15) | 3.29 to 4.80 | 3.04 to 4.42 |
| Lowest ankle or toe | equal to vanilla | equal to vanilla |
| Rest frame against the engine rest | 0.0° | 0.0° |
| Frame rate | 30 fps | 30 fps |

Findings the validator forced:

- **The body import set the scene to 24 fps.** The FBX importer takes the rate from the preview body file;
  the restyle now resets 30 fps after it and refuses to export at any other rate.
- **A swing foot must drop with the hips.** With the ankle on the vanilla path in every frame, the goblin
  run's swing knee folded up to 50° more than vanilla. The lifted foot now drops with the pelvis
  (`SWING_LIFT_M`), and a planted heel or toe keeps the vanilla height.
- **The vanilla walk already has a seam kink** (2.74° velocity change on `r_thigh`). Scaling a motion by s
  scales that kink by up to s, so the loop check allows the source seam times the spec's largest gait scale.

**Previews:** contact sheets in `E:\LOTRAOMAssets\orc_locomotion\previews\{orc,goblin}\` (rows: vanilla
side, restyle side, vanilla front, restyle front; columns at 0, 25, 50 and 75 per cent).

**Staging:** `E:\LOTRAOMAssets\troll_clips_to_import\orc_locomotion\{orc,goblin}\`: 16 FBX each, plus
`clips_index.json` (for the clip generator), `restyle_report.json` and `validate_report.json`.

## Kit Import Checklist

Steps for Mike are marked **Mike**; the rest run with the Kit and the game closed.

Folders (Mike's choice): orc clips in `Race Test\Mordor\Orc\animations`, goblin clips in
`Race Test\Mordor\Goblin\animations`, under the Armory's `AssetSources` (FBX) and `Assets` (compiled).

1. **Mike (done 2026-10-14):** copy each variant's 16 FBX into its `AssetSources` folder (verified
   byte-identical to staging).
2. **Mike (done 2026-10-14):** in the Modding Kit, import each FBX as a **Skeleton Animation**. Create no
   Animation Clip; step 4 writes them. Do not rename anything in the Kit. Save, then close the Kit.
3. **Done 2026-10-14:** wire the masters (the Kit left all 32 EMPTY), per folder:
   `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\wire_anim_master_skeletons.ps1 -Masters "<Armory>\Assets\Race Test\Mordor\Orc\animations" -SkeletonGuid dd7f3586-10ea-47d5-880e-a0c263862217 -BoneNum 28 -Apply`.
   Census after: ok=16 in each folder.
4. **Done 2026-10-14:** clone the clips, once per variant (Windows PowerShell 5.1):
   `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\gen_troll_anim_clips.ps1 -CloneByName -Masters "<Armory>\Assets\Race Test\Mordor\Orc\animations" -ClipsIndex <staging>\orc\clips_index.json -ClipPrefix taom_orc_ -TravelScale 0.95 -SkeletonGuid dd7f3586-10ea-47d5-880e-a0c263862217`,
   dry run, `-Apply` (it fixes the checksums), `-Verify`. The goblin run uses its own folder,
   `goblin\clips_index.json`, `-ClipPrefix taom_goblin_` and `-TravelScale 0.8`. Result: 16 of 16 ok per variant.
5. **Mike (done 2026-10-14):** open the Armory in the Kit and save (it writes the RuntimeDataCache entries),
   then close it.
6. **Done 2026-10-14:** `python tools/check_rdc_entries.py --under "Race Test/Mordor/Orc/animations"` and the
   Goblin folder: 0 clips without an entry (the cache grew by exactly the 32 clips; masters never get one). The
   clips still pass `-Verify` and the masters still read `ok` after the save.
7. **Done 2026-10-14:** bind: `python tools/bind_orc_goblin_locomotion.py` (dry run), `--apply` (backup
   `action_sets.xml.bak-orc-goblin-bind-20261014-132544`), `--check` 0 problems,
   `python tools/audit_action_set_parity.py` exit 0. At 17:08 the same day the review's binder rebound both sets
   with all 42 codes (backup `action_sets.xml.bak-orc-goblin-bind-20261014-170848`, `--check` 0 problems, parity
   exit 0), and the snapshot `docs/reference/lotrlome-armory-snapshot/action_sets.xml` is a byte copy of the
   live file (parity exit 0 on it too). The binder reads each race's clips from its own folder above.
8. In game: the test steps under Testing.

**TpacTool for steps 3 and 4:** ShortJake's TpacTool v0.5.1 at `E:\Release_v0.5.1\bin`, now every tpac
tool's default ([development-machines.md](../reference/development-machines.md)). Proven 2026-10-14 in the
scratchpad: the generator's `-CloneByName` dry run, `-Apply` and `-Verify` exit 0 on a copy of a cave troll
master, and the written clip matches vanilla `walk_forward_unarmed` in every field except the range, the name
and LoopDisplacement (1.71 = 1.8 x 0.95).

## Testing

**Result (2026-10-14):** Mike checked the orcs and goblins in game after `/armory-audit` came back CLEAN:
the animations look good. That check ran with the first 18 rows per set.

**Owed in game for the 24 codes bound later that day:** an orc and a goblin with throwing weapons, with a
dagger, with a hand shield and with a polearm and shield: walk and run in each; and the idle of a running
formation that halts. Each plays a clip that already passed, so a fault would show as a wrong arm or shield
pose, not a broken clip.

The checks, for this and the later items:

- Custom Battle with an orc troop (`mordor_orc_recruit`) and a goblin troop (`goblin_snaga`): walk, run and
  idle in the unarmed, one-handed and shield stances, both stances; a formation march; a charge.
- Diagonal and backward movement in formation: the sideways and backward clips are still human, and the
  engine blends them with the restyled forward clips.
- The transition from walk or idle into an attack and a block (no pop of the trunk or the shield arm).
- Foot slide at walk and run speed. Mike's eye decides.
- Town orcs (human civilian walk and idle with the orc run, by design) and female warriors.
- With DualWield active: its `_dw` sets keep DualWield's sidestep and backpedal clips but inherit the plain
  one-handed forward walk and run, because DualWield writes its forward clips only over vanilla ones.
- `rgl_log`: no `Could not find animation:` for a `taom_orc_` or `taom_goblin_` clip.
- The reinstall gate, after any Armory sync: `python tools/bind_orc_goblin_locomotion.py --check`, or
  `python -m pytest tools/tests/test_bind_orc_goblin_locomotion.py` (its live class). `--check` also fails
  when a race's clip folder is empty, which is what a reinstall leaves (no clips and no rows), unless
  `--allow-no-clips` is passed.

## Key Files

| File | Purpose |
|---|---|
| `tools/blender/restyle_human_clip.py` | Restyles vanilla clips per variant; exports Kit-ready FBX, `clips_index.json`, contact sheets |
| `tools/blender/locomotion_variants/orc.json`, `goblin.json` | The approved motion spec per variant |
| `tools/blender/validate_locomotion_clips.py` | Re-imports each FBX and checks it against its vanilla source |
| `tools/bind_orc_goblin_locomotion.py` | Writes, and with `--check` proves, the rows in the two Armory sets |
| `tools/tests/test_bind_orc_goblin_locomotion.py` | The binder's tests, and the live reinstall gate |
| `tools/read_anim_keyframes_tpac.ps1` | Dumps the vanilla source masters and `clips_index.json` |
| `tools/blender/human_skeleton_engine.json` | Engine rest frames of `human_skeleton` |
| `tools/blender/retarget_mannequin_to_human.py` | Engine-frame rig, Kit-ready exporter and export check (reused) |
| `tools/gen_troll_anim_clips.ps1` | Clones vanilla clip recipes onto the new masters |
| `tools/wire_anim_master_skeletons.ps1` | Wires Kit-imported masters to the skeleton |
| `tools/check_rdc_entries.py` | Confirms each clip package has its RuntimeDataCache entry |
| `tools/audit_action_set_parity.py` | Gates the Armory `action_sets.xml` structure |
| `E:\LOTRAOMAssets\orc_locomotion\src\` | Source JSON (outside the repo) |

## Dependencies

- **The live Armory, unversioned:** the 64 packages (32 masters, 32 clips) in
  `Assets\Race Test\Mordor\{Orc,Goblin}\animations`, their FBX in the same folders under `AssetSources`, their
  RuntimeDataCache entries, and the rows in `ModuleData\action_sets.xml`. A reinstall or an Armory sync from a
  copy without them removes them.
- **Native's `as_human_warrior`:** the binder reads its code-to-clip map, so an engine update that rebinds a
  walk or run code changes the plan; `--check` then reports the difference.
- **`human_skeleton`:** the clips hold its 28 bones in the engine's rest frames
  (`tools/blender/human_skeleton_engine.json`).

## How to Restore the Rows After a Reinstall

1. Run `python tools/bind_orc_goblin_locomotion.py --check`.
2. If it reports a set with no restyled clip on disk, copy the 16 FBX per race back from
   `E:\LOTRAOMAssets\troll_clips_to_import\orc_locomotion\{orc,goblin}\` and repeat steps 1 to 7 of the
   Kit Import Checklist.
3. If the clips are there but rows are missing, close the game and the Kit, then run
   `python tools/bind_orc_goblin_locomotion.py --apply`.
4. Run `python tools/audit_action_set_parity.py`.

## Changelog

- 2026-10-14: the binder binds every walk and run code that plays one of the 16 clips (42 per race, was 18);
  it reads Native with ElementTree (a commented-out row is never read), refuses to drop a bound row without
  `--allow-shrink`, fails `--check` on an empty clip folder, checks every link of each derived chain, and
  exits 2 on bad input.
- 2026-10-14: deep review; the binder gained tests and a live reinstall gate; Mike's in-game check passed.
- 2026-10-14: item 1 imported, wired, generated, Kit-saved and bound in the live Armory (both races).
- 2026-10-14: item 1 built and validated (32 clips), binder written and proven on a copy of the Armory file.
- 2026-10-14: Phase 1 discovery and motion spec. Binding through the Armory's own XSLT ruled out (merge order).

## GitHub Issue

- **Issue:** #791, [Orc and goblin walk, run and idle animations](https://github.com/haterade22/TAOM/issues/791)
- **Status:** Open
