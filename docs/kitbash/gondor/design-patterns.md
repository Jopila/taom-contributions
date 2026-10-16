# Gondor design patterns

How Gondor parts join, measured on 2026-10-15. Per-part numbers are in [meshes.md](meshes.md).
Every offset below is in the **Blender import frame** unless it says "engine frame".

## The frame rule (proved)

**The engine sees every Gondor kit FBX rotated 180 degrees about Z relative to Blender's FBX
import**, the same rule as Erebor. A point at (x, y, z) in Blender is at (-x, -y, z) in the part's
engine frame; a prefab offset (x, y, z, yaw) converts to (-x, -y, z, yaw).

**Proof 1: three authored prefabs rebuilt under each reading**
(`blender_render_erebor_prefab.py --kit gondor --mode both`, `taom_gondor_buildings.xml`):

| Rebuilt prefab | Kit parts placed | With the rule | Without it |
|---|---|---|---|
| `p_gondor_trebuchet_house_01` | 58 | 9.03 x 7.52 x 15.40 m, a closed tower with a hip roof | 14.44 x 12.44 m, the four roof panels splayed outward |
| `p_gondor_minas_leftmain_01` | 100 | 10.41 x 14.26 x 10.81 m, a closed house inside a trim parapet | 10.41 x 14.38 x 12.94 m |
| `p_gondor_minas_wallhouse_01` | 72 | 9.17 x 8.75 x 5.51 m, coherent but open | 9.17 x 8.75 x 7.02 m, panels standing out sideways |

The wallhouse does not close with kit parts alone: it also places 7 `sm_numenorean_wall_support_a01`,
4 `sm_mordor_wall_column_a03` and 2 `editor_plane_low` (measured). The trebuchet house adds vanilla
wooden platforms and a balcony, the left main house 7 Numenorean and Mordor window walls.
Two tool limits also show in the wallhouse (the frame verdict does not depend on it): 9 of its
placements carry a `non_orthogonal_rotation_mat`, which the rebuild and the miner read as
unrotated (they parse `rotation_euler` only; 1,400 of the 51,491 Gondor kit placements are under
one, a follow-up), and until the 2026-10-15 review the rebuild dropped its four bare-`.lod` gable
plates (now 125 meshes placed, dimensions unchanged).

**Proof 2: file level** (`blender_kit_frame_probe.py`, then `fbx_frame_compare.py`). Three source
objects were exported untouched with the assembler's FBX settings and compared raw against their
sources. Both files carry the same axis header (Y up, front Z, coord X, `UnitScaleFactor` 1, so
1 unit = 1 cm). The sources store metre-valued vertices under a node rotation X -90 (and Y -90 on
the ramp) and a scale of 100; the export bakes that rotation and scale into centimetre-valued
vertices under an identity node. The bounds in the FBX frame are equal:

| Object | Source node bounds | Exported bounds |
|---|---|---|
| `gondor_wall_3m_a_clean.wall` | (-1.5, 0, -0.25) to (1.5, 3.0, 0.25) | identical |
| `gondor_stairs_3m_a_clean.wall` (asymmetric) | (-1.3, -0.05, 0.01) to (1.3, 3.0, 3.01) | identical |
| `gd_ramp_large_a1` (asymmetric, node Y -90) | (-40, -20, -7.063) to (40, 40.3, 7.063) | identical |

So an exported building keeps its internal joins and lands turned 180 degrees as a whole, as
Erebor's did. **Why the turn exists** (verified from the editor DLL's FBX importer in the
2026-10-15 review, native decompile): for a Y-up file the importer converts the scene to Z up, front
+Y, so a file point (x, y, z) arrives as (-x, z, y), where Blender's importer gives (x, -z, y). Never
change the exporters' axes or `bake_space_transform` (`kitbash_kits.FBX_EXPORT`); a Z-up export would
skip the conversion.

**Shipped buildings differ.** Of the 88 objects in `buildings/`, 53 import with an identity node,
29 (`heroBuildingA/B`, `BuildingC` to `F`) at rotation X 90 and scale 0.01 (the files with
`UnitScaleFactor` 100), and 6 with other node rotations or scales (for example
`gondor_building_citadel_a1` at 0.8 x 0.8 x 0.96). Check a shipped building's frame on its own
before using it as a reference or a prop.

## Join matrix (tested)

