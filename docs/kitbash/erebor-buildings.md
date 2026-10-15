# Erebor buildings: merged buildings from the Erebor kit

Sibling to [lond-cirion-buildings.md](lond-cirion-buildings.md). Whole Erebor buildings are
assembled in headless Blender from the measured kit pieces and exported as **one FBX per
building**: a base mesh, two LOD tiers and one `bo_` collision body, so a building costs one prefab
entity instead of one per piece. Issue [#797](https://github.com/haterade22/TAOM/issues/797);
the step-by-step prompt is [erebor/blender-build-prompt.md](erebor/blender-build-prompt.md).

The kit itself (every piece measured, the join rules and the recipes taken from the authored
TAOM_Map prefabs) is documented in [erebor/meshes.md](erebor/meshes.md) and
[erebor/design-patterns.md](erebor/design-patterns.md). Read "The frame rule" there first.

## Buildings

| Building | Footprint | Storeys | Roof | Pieces | Tris base / lod3 / lod6 / bo | Status |
|---|---|---|---|---|---|---|
| `erebor_dwarf_house_01` | 9 x 6 m (9.79 x 6.89 outer) | 1 x 3 m | gable, slate, ridge along X at z 4.5 | 63 | 9,202 / 3,442 / 2,226 / 1,428 | Exported and probed 2026-10-15; Kit import owed |

Planned next, one brief and one review at a time: a great hall, a multi-storey keep, a castle wall
section with a tower and a gate, and a forge or market. All are exterior shells for scene dressing.

### `erebor_dwarf_house_01`

- **Walls**, dressed block (`_C`):
  - Front (-Y in Blender): `Win_C3`, `Door_C1`, `Win_C3`.
  - Back: `Win_C1`, `Wall_3m_C`, `Win_C1`.
  - West end: two `Win_C2`. East end: `Wall_3m_C` and `Win_C2`.
- **Corners:** `Clmn_3m_C` with a `Clmn_Top_D` cap (x1.1/1.1/1.2) at z 3.
- **Door:** `Clmn_3m_A` pilasters (x1.5/1.5/1) with `Clmn_Top_F` bases and caps (x1.5/1.5/1.2).
- **Courses:** `Beam_3m_B` at z 0 and `Beam_3m_C` at z 3 on every wall bay.
- **Roof:** `Roof_Str_A1` slate panels on both long walls, a `Roof_Side_Str_A1` ridge strip, and
  `Roof_Side_A1` gable wedges on both ends.
- **Floor:** six `Ground_3m_C1` paving tiles.
- **Grade:** the underside of the floor slab and the wall bases are at z 0, so the house **sits on
  the terrain**. The lowest points are the base beam (z -0.125) and the pilaster bases (z -0.161),
  which sink just under the ground. A first export carried a 3 m plain-wall skirt below grade;
  Mike removed it on review (2026-10-15).

## Pipeline

| Step | Tool | Output (desktop, not in git) |
|---|---|---|
| Brief and blockout | `tools/oneoff/blender_assemble_erebor_buildings.py --preview` | `E:\LOTRAOMAssets\_export\erebor\buildings\<name>_{q1,q2,front,side,top}.png`, `<name>_preview.json` |
| Export | `... --export` | `TAOM_Map/AssetSources/Scenes/erebor/buildings/<name>.fbx` and `<name>_import.md`; `<name>_expected.json` (planned openings) and `frame_probe.fbx` in the output folder |
| Probe | `tools/oneoff/blender_probe_erebor_building.py` | `<name>_probe.json` |
| Frame check | `tools/oneoff/fbx_frame_compare.py` | console |

**Building specs** are Python functions in the assembler that return placements
`(piece, location, yaw, scale, role)`, written in the Blender import frame with the join rules of
`design-patterns.md`. One spec drives the blockout render, the triangle budget, the export and the
planned-opening list, so the approved brief and the exported building cannot drift apart.

**Export rules:**

- **LOD tiers** use the Lond Cirion names. `.lod3` takes each piece's `.lod2`, else `.lod4`, else
  LOD0. `.lod6` takes `.lod5`, else `.lod4`, `.lod2`, LOD0. Roof pieces and plain walls have no
  authored LODs and stay at full detail in every tier. The five unit-size props use their `_lod2` as
  the base tier.
- **The collision body** joins every piece's own `bo_` twin. A slate or stone variant with no twin
  borrows the same-shape twin's body (`BO_STANDIN`). The four thin roof trims have no body by design
  (`BO_NONE`).
- **Material names stay exact.** Importing twelve FBX files makes Blender rename repeated materials
  to `.NNN`; the assembler folds them back before joining, and the export check fails on any
  suffixed name. The `bo_` body carries `stone`, as the kit bodies do.
- **Export settings** are the Lond Cirion ones (`export_scene.fbx`, `bake_space_transform=True`,
  default axes). Every tier asserts its triangle count against the sum of its parts.

## The frame rule holds for our exports (verified)

`fbx_frame_compare.py` reads binary FBX without Blender. The roof panel and the chamfer wall
exported untouched in `frame_probe.fbx` have **the same bounds, in the file's own axes, as in their
kit source files** (roof panel (-1.5, -0.045, -3.022) to (1.5, 1.5, 0)), and both files carry the
same axis header (Y up, front Z, coord X, 1 unit = 1 cm). The kit stores a piece as centimetre data
under a node rotation of X -90 and a scale of 100; the export bakes the same transform into the
vertices. The engine applies node transforms (the authored prefabs only line up that way), so it
reads an export exactly as it reads the kit: every join holds, and the building as a whole lands
turned 180 degrees about Z from the Blender view. **The door side of `erebor_dwarf_house_01` faces +Y
in the editor at rotation 0.** The editor import confirms it.

## Verification stack

The probe imports the **exported** FBX, so it checks what the Kit receives:

| Check | Method | `erebor_dwarf_house_01` |
|---|---|---|
| Tiers | each object present; triangles equal the brief | 9,202 / 3,442 / 2,226 / 1,428, all equal |
| Grounding | lowest point of every tier against the brief | z -0.161 on every tier |
| Footprint | outer size against the brief, 0.01 m | 9.79 x 6.89 m |
| Walls | 5 cm ray grid through each wall line; a ray that passes must be inside a planned opening (from the catalog apertures), and each opening must pass rays | 0 stray rays; openings 88 to 94 % open (frames fill the edges) |
| Gables | rays along the ridge through each gable triangle | 0 of 732 pass |
| Roof | down-rays over the footprint must stop above the eave, visual and `bo_` | 0 of 805 below the eave on either |
| Floor | down-rays from under the eave must land on the floor | 693 of 693 at z 0.10 to 0.16 |
| `bo_` walls | the wall rays against the collision body | 0 stray; 56 rays on the back windows' frame edges, reported apart as edge rays |
| Shared faces | triangles lying on another triangle within 2 mm, grouped by plane | 21.2 m2 same facing, all hidden: 15.5 m2 face down at z 0 into the terrain, 2.8 m2 are wall tops under the eave beam, the rest are thin end faces inside trims and pilasters (areas are upper bounds, whole triangles counted) |

## Deliberate choices, do not "fix"

- **Gable wedges are brown brick on block walls.** The kit has only one gable wedge material
  (`Roof_Side_A1` uses `M_DW_Wall_Brick_B2`); the authored `dwarf_house_a` has the same pairing.
- **Doorways are open.** The kit has no door leaf, so the floor, the inner wall faces and the
  underside of the roof (`M_Wood_Planks_A_Old_Worn`) show through the door.
- **Buried faces are kept.** Deleting them would save a few hundred triangles per building for a new
  mesh-surgery step; they all face into the terrain, a trim or a column.

## Known open

- **The first Kit import** (`erebor_dwarf_house_01`): bind the six materials, confirm the door faces
  +Y at rotation 0, and look for flicker at the corners and the eave.
- **Preview colours are not the asset:** the kit textures do not resolve in Blender, so renders and
  `erebor_dwarf_house_01_inspect.blend` use flat colours per material.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/kitbash/README.md](./README.md)

<!-- backlinks-end -->
