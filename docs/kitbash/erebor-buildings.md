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
| `erebor_dwarf_house_01` | 9 x 6 m (9.79 x 6.89 outer) | 1 x 3 m | gable, slate, ridge along X at z 4.5 | 67 | 14,314 / 3,490 / 2,274 / 1,476 | **Approved in the Kit** 2026-10-15 (second import, after review) |
| `erebor_great_hall_01` | 21 x 12 m with 3 m chamfers (21.79 x 12.89 outer) | 2 x 3 m | hipped, slate, flat deck at z 7.5 | 247 | 25,590 / 10,390 / 7,382 / 4,404 | Imported in the Kit 2026-10-15; Mike went on to the barracks ("Excellent, now lets do a barracks") |
| `erebor_barracks_01` | 24 x 12 m (24.79 x 12.89 outer) | 2 x 3 m | 12 m gable, stone, ridge along X at z 9 | 236 | 28,216 / 12,552 / 8,168 / 5,384 | Brief approved and imported in the Kit 2026-10-15; **Mike's verdict owed** |

Planned next, one brief and one review at a time: a multi-storey keep, a castle wall section with a
tower and a gate, and a forge or market. All are exterior shells for scene dressing.

All three were re-exported after the 2026-10-15 review fixes with the same placements (as a set),
triangle counts and bounds. The Kit packages from the first exports hold the same shapes, so no
re-import is needed; a re-import gives the same building.

### `erebor_dwarf_house_01`

- **Walls**, dressed block (`_C`):
  - Front (-Y in Blender): `Win_C3`, `Door_C1`, `Win_C3`.
  - Back: `Win_C1`, `Wall_3m_C`, `Win_C1`.
  - West end: two `Win_C2`. East end: `Wall_3m_C` and `Win_C2`.
- **Corners:** `Clmn_3m_C` with the square `Clmn_Top_D` (x1.1/1.1/1.2) as its cap at z 3 and as
  its base at z 0. The `Clmn_Top_*` capitals serve as column bases as well as caps.
- **Door:** `Clmn_3m_A` pilasters (x1.5/1.5/1) with `Clmn_Top_F` bases and caps (x1.5/1.5/1.2).
- **Courses:** `Beam_3m_B` at z 0 and `Beam_3m_C` at z 3 on every wall bay.
- **Roof:** `Roof_Str_A1` slate panels on both long walls, a `Roof_Side_Str_A1` ridge strip, and
  `Roof_Side_A1` gable wedges on both ends.
- **Floor:** six `Ground_3m_A2` carved stone tiles, as in `dwarf_house_b`.
- **Grade:** the underside of the floor slab and the wall bases are at z 0, so the house **sits on
  the terrain**. The lowest points are the base beam (z -0.125) and the pilaster bases (z -0.161),
  which sink just under the ground.
- **Review history (Mike, 2026-10-15):**
  - The first export carried a 3 m plain-wall skirt below grade. Removed: the floor sits on the terrain.
  - The first Kit import showed the `Ground_3m_C1` pavement floor reading as roof tiles (a plain block
    grid like the slate roof). The floor is now the stone `Ground_3m_A2`.
  - The corner columns had no base. They now take `Clmn_Top_D` at the foot as well as the head.
- **Probe (2026-10-15, after the review fixes):**
  - Tiers 14,314 / 3,490 / 2,274 / 1,476, all equal to the brief; lowest point z -0.161 on every
    tier; outer size 9.79 x 6.89 m.
  - Walls: 0 stray rays on all four wall lines; every window and the door 88 to 94 % open (the
    frames fill the edges). `bo_` walls: 0 stray; 56 rays on the back windows' frame edges,
    reported apart as edge rays.
  - Gables 0 of 732 rays through; roof 0 of 805 down-rays below the eave on the mesh or the `bo_`;
    floor 589 of 589 at z 0.10 to 0.12.
  - Same-facing shared faces 21.2 m2, all on the 12 planes the probe lists and all hidden: 15.5 m2
    face down at z 0 into the terrain, 2.8 m2 are wall tops under the eave beam, the rest are thin
    end faces inside trims, pilasters and column bases.

### `erebor_great_hall_01`

- **Plan:** 21 x 12 m between wall centres (5 bays on the long sides, 2 on the ends) with 3 m
  chamfered corners; two storeys of 3 m. Centred on the origin, front to -Y in Blender.
- **Walls**, dressed block (`_C`), both storeys:
  - Front, lower: `Win_C3`, `Wall_3m_C`, `Door_C1`, `Wall_3m_C`, `Win_C3`. Upper: `Win_C2`,
    `Win2_C1`, `Win_C2`, `Win2_C1`, `Win_C2`.
  - Back, lower: two `Win_C1` between plain walls. Upper: two `Win_C2`.
  - Ends: `Win_C1` below, `Win_C2` above. Chamfers: `Wall_3m_Corn_C` below, `Corn_Win_C` above.
