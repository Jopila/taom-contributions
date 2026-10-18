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

**Status (2026-10-18):** Mike's review (2026-10-17) replaced the orc gait with a new motion (see "Motion spec revision
(2026-10-17)") and added two per-soldier orc variants. The review of 2026-10-17/18 then fixed the restyle's planted-foot
clamp and the validator's slide gate (see the RCA), so all 36 orc clips were re-exported on 2026-10-18 and pass the
validator (16 main, 10 B, 10 C). A second Codex pass the same day found the validator and the generator still too
trusting (see "Limp Variants" and the checklist), so the clips were exported once more. The new FBX are staged and were
copied into the live Armory again on 2026-10-18 (at 00:02, at about 00:51 and, after a third pass, at 01:15, backup
stamp 01:15:22; the 01:15 copy is the one imported). **On 2026-10-18 the 36 clips went into the game:** Mike
imported the masters, the clips were generated and Kit-saved, and the binder wrote `as_orc_warrior_b` and
`as_orc_warrior_c` beside the unchanged 42 rows of `as_orc_warrior` (Kit Import Checklist, steps 1 to 7). Mike's
Custom Battle at 09:41 loaded both variant sets and split 125 orcs 38 main, 37 B and 50 C; his verdict on the look is
owed. Goblins are next.

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

This table now describes the superseded orc A; the orc column no longer matches `orc.json`. The goblin column is
current. The orc values in force are under "Motion spec revision (2026-10-17)".

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

## Limp Variants (restyle tool extension, 2026-10-17)

`restyle_human_clip.py` takes two optional spec blocks. A spec without them restyles exactly as before (orc.json: 79 of
the 80 report numbers checked against the staged report match, and every report key matches an unmodified tool's run).

- **`limp`**: an asymmetric gait, a heavy slightly injured brawler. Static posture: `lead_shoulder_side` (defaults to
  `strong_side`; that shoulder is the leading and the low one), `lead_shoulder_yaw_deg` (upper trunk yawed 40/60 over
  spine1 and spine2, neck and head turned back so the face keeps its direction), `low_shoulder_deg`,
  `low_shoulder_lean_deg`, `lead_shoulder_protract_deg`, `low_arm_forward_deg`, `low_arm_swing_scale`,
  `arm_forward_deg`, `knee_bow_deg` or the metric `knee_bow_m`, `knee_max_deg`, `min_track_half_m`. Cycle:
  `strong_side`, `timing_split`, `strong_lift_scale`, `weak_lift_scale`, `weak_lift_max_m`, `weak_toe_down_deg`,
  `weak_swing_out_m`, `weak_trail_m`, `weak_hip_hike_deg` or `weak_hip_hike_m`, `strong_drop_m`, `weak_drop_m`,
  `strong_lean_deg`, `shoulder_roll_deg`, `weight_over_foot`, `foot_outer_edge_deg`. Every key, its meaning and its
  default is in `LIMP_DEFAULTS`.
- **Idles get only `arm_forward_deg`.** A gait clip gets the whole limp; a stand clip gets that one key and the
  `LIMP_DEFAULTS` for the rest (`limp_for_kind`). The stride and stomp keys mean nothing to a standing clip, and a spec
  that mirrors the low shoulder (orc C) would flip the shoulder at every halt, because B and C inherit the main orc's
  idles. The consequence is visible: a B or C soldier changes posture when it halts, from its own gait to the main
  orc's idle.
- **`min_track_half_m` is measured from a fixed line.** The clamp keeps each planted ankle at least that far from the
  clip's MEAN pelvis line (the report's `track_centre_x_m`), and the validator and the achieved metric measure against
  the same line. The first version measured from the current frame's pelvis, which `weight_over_foot` and the sway move
  toward the stance foot, so the clamp pushed planted ankles sideways: the main orc walk's right foot moved sideways at
  1.42 cm per frame against 0.31 in the source. After the fix it moves at 0.18.
- **`run`**: keys that override the top level (and the limp keys) for `run_*` clips. It may not change `stride_scale`,
  because `gen_troll_anim_clips.ps1` applies one `-TravelScale` per variant. The 30 degree trunk flex cap, the clavicle
  cap (per side, drop plus `low_shoulder_deg`, protract plus `lead_shoulder_protract_deg`), a 20 degree cap on
  `lead_shoulder_yaw_deg` and an 8 degree cap on trunk lateral bend (`low_shoulder_lean_deg` plus `strong_lean_deg`)
  are checked on the base spec and on the merged run spec; `--allow-over-cap` lifts them for both. `mirror_sides`
  swaps the strong side and an explicit lead side.