Mined from 51,491 placements in 576 prefabs (`gondor_prefab_joins.json`), converted with the frame
rule and placed in Blender (`blender_test_erebor_joins.py --kit gondor`). **Z-fight** is shared
surface between two placements with normals facing the same way (visible); **butt** is the same
with opposite normals (hidden). Faces shared between sub-objects of one part are authored and
are not counted.

| Join | Placement (second part relative to the first) | Mined | Z-fight m2 | Butt m2 | Verdict |
|---|---|---|---|---|---|
| Wall run | `wall_3m_a` at (-3, 0, 0) yaw 0 | n 915, 88 prefabs | 0 | 1.50 | clean |
| Storey stack | `wall_3m_a` at (0, 0, 3) | n 1,057, 88 prefabs | 0 | 1.75 | clean |
| Door then window | `wall_3m_window_a` at (-3, 0, 0) from `wall_3m_a_door_a` | n 37, 23 prefabs | 0 | 1.85 | clean |
| 90 degree corner | `wall_3m_window_a` at (1.25, 1.25, 0) yaw -90 | n 102, 55 prefabs | **3.38** | 0.21 | **risky**: see below |
| Trim on the wall | `wall_trim_3m_c` at (0, 0, 3) yaw 0 on `wall_3m_a` | n 175, 37 prefabs | 0.13 | 0 | clean (0.13 is the stonebrick top) |
| Trim corner | `wall_trim_corner_c` at (1.75, 0, 0); next run at (1.75, 1.5, 0) yaw 90 | n 271 and 228, 44 prefabs | 0 | 0 | clean |
| Paving grid | `ground_3m_a` every 3 m | n 1,242 and 958, 69 prefabs | 0.03 | 0.75 | clean |
| Wall on paving | `wall_3m_a` at (-1.5, 0.25, 0) yaw 90 | n 54, 41 prefabs | 0.88 | 0.13 | check: probably the hidden underside at z 0 (inferred) |
| Platform grid | `ground_platform_3m_a` every 3 m | n 216, 27 prefabs | 0.02 | 4.54 | clean |
| Roof strip run | `roof_a_30_1-5m_straight` every 1.5 m | n 419, 61 prefabs | 0 | 0.08 | clean |
| Roof outer corner | `roof_a_30_1-5m_corner_out` at (2.45, 0, 0) | n 131, 45 prefabs | 0 | 0 | clean |
| Roof on wall | `roof_a_30_1-5m_straight` at (0.75, 0, 3.25) yaw 180 | n 7, 29 prefabs | 0 | 0 | low evidence; re-test per building |
| 45 degree roof run | `roof_a_45_3m_straight` every 3 m | n 393, 24 prefabs | 0 | 0.21 | clean |
| 45 degree gable | `roof_a_45_3m_side_a_clean` twice at one origin, yaw 0 and 180, with their bare-`.lod` infill plates | n 66, 25 prefabs | 0 | 2.65 | clean (Lond Cirion rule 5) |
| Pillar drums | `pillar_mid_a1` every 1 m in z | n 137, 24 prefabs | 0 | 0.19 | clean |

**The authored corner z-fights.** Panel B spans x 1.0 to 1.5 and panel A ends at x 1.5, so each
panel's 0.5 x 3 m end cap lies in the other's outer face plane, both facing out: 3.0 m2 per corner
per storey, plus the stonebrick relief. The authors use it in 55 prefabs. Our buildings put a
`gondor_column_3m_a_1x1` on every corner and storey, 2 % wider in plan (1.06 m): it encloses the
end caps, and its cap faces clear the 6 m trim's faces (both sat at about 0.51 m, measured).

## Placement rules learned building the six (2026-10-15)

- **Lift a trim by its measured cover, not a fixed amount.** A trim's top is a moulding: over the
  wall band it sits below its bbox top (the 3 m trim has V-grooves between its blocks, the 6 m trim
  slopes 2 cm), so "top flush with the storey line" leaves the wall top coplanar with it and open to
  the sky. The assembler measures each trim by down-rays every 2 cm (lift 14.9 mm for
  `wall_trim_3m_b_clean`, 25 mm for `wall_trim_6m_b`).