- **Columns:** `Clmn_3m_C` (two high) where each chamfer meets a straight wall, with
  `Clmn_Top_D` at the foot and the head. Door: `Clmn_3m_A` pilasters two high with `Clmn_Top_F` at
  0, 3 and 6 m.
- **Courses:** `Beam_3m_B` at z 0, `Beam_3m_A` at z 3, `Beam_3m_C` at z 6, on every bay and across
  each chamfer (scaled 1.5 along the beam, turned 45 degrees), as in `dwarf_house_b`.
- **Roof:** hipped: `Roof_Str_A1` slate panels on every straight wall rise 1.5 m to a flat deck of
  `Roof_Top_A1` at z 7.5; `Roof_Cor_Tri_A1` over each chamfer; `Roof_Side_Cor_Out_A1` ribs on the
  panel seams, `Roof_Side_Str_A1` strips on every deck tile edge, `Ground_Trim_Corner_A1` studs at
  the rib heads.
- **Floor:** `Ground_3m_A1` stone with `Ground_3m_A1_Corner` in the chamfer cells (only A1 has a
  chamfer half tile). On the terrain.
- **Budget cut** (per placement, `base_lod`): the floor tiles use their flat 12-triangle `.lod2` as
  the base tier, since the floor is seen only through the door and windows (saves 16,416).
- **Review history (2026-10-15):** the brief placed the two statues at x3 in front of the plain bays
  beside the door. Mike removed them ("not needed for this"); the plain bays stay. Their source FBX
  material names (`tripo_mat_8fbb426d`, `SD_DW_IH_Statue_B`) have no material package in
  `TAOM_Map/Assets`, which a building that uses them must resolve in the Kit.
- **Probe (2026-10-15, after the review fixes):**
  - Tiers 25,590 / 10,390 / 7,382 / 4,404, all equal to the brief; lowest point z -0.161;
    outer size 21.79 x 12.89 m.
  - Walls: both storeys of all four wall lines 0 stray rays, every window and the door open.
    `bo_` walls: 0 stray, 280 edge rays on window frames.
  - Chamfers: the four plain ones closed (0 of 2,668 rays each), the four window ones open (176
    of 2,668 each).
  - Roof 0 of 3,637 down-rays below the eave on the mesh or the `bo_`; floor 3,141 of 3,141 at
    z 0.10.
  - Same-facing shared faces 30.4 m2. The 12 planes the probe lists hold 29.0 m2, all hidden:
    17.4 m2 face down into the terrain, 5.0 m2 are upper wall tops under the eave beam, 2.9 m2 sit
    at the storey line inside the string course, the rest are thin end faces inside trims and
    columns. The other 1.4 m2 lies on smaller planes the probe does not list.

### `erebor_barracks_01`

- **Plan:** 24 x 12 m between wall centres (8 x 4 bays), square corners, two storeys of 3 m.
  Centred on the origin, front to -Y in Blender.
- **Walls**, dressed block (`_C`):
  - Front, lower: two `Door_C1` at x -7.5 and 7.5 with `Win_C3` and `Win_C1` between. Upper:
    `Win2_C1` dormitory windows in all eight bays.
  - Back: `Win_C1` and plain walls on both storeys. Ends: `Win_C1` below; `Win_C2` and `Win_C1`
    above.
- **Columns:** `Clmn_3m_C` two high at the corners and at the centre of each long wall, with
  `Clmn_Top_D` at the foot and the head. Each door has `Clmn_3m_A` pilasters (x1.5) on both sides,
  one storey high, with `Clmn_Top_F` base and cap.
- **Courses:** `Beam_3m_B` at z 0, `Beam_3m_A` at z 3, `Beam_3m_C` at z 6.
- **Roof:** a **12 m gable** in stone: two rows of `Roof_Str_B1` per side (eaves at z 6, second row
  at z 7.5), ridge at z 9 with a `Roof_Side_Str_A1` strip. Each gable end is `Roof_Side_A1` (outer
  3 m) plus `Roof_Side_A2` (inner 3 m), which continue one coped slope to the ridge: the kit's
  gable wedges are built for a 12 m span. The stone panels have no `bo_` twin and borrow
  `bo_SM_DW_Roof_Str_A1`. The rule is in [design-patterns.md](erebor/design-patterns.md),
  "Our 12 m gable".
