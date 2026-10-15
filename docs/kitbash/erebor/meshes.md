# Erebor meshes

Every piece in the three Erebor source folders, **measured** in headless Blender on 2026-10-15
(`tools/oneoff/blender_catalog_erebor.py`) and checked against 25,332 authored placements in
the TAOM_Map prefabs (`tools/oneoff/mine_erebor_prefab_joins.py`). The join rules and ready
recipes are in [design-patterns.md](design-patterns.md); this file is the per-piece reference.

**Source:** `Modules/TAOM_Map/AssetSources/Scenes/erebor/{meshes,walls,statues}/*.fbx`, 12 files,
434 mesh objects, **145 pieces** once each piece's LODs and `bo_` body are folded into it. The
nature cliffs (`sm_nat_*`) used by `taom_erebor_kitbash.xml` live outside these folders and are
not catalogued here.

**Measured outputs** (desktop, not in git): `E:\LOTRAOMAssets\_export\erebor\catalog\`
holds `erebor_catalog.json` (every number below plus normal-area buckets, down-ray profiles and
`bo_` bounds), one four-view sheet per piece in `views\<piece>.png` (front -Y, back +Y, side +X,
top, 1 m grid, red dot = pivot) and numbered contact sheets in `sheets\`. The `#` column is the
number on those sheets.

## Read this first

- **All coordinates are in the Blender import frame.** The engine sees every one of these FBX
  files rotated 180 degrees about Z from that frame (verified, see
  [design-patterns.md](design-patterns.md) "The frame rule"). A building assembled and exported
  from Blender keeps its internal joins; it only lands turned 180 degrees as a whole.
- **Node transforms are baked.** Twenty pieces carry a node rotation or scale in the FBX (the
  "node ... (baked)" notes). The game applies it, so the sizes here are the world-space shapes.
  Verified by the prefab rebuilds, which only line up with the node transform applied.
- **Place by pivot, never by bounding box.** Most roof, corner, castle and merlon pieces are
  authored with the pivot on an edge, a corner or outside the mesh (Pivot column: `min`, `max`,
  `mid` or the fraction along the axis; `outside` for merlons).
- **Walls have no inside or outside.** Both faces of every 3 m wall, door and window piece are
  identical (equal triangle split, symmetric bounds), so a wall can face either way.
- **Openings** come from ray grids through the piece: the bottom edge counts as closed, so a door
  that reaches the ground is reported as an opening at z 0.

## Material letters (measured)

The letter in a name is **not** one shared texture family across the kit. Measured bindings:

| Pieces | Letter or digit | Material |
|---|---|---|
| `Wall_3m_*_A*`, `Corn*_A`, `Roof_Side_A*` | A | `M_DW_Wall_Brick_B2` (brown brick); door and window variants add `M_DW_Trim_A1` |
| `Wall_3m_*_B*`, `Corn*_B` | B | `M_DW_Wall_Brick_B1` + `M_DW_Trim_A1` |
| `Wall_3m_*_C*`, `Corn*_C` | C | `M_DW_Wall_Block_A1` + `M_DW_Trim_A1` (dressed block, the look most authored houses use) |
| `Ground_3m_A1`, `_A2`, `_B1` | letter and digit | `M_DW_Ground_Stone_A1`, `_A2`, `_B1` |
| `Ground_3m_C1` | C | `M_DW_Ground_Pavement_A1` |
| `Roof_Str_A*` / `Roof_Str_B*` | letter = material, digit = size | A slate, B stone; 1 = 3 m, 2 = 1.5 m |
| `Roof_Cor_*`, `Roof_Top_*` | letter = size, digit = material | A 3 m, B 1.5 m (`Cor_Tri_C` 3 x 1.5 m); 1 slate, 2 stone |
| Beams, columns, capitals, ground trims | none | `M_DW_Trim_A1` |

Every roof piece also carries `M_Wood_Planks_A_Old_Worn` on its underside. Window digits are
**shapes**: 1 = narrow slit 0.40 x 1.70 m, 2 = octagon 0.90 x 0.90 m, 3 = tall slit
0.50 x 1.90 m; `Win2` = two narrow slits. Doors are 1.10 x 2.35 m in every letter.

## Collision bodies

140 pieces have a `bo_` twin with the exact name. Sixteen do not: the stone-roof and 1.5 m roof
variants (`Roof_Cor_In_A2`, `_B2`, `Roof_Cor_Out_A2`, `_B2`, `Roof_Cor_Tri_A2`, `_B2`, `_C1`,
`Roof_Str_B1`, `_B2`, `Roof_Top_A2`, `_B2`), the four roof trims (`Roof_Side_Cor_Out_A1`, `_A2`,
`Roof_Side_Str_A1`, `_A2`) and `Castle_Wall_A1_Ramp_Mrln_01`, whose body is misnamed
`bo_SM_DW_Castle_Wall_A1_Ramp_Mrln_0` (truncated). A slate and a stone variant of the same size
share one shape, so a merged building can borrow the twin's body (for example
`bo_SM_DW_Roof_Str_A1` under `Roof_Str_B1`).

No piece has a decal sub-object or a bare `.lod` name (the two Lond Cirion traps). LODs are
`.lodN` siblings on the `SM_DW_*` pieces and `_lodN` on the five unit-size props; roof pieces,
plain walls, plain corner walls and pillars have **no LODs**.

## Pieces by family

### Ground tiles and floor trim

3 m floor tiles with their pivot at the centre of the top-of-slab plane (z 0, the slab is
0.10 to 0.12 m thick above it). `Ground_3m_A1_Corner` is a half tile cut on the diagonal: it
fills the floor of a chamfered corner cell (`Wall_3m_Corn_*`). `Ground_6m_A1` is not a big tile:
it is a 6 x 6 m **plinth block** with a 2.9 m skirt below grade and its top at 0.10 to 0.18, used
to raise a house (`dwarf_house_a` stands on two of them). **For a floor, use the stone tiles
(`_A1`, `_A2`, `_B1`):** the `_C1` pavement is a plain grid of rectangular blocks that reads as the
slate roof in game (Mike, first Kit import of `erebor_dwarf_house_01`, 2026-10-15). Trim strips run along Y and sit
**centred on a tile seam**; `Ground_Trim_Corner_*` is a small stud for seam crossings and roof
corners, not an L-piece.


| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes |
|---|---|---|---|---|---|---|---|---|
| 0 | `SM_DW_Ground_3m_A1` | 3.00 x 3.00 x 0.12 | 0.00 to 0.12 | mid/mid/min | 0.12 | 696 | lod2 |  |
| 1 | `SM_DW_Ground_3m_A1_Corner` | 3.00 x 3.00 x 0.10 | 0.00 to 0.10 | mid/mid/min | 0.10 | 8 | none |  |
| 2 | `SM_DW_Ground_3m_A2` | 3.00 x 3.00 x 0.12 | 0.00 to 0.12 | mid/mid/min | 0.11 | 696 | lod2 |  |
| 3 | `SM_DW_Ground_3m_B1` | 3.00 x 3.00 x 0.12 | 0.00 to 0.12 | mid/mid/min | 0.11 | 724 | lod2 |  |
| 4 | `SM_DW_Ground_3m_C1` | 3.00 x 3.00 x 0.12 | 0.00 to 0.12 | mid/mid/min | 0.12 | 132 | lod2 |  |
| 5 | `SM_DW_Ground_6m_A1` | 6.00 x 6.00 x 3.08 | -2.90 to 0.18 | mid/mid/0.94 | 0.10 | 664 | lod2, lod4 |  |
| 6 | `SM_DW_Ground_Trim_3m_A1` | 0.22 x 3.00 x 0.17 | -0.03 to 0.14 | mid/mid/0.18 | 0.14 | 300 | lod2 |  |
| 7 | `SM_DW_Ground_Trim_3m_A2` | 0.20 x 3.00 x 0.18 | -0.03 to 0.15 | mid/mid/0.17 | 0.14 | 60 | lod2 |  |
| 8 | `SM_DW_Ground_Trim_Corner_A1` | 0.40 x 0.40 x 0.22 | -0.05 to 0.17 | mid/mid/0.23 | 0.16 | 76 | lod2 |  |
| 9 | `SM_DW_Ground_Trim_Corner_A2` | 0.30 x 0.30 x 0.22 | -0.04 to 0.18 | mid/mid/0.18 | 0.17 | 36 | lod2 |  |

### Pillars

Hall-scale columns, all pivot at base centre. `Pillar_A` is a 46 m column with a 9.5 m
footprint, `Pillar_A1` a 15 m column on a 9.5 m pedestal whose top is at 4.0 m, `Pillar_A2` a
plain 3.5 m square shaft 20 m tall (28 triangles), `Pillar_A3` 11 m tall. None has LODs. The
old catalog called `Pillar_A` "short"; it is the tallest piece in the kit.


| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes |
|---|---|---|---|---|---|---|---|---|
| 10 | `SM_DW_Pillar_A` | 9.50 x 9.50 x 46.00 | 0.00 to 46.00 | mid/mid/min | 46.00 | 2152 | none |  |
| 11 | `SM_DW_Pillar_A1` | 9.50 x 9.50 x 15.00 | 0.00 to 15.00 | mid/mid/min | 4.00 | 1498 | none |  |
| 12 | `SM_DW_Pillar_A2` | 3.50 x 3.50 x 20.00 | 0.00 to 20.00 | mid/mid/min | 20.00 | 28 | none |  |
| 13 | `SM_DW_Pillar_A3` | 7.00 x 7.00 x 11.00 | 0.00 to 11.00 | mid/mid/min | 11.00 | 650 | none |  |

### Platforms (terrace blocks)

Terrace blocks for building on a slope. The walkable top is at **z 0** in every piece; the
`6m` pieces carry a foundation down to z -12 (the `_Ext` piers to z -9), the `3m` pieces only to
z -1.56. `_A1` variants have a 1.2 m balustrade along one edge (the 0.4 to 0.45 m openings are its
balusters), `_A2` variants a low 0.2 m curb. `_Mid` is a 12 x 12 m slab, `_Corner` (12 x 12 m,
pivot at centre) and `_A1_Corner` / `_A2_Corner` (an L, pivot off-centre) turn the terrace edge.
`_Stairs` pieces carry a 27 degree flight that climbs toward -X from the terrace top.


| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes |
|---|---|---|---|---|---|---|---|---|
| 14 | `SM_DW_Platform_3m_A1` | 3.00 x 6.12 x 2.76 | -1.56 to 1.20 | mid/mid/0.56 | 0.00 | 6788 | lod2, lod5 | opening 0.40 x 0.45 at z 0.20 (through Y); opening 0.40 x 0.45 at z 0.20 (through Y); +9 more openings |
| 15 | `SM_DW_Platform_3m_A2` | 3.00 x 6.12 x 1.76 | -1.56 to 0.20 | mid/mid/0.88 | 0.00 | 3692 | lod2, lod5 | opening 3.03 x 1.35 at z -1.56 (through X); opening 0.98 x 0.05 at z -1.56 (through X); +1 more openings |
| 16 | `SM_DW_Platform_6m_A1` | 6.00 x 6.30 x 13.20 | -12.00 to 1.20 | mid/mid/0.91 | 0.00 | 25028 | lod2, lod5 | opening 0.45 x 0.44 at z 0.21 (through Y); opening 0.45 x 0.44 at z 0.21 (through Y); +7 more openings |
| 17 | `SM_DW_Platform_6m_A1_Corner` | 12.15 x 12.15 x 13.27 | -12.00 to 1.27 | 0.74/0.26/0.90 | 0.00 | 66242 | lod2, lod5 | opening 2.23 x 10.39 at z -12.00 (through Y); opening 0.67 x 10.06 at z -12.00 (through Y); +14 more openings |
| 18 | `SM_DW_Platform_6m_A1_Ext` | 1.62 x 6.12 x 9.00 | -9.00 to 0.00 | mid/mid/max | 0.00 | 10040 | lod2, lod5 |  |
| 19 | `SM_DW_Platform_6m_A2` | 6.00 x 6.30 x 12.21 | -12.00 to 0.20 | mid/mid/0.98 | 0.00 | 18356 | lod2, lod5 | opening 3.00 x 11.80 at z -12.00 (through X) |
| 20 | `SM_DW_Platform_6m_A2_Corner` | 12.15 x 12.15 x 12.23 | -12.00 to 0.23 | 0.74/0.26/0.98 | 0.00 | 48132 | lod2, lod5 | opening 2.15 x 10.48 at z -12.00 (through Y); opening 0.71 x 10.07 at z -12.00 (through Y); +2 more openings |
| 21 | `SM_DW_Platform_6m_A2_Ext` | 1.62 x 1.61 x 9.00 | -9.00 to 0.00 | mid/mid/max | 0.00 | 5020 | lod2, lod5 |  |
| 22 | `SM_DW_Platform_6m_Corner_A1` | 12.00 x 12.00 x 13.27 | -12.00 to 1.27 | mid/mid/0.90 | 0.00 | 80272 | lod2, lod5 | opening 2.89 x 10.51 at z -12.00 (through Y); opening 0.44 x 0.44 at z 0.16 (through Y); +14 more openings |
| 23 | `SM_DW_Platform_6m_Corner_A2` | 12.00 x 12.00 x 12.23 | -12.00 to 0.23 | mid/mid/0.98 | 0.00 | 67320 | lod2, lod5 | opening 2.97 x 10.50 at z -12.00 (through Y); opening 2.97 x 10.50 at z -12.00 (through X) |
| 24 | `SM_DW_Platform_6m_Mid_A1` | 12.12 x 12.12 x 2.84 | -1.57 to 1.27 | mid/mid/0.55 | 0.00 | 30924 | lod2, lod5 | opening 0.40 x 0.51 at z 0.15 (through Y); opening 0.40 x 0.51 at z 0.15 (through Y); +14 more openings |
| 25 | `SM_DW_Platform_6m_Mid_A2` | 12.12 x 12.12 x 1.77 | -1.57 to 0.20 | mid/mid/0.88 | 0.00 | 17596 | lod2, lod5 |  |
| 26 | `SM_DW_Platform_6m_Stairs_A1` | 6.04 x 6.30 x 16.19 | -12.00 to 4.19 | mid/mid/0.74 | 3.00 | 26816 | lod2, lod5 | opening 0.82 x 1.08 at z 1.50 (through Y); opening 0.82 x 0.81 at z 2.17 (through Y); +7 more openings; 27 deg slope rising toward -X |
| 27 | `SM_DW_Platform_6m_Stairs_A2` | 6.04 x 6.30 x 15.25 | -12.00 to 3.25 | mid/mid/0.79 | 1.00 | 20708 | lod2, lod5 | opening 2.96 x 11.69 at z -12.00 (through X); 27 deg slope rising toward -X |

### Roof

One pitch for the whole kit: **26.57 degrees** (1.5 m rise over 3 m run, measured as 27).

- `Roof_Str_*` is the sloped panel. 3 m set (`_A1`, `_B1`): pivot at the **centre of the eave
  edge**, the panel rises toward +Y to 1.5 m at y 3.02. 1.5 m set (`_A2`, `_B2`): pivot at the
  **+X end of the eave edge**, the panel spans x -1.5 to 0 and rises to 0.75 m.
- `Roof_Top_*` is a **flat deck plate** 0.045 m thick, top at its pivot. It closes the top of a
  hipped roof at eave + 1.5 m (eave + 0.75 m for the 1.5 m set). The old catalog called it a ridge
  cap strip; it is a 3 x 3 m (or 1.5 x 1.5 m) square.
- `Roof_Cor_Out_*` is the outside hip corner (two triangular facets), pivot at its outer eave
  corner; it rises toward +Y and -X. `Roof_Cor_In_*` is the inside (valley) corner, pivot at its
  inner eave corner; it rises toward +X and +Y.
- `Roof_Cor_Tri_*` is the facet over a **chamfered corner**: its eave runs along the diagonal of
  the 3 m cell, its high vertex (1.5 m) is on the pivot, slope 35 degrees. It pairs with
  `Wall_3m_Corn_*`. The old catalog called it a valley fill.
- `Roof_Side_A1` is a **gable wedge**: a 0.62 m thick wall in the `_A` brick that rises from 0.3
  to 1.8 m over 3 m with a coping on top, pivot at the base centre. `Roof_Side_A2` is the next
  3 m of a wider gable (1.8 to 3.3 m).
- `Roof_Side_Str_*` is a straight 3 m (or 1.5 m) trim strip, used as the **ridge strip** of a
  gable and as the **frame of a flat deck**. `Roof_Side_Cor_Out_*` is a sloped strip that runs
  from the eave up a panel edge: the **rib** between panels and the hip trim on a corner.


| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes |
|---|---|---|---|---|---|---|---|---|
| 28 | `SM_DW_Roof_Cor_In_A1` | 3.11 x 3.11 x 1.57 | -0.04 to 1.52 | 0.02/0.02/0.03 | 1.49 | 22 | none | 27 deg slope rising toward +X; 27 deg slope rising toward +Y |
| 29 | `SM_DW_Roof_Cor_In_A2` | 3.11 x 3.11 x 1.57 | -0.04 to 1.52 | 0.02/0.02/0.03 | 1.49 | 22 | none | 27 deg slope rising toward +X; 27 deg slope rising toward +Y; **no bo_** |
| 30 | `SM_DW_Roof_Cor_In_B1` | 1.61 x 1.61 x 0.82 | -0.04 to 0.77 | 0.03/0.03/0.05 | 0.73 | 22 | none | 27 deg slope rising toward +X; 27 deg slope rising toward +Y |
| 31 | `SM_DW_Roof_Cor_In_B2` | 1.61 x 1.61 x 0.82 | -0.04 to 0.77 | 0.03/0.03/0.05 | 0.73 | 22 | none | 27 deg slope rising toward +X; 27 deg slope rising toward +Y; **no bo_** |
| 32 | `SM_DW_Roof_Cor_Out_A1` | 3.11 x 3.11 x 1.57 | -0.04 to 1.52 | 0.98/0.02/0.03 | 0.05 | 22 | none | 27 deg slope rising toward +Y; 27 deg slope rising toward -X |
| 33 | `SM_DW_Roof_Cor_Out_A2` | 3.11 x 3.11 x 1.57 | -0.04 to 1.52 | 0.98/0.02/0.03 | 0.05 | 22 | none | 27 deg slope rising toward +Y; 27 deg slope rising toward -X; **no bo_** |
| 34 | `SM_DW_Roof_Cor_Out_B1` | 1.61 x 1.61 x 0.82 | -0.04 to 0.77 | 0.97/0.03/0.05 | 0.05 | 22 | none | 27 deg slope rising toward +Y; 27 deg slope rising toward -X |
| 35 | `SM_DW_Roof_Cor_Out_B2` | 1.61 x 1.61 x 0.82 | -0.04 to 0.77 | 0.97/0.03/0.05 | 0.05 | 22 | none | 27 deg slope rising toward +Y; 27 deg slope rising toward -X; **no bo_** |
| 36 | `SM_DW_Roof_Cor_Tri_A1` | 3.02 x 3.02 x 1.54 | -0.04 to 1.50 | 0.01/0.99/0.03 | 0.01 | 8 | none | 35 deg slope rising toward -X+Y |
| 37 | `SM_DW_Roof_Cor_Tri_A2` | 3.02 x 3.02 x 1.54 | -0.04 to 1.50 | 0.01/0.99/0.03 | 0.01 | 8 | none | 35 deg slope rising toward -X+Y; **no bo_** |
| 38 | `SM_DW_Roof_Cor_Tri_B1` | 1.52 x 1.52 x 0.80 | -0.04 to 0.75 | 0.01/0.99/0.06 | 0.01 | 8 | none | 35 deg slope rising toward -X+Y |
| 39 | `SM_DW_Roof_Cor_Tri_B2` | 1.52 x 1.52 x 0.80 | -0.04 to 0.75 | 0.01/0.99/0.06 | 0.01 | 8 | none | 35 deg slope rising toward -X+Y; **no bo_** |
| 40 | `SM_DW_Roof_Cor_Tri_C1` | 3.01 x 3.02 x 0.80 | -0.04 to 0.75 | min/mid/0.06 | 0.21 | 12 | none | 35 deg slope rising toward -X+Y; **no bo_** |
| 41 | `SM_DW_Roof_Cor_Tri_C2` | 3.01 x 3.02 x 0.80 | -0.04 to 0.75 | min/mid/0.06 | 0.21 | 12 | none | 35 deg slope rising toward -X+Y |
| 42 | `SM_DW_Roof_Side_A1` | 0.62 x 3.00 x 1.80 | 0.00 to 1.80 | mid/mid/min | 0.35 | 56 | none | 27 deg slope rising toward +Y; node rot -0/-0/90 (baked) |
| 43 | `SM_DW_Roof_Side_A2` | 0.62 x 3.00 x 3.30 | 0.00 to 3.30 | mid/mid/min | 1.85 | 56 | none | 27 deg slope rising toward +Y; node rot -0/-0/90 (baked) |
| 44 | `SM_DW_Roof_Side_Cor_Out_A1` | 0.14 x 3.01 x 1.55 | -0.02 to 1.52 | mid/min/0.02 | 0.05 | 10 | none | 27 deg slope rising toward +Y; node rot -0/-0/-45 (baked); **no bo_** |
| 45 | `SM_DW_Roof_Side_Cor_Out_A2` | 0.14 x 1.51 x 0.80 | -0.02 to 0.77 | mid/min/0.03 | 0.05 | 10 | none | 27 deg slope rising toward +Y; node rot -0/-0/-45 (baked); **no bo_** |
| 46 | `SM_DW_Roof_Side_Str_A1` | 0.20 x 3.00 x 0.11 | -0.05 to 0.05 | mid/mid/mid | 0.04 | 34 | none | node rot -0/-0/-45 (baked); **no bo_** |
| 47 | `SM_DW_Roof_Side_Str_A2` | 0.20 x 1.50 x 0.11 | -0.05 to 0.05 | mid/max/mid | 0.04 | 34 | none | node rot -0/-0/-45 (baked); **no bo_** |
| 48 | `SM_DW_Roof_Str_A1` | 3.00 x 3.02 x 1.54 | -0.04 to 1.50 | mid/min/0.03 | 0.03 | 12 | none | 27 deg slope rising toward +Y |
| 49 | `SM_DW_Roof_Str_A2` | 1.50 x 1.52 x 0.80 | -0.04 to 0.75 | max/min/0.06 | 0.03 | 12 | none | 27 deg slope rising toward +Y |
| 50 | `SM_DW_Roof_Str_B1` | 3.00 x 3.02 x 1.54 | -0.04 to 1.50 | mid/min/0.03 | 0.03 | 12 | none | 27 deg slope rising toward +Y; **no bo_** |
| 51 | `SM_DW_Roof_Str_B2` | 1.50 x 1.52 x 0.80 | -0.04 to 0.75 | max/min/0.06 | 0.03 | 12 | none | 27 deg slope rising toward +Y; **no bo_** |
| 52 | `SM_DW_Roof_Top_A1` | 3.00 x 3.00 x 0.04 | -0.04 to 0.00 | mid/mid/max | 0.00 | 12 | none | node rot -0/-0/90 (baked) |
| 53 | `SM_DW_Roof_Top_A2` | 3.00 x 3.00 x 0.04 | -0.04 to 0.00 | mid/mid/max | 0.00 | 12 | none | node rot -0/-0/90 (baked); **no bo_** |
| 54 | `SM_DW_Roof_Top_B1` | 1.50 x 1.50 x 0.04 | -0.04 to 0.00 | max/min/max | 0.00 | 12 | none | node rot -0/-0/90 (baked) |
| 55 | `SM_DW_Roof_Top_B2` | 1.50 x 1.50 x 0.04 | -0.04 to 0.00 | max/min/max | 0.00 | 12 | none | node rot -0/-0/90 (baked); **no bo_** |

### Wall overlays (beams, columns, capitals)

All in `M_DW_Trim_A1`. Beams are 3 m long, centred on their pivot in all three axes and
**straddle the wall centreline** (0.60 to 0.68 m deep over a 0.50 to 0.54 m wall), so they sit on
the wall line, not on a face. Authored use: `Beam_3m_B` at z 0 as a base course, `Beam_3m_C` or
`Beam_3m_A` at z 3 as an eave course. Columns are 0.6 m square and 3 m tall with the pivot at the
base; capitals (`Clmn_Top_*`) are centred on their pivot and go at the column foot (z 0) and head
(z 3), usually scaled 1.1 to 1.5.


| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes |
|---|---|---|---|---|---|---|---|---|
| 56 | `SM_DW_Wall_Beam_3m_A` | 3.00 x 0.64 x 0.25 | -0.12 to 0.12 | mid/mid/mid | 0.13 | 52 | lod4 | node scale 1/0.8/1 (baked) |
| 57 | `SM_DW_Wall_Beam_3m_B` | 3.00 x 0.68 x 0.25 | -0.12 to 0.12 | mid/mid/mid | 0.13 | 84 | lod4 | node scale 1/0.85/1 (baked) |
| 58 | `SM_DW_Wall_Beam_3m_C` | 3.00 x 0.60 x 0.25 | -0.12 to 0.12 | mid/mid/mid | 0.12 | 44 | lod4 | node scale 1/0.75/1 (baked) |
| 59 | `SM_DW_Wall_Clmn_3m_A` | 0.60 x 0.60 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 620 | lod2, lod4 |  |
| 60 | `SM_DW_Wall_Clmn_3m_B` | 0.60 x 0.60 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 492 | lod4 |  |
| 61 | `SM_DW_Wall_Clmn_3m_C` | 0.60 x 0.60 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 44 | lod4 |  |
| 62 | `SM_DW_Wall_Clmn_Top_A` | 0.90 x 0.90 x 0.30 | -0.15 to 0.15 | mid/mid/mid | 0.15 | 156 | lod2, lod4 | node rot -0/-0/90 (baked) |
| 63 | `SM_DW_Wall_Clmn_Top_B` | 0.85 x 0.85 x 0.27 | -0.13 to 0.13 | mid/mid/mid | 0.13 | 204 | lod2, lod4 |  |
| 64 | `SM_DW_Wall_Clmn_Top_C` | 0.66 x 0.66 x 0.27 | -0.13 to 0.13 | mid/mid/mid | 0.13 | 92 | lod4 |  |
| 65 | `SM_DW_Wall_Clmn_Top_D` | 0.72 x 0.72 x 0.27 | -0.13 to 0.13 | mid/mid/mid | 0.13 | 432 | lod4 |  |
| 66 | `SM_DW_Wall_Clmn_Top_E` | 0.86 x 0.86 x 0.27 | -0.13 to 0.13 | mid/mid/mid | 0.13 | 76 | lod4 |  |
| 67 | `SM_DW_Wall_Clmn_Top_F` | 0.66 x 0.66 x 0.27 | -0.13 to 0.13 | mid/mid/mid | 0.13 | 188 | lod4 |  |

### Walls (3 m modules)

3 m long, 3 m tall, pivot at base centre, 0.50 m (`_A`) or 0.54 m (`_B`, `_C`) thick;
door and window pieces are 0.60 to 0.62 m where their frames stand proud. `Corn*` pieces are the
**chamfer walls**: a straight 4.24 m wall across the diagonal of a 3 x 3 m cell, pivot on the
**inner** corner of that cell (the wall cuts off the corner opposite the pivot). Their bounding
box is 3.25 m because the wall's thickness pushes past the cell.


| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes |
|---|---|---|---|---|---|---|---|---|
| 69 | `SM_DW_Wall_3m_A` | 3.00 x 0.50 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 12 | none |  |
| 70 | `SM_DW_Wall_3m_B` | 3.00 x 0.54 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 32 | none |  |
| 71 | `SM_DW_Wall_3m_C` | 3.00 x 0.54 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 52 | none |  |
| 72 | `SM_DW_Wall_3m_Corn_A` | 3.25 x 3.25 x 3.00 | 0.00 to 3.00 | min/min/min | 3.00 | 12 | none |  |
| 73 | `SM_DW_Wall_3m_Corn_B` | 3.25 x 3.25 x 3.00 | 0.00 to 3.00 | min/min/min | 3.00 | 32 | none |  |
| 74 | `SM_DW_Wall_3m_Corn_C` | 3.25 x 3.25 x 3.00 | 0.00 to 3.00 | min/min/min | 3.00 | 52 | none |  |
| 75 | `SM_DW_Wall_3m_Corn_Win_A` | 3.25 x 3.25 x 3.00 | 0.00 to 3.00 | min/min/min | 3.00 | 224 | lod2, lod5 |  |
| 76 | `SM_DW_Wall_3m_Corn_Win_B` | 3.25 x 3.25 x 3.00 | 0.00 to 3.00 | min/min/min | 3.00 | 264 | lod2, lod5 |  |
| 77 | `SM_DW_Wall_3m_Corn_Win_C` | 3.25 x 3.25 x 3.00 | 0.00 to 3.00 | min/min/min | 3.00 | 288 | lod2, lod5 |  |
| 78 | `SM_DW_Wall_3m_Door_A1` | 3.00 x 0.62 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 416 | lod2, lod5 | opening 1.10 x 2.35 at z 0.00 (through Y) |
| 79 | `SM_DW_Wall_3m_Door_B1` | 3.00 x 0.62 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 456 | lod2, lod5 | opening 1.10 x 2.35 at z 0.00 (through Y) |
| 80 | `SM_DW_Wall_3m_Door_C1` | 3.00 x 0.62 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 468 | lod2, lod5 | opening 1.10 x 2.35 at z 0.00 (through Y) |
| 81 | `SM_DW_Wall_3m_Win2_A1` | 3.00 x 0.60 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 404 | lod2, lod5 | opening 0.40 x 1.70 at z 0.65 (through Y); opening 0.40 x 1.70 at z 0.65 (through Y) |
| 82 | `SM_DW_Wall_3m_Win2_B1` | 3.00 x 0.60 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 424 | lod2, lod5 | opening 0.40 x 1.70 at z 0.65 (through Y); opening 0.40 x 1.70 at z 0.65 (through Y) |
| 83 | `SM_DW_Wall_3m_Win2_C1` | 3.00 x 0.60 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 444 | lod2, lod5 | opening 0.40 x 1.70 at z 0.65 (through Y); opening 0.40 x 1.70 at z 0.65 (through Y) |
| 84 | `SM_DW_Wall_3m_Win_A1` | 3.00 x 0.60 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 224 | lod2, lod5 | opening 0.40 x 1.70 at z 0.65 (through Y) |
| 85 | `SM_DW_Wall_3m_Win_A2` | 3.00 x 0.60 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 272 | lod2, lod5 | opening 0.90 x 0.90 at z 1.05 (through Y) |
| 86 | `SM_DW_Wall_3m_Win_A3` | 3.00 x 0.60 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 336 | lod2, lod5 | opening 0.50 x 1.90 at z 0.55 (through Y) |
| 87 | `SM_DW_Wall_3m_Win_B1` | 3.00 x 0.60 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 264 | lod2, lod5 | opening 0.40 x 1.70 at z 0.65 (through Y) |
| 88 | `SM_DW_Wall_3m_Win_B2` | 3.00 x 0.60 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 312 | lod2, lod5 | opening 0.90 x 0.90 at z 1.05 (through Y) |
| 89 | `SM_DW_Wall_3m_Win_B3` | 3.00 x 0.60 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 376 | lod2, lod5 | opening 0.50 x 1.90 at z 0.55 (through Y) |
| 90 | `SM_DW_Wall_3m_Win_C1` | 3.00 x 0.60 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 264 | lod2, lod5 | opening 0.40 x 1.70 at z 0.65 (through Y) |
| 91 | `SM_DW_Wall_3m_Win_C2` | 3.00 x 0.60 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 312 | lod2, lod5 | opening 0.90 x 0.90 at z 1.05 (through Y) |
| 92 | `SM_DW_Wall_3m_Win_C3` | 3.00 x 0.60 x 3.00 | 0.00 to 3.00 | mid/mid/min | 3.00 | 400 | lod2, lod5 | opening 0.50 x 1.90 at z 0.55 (through Y) |