- **Orient a one-sided part from its bounds.** `buttress_a_clean` lies wholly on one side of its
  mounting plane (y -0.55 to 0.01); a vertex-count side test (`lc.wall()`'s `detail_side()`) put every
  buttress inside its wall.
- **A 45 degree hip roof on a 6 x 6 m footprint** is four `roof_a_45_3m_corner_out` quadrants with
  the origin on each footprint corner at the eave, turned so the measured high point is the centre.

## Lond Cirion rules, checked against this catalog

| Rule ([lond-cirion-buildings.md](../lond-cirion-buildings.md)) | Measured | Verdict |
|---|---|---|
| 1. A part is a prefix; LODs per sub-object; one `bo_` | 330 kit parts; 3 without `bo_` (`gondor_castle_wall_tower_L1_A_top`, `gondor_stair_trim_top_dirty`, `gondor_stair_trim_bot_dirty`); one part carries a Blender `.NNN` copy of its body (`bo_gondor_castle_wall_tower_L1_A_int.001`, 36 tris beside the real 1,166) | holds; the tools keep the plain name |
| 2. Decal trap: `gondor_wall_trim_6m_a.decalleak` hangs 1.474 m low | up-axis -1.734 against the body's -0.26: 1.474 m | holds; 161 decal objects in all folders, 124 in the kit (the old "104" counted 15 families) |
| 3. Bare `.lod` trap: `gondor_roof_a_45_3m_side_a_clean.wall.lod` | found, plus a second case: `gondor_stairs_3m_b_clean.wall.lod` | holds; the rule now covers two parts |
| 4. Origin-anchored parts | 44 kit parts have pivot `outside` on all three axes | holds; place by pivot |
| 5. Gable seating on the infill plate | the mirrored 45 degree halves and their plates close at one origin (butt 2.65 m2, no z-fight) | holds |
| 6. Trims straddle the wall line | `wall_trim_6m_a` depth plus or minus 0.51 m on a plus or minus 0.25 m wall: 0.26 m proud; the trim-on-wall join sits on the line | holds |
| 7. Grip the structural body | 91 parts have a `.wall` sub-object; the catalog records its bounds as `structural` | holds |
| `roof_a_30_*` is 26.57 degrees | rise 1.5 m over a 3.0 m run (`roof_a_30_3m_straight`); the catalog's dominant facet reads 20 degrees, the shingle faces | holds |
| No door leaf, no glazing | kit `.door` and `.window` sub-objects are 0.55 to 0.6 m deep (stone surrounds); no kit part has a glass material | holds for the kit. Outside it: 6 stained-glass panes in `Comprehend` (492,032 tris each) and `doors_windows_c` on 32 objects in 14 shipped building files |

## Where an older doc or count was wrong

- **The prompt's counts** are raw object prefixes. Grouped, `meshes/` is 263 parts (266 raw minus
  the two excluded files and `.NNN` duplicates); `walls/` is 67. The mine finds 51,491 placements
  of 328 kit parts, against the prompt's 51,358 of 342 pieces (inferred: the prompt's filter also
  counted non-kit Gondor names).
- **lond-cirion-buildings.md "identity/metres"** describes the shipped `buildings/` files (and only
  29 of their objects), not the kit parts, which store metre-valued vertices under X -90, scale 100.
- **"104 catalog names contain decal"** was the 15-family count; the full kit has 124.
- **The bare `.lod` trap** has a second case (above).

## Recipes from the authored buildings

| Prefab | Footprint | Kit parts (count) |
|---|---|---|
| `p_gondor_trebuchet_house_01`, tower | 9.03 x 7.52 m, 15.4 m tall | `column_corner_trim_a_clean` 16, `wall_trim_6m_b` 12, `buttress_a_dirty` 8, `wall_6m_arch_b_clean` 4, `roof_a_45_3m_corner_out` 4, `roof_a_30_3m_side_a_clean` 4, `ground_3m_a_normal` 4, `wall_6m_a_dirty` 2, `wall_6m_window3_a_dirty` 2, `wall_trim_3m_b` 2 |
| `p_gondor_minas_leftmain_01`, house | 10.41 x 14.26 m | `wall_trim_3m_c_clean` 43, `wall_3m_a_clean` 22, `ground_3m_a_dirty` 10, `roof_a_45_3m_straight` 7, `wall_trim_corner_c_clean` 6, `wall_3m_a_door2_a_dirty` 4, `wall_3m_window2_a_clean` 4, `ground_3m_curv_a_dirty` 2, `wall_3m_curv_a_dirty` 2 |
| `p_gondor_minas_wallhouse_01`, house in a wall | 9.17 x 8.75 m | `wall_3m_a_dirty` 16, `wall_trim_3m_c_clean` 12, `buttress_a_clean` 8, `wall_trim_6m_b` 8, `column_3m_a_1x1` 8, `wall_3m_a_clean` 5, `roof_a_45_3m_side_a_clean` 4, three door and window panels of 3 each, `wall_6m_a_clean` 2 |

