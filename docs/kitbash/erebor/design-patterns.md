# Erebor design patterns

How the Erebor kit pieces join, measured on 2026-10-15 and proved against the buildings the
original authors made with them. Per-piece sizes, pivots and openings are in
[meshes.md](meshes.md). Everything here is in the **Blender import frame** unless a line says
"engine frame".

**Evidence base:**

- **Pieces:** `tools/oneoff/blender_catalog_erebor.py` (bounds, pivots, slopes, openings).
- **Authored joins:** `tools/oneoff/mine_erebor_prefab_joins.py` read 25,332 kit placements in
  324 TAOM_Map prefabs. The richest are `taom_erebor_kitbash.xml` (`dwarf_house_a`,
  `dwarf_house_b`, `dwarf_barracks`, `dwarf_castle_wall_c`, `dwarf_tower_ramp_a`) and the
  Soisson, Shogun and Fink housing prefabs.
- **Rebuilds:** `tools/oneoff/blender_render_erebor_prefab.py` rebuilt those prefabs from the
  source FBX (renders in `E:\LOTRAOMAssets\_export\erebor\prefab_renders\`).
- **Join tests:** `tools/oneoff/blender_test_erebor_joins.py` placed every join below and measured
  it (renders and `joins.json` in `E:\LOTRAOMAssets\_export\erebor\joins\`).

## The frame rule (verified)

**The engine sees every Erebor source FBX rotated 180 degrees about Z relative to Blender's FBX
import.** A point at (x, y, z) in Blender is at (-x, -y, z) in the piece's engine frame.

| Rebuilt prefab | Pieces | Size with the rule | Size without it |
|---|---|---|---|
| `dwarf_house_a` | 72 | 12.79 x 6.99 m, a closed gable house | 12.79 x 12.05 m, roof panels splayed outward |
| `dwarf_house_b` | 156 | 15.79 x 9.99 m, a closed hipped hall | 21.05 x 15.05 m, panels and corners apart |
| `dwarf_barracks` | 378 | 16.49 x 22.49 m | 21.28 x 27.05 m |

Two independent checks agree: the 1.5 m hip corner (`Roof_Cor_Out_B1`) only rises toward its
panel row with the rule, and the `Castle_Tower_B1` bastion only lands on the merlon face with it.

**How to use it:**

- **An offset read from a prefab** converts as (x, y, z, yaw) to (-x, -y, z, yaw); a full matrix
  M converts as C M C with C = Rz(180).
- **A building assembled and exported in Blender** keeps its joins and arrives in the game turned
  180 degrees as a whole, which only changes its heading. Verified at the file level:
  `tools/oneoff/fbx_frame_compare.py` shows an exported piece stored with the same bounds and axis
  header as in its kit source file ([erebor-buildings.md](../erebor-buildings.md)). The first Kit
  import is the editor-side confirmation.
- **The prefab euler order** is the engine's `ApplyEulerAngles`: about Z, then X, then Y, so
  R = Rz Rx Ry. Almost every kit placement is Z-only.

## Measured conventions

| Convention | Value |
|---|---|
| Units, axes | metres, Z up |
| Plan grid | 3 m. Wall pivots sit on grid lines; a footprint is a whole number of 3 m bays between wall centrelines |
| Storey | 3.0 m: walls 0 to 3, the next storey's walls and floor at z 3 |
| Wall thickness | 0.50 (`_A`), 0.54 (`_B`, `_C`), 0.60 to 0.62 for door and window frames; both faces identical |
| Floor | `Ground_3m_*` top of slab at z 0.10 to 0.12 above its pivot |
| Roof pitch | 26.57 degrees (1.5 m over 3 m), every panel |
| Eave | `Roof_Str` pivot on the wall centreline at the wall top: **no overhang** |
| Ridge or deck | eave + 1.5 m (3 m set), eave + 0.75 m (1.5 m set) |
| Castle module | 20 m long, 6.2 m thick, walk at z 6, outer face -Y |

## Join matrix (tested)

Measured in Blender with each join placed as written. **Z-fight** is shared surface with both
normals the same way, the in-game flicker risk. **Butt** is shared surface back to back, a hidden
joint. **Intersecting** counts triangle pairs that cut each other (designed overlaps show here).

| Join | Rule (Blender frame) | Evidence | Z-fight m2 | Butt m2 | Intersecting |
|---|---|---|---|---|---|
| Wall run | next wall at x + 3 | `dwarf_house_a` | 0 | 1.52 | 26 |
| 90 degree corner | walls end on the corner point (x 1.5, y 0), second wall yaw 90 at (1.5, 1.5); `Clmn_3m_C` on the corner, `Clmn_Top_D` x1.1/1.1/1.2 at z 3 | `dwarf_house_a` | 0.74 | 0 | 318 |
| Chamfer corner | `Corn` at (1.5, 3) yaw 270 after a wall at the origin; next wall yaw 90 at (4.5, 4.5); `Ground_3m_A1_Corner` at (3, 1.5) | `dwarf_house_b` | 2.30 | 1.58 | 37 |
| Storey stack | same wall at z + 3 | `dwarf_barracks` | 0 | 1.50 | 2 |
| Beam courses | `Beam_3m_B` at z 0 and `Beam_3m_C` (or `_A`) at z 3, on the wall pivot | `dwarf_house_a`, `_b` | 0.27 | 0 | 56 |
| Pilaster | `Clmn_3m_A` x1.5/1.5/1 on a wall seam, `Clmn_Top_F` x1.5/1.5/1.2 at z 0 and z 3 | `dwarf_house_a` | 0.91 | 1.52 | 477 |
| Eave on wall | `Roof_Str_*1` at (0, 0, 3) on a wall at the origin, rising inward | `dwarf_house_a` | 0 | 0 | 9 |
| Panel row | next panel at x + 3 | `dwarf_house_a` | 0 | 0.17 | 0 |
| 6 m gable | panels at y 0 yaw 0 and y 6 yaw 180; `Roof_Side_Str_A1` yaw 90 at (0, 3, 4.5); end walls yaw 90 at x - 1.5; `Roof_Side_A1` on them at z 3, yaw 0 at y 1.5 and yaw 180 at y 4.5 | `dwarf_house_a` | 0.02 | 5.43 | 235 |
| Flat deck | `Roof_Top_A1` at (0, 4.5, 4.5) behind a panel at the origin; `Roof_Side_Str_A1` yaw 90 at (0, 3, 4.5) frames it; `Roof_Side_Cor_Out_A1` at (1.5, 0, 3) is the rib on the panel seam | `dwarf_house_b` | 0.02 | 0 | 80 |
| Chamfer roof | `Roof_Cor_Tri_A1` on the `Corn` pivot at z 3, yaw = `Corn` yaw + 90 | `dwarf_house_b` | 0 | 0.06 | 62 |
| 1.5 m hip corner | `Roof_Cor_Out_B1` at (1.5, 0) beside a `Str_A2` at the origin; the side panel is `Str_A2` yaw 90 at (1.5, 3.0) | Soisson and Shogun housing, 1,692 placements | 0 | 0 | 39 |
| Floor | tiles every 3 m; `Ground_Trim_3m_A1` on the seam (x 1.5) | `dwarf_house_b` | 0.04 | 0.30 | 286 |
| Plinth | two `Ground_6m_A1` 6 m apart under the walls | `dwarf_house_a` | 0 | 19.18 | 735 |
| Castle run | next `Castle_Wall_A1_Str` at x + 20 | module length | 0.17 | 72.48 | 13 |
| Castle gate | `_Gate` at x + 20 | module length | 0 | 72.49 | 29 |
| Castle ramp | `_Ramp` at x + 20 continues a wall at its low end | module length | 0.33 | 72.50 | 21 |
| Bastion | `Castle_Tower_B1` at (1, -3, 0) | `dwarf_castle_wall_c` | 2.45 | 70.00 | 197 |
| Wall stairs | `Castle_Wall_Stairs_A1` yaw 180 at (8.5, 4.5, 0) | `dwarf_castle_wall_c` | 0 | 107.75 | 957 |
| Tower and ramps | `_Ramp` at (-14, 0.5) yaw 0 and (14, 0.5) yaw 180, both rising to the tower | `dwarf_tower_ramp_a` | 11.00 | 77.65 | 703 |
| Terrace row | `Platform_6m_A2` yaw 90 every 6 m | `dwarf_platform_b` | 0 | 6.97 | 940 |

**Merlons and bastion merlons** always take their parent's exact transform (they share its pivot).

**The z-fight rows are authored and accepted:**

- **Chamfer, 2.30 m2:** the downward bottom faces of the floor triangle and the chamfer wall coincide at grade, where nothing sees them.
- **Corner, 0.74 m2, and pilaster, 0.91 m2:** the column's faces sit on the wall faces it covers.
- **Bastion, 2.45 m2, and tower, 11.0 m2:** the bastion and the ramps run into the wall and the tower.

A merged building should still delete faces that are fully buried.

## Recipes from the authored buildings

Placements below are the authored ones converted to the Blender frame.

### Gable house, 12 x 6 m, one storey (`dwarf_house_a`, 72 pieces)

- **Plinth:** `Ground_6m_A1` at (-1.5, 3, 0) and (4.5, 3, 0).
- **Walls** (`_C` dressed block):
  - Long sides at y 0 and y 6, pivots x -3, 0, 3, 6.
  - Ends at x -4.5 and x 7.5, yaw 90, pivots y 1.5 and 4.5.
  - One `Door_C1` per long side; `Win_C3` and `Win_C2` in the other bays.
- **Corners:** `Clmn_3m_C` with a `Clmn_Top_D` cap (x1.1/1.1/1.2) at z 3.
- **Door bays:** `Clmn_3m_A` pilasters with `Clmn_Top_F` base and cap.
- **Courses:** `Beam_3m_B` on every wall at z 0, `Beam_3m_C` at z 3.
- **Roof:**
  - `Roof_Str_B1` (stone) at z 3 along y 0 (yaw 0) and y 6 (yaw 180).
  - `Roof_Side_Str_A1` yaw 90 along the ridge at (x, 3, 4.5).
  - `Roof_Side_A1` gable wedges on both end walls.

### Chamfered hipped hall, 15 x 9 m, one storey (`dwarf_house_b`, 156 pieces)

- **Walls** (`_A` brick):
  - Long sides at y 0 and y 9 over x -4.5 to 4.5.
  - Ends at x -7.5 and 7.5 over y 3 to 6.
  - Four chamfer corners: `Corn_A` at (-4.5, 3) yaw 180 and (4.5, 3) yaw 270; `Corn_Win_A` at (-4.5, 6) yaw 90 and (4.5, 6) yaw 0.
  - Doors at both ends and mid-front; `Win2_A1` and `Win_A3` in the other bays.
- **Floor:** `Ground_3m_A2` on the 3 m grid, `Ground_3m_A1_Corner` in the four chamfer cells, trims on every seam.
- **Roof:**
  - `Roof_Str_A1` (slate) at z 3 on every straight wall.
  - `Roof_Cor_Tri_A1` on each chamfer.
  - Flat `Roof_Top_A1` deck at z 4.5 down the centre line y 4.5.
  - `Roof_Side_Str_A1` frames the deck.
  - `Roof_Side_Cor_Out_A1` ribs on every panel seam.
  - `Ground_Trim_Corner_A1` studs at the rib heads (z 4.4) and on the floor seam crossings (z 0).
- **Courses:** `Beam_3m_C` at z 0 and `Beam_3m_A` at z 3, scaled 1.5 along X and turned 45 degrees across the chamfers.
- **Seams:** `Clmn_3m_B` pilasters on the wall seams.

### Two storeys (`dwarf_barracks`, 378 pieces)

The hall pattern stacked:

- **Walls** at z 0 and z 3.
- **Floor tiles** at z 0 and z 3.
- **Eave** at z 6, deck at z 7.5, studs at z 7.4.

### Castle wall section (`dwarf_castle_wall_c`)

- **Wall:** `Castle_Wall_A1_Str` at the origin with all ten `Str_Mrln_*` on the same transform.
- **Bastion:** `Castle_Tower_B1` with its seven merlons at (1, -3, 0).
- **Stairs:** `Castle_Wall_Stairs_A1` yaw 180 at (8.5, 4.5, 0) on the inner face.

Further sections join every 20 m.

### Tower with ramps (`dwarf_tower_ramp_a`)

- **Tower:** `Castle_Tower_A1` and `_A1_int` at the origin.
- **Ramps:** a `Castle_Wall_A1_Ramp` (and merlons) at (-14, 0.5, 0) yaw 0 and (14, 0.5, 0) yaw 180. Each climbs from the walk height 6 m to 18 m at the tower.

## Lond Cirion rules, checked against this kit

| Rule (lond-cirion-buildings.md) | Applies here? |
|---|---|
| A part is a prefix of sub-objects | No: each Erebor piece is one object. Merlons are separate objects that share the parent pivot |
| Decal trap | No decal objects in any of the 12 files |
| Bare `.lod` trap | None found |
| Origin-anchored parts | **Yes, kit-wide.** Roof panels pivot on the eave edge (3 m set) or eave corner (1.5 m set); corner, chamfer and triangle pieces on a corner; the bastion on its back face; merlons outside the mesh. Place by pivot only |
| Gable seating on the infill plate | Different: `Roof_Side_A1` sits on the end wall at the eave height, pivot at its base centre |
| Trims straddle, they do not hang | **Yes.** Beams are centred on the wall line; ground trims are centred on a tile seam |
| Grip the structural body | No sub-objects, so not needed |
| Material `.NNN` duplicates | **Will apply** when an assembler joins pieces from several FBX files: remap before joining |

## Unit-size props

The five props have no authored placement. Building scale, decided 2026-10-15 and checked in a
render beside a wall and a door (`joins_props/props_scale.png`): **x3** for the Moria column, the
broken column and both statues, **x3.06** for `kys_dw_wall_001` so it fills a 3 m bay. Statues
face +X and pivot at their centre: pivot z = 1.44 (`_A`) or 1.47 (`_B`) above the floor, yaw 270
to face -Y. Full sizes are in [meshes.md](meshes.md).

## Our buildings

Buildings assembled from these rules, their export and their probe results are in
[erebor-buildings.md](../erebor-buildings.md). **Grade:** a building sits with the underside of
its floor slab on the terrain (z 0); no below-grade skirt (Mike, 2026-10-15).

## Open questions for Mike

- **Confirm the round trip on the first Kit import:** the building should land turned 180 degrees as a whole and otherwise intact.

## Update rule

When a build or a Kit import shows something new (a pivot quirk, a rotation, a join that fails),
add it here with the measurement or render that proves it. Re-run
`tools/oneoff/blender_catalog_erebor.py` and `tools/oneoff/erebor_catalog_to_md.py` if the kit
FBX files change.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/kitbash/erebor/README.md](./README.md)

<!-- backlinks-end -->