- **Floor:** `Ground_3m_A2` at its flat `.lod2`, on the terrain.
- **Probe (2026-10-15, after the review fixes):**
  - Tiers 28,216 / 12,552 / 8,168 / 5,384, all equal to the brief; lowest point z -0.161;
    outer size 24.79 x 12.89 m.
  - Walls: both storeys of all four wall lines 0 stray rays, every window and both doors open.
    `bo_` walls: 0 stray, 952 edge rays on window frames.
  - Gables 0 of 3,250 rays through, up to the ridge; roof 0 of 4,465 down-rays below the eave on
    the mesh or the `bo_`; floor 3,913 of 3,913 at z 0.10.
  - Same-facing shared faces 36.2 m2. The 12 planes the probe lists hold 32.0 m2, all hidden:
    22.4 m2 face down into the terrain, 2.4 m2 are wall tops under the eave beam, 2.5 m2 sit at
    the storey line inside the string course, the rest are thin end faces on the wall lines inside
    trims, columns and pilasters. The other 4.2 m2 lies on smaller planes the probe does not list.

## Pipeline

Delete the old marker (`DONE.txt`, the probe's `probe_DONE.txt`) before each launch: the
launcher returns at once, and until the script starts, the file there is the previous verdict.

| Step | Tool | Output (desktop, not in git) |
|---|---|---|
| Brief and blockout | `tools/oneoff/blender_assemble_erebor_buildings.py --preview` (the default) | `E:\LOTRAOMAssets\_export\erebor\buildings\<name>_{q1,q2,front,side,top}.png`, `<name>_preview.json` |
| Export | `... --export` | `TAOM_Map/AssetSources/Scenes/erebor/buildings/<name>.fbx` and `<name>_import.md`; `<name>_expected.json` and `<name>_export.json` in the output folder |
| Probe | `tools/oneoff/blender_probe_erebor_building.py` | `<name>_probe.json` |
| Inspect | `... --inspect` | `<name>_inspect.blend` from the exported FBX, one collection per tier |
| Frame check, once per kit | `... --frame-probe`, then `tools/oneoff/fbx_frame_compare.py` | `frame_probe.fbx`; console |
| Kit import | Mike, in the Modding Kit | `TAOM_Map/Assets/Scenes/erebor/buildings/<name>_geo.tpac` |
| Package check | `python tools/check_rdc_entries.py --module "<game>\Modules\TAOM_Map" --under Scenes/erebor/buildings` | console; `without-rdc=0` (3 of 3 on 2026-10-15) |

**Building specs** are Python functions in the assembler that return placements
`(piece, location, yaw, scale, role, base_lod)`, written in the Blender import frame with the join
rules of `design-patterns.md`. Helpers hold the rules that repeat: `column()` (a `Clmn_3m_C` per
storey with `Clmn_Top_D` at the foot and the head), `pilaster()` and `courses()`. One spec drives
the blockout render, the triangle budget, the export and the probe layout. Preview and export are
separate runs, because Gate 2 sits between them; the probe compares the export with the preview
that Mike approved.

**The probe layout comes from the placements.** The eave is the highest wall top, the half sizes
are the outermost wall lines, and the chamfer size comes from the chamfer pivots. The assembler's
`ROOF` table adds only what placements cannot show: the roof form, the ridge axis and, for a gable,
its rise (the layout's `ridge_z` is eave + rise). `<name>_expected.json` then carries the planned
openings (catalog apertures moved by each placement), every wall line with its span and height,
and every chamfer wall with a window flag.

**Export rules:**

- **LOD tiers** use the Lond Cirion names. `.lod3` takes each piece's `.lod2`, else `.lod4`, else
  LOD0. `.lod6` takes `.lod5`, else `.lod4`, `.lod2`, LOD0. Roof pieces and plain walls have no
  authored LODs and stay at full detail in every tier. The five unit-size props use their `_lod2` as
  the base tier. The import note lists, per tier, how many placements took each level.
- **The collision body** joins every piece's own `bo_` twin. A slate or stone variant with no twin
  borrows the same-shape twin's body (`BO_STANDIN`). The four thin roof trims have no body by design
  (`BO_NONE`).
- **Material names stay exact.** Importing twelve FBX files makes Blender rename repeated materials
  to `.NNN`; the assembler folds them back before joining. The `bo_` body carries `stone`, as the
  kit bodies do. The import note marks any material with no `<name>_mtl.tpac` under
  `TAOM_Map/Assets`.
- **Budget cuts** are per placement: `base_lod` makes an authored LOD the base tier, and no lower
  tier uses a more detailed one.
- **Export settings** are the Lond Cirion ones (`export_scene.fbx`, `bake_space_transform=True`,
  default axes).
- **The export refuses** (status `failed`, the live folder untouched) when a spec names an
  unknown piece, a tier's triangles differ from the sum of its parts, a material keeps a `.NNN`
  suffix, or a placement's collision body is missing (the `BO_NONE` trims have none by design). It builds the FBX in the output folder first, then
  copies the previous live FBX to `<name>.fbx.bak-<mtime>` and copies the new one in. Delete the
  `.bak-*` copy once the new building is checked.

## The frame rule holds for our exports (verified)

`fbx_frame_compare.py` reads binary FBX without Blender. The roof panel and the chamfer wall
exported untouched in `frame_probe.fbx` have **the same bounds, in the file's own axes, as in their
kit source files** (roof panel (-1.5, -0.045, -3.022) to (1.5, 1.5, 0)), and both files carry the
same axis header (Y up, front Z, coord X, 1 unit = 1 cm). The kit stores a piece as centimetre data
under a node rotation of X -90 and a scale of 100; the export bakes the same transform into the
vertices. The engine applies node transforms (without them no kit piece fits the 3 m grid of the
authored prefabs), so it reads an export exactly as it reads the kit: every join holds, and the
building as a whole lands turned 180 degrees about Z from the Blender view. **Expected:** the door
side of `erebor_dwarf_house_01` faces +Y in the editor at rotation 0. The frame rule predicts it;
the editor has not confirmed it yet. `fbx_frame_compare.py` models the node translation, rotation,
pre-rotation and scale; it flags a model that also uses pivots, offsets, post-rotation, a
geometric transform or a parent model. No Erebor file does.

## Verification stack

The probe imports the **exported** FBX, so it checks what the Kit receives. The results are in each
building's section above.

| Check | Method |
|---|---|
| Tiers | each object present; triangles equal the brief |
| Grounding | lowest point of every tier against the brief |
| Footprint | outer size against the brief, 0.01 m |
| Walls | 5 cm ray grid through each wall line over its full height; a ray that passes must be inside a planned opening (from the catalog apertures), and each opening must pass rays |
| Chamfer walls | rays along each chamfer wall's normal: a plain one must be closed, a window one must pass rays |
| Gables | rays along the ridge through each gable triangle, up to the layout's `ridge_z` |
| Roof | down-rays over the footprint must stop above the eave, visual and `bo_` |
| Floor | down-rays from under the eave must land on the floor (visual mesh only) |
| `bo_` walls | the wall rays against the collision body; rays on frame edges are reported apart as edge rays |
| Shared faces | triangles lying on another triangle within 2 mm, grouped by plane; the 12 largest planes are listed (areas are upper bounds, whole triangles counted). The scan depends on triangle order: the same hall placements gave 30.6 m2 before the review fixes reordered them and 30.4 m2 after, so read the area as an estimate |

**Probe limits** (owed in the probe, which the Gondor work now shares): the probe writes no
pass or fail of its own, so read every check; the floor rays do not test the `bo_` body; and a
floor ray passes anywhere above z -0.05, not only at grade.

## Deviations from the prompt

- **No 1e-4 m snapping.** Every coordinate is a literal or a sum of 1.5 m steps, so nothing needs
  it.
- **Renders:** five per building (two three-quarter views, front, side, top), no close-up of a
  risky join. The probe's ray checks cover the joins instead.
- **Briefs named no risky joins.** The probe checks every gable, chamfer and eave line.

## Deliberate choices, do not "fix"

- **Gable wedges are brown brick on block walls.** The kit has only one gable wedge material
  (`Roof_Side_A1` uses `M_DW_Wall_Brick_B2`); the authored `dwarf_house_a` has the same pairing.
- **Doorways are open.** The kit has no door leaf, so the floor, the inner wall faces and the
  underside of the roof (`M_Wood_Planks_A_Old_Worn`) show through the door.
- **Buried faces are kept.** Deleting them would save a few hundred triangles per building for a new
  mesh-surgery step; they all face into the terrain, a trim or a column.

## Known open

- **Heading in the editor:** the door side of each building should face +Y at rotation 0 (the
  frame rule). Not yet reported for any of the three imports.
- **`erebor_barracks_01`:** Mike's verdict on the Kit import.
- **Preview colours are not the asset:** the kit textures do not resolve in Blender, so renders and
  `<name>_inspect.blend` use flat colours per material.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/kitbash/erebor/blender-build-prompt.md](erebor/blender-build-prompt.md)
- [docs/kitbash/erebor/design-patterns.md](erebor/design-patterns.md)
- [docs/kitbash/erebor/README.md](erebor/README.md)
- [docs/kitbash/gondor-buildings.md](./gondor-buildings.md)
- [docs/kitbash/gondor/blender-build-prompt.md](gondor/blender-build-prompt.md)
- [docs/kitbash/README.md](./README.md)

<!-- backlinks-end -->