**Uneven timing without foot slide.** The engine moves the agent at a constant speed, so a planted foot must fall back
at a constant rate V in every frame. A time warp alone makes it slide. The tool finds each foot's contact in the source
(the validator's definition), keeps the strong contact at its source phase and moves the weak one to `timing_split`
(frame count and period are unchanged, so LoopDisplacement is too), samples the source at fractional frames (slerp and
lerp of the local transforms), and rebuilds each ankle's forward path: exactly V per frame over the contact plateau (V is
the median planted speed times `stride_scale`), and the source swing profile, read at the source position the time map
plays, between plateaus. The time map is a smooth phase modulation (continuous derivative across both contacts and
the loop seam, so no bone's velocity jumps where the pace changes). A source that misses closure by 1 to under 6
degrees (three vanilla run clips: 1.1, 1.54, 5.21) is closed first over its last 6 frames and the report says so
(`loop_closure`); a worse one is refused. Keeping the strong contact at its phase limits the misalignment against the vanilla side and
back clips, which the engine blends at one shared phase in diagonal movement.

**Report.** Each limp clip's `restyle_report.json` entry lists the contact intervals (frames and phases), `timing_split`,
the planted speed, `step_points` (the vanilla StepPoints mapped through the warp; the vanilla values are in
`tools/blender/vanilla_locomotion_step_points.json`) and an `achieved` block with the measured value of every metric
target. Each step point is mapped in place (X stays the image of vanilla X, Y of vanilla Y, even when the warp
plays one past the loop seam), and the entry carries `step_points_wrapped` when one wrapped. `gen_troll_anim_clips.ps1 -CloneByName -RestyleReport <restyle_report.json>` writes those step
points into the cloned clips (X and Y; Z and W stay) and `-Verify` checks them; the flag was named `-StepPointsFrom`
until the review. The report also sets `-TravelScale` (its `stride_scale`), and a typed value must match it within
0.001 or the run is refused: a hand-typed 1.377 once skated the troll's feet by 12%. The generator refuses a stale
report (see the checklist).

**The validator finds the contact itself.** It judges a limp clip against the warped source, and for each foot it finds
the grounded plateau again, independently, on the exported poses and on the source poses (the restyle's own contact
definitions: `contact_indices`, the longest stance, `plateau_window`). It never takes the frames from the report: the
report's `frames` must cover the plateau found (all but 1 frame), and the gates run over the independent plateau. A
first version compared the clip's reported plateau with the source's whole contact (heel strike and toe-off included),
about twice too loose; the second still believed the report's frames, and a counterexample with two reported quiet
frames and six grounded ones slid 1.5 cm per frame in the omitted frames and passed. Over the plateau it gates the
steady back-rate and its residual, lateral and vertical speed against the source's plateau, and the metrics it can measure
on the clip itself. Coverage is checked both ways: every frame found must be claimed (all but 1), and every frame the
report claims must lie on the plateau found or on one of its two end frames, or the foot fails (`claimed_outside`). A
version that judged only the plateau it found passed a claim of frames 6 and 7 skating 10 cm per frame, because a
foot that stalls, skids or lifts drops out of the found plateau by definition. The landing and take-off frame of a plateau
of 4 or more frames are left out of the plateau gates on both clips and judged by their own two gates: the clip's
sideways speed there may exceed the source's largest, read where the time map plays that frame, by at most 1.0 cm per
frame, and the frame may not move back faster than the planted speed times (1 + the stride tolerance) plus 0.5 cm
(`end_frame_back_cm`). Over the 36 clips the largest sideways excess is 0.21 cm per frame (0.64 before the reference followed the time map)
and the tightest back margin is 0.62 cm per frame (the main walk's right foot: 4.18 against a limit of 4.80). The validator
also applies `limp_for_kind`, so an idle is checked against the arm hang only. Its pure parts are tested in
`tools/tests/test_validate_locomotion_clips_pure.py`.

**Hand clearance.** `hand_clearance_m` (top level, 0 = off) keeps each forearm and hand that far from the thighs, calves
and torso: per frame the tool swings the upper arm outward about world forward (then back, to 20 degrees, if 45 degrees
outward is not enough) by the smallest angle that gives the gap, smooths the correction in time (running max, then
[1 2 1] passes, wrapping at a closed loop's seam) and re-checks. The body is five capsules of fixed radii (thigh 0.085,
calf 0.06, torso 0.14, hand 0.05, forearm 0.04 m) that approximate the male body; they are not measured from the mesh.
`shield_arm_clearance_m` is the larger gap for the shield arm, the LEFT arm in every `with_shield` clip (left stance too:
all four vanilla shield clips raise the left hand 0.2 to 0.45 m against their plain clip). The report row
`hand_clearance` has the min gap per arm before and after and the largest correction; the validator's `clearance` check
fails any frame under the clearance less 0.5 cm.

**Limits found while building it.**

- A longer strong stance needs a longer step, and the leg has to reach it: a run at `stride_scale` 1.0 with
  `timing_split` 0.58 overreached by 4.6 cm. The orc C base (0.85) does not. Use `knee_max_deg`, `strong_drop_m` or a
  smaller stride when a run overreaches; `ik_max_unreached_m` and the achieved block say by how much.
- `weak_trail_m` 0.15 in a run passes at 0.10 on a plain base and trails the weak leg to straight at its swing end; the
  IK clamps it at its reach.
- Step points: every vanilla walk and run clip reads 0.4 and 0.898039 (229/255), the same in the run as in the walk, so
  they are not tied to the run's foot plants. How the engine uses them beyond footstep sounds is UNVERIFIED.

## Motion spec revision (2026-10-17)

Mike's review session replaced the orc gait and added two per-soldier variants. `orc.json` now holds the working
"orc C" motion (the main orc gait, `as_orc_warrior`, 16 clips); `orc_b.json` and `orc_c.json` are the variants (10
clips each: five walks and five runs). The 2026-10-14 orc spec (the Motion Spec table above) is now orc
A and lives only in git history. The goblin spec is unchanged.

| Value | Main orc (`orc.json`) | B (`orc_b.json`) | C (`orc_c.json`) |
|---|---|---|---|
| Clip prefix, action set | `taom_orc_`, `as_orc_warrior` | `taom_orc_b_`, `as_orc_warrior_b` | `taom_orc_c_`, `as_orc_warrior_c` |
| Posture | lowest, most hunched | highest | the middle |
| Spine flex (spine, spine1, spine2) | 9, 14, 17 = 40° | 6, 8, 9 = 23° | 7, 11, 12 = 30° |
| Neck, head flex | 25°, -60° | 12°, -30° | 18°, -45° |
| Pelvis tilt | 12° | 8° | 10° |
| Knee bend add | 30° | 13° | 21° |
| Elbow bend add | 25° | 12° | 18° |
| Arm swing scale | 1.0 | 1.3 | 1.15 |
| Stance width per foot, foot splay | +8 cm, 25° | +8 cm, 25° | +8 cm, 25° |
| Stride scale (`-TravelScale`) | 0.85 | 1.0 | 0.89 |
| Bob scale, sway scale | 1.1, 2.0 | 1.5, 2.0 | 1.3, 2.0 |
| Weak leg, strong leg | left, right | left, right | right, left (`mirror_sides`) |
| Timing split, walk / run | 0.60 / 0.62 | 0.55 / 0.57 | 0.58 / 0.60 |
| Weak-leg trail in the run | 0.15 m | 0.08 m | 0.12 m |
| Idles | the 2026-10-14 idle block | inherited from the main orc | inherited from the main orc |

Decisions:

- **Main orc.** Mike's metric orc spec: wide tracks of about 1.5 times the hip width, toe-out
  25°, a crouch, the neck nearly horizontal with the head tilted up so the eyes look forward, and a limp with a weak
  LEFT leg, a strong RIGHT leg, a low leading right shoulder and 60/40 timing. The run is a lope with a trailing weak
  leg. Arms hang 12° out from the body, plus the hand clearance rule (`hand_clearance_m` 0.08, `shield_arm_clearance_m`
  0.15). Its 6 stand clips keep the 2026-10-14 idle block.
- **Over the combat caps, on Mike's request.** The main orc's spine is 40° (cap 30°) and its clavicle drop plus
  `low_shoulder_deg` is 12° (cap 10°); the clips were restyled with `--allow-over-cap`. The attack blend from a walk
  is owed an in-game look, because attack clips start from the stance pose.
- **Variants.** B is the high orc with the mildest limp (bigger orcs drag less). C is the middle posture with a
  mirrored limp, so the three read differently in a line. C's stride is 0.89 because 0.92 and 0.95 gave a loop-seam
  hitch in the run's non-limp path. Strides are one per variant for walk and run (`gen_troll_anim_clips.ps1
  -TravelScale`).
- **Weapons.** Reviewed in the viewer `tools/blender/show_gait_variants.py --props` with
  `tools/blender/gait_props.json` (the Armory orc scimitar, axe, shields, spear and bow). No weapon or shield
  penetrates the body. The sword tip reaches the floor in the hunched orcs, up to about 12 cm below it in the main
  orc's walk and run (vanilla stays above); Mike accepted this ("this is fine, excellent"). The sword attach angle in
  the viewer is set by eye, not from engine data.
- **Out of scope today.** Bows, crossbows, two-handed weapons and polearms without a shield keep the vanilla walk and
  run clips, so orcs walk like humans with them. Goblins are next.

**Validation (2026-10-18, after the second review pass; first passed 2026-10-17):** every restyled clip passes
`validate_locomotion_clips.py`: 16 of 16 main, 10 of 10 B and 10 of 10 C (`validate_report.json` beside each
`restyle_report.json`, re-read 2026-10-18). The main orc's report lists four cap violations (trunk flex 40.0° over 30,
clavicle 12.0° over 10, each for the base spec and for the run).

**Staging:** `E:\LOTRAOMAssets\troll_clips_to_import\orc_locomotion\orc_main` (16 FBX), `orc_b` (10) and `orc_c`
(10), each with `clips_index.json`, `restyle_report.json` and `validate_report.json`. The 2026-10-14 FBX stay in
`...\orc` as the fallback.

**Tools changed:**

- `restyle_human_clip.py`: the limp, the `run` override, `mirror_sides`, metric targets, loop closure for near-closed
  sources, a smooth (C1) time warp, hand clearance (also on idles), and a pitch of the neck and head about the body's
  side axis (a near-horizontal neck made the FWD-based turn flip 63° in one frame).
- `validate_locomotion_clips.py`: counts declared posture turns in the joints limit, adds the clearance check and the
  warped-source checks; since the review the slide check finds each foot's plateau itself on both clips, gates lateral
  and vertical speed, judges the landing and take-off frames separately, applies `limp_for_kind`, and has pure tests.
- `gen_troll_anim_clips.ps1`: `-RestyleReport` (named `-StepPointsFrom` until the review): step points from the
  report, `-TravelScale` from the report's `stride_scale`, full record validation before anything is written, and a
  `-Verify` that checks STEP (StepPoints) and LOOP (LoopDisplacement) and fails when it checked no step point at all.
  `restyle_human_clip.py` writes the provenance the generator checks (below).
- `bind_orc_goblin_locomotion.py`: the variant sets (see Gait variants).
- `show_gait_variants.py`: the viewer.

## Kit Import Checklist

Steps for Mike are marked **Mike**; the rest run with the Kit and the game closed.

**Revision of 2026-10-17, stage 1 done at 22:44 and repeated at 00:02 on 2026-10-18.** Backup
`E:\LOTRAOMAssets\backups\orc_locomotion_20261017-224440`: both orc animation folders, the 16 clip cache entries,
`rdc_names_before.txt` and `manifest.json` (sha256). Then the 16 main FBX were copied over
`AssetSources\Race Test\Mordor\Orc\animations` (same names), and the B and C FBX into the new folders
`...\Orc\animations_b` and `...\Orc\animations_c`, all hash-verified. After the review fixes the three sets were
re-exported and copied again on 2026-10-18 at 00:02 (hash-verified); the backup
`E:\LOTRAOMAssets\backups\orc_locomotion_20261018-000215` holds the superseded pre-fix main FBX. After the second
review pass the clips were exported and copied once more (backup `...\orc_locomotion_20261018-005147`, stamp 00:51:47,
holds the 16 superseded main FBX); all 36 live FBX match the staging folders by sha256. A third pass changed the
validator and the provenance hash, so the clips were exported and copied once more (backup
`...\orc_locomotion_20261018-011522`, stamp 01:15:22, holds the superseded main FBX); this 01:15 copy is the one
imported. **Rollback to the clips of 2026-10-14**, with the game and the Kit closed:

1. Optional cleanup, while the packages are still in place (the entries are inert once their packages are gone): move
   the 36 cache entries of today's clips out of `RuntimeDataCache`. Each is `<GUID>.rdc`, where the GUID is bytes 8 to
   24 of a `taom_orc_*_anm.tpac` in the three folders (`check_rdc_entries.package_guid` reads it; the tool itself
   prints GUIDs only for packages without an entry).
2. Move `Assets\Race Test\Mordor\Orc\animations_b` and `animations_c`, and their `AssetSources` twins, out of the module.
3. Restore `Assets\...\Orc\animations`, `AssetSources\...\Orc\animations` and the 16 cache entries from
   `...\orc_locomotion_20261017-224440`. Take the entries from the backup: the Kit re-cooked one of them
   (`7F28D977-1C2F-49C7-9715-C60F0FB2FEF4.rdc`, the old `run_forward_1h_left_stance`) at 08:28 against the re-imported
   master, so the live copy no longer matches the old package.
4. Run `python tools/bind_orc_goblin_locomotion.py --apply --allow-shrink`: it drops the variant block and keeps every
   later edit of `action_sets.xml`. (Restoring `action_sets.xml.bak-orc-goblin-bind-20261018-092156` gives the same
   bytes only while nothing else has written the file since 09:21:56.)

The engine opens a cache entry only through the GUID of a package it registers (v1.5.5, 2026-10-18 review), so an
entry whose package is gone is inert, but a restored package needs its own entry back.

**Steps 1 to 7 done on 2026-10-18**; Mike's Custom Battle (step 8) ran at 09:41 and his verdict on the look is owed:

1. **Mike (done 2026-10-18):** Kit REIMPORT of the 16 masters (same names, never rename), and an import of the 10 B
   and 10 C masters. Save and close the Kit.
2. **Done 2026-10-18:** `wire_anim_master_skeletons.ps1` on each of the three folders. The Kit left the 20 new masters
   EMPTY; census after: ok=16, ok=10, ok=10.
3. **Done 2026-10-18 08:33:** the 16 old clip packages (`taom_orc_<clip>_anm.tpac`) moved to
   `E:\LOTRAOMAssets\backups\orc_locomotion_oldclips_20261018-083300`, each first matched by sha256 to its copy in the
   2026-10-17 backup, because `gen_troll_anim_clips.ps1 -Apply` never overwrites.
4. **Done 2026-10-18** (36 written; `-Verify` ok=16, 10, 10, step points checked 10 per folder):
   `gen_troll_anim_clips.ps1` per folder with `-CloneByName`, `-ClipPrefix` and
   `-RestyleReport <staging>\restyle_report.json`; dry run, `-Apply`, `-Verify`. The report supplies `-TravelScale`
   (0.85 main, 1.0 B, 0.89 C); a typed value must match it within 0.001. The generator refuses a report that is stale
   or does not describe the clips: one whose `spec.clip_prefix` is not `-ClipPrefix`; that lacks an entry for a clip of
   the index; that holds ANY entry with an error or a failed export (the restyle drops a failed clip from
   `clips_index.json` but keeps its entry, so checking the index alone let it through); that has a gait (a clip named
   `walk_*` or `run_*`; an entry whose `kind` disagrees is refused) without two step points; whose gait clips disagree on
   `stride_scale`; or that carries no provenance. Provenance is a top-level `spec_sha` (the whole loaded spec) and, on each
   entry, its own `spec_sha` (the spec merged for that clip plus its kind) and `loaded_spec_sha`; every gait entry's
   `loaded_spec_sha` must equal the top-level `spec_sha`, so a partial run that kept older entries beside new ones is
   refused, and so is an idle entry without provenance. The hash behind the top-level `spec_sha` and every entry's
   `loaded_spec_sha` covers the loaded spec plus the sha256 of `restyle_human_clip.py` and
   `retarget_mannequin_to_human.py` (the report lists them as `code_shas`), so a change to the tool with the spec file
   unchanged is not the same provenance. The 2026-10-14 orc report has none and is refused; regenerate, never edit. Numbers are checked as JSON numbers
   before conversion: a null, bool, string, NaN or Infinity is refused, the stride must be finite and above 0, a step
   point finite in [0, 1]. `-Verify` fails when it checked no step point.
5. **Mike (done 2026-10-18):** Kit save (writes the cache entries); then `check_rdc_entries.py` on the three folders:
   0 packages without an entry, and the cache grew by exactly 36. After the save the clips still pass `-Verify`, the
   masters still read ok and no item checksum is stale. The Kit wrote one B master and its clip
   (`run_forward_1h_left_stance`) once more at 09:21:20 to 09:21:22; `-Verify` re-run on all three folders later that
   morning: ok=16, 10, 10.
6. **Done 2026-10-18 09:21:** `bind_orc_goblin_locomotion.py --apply` (writes the variant sets, 24 rows each; the 42
   rows per main set are unchanged), then `--check` with the variants required (0 problems) and
   `audit_action_set_parity.py` (0 gaps), both exit 0.
7. **Done 2026-10-18:** the snapshot refresh, as on 2026-10-14: a byte copy of the live `action_sets.xml` to
   `docs/reference/lotrlome-armory-snapshot/`, `audit_action_set_parity.py --live` on the copy (exit 0), and the
   snapshot README entry. Then `/armory-audit regen`: new masters make the catalogue stale, and the session-start
   ARMORY ART DRIFT banner fires until it is regenerated.
8. **Ran 2026-10-18 09:41 (Mike's Custom Battle):** the TAOM log shows `as_orc_warrior_b=present
   as_orc_warrior_c=present` and 125 orcs split 38 main, 37 B and 50 C; the rgl log has no missing clip, missing set or
   duplicate name. **Owed:** Mike's verdict on the look: mixed gaits in a marching formation, an attack from a walk with
   the over-cap spine, and the shield and sword look.

The 2026-10-14 steps below are the history of the first import.

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

## Gait variants (per soldier)

The C# AnimationVariety feature gives each orc one of several gaits by swapping its action set at spawn. The
binder writes the sets; the feature picks among them. Variant A is the existing `as_orc_warrior`.

| Set | `base_set` | Holds |
|---|---|---|
| `as_orc_warrior_b`, `as_orc_warrior_c` | `as_orc_warrior` | 24 rows each: every `act_walk_forward_*` and `act_run_forward_*` code whose Native clip is one of the 10 clips below, bound to `taom_orc_b_<clip>` or `taom_orc_c_<clip>` |

- **The 10 clips:** `walk_forward_unarmed`, `_1h`, `_1h_left_stance`, `_1h_with_shield`, `_1h_with_shield_left_stance`, and
  the same five for `run_forward_`. Each plays on several codes, as the second table under Discovery explains, and all
  of them are bound.
- **No orc female twins.** The Armory `orc` Monster declares no `female_action_set` (`monsters.xml:419-420`), so a
  female orc spawns on `as_orc_warrior` and an `as_orc_female_warrior_b` would be unreachable; the review removed them.
  The goblin Monster does declare one (`monsters.xml:1392`), so the binder keeps the twin mechanism for the goblin
  variants: a self-closing set whose `base_set` is its variant, like `as_orc_female_warrior`.
- **No idle codes.** A variant never overrides an `act_walk_idle_*` or `act_run_idle_*` code, so it inherits variant A's
  idles through `base_set`.
- **Order matters.** `base_set` copies at parse time, so the generated block sits right after `as_orc_warrior` (and
  therefore after its rows). The block is wrapped in `BEGIN/END gait variants of as_orc_warrior` comments that the tool
  owns. The engine merges a repeated `action_set` id into its FIRST definition (`Module.cs:1472`), so an earlier copy
  with a wrong `base_set` would survive while a later one passed the check. The binder therefore refuses every mode
  (exit 2, bytes unchanged) on a repeated owned id, a variant outside the block or a second block, whether or not a
  block already exists, and a rewrite of the block refuses to delete a set between the markers that the tool does not
  own.
- **Folders** (the `VARIANTS` table in the binder), under the Armory `Assets`: `Race Test\Mordor\Orc\animations_b`
  and `animations_c`, packages named `taom_orc_b_<clip>_anm.tpac` and `taom_orc_c_<clip>_anm.tpac`. The B and C FBX
  are in those AssetSources folders since 2026-10-17 (see Motion spec revision). Goblin variants
  (`Goblin\animations_b`, `as_goblin_warrior_b`) are one more table row each, not yet added.
- **Skip rules.** A variant folder with none of the 10 packages is skipped with a note and never written (none today:
  the orc B and C folders are imported since 2026-10-18, and the goblin variants have no `VARIANTS` row yet). One with
  only some is an error (exit 2) in the plan and `--apply`.

Commands (the same tool; dry run by default):

    python tools/bind_orc_goblin_locomotion.py            # lists the variant sets and rows, or why one is skipped
    python tools/bind_orc_goblin_locomotion.py --apply    # backup action_sets.xml.bak-orc-goblin-bind-<stamp>
    python tools/bind_orc_goblin_locomotion.py --check
    python tools/audit_action_set_parity.py

`--check` fails a variant that is skipped and absent (no clips and no set), which is what a reinstall leaves, unless
`--allow-no-clips` (an install before the Kit import; it then prints `no variant clips on disk yet`). It also fails
when a variant set is in the file but its clips are missing, its rows differ from the plan, it carries an idle or
non-forward code or a row whose package is not on disk, a variant is defined before `as_orc_warrior` or a twin before
its variant, or a twin has its own rows. Until 2026-10-18 an absent variant passed plain `--check` and needed
`--require-variants`, which is gone. `--apply` writes only what is on disk, so its own re-check allows an absent
variant, and it drops a variant's rows only with `--allow-shrink`, like the base sets.

## Testing

**Result (2026-10-14):** Mike checked the orcs and goblins in game after `/armory-audit` came back CLEAN:
the animations look good. That check ran with the first 18 rows per set.

**Result (2026-10-15), with all 42 rows per set:** Mike: the orc and goblin animations work in game, and no
crash so far. The report does not name the weapon classes below, so their check stays open.

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
- The reinstall gate, after any Armory sync: `python tools/bind_orc_goblin_locomotion.py --check`; it also fails
  when the orc variants have vanished with their clips. The pytest alternative is
  `python -m pytest tools/tests/test_bind_orc_goblin_locomotion.py` (its live class). `--check` also fails
  when a race's clip folder is empty, which is what a reinstall leaves (no clips and no rows), unless
  `--allow-no-clips` is passed.

## Key Files

| File | Purpose |
|---|---|
| `tools/blender/restyle_human_clip.py` | Restyles vanilla clips per variant; exports Kit-ready FBX, `clips_index.json`, contact sheets |
| `tools/blender/locomotion_variants/orc.json`, `goblin.json` | The motion spec per race (`orc.json` is the main orc gait since 2026-10-17; `goblin.json` is the 2026-10-14 spec) |
| `tools/blender/locomotion_variants/orc_b.json`, `orc_c.json` | The two per-soldier orc gaits (`taom_orc_b_`, `taom_orc_c_`) |
| `tools/blender/show_gait_variants.py`, `tools/blender/gait_props.json` | The gait viewer; `--props` loads the Armory weapons and shields listed in the JSON |
| `tools/blender/vanilla_locomotion_step_points.json` | Vanilla StepPoints of the walk and run clips (read from the installed tpac), mapped through a limp's warp |
| `tools/blender/validate_locomotion_clips.py` | Re-imports each FBX and checks it against its vanilla source |
| `tools/tests/test_validate_locomotion_clips_pure.py` | The validator's pure parts (contact stats, plateau slide, lateral and vertical speed) |
| `tools/bind_orc_goblin_locomotion.py` | Writes, and with `--check` proves, the rows in the two Armory sets |
| `tools/tests/test_bind_orc_goblin_locomotion.py` | The binder's tests, and the live reinstall gate |
| `tools/read_anim_keyframes_tpac.ps1` | Dumps the vanilla source masters and `clips_index.json` |
| `tools/blender/human_skeleton_engine.json` | Engine rest frames of `human_skeleton` |
| `tools/blender/retarget_mannequin_to_human.py` | Engine-frame rig, Kit-ready exporter and export check (reused); `json_pose_sampler` and `two_bone_ik` are shared with the restyle |
| `tools/tests/test_restyle_human_clip_pure.py` | The restyle's pure parts (spec loading, caps, clip kinds, stale markers) with `bpy` stubbed |
| `tools/gen_troll_anim_clips.ps1` | Clones vanilla clip recipes onto the new masters; `-RestyleReport` writes a limp's step points and sets the travel scale |
| `tools/wire_anim_master_skeletons.ps1` | Wires Kit-imported masters to the skeleton |
| `tools/check_rdc_entries.py` | Confirms each clip package has its RuntimeDataCache entry |
| `tools/audit_action_set_parity.py` | Gates the Armory `action_sets.xml` structure |
| `E:\LOTRAOMAssets\orc_locomotion\src\` | Source JSON (outside the repo) |

## Dependencies

- **The live Armory, unversioned:** the 104 packages: 32 masters and 32 clips in
  `Assets\Race Test\Mordor\{Orc,Goblin}\animations`, and 20 masters and 20 clips in `Orc\animations_b` and
  `animations_c`; their FBX in the same folders under `AssetSources`, their RuntimeDataCache entries, and the rows and
  variant sets in `ModuleData\action_sets.xml`. A reinstall or an Armory sync from a
  copy without them removes them.
- **Native's `as_human_warrior`:** the binder reads its code-to-clip map, so an engine update that rebinds a
  walk or run code changes the plan; `--check` then reports the difference.
- **`human_skeleton`:** the clips hold its 28 bones in the engine's rest frames
  (`tools/blender/human_skeleton_engine.json`).

## How to Restore the Rows After a Reinstall

1. Run `python tools/bind_orc_goblin_locomotion.py --check`.
2. If it reports a set or a variant with no restyled clip on disk, copy the FBX back from
   `E:\LOTRAOMAssets\troll_clips_to_import\orc_locomotion\`: `orc_main` (16) into `AssetSources\...\Orc\animations`,
   `orc_b` and `orc_c` (10 each) into `animations_b` and `animations_c`, and `goblin` (16) into
   `...\Goblin\animations`. Then repeat steps 1 to 7 of the Kit Import Checklist; the orc folders take
   `-RestyleReport` from their own staging folder (`orc\` holds the refused 2026-10-14 report), and the goblins keep the
   2026-10-14 command (`-TravelScale 0.8`).
3. If the clips are there but rows are missing, close the game and the Kit, then run
   `python tools/bind_orc_goblin_locomotion.py --apply`.
4. Run `python tools/audit_action_set_parity.py`.

## Known Follow-ups

Found by the 2026-10-17/18 review and not fixed:

- **Knee-limit pelvis drop.** With `knee_max_deg` set, the pelvis drop that keeps a leg inside its limit can jump near
  full reach. Replace it with a smooth cap and add a pelvis-height step check to the validator.
- **Swing foot drops with the hips.** In the crouched specs the crouch (`knee_bend_deg` 30 in `orc.json`) and the knee
  limit (`knee_max_deg` 160) each lower the swing foot with the pelvis, so the clip's foot touches down about two frames
  before the source's. At the landing frame of the main orc's `run_forward_1h` strong foot the sideways speed is 1.86 cm
  per frame against the source's 1.80 at the same frame. That is not a skid; it is inherent to the approved crouch, and
  the validator's end-frame gate passes it. A cap on how far a swing foot may drop with the hips (for example never
  below half its source clearance) would change every crouched spec's swing path, so it needs its own change and a re-run
  of all 36 clips.
- **Stage-2 scripts need the shared guard.** The stage-1 copy script (a scratch script) had a process guard that failed
  open. Any script that writes the live Armory in stage 2 must import `_gamedir.game_or_kit_running` instead of its own.
- **Sword tip on the floor.** The sword tip reaches the floor in the hunched orcs (up to about 12 cm below it in the main
  orc's walk and run). Mike accepted this on 2026-10-17; it is a record, not an open defect.

## Changelog

- 2026-10-18: install review. Plain `--check` now fails an orc variant that is absent (a reinstall drops its clips and
  set together) unless `--allow-no-clips`, so `/armory-audit` and the live test catch it; `--require-variants` is gone.
  The rollback names every step, the restore names the current FBX sources, and the inventory counts 104 packages.
- 2026-10-18: imported. Mike re-imported the 16 main masters and imported the 10 B and 10 C; masters wired, the 16 old
  clips moved out, 36 clips generated, Kit-saved and bound (`as_orc_warrior_b`, `_c`); snapshot refreshed. Mike's
  Custom Battle at 09:41 split 125 orcs 38 main, 37 B and 50 C.
- 2026-10-18: third review pass. The validator checks coverage both ways and bounds an end frame's backward speed;
  its end-frame sideways reference follows the time map; the idle block is number-checked; the generator requires
  provenance on every entry, and the provenance hash covers the tool's code. All 36 orc clips re-exported and copied
  again (backup stamp 01:15:22). Still not imported.
- 2026-10-18: second review pass. The validator finds each foot's plateau itself and judges landing and take-off frames
  with their own gate; `low_arm_swing_scale` and other numbers refuse bools and non-finite values; the binder refuses
  any foreign set inside the generated region; the generator checks every report entry, classifies gaits by name and
  requires provenance (`spec_sha`, `loaded_spec_sha`); the restyle's clamp has a test that fails with the old
  reference. All 36 orc clips re-exported and copied again (about 00:51). Still not imported.
- 2026-10-18: review fixes. The restyle's `min_track_half_m` clamp measures from the clip's mean pelvis line, so planted
  ankles no longer slide sideways; the validator compares plateau with plateau and gates lateral and vertical speed (new
  tests); idle clips get only `arm_forward_deg` from the limp; step points are mapped in place. `-StepPointsFrom` became
  `-RestyleReport`, takes `-TravelScale` from the report and refuses a stale one. The binder guards duplicates, a
  second block and a foreign set between the markers, gained `--check --require-variants`, and lost the orc female
  twins. All 36 orc clips re-exported (16, 10, 10 pass) and re-copied to the live Armory `AssetSources` at 00:02 (backup
  `orc_locomotion_20261018-000215`). Still not imported.
- 2026-10-17: Mike's review replaced the main orc gait (`orc.json`) and added the B and C variants (36 clips, all pass
  the validator). Restyle, validator, clip generator and binder extended, viewer added. Stage 1 of the Armory copy
  done (backup, FBX in place, hash-verified); the Kit import and binding are owed.
- 2026-10-14: the binder binds every walk and run code that plays one of the 16 clips (42 per race, was 18);
  it reads Native with ElementTree (a commented-out row is never read), refuses to drop a bound row without
  `--allow-shrink`, fails `--check` on an empty clip folder, checks every link of each derived chain, and
  exits 2 on bad input. The restyle shares its leg IK and a JSON pose sampler with the retarget tool (about 5 s
  per variant, was 31 s; parity held on all 32 clips). `2952ccc54`.
- 2026-10-14: deep review; the binder gained tests and a live reinstall gate; Mike's in-game check passed.
  Committed with item 1 in `776054c0c`.
- 2026-10-14: item 1 imported, wired, generated, Kit-saved and bound in the live Armory (both races).
- 2026-10-14: item 1 built and validated (32 clips), binder written and proven on a copy of the Armory file.
- 2026-10-14: Phase 1 discovery and motion spec. Binding through the Armory's own XSLT ruled out (merge order).

## GitHub Issue

- **Issue:** #791, [Orc and goblin walk, run and idle animations](https://github.com/haterade22/TAOM/issues/791)
- **Status:** Open (items 2 to 6 remain; the in-game check of the 24 codes bound later on 2026-10-14 is owed, and so is the whole 2026-10-17 revision from the Kit import on)
- **Review:** [rca-orc-goblin-locomotion-2026-10-14.md](../reviews/rca-orc-goblin-locomotion-2026-10-14.md)