### Decoration

A 5 x 5 m treasure heap 1 m high, pivot at base centre.


| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes |
|---|---|---|---|---|---|---|---|---|
| 68 | `SM_DW_Pile_Gold_A1` | 5.00 x 5.00 x 1.00 | 0.00 to 1.00 | mid/mid/min | 0.69 | 352 | lod4 |  |

### Castle

Fortification scale, all in mixed brick, block, trim, rock and obsidian.

- `Castle_Wall_A1_Str`: **20 m** long, 6.2 m thick, 12 m tall from z -6 to z 6, pivot at its
  centre. The walk is at z 6. Merlons stand on the **-Y face** only, so -Y is the outer face.
- `_Gate`: the same 20 m module with a 7.67 x 6.21 m passage from z 0.1 and a stepped top to 9 m.
- `_Ramp`: a 20 m module whose top climbs 12 m (31 degrees) toward +X, from 6 m to 18 m.
- `Castle_Tower_A1` / `_A2`: a **square tower with chamfered corners** (not round), 10.8 m wide,
  from z -6 to z 36.3, windows every 12 m. `_A1_int` is its hollow interior with a
  2.16 x 3.66 m door at the base.
- `Castle_Tower_B1`: a 6.3 m **bastion** that projects 6 m from a wall's outer face, pivot on its
  back face, same height band as the wall (z -6 to 6).
- `Castle_Wall_Stairs_A1`: a 21 m flight on a wall's inner face, top landing at the pivot end at
  z 6 (the walk), foot at z -3 at the far end.


| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes |
|---|---|---|---|---|---|---|---|---|
| 95 | `SM_DW_Castle_Tower_A1` | 10.80 x 10.80 x 42.30 | -6.00 to 36.30 | mid/mid/0.14 | 36.30 | 7370 | lod4 | opening 0.72 x 1.06 at z 7.04 (through Y); opening 0.72 x 1.06 at z 19.03 (through Y); +13 more openings |
| 96 | `SM_DW_Castle_Tower_A1_int` | 9.88 x 9.88 x 36.28 | -0.24 to 36.04 | mid/mid/0.01 | 36.00 | 7354 | lod2 | opening 2.16 x 3.66 at z 0.07 (through Y); opening 1.85 x 1.83 at z 11.96 (through X); +3 more openings |
| 97 | `SM_DW_Castle_Tower_A2` | 10.80 x 10.80 x 42.30 | -6.00 to 36.30 | mid/mid/0.14 | 36.30 | 7134 | lod4 | opening 0.72 x 1.06 at z 7.04 (through Y); opening 0.72 x 1.06 at z 19.03 (through Y); +12 more openings; node rot -0/-0/90 (baked) |
| 98 | `SM_DW_Castle_Tower_B1` | 6.30 x 6.30 x 12.00 | -6.00 to 6.00 | mid/0.98/mid | 6.00 | 248 | lod4 |  |
| 106 | `SM_DW_Castle_Wall_A1_Gate` | 20.00 x 6.37 x 15.09 | -6.09 to 9.00 | mid/mid/0.40 | 9.00 | 6752 | lod4 | opening 7.67 x 6.21 at z 0.11 (through Y); opening 7.67 x 0.17 at z -6.09 (through Y); +1 more openings |
| 119 | `SM_DW_Castle_Wall_A1_Ramp` | 20.00 x 6.20 x 24.00 | -6.00 to 18.00 | mid/mid/0.25 | 6.15 | 728 | lod4 | 31 deg slope rising toward +X |
| 130 | `SM_DW_Castle_Wall_A1_Str` | 20.00 x 6.20 x 12.00 | -6.00 to 6.00 | mid/mid/mid | 6.00 | 864 | lod2 |  |
| 141 | `SM_DW_Castle_Wall_Stairs_A1` | 21.01 x 3.11 x 9.05 | -3.03 to 6.02 | 0.07/mid/0.33 | 6.00 | 4142 | lod4 | 27 deg slope rising toward -X |

### Castle merlons

Each merlon is a separate object that shares its parent's pivot, so it is placed with
**the same transform as its wall, gate, ramp or bastion**. Its own pivot therefore reads
`outside`. `Ramp_Mrln_01` is the piece with the truncated `bo_` name.


| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes |
|---|---|---|---|---|---|---|---|---|
| 99 | `SM_DW_Castle_Tower_B1_Mrln_01` | 0.52 x 2.50 x 2.00 | 5.50 to 7.51 | outside/outside/outside | 7.09 | 216 | lod4 |  |
| 100 | `SM_DW_Castle_Tower_B1_Mrln_02` | 0.97 x 2.43 x 2.00 | 5.50 to 7.51 | outside/outside/outside | 7.04 | 216 | lod4 |  |
| 101 | `SM_DW_Castle_Tower_B1_Mrln_03` | 2.40 x 2.35 x 2.26 | 5.38 to 7.64 | outside/outside/outside | 7.03 | 120 | lod4 | 45 deg slope rising toward -X-Y |
| 102 | `SM_DW_Castle_Tower_B1_Mrln_04` | 2.50 x 0.52 x 2.00 | 5.50 to 7.51 | 0.44/outside/outside | 7.09 | 216 | lod4 |  |
| 103 | `SM_DW_Castle_Tower_B1_Mrln_05` | 2.53 x 2.65 x 2.30 | 5.35 to 7.65 | outside/outside/outside | 7.04 | 120 | lod4 | 50 deg slope rising toward -X+Y; 39 deg slope rising toward +X-Y |
| 104 | `SM_DW_Castle_Tower_B1_Mrln_06` | 0.72 x 2.71 x 2.03 | 5.50 to 7.54 | outside/outside/outside | 6.47 | 216 | lod4 |  |
| 105 | `SM_DW_Castle_Tower_B1_Mrln_07` | 0.52 x 2.50 x 2.00 | 5.50 to 7.51 | outside/0.76/outside | 6.96 | 216 | lod4 |  |
| 107 | `SM_DW_Castle_Wall_A1_Gate_Mrln_01` | 2.50 x 0.52 x 2.00 | 5.50 to 7.51 | outside/outside/outside | 7.09 | 216 | lod4 |  |
| 108 | `SM_DW_Castle_Wall_A1_Gate_Mrln_02` | 1.92 x 0.68 x 1.23 | 5.78 to 7.01 | outside/outside/outside | 6.99 | 216 | lod4 |  |
| 109 | `SM_DW_Castle_Wall_A1_Gate_Mrln_03` | 1.92 x 0.68 x 1.23 | 6.97 to 8.20 | outside/outside/outside | 8.18 | 216 | lod4 |  |
| 110 | `SM_DW_Castle_Wall_A1_Gate_Mrln_04` | 1.92 x 0.68 x 1.23 | 8.16 to 9.39 | outside/outside/outside | 9.37 | 216 | lod4 |  |
| 111 | `SM_DW_Castle_Wall_A1_Gate_Mrln_05` | 2.50 x 0.52 x 2.00 | 8.50 to 10.51 | outside/outside/outside | 10.09 | 216 | lod4 |  |
| 112 | `SM_DW_Castle_Wall_A1_Gate_Mrln_06` | 2.50 x 0.52 x 2.00 | 8.50 to 10.51 | 0.84/outside/outside | 10.09 | 216 | lod4 |  |
| 113 | `SM_DW_Castle_Wall_A1_Gate_Mrln_07` | 2.50 x 0.52 x 2.00 | 8.50 to 10.51 | 0.04/outside/outside | 10.09 | 216 | lod4 |  |
| 114 | `SM_DW_Castle_Wall_A1_Gate_Mrln_08` | 2.50 x 0.52 x 2.00 | 8.50 to 10.51 | outside/outside/outside | 10.09 | 216 | lod4 |  |
| 115 | `SM_DW_Castle_Wall_A1_Gate_Mrln_09` | 1.86 x 0.61 x 1.37 | 8.26 to 9.63 | outside/outside/outside | 9.60 | 216 | lod4 |  |
| 116 | `SM_DW_Castle_Wall_A1_Gate_Mrln_10` | 1.86 x 0.61 x 1.37 | 6.93 to 8.30 | outside/outside/outside | 8.28 | 216 | lod4 |  |
| 117 | `SM_DW_Castle_Wall_A1_Gate_Mrln_11` | 1.86 x 0.61 x 1.37 | 5.60 to 6.97 | outside/outside/outside | 6.95 | 216 | lod4 |  |
| 118 | `SM_DW_Castle_Wall_A1_Gate_Mrln_12` | 2.50 x 0.52 x 2.00 | 5.50 to 7.51 | outside/outside/outside | 7.09 | 216 | lod4 |  |
| 120 | `SM_DW_Castle_Wall_A1_Ramp_Mrln_01` | 2.50 x 0.52 x 2.62 | 5.72 to 8.34 | outside/outside/outside | 8.19 | 216 | lod4 | **no bo_** |
| 121 | `SM_DW_Castle_Wall_A1_Ramp_Mrln_02` | 2.50 x 0.52 x 2.62 | 6.92 to 9.54 | outside/outside/outside | 9.39 | 216 | lod4 | 58 deg slope rising toward +X |
| 122 | `SM_DW_Castle_Wall_A1_Ramp_Mrln_03` | 2.50 x 0.52 x 2.62 | 8.12 to 10.74 | outside/outside/outside | 10.59 | 216 | lod4 | 58 deg slope rising toward +X |
| 123 | `SM_DW_Castle_Wall_A1_Ramp_Mrln_04` | 2.50 x 0.52 x 2.62 | 9.32 to 11.94 | outside/outside/outside | 11.79 | 216 | lod4 | 58 deg slope rising toward +X |
| 124 | `SM_DW_Castle_Wall_A1_Ramp_Mrln_05` | 2.50 x 0.52 x 2.62 | 10.52 to 13.14 | 0.84/outside/outside | 12.99 | 216 | lod4 | 58 deg slope rising toward +X |
| 125 | `SM_DW_Castle_Wall_A1_Ramp_Mrln_06` | 2.50 x 0.52 x 2.62 | 11.72 to 14.34 | 0.04/outside/outside | 14.19 | 216 | lod4 | 58 deg slope rising toward +X |
| 126 | `SM_DW_Castle_Wall_A1_Ramp_Mrln_07` | 2.50 x 0.52 x 2.62 | 12.92 to 15.54 | outside/outside/outside | 15.39 | 216 | lod4 | 58 deg slope rising toward +X |
| 127 | `SM_DW_Castle_Wall_A1_Ramp_Mrln_08` | 2.50 x 0.52 x 2.62 | 14.12 to 16.74 | outside/outside/outside | 16.59 | 216 | lod4 | 58 deg slope rising toward +X |
| 128 | `SM_DW_Castle_Wall_A1_Ramp_Mrln_09` | 2.50 x 0.52 x 2.62 | 15.32 to 17.94 | outside/outside/outside | 17.79 | 216 | lod4 | 58 deg slope rising toward +X |
| 129 | `SM_DW_Castle_Wall_A1_Ramp_Mrln_10` | 2.50 x 0.51 x 2.62 | 16.52 to 19.14 | outside/outside/outside | 18.48 | 216 | lod4 | 58 deg slope rising toward +X |
| 131 | `SM_DW_Castle_Wall_A1_Str_Mrln_01` | 2.50 x 0.52 x 2.00 | 5.50 to 7.51 | outside/outside/outside | 7.09 | 216 | lod4 |  |
| 132 | `SM_DW_Castle_Wall_A1_Str_Mrln_02` | 2.50 x 0.52 x 2.00 | 5.50 to 7.51 | outside/outside/outside | 7.09 | 216 | lod4 |  |
| 133 | `SM_DW_Castle_Wall_A1_Str_Mrln_03` | 2.50 x 0.52 x 2.00 | 5.50 to 7.51 | outside/outside/outside | 7.09 | 216 | lod4 |  |
| 134 | `SM_DW_Castle_Wall_A1_Str_Mrln_04` | 2.50 x 0.52 x 2.00 | 5.50 to 7.51 | outside/outside/outside | 7.09 | 216 | lod4 |  |
| 135 | `SM_DW_Castle_Wall_A1_Str_Mrln_05` | 2.50 x 0.52 x 2.00 | 5.50 to 7.51 | 0.84/outside/outside | 7.09 | 216 | lod4 |  |
| 136 | `SM_DW_Castle_Wall_A1_Str_Mrln_06` | 2.50 x 0.52 x 2.00 | 5.50 to 7.51 | 0.04/outside/outside | 7.09 | 216 | lod4 |  |
| 137 | `SM_DW_Castle_Wall_A1_Str_Mrln_07` | 2.50 x 0.52 x 2.00 | 5.50 to 7.51 | outside/outside/outside | 7.09 | 216 | lod4 |  |
| 138 | `SM_DW_Castle_Wall_A1_Str_Mrln_08` | 2.50 x 0.52 x 2.00 | 5.50 to 7.51 | outside/outside/outside | 7.09 | 216 | lod4 |  |
| 139 | `SM_DW_Castle_Wall_A1_Str_Mrln_09` | 2.50 x 0.52 x 2.00 | 5.50 to 7.51 | outside/outside/outside | 7.09 | 216 | lod4 |  |
| 140 | `SM_DW_Castle_Wall_A1_Str_Mrln_10` | 2.50 x 0.52 x 2.00 | 5.50 to 7.51 | outside/outside/outside | 7.09 | 216 | lod4 |  |