## Roles

| Family | Role |
|---|---|
| Wall panels (3 m, 6 m, straight and curved) | Storey walls, 3 m tall, 0.5 m thick, centred on the wall line. Plain `_a`, door, door2, window, window2, window3, arch; `_clean`, `_dirty`, `_cracked`, `_dest_*` weathering |
| Trims | String courses at each storey line, cornices, parapets; corner trims at the corners |
| Roofs | `roof_a_30_*` (26.57 degrees) and `roof_a_45_*` (45 degrees): straight panels, 1.5 m strips, outer corners, gable sides, edge strips; one circular 6 m roof |
| Pillars, columns | Drums stacked every 1 m; corner columns that also hide the corner z-fight |
| Buttress | Wall buttresses, 6 m tall, mounted from a plane at y 0 (rule 4) |
| Stairs | 3 m and 12 m straight, 3 m curved; measure which end is the top (rule 4) |
| Ground and paving | Floors and plazas on the 3 m grid; raised platforms |
| Railing, bridge | Balustrades; a 15 m bridge with extensions and merlons |
| Castle (meshes, `walls/`) | Castle walls, towers, ramps; not house parts |
| Bricks, rubble | Greebles: the mine shows `gondor_stone_brick_a` placed 5,908 times in 25 prefabs, mostly scaled and tilted |
| Statues, `Comprehend`, ships | Props only, and only when a brief asks |

**Floors read as floors (inferred from material names; textures do not resolve in Blender).** The
roofs use `M_Gondor_Roof_Tiles_A`; the paving uses `gondor_tiles_a_*` and `gondor_bricks_a_*`. No
roof material is on a floor part, so the Erebor `_C1` problem should not repeat.

## Parts not classified

- `gondor_wall_6m_a_dest_a`: only an 18-tri `.lod4` exists, no base.
- `gondor_castle_wall_tower_l3_b`: no `.wall` base; its `.wall.lod3` and `.wall.lod6` probably
  belong to the bare-prefix base (equal bounds).
- 16 `Comprehend` props (`GondorStained1` to `6`, `StainedM1` to `6`, `IMRA`, swans): 492,032 tris
  each at a node scale of 0.002 to 0.007. Not usable as building props without decimation.

## Proposed buildings (recommended order)

Mike asked for all six in one FBX (2026-10-15). As built: the 6 m gable depth of
`lc.gabled_roof()` set the townhouse (12 x 6), the barracks (24 x 6) and the hall (18 x 6); the
tower is 6 x 6 m so four hip quadrants close its roof; the hall has engaged pillars on its walls
instead of a portico, which would need a second roof run. The built set, its recipes and probe
results: [../gondor-buildings.md](../gondor-buildings.md).

| # | Building | Why in this order |
|---|---|---|
| 1 | **Town house**, 9 x 6 m, one storey, 26.57 degree gable roof | Smallest; proves the corner fix, the roof on the wall and the gable |
| 2 | **Townhouse**, 12 x 9 m, two storeys, 45 degree roof, string course | Adds the storey stack and the trim line |
| 3 | **Watch tower**, about 9 x 7.5 m, four storeys, hip roof, buttresses | Follows `p_gondor_trebuchet_house_01`; adds outer roof corners and corner columns |
| 4 | **Barracks**, 18 x 9 m, two storeys, buttresses | A longer run with door and window rhythm; tests the budget cuts |
| 5 | **Hall or temple**, 15 x 12 m, pillar portico, 45 degree gable | Pillars with base and cap; a large gable |
| 6 | **Market arcade**, 12 x 6 m, open 6 m arches | Open frontage; exercises the arch panels |

## Update rule

When a building teaches something new (a join, a pivot, a trap), add it here and in
[../gondor-buildings.md](../gondor-buildings.md) in the same change.