### Props (unit size, unplaced)

Five Tripo or hand-made props normalised to about 1 m, authored Y-up (node rotation X 90).
**None is placed in any TAOM_Map prefab**, so there is no authored scale.

**Building scale (decided 2026-10-15, render-checked beside a wall and a door):** x3 for all
five, so one unit is one 3 m storey or bay; `kys_dw_wall_001` at x3.06 so it fills a 3 m bay
exactly (3.00 x 0.64 x 1.97 m). At x3 the Moria column is 1.45 m wide and 3.0 m tall, the broken
column 2.95 m tall, and each statue about 2.9 m (a heroic statue, about twice dwarf height).
**Both statues face +X** and pivot at their **centre**, so their pivot goes 1.44 m (`_A`) or
1.47 m (`_B`, which carries its own plinth) above the floor. Their LOD0 is heavy (28,000 to
52,000 triangles): a merged building should use `_lod2` as its base tier for these props.


| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes |
|---|---|---|---|---|---|---|---|---|
| 93 | `dw_pillar_moria_01` | 0.48 x 0.48 x 1.00 | 0.00 to 1.00 | mid/mid/min | 1.00 | 52028 | lod1, lod2, lod3, lod4 | node rot 90/-0/0 (baked) |
| 94 | `kys_dw_pillar_broken_01` | 0.64 x 0.66 x 0.98 | 0.00 to 0.98 | mid/mid/min | 0.02 | 8822 | lod1, lod2, lod3, lod4 | node rot 90/-0/0 (baked) |
| 142 | `kys_dw_wall_001` | 0.98 x 0.21 x 0.64 | 0.00 to 0.64 | mid/mid/min | 0.61 | 28258 | lod1, lod2, lod3, lod4 | node rot 90/-0/0 (baked) |
| 143 | `SD_DW_IH_Statue_A` | 0.40 x 0.44 x 0.97 | -0.48 to 0.49 | mid/mid/mid | -0.23 | 28251 | lod1, lod2, lod3, lod4, lod5 |  |
| 144 | `SD_DW_IH_Statue_B` | 0.40 x 0.48 x 0.98 | -0.49 to 0.49 | mid/mid/mid | -0.38 | 28301 | lod1, lod2, lod3, lod4, lod5 |  |

## What the old catalog got wrong

Kept here so nobody restores the old guesses. Each line was measured on 2026-10-15.

| Old claim | Measured |
|---|---|
| Letter `_a/_b/_c` is one material family across the kit (A = clean block) | Per sub-family, see "Material letters"; wall `_A` is brown brick, `_C` is dressed block |
| Window 1 = round arched, 2 = slit, 3 = arrow slit | 1 = 0.40 x 1.70 slit, 2 = 0.90 octagon, 3 = 0.50 x 1.90 slit |
| `Pillar_a` is a stout short column | 46 m tall, 9.5 m footprint |
| Platforms are raised 6 m floors | Terrace blocks: top at z 0, foundation to z -12 |
| `Roof_Top` is a ridge cap strip | A flat 3 x 3 m deck plate |
| `Roof_Side` is bargeboard eave trim | A gable wedge wall |
| `Roof_Side_Str` is a straight eave run | A ridge strip and deck frame |
| `Roof_Side_Cor_Out` is eave trim at wall top | A sloped rib up a panel edge |
| `Roof_Cor_Tri` is an interior valley fill | The roof facet over a chamfered corner |
| `Castle_Tower_A1` is cylindrical | Square with chamfered corners |
| `Castle_Tower_B1` is the base of a tower | A wall bastion |
| Castle wall module is about 3 m | 20 m |
| `Ground_6m_A1` is a large tile | A plinth block with a 2.9 m skirt |
| Kit total about 164 pieces | 145 pieces in these folders; the cliffs come from elsewhere |

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/kitbash/erebor/README.md](./README.md)

<!-- backlinks-end -->
