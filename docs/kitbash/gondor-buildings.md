# Gondor buildings

Six whole buildings assembled from the Gondor kit part families in headless Blender and exported
together in **one FBX**, as Mike asked on 2026-10-15. The kit catalog, the frame rule and the join
matrix are in [gondor/](gondor/); this file is the pipeline, the recipes, the probe results and the
review history. It follows [erebor-buildings.md](erebor-buildings.md) and
[lond-cirion-buildings.md](lond-cirion-buildings.md).

## The file

`Modules/TAOM_Map/AssetSources/Scenes/Gondor/buildings_taom/gondor_buildings_a.fbx`, with
`gondor_buildings_a_import.md` beside it (object table, materials to bind, placement, door sides).

- Four objects per building: `<name>`, `<name>.lod3`, `<name>.lod6`, `bo_<name>`; 24 in all.
- Every building is centred on the origin, so the six overlap in the file, as the Lond Cirion
  sections did. The Kit makes one metamesh per prefix (verified on the compiled Lond Cirion file).
- The door side is -Y in Blender, so +Y in the editor at rotation 0 (the frame rule), except the
  market arcade: its door is on the east end, +X in Blender, -X in the editor; its long sides are
  open arches.
- No below-grade skirt: wall bases and the underside of every floor slab sit at z 0.
- Every material in the file has a `_mtl.tpac` under `TAOM_Map/Assets` (the import note checks).

## Buildings (measured from the export)

| Building | Footprint, height | Storeys | Roof | Placements | Tris base / lod3 / lod6 / bo |
|---|---|---|---|---|---|
| `gondor_town_house_01` | 9 x 6 m (10.06 x 7.52 outer), 4.71 m | 1 x 3 m | 26.57 degree gable, ridge along X | 49 | 18,038 / 4,818 / 1,986 / 956 |
| `gondor_townhouse_01` | 12 x 6 m (13.06 x 7.33), 9.17 m | 2 x 3 m | 45 degree gable | 96 | 61,722 / 13,734 / 5,602 / 2,720 |
| `gondor_watch_tower_01` | 6 x 6 m (7.60 x 7.60), 15.05 m | 4 x 3 m | 45 degree pyramid | 100 | 75,572 / 20,284 / 6,124 / 2,732 |
| `gondor_barracks_01` | 24 x 6 m (25.06 x 10.11 with the stair), 7.93 m | 2 x 3 m | 26.57 degree gable | 177 | 90,856 / 23,724 / 8,334 / 4,112 |
| `gondor_hall_01` | 18 x 6 m (19.06 x 8.24), 9.17 m | 1 x 6 m | 45 degree gable | 98 | 67,974 / 24,006 / 4,178 / 2,576 |
| `gondor_market_arcade_01` | 12 x 6 m (13.06 x 8.24), 7.71 m | 1 x 6 m | 26.57 degree gable | 66 | 28,388 / 10,756 / 2,224 / 1,408 |

The outer size adds the corner columns (0.53 m past each corner) and, where present, buttresses,
pillars and the stair. The assembler stops before writing anything if a tier's triangles differ from
the sum of its parts (a dropped sub-object), if the catalog is missing, or if its Lond Cirion
geometry selection disagrees with `kitbash_kits.base_names` on a placed part.

## Recipes

All walls are 3 m or 6 m panels on the footprint lines, gripped by their `.wall` body (Lond
Cirion rule 7). Every corner has a `gondor_column_3m_a_1x1` per storey, 2 % wider in plan: it closes
the corner notch and encloses the authored corner's coplanar end caps
([design-patterns.md](gondor/design-patterns.md) "Join matrix"). String courses are
`gondor_wall_trim_3m_b_clean` on the wall line (rule 6), raised by their measured cover (below).
Floors are `gondor_ground_3m_a_normal.floor`, slab bottom on the storey line.

- **Town house:** front window, door, window; back plain, window, plain; one window per end.
  Roof: `roof_a_30_3m_straight` both sides, edge strips, `side_a_clean` gables, ridge cap.
- **Townhouse:** ground floor window, door, window, window; upper floor four `window2` panels;
  back and ends windowed above. Floors at z 0 and 3. 45 degree roof as Lond Cirion house_01.
- **Watch tower:** door at the front, windows on storeys 2 and 3 alternating per face, `window2`
  all round on storey 4; floors on every storey; four `buttress_a_clean` at the middle of each
  face; a `wall_trim_6m_b` cornice at z 12; the roof is four `roof_a_45_3m_corner_out` quadrants,
  origin on each footprint corner, each turned so its measured high point is the centre
  (apex z 15.04).
- **Barracks:** parade front with two doors at x -7.5 and 7.5 and an external stair at x 1.5 to an
  upper door; blank service wall at the back below, windows above; 12 buttresses on the bay joints
  (none on the front at x 0 and 3, where the stair stands). Floors at z 0 and 3.
- **Hall:** one 6 m storey of 6 m panels: `window3` panels either side of an arched door
  (`wall_6m_arch_c_clean`), engaged round pillars either side of the door bay on both long faces
  (base `pillar_bot_a`, four `pillar_mid_a1` drums, `pillar_mid_a2`, capital `pillar_top_b`: 6.0 m),
  a `wall_trim_6m_b` cornice.
- **Market arcade:** two open `wall_6m_arch_b_clean` arches (4.56 x 5.24 m) on each long side, an
  engaged pillar between them on both sides, an arched door at the east end, a plain west end.

## Probe results (`blender_probe_erebor_building.py --name`, on the exported FBX)

| Check | Result, all six |
|---|---|
| Tiers equal the brief | yes |
| Facades | 0 stray see-through rays on every wall line; every planned opening at least 0.90 open |
| Gables | 0 open rays: 732 per 26.57 degree gable building, 1,624 per 45 degree one (the band follows `ridge_z`, so it reaches the apex); the tower has a pyramid |
| Roof | 0 down-rays below the eave; the `bo_` blocks every roof ray |
| Floor | every ray from under the eave lands on a floor (z 0.10 to 0.11, or the upper floor at 3.10 or 9.10) |
| Grounding | passes with `--ground` set to each building's planned lowest point: -0.05 (the authored depth of the bodies; panels reach -0.01), -0.25 on the barracks (the stair foot, the stair's own body and lod6) |
| Frame | `fbx_frame_compare.py`: same axis header as the source parts, identity nodes, centimetre-valued vertices |

**Shared faces.** Two tools count same-facing coincident faces, with different rules: the probe's
`doubled` check stops at the first neighbour of either facing; `blender_zfight_attribution.py`
counts any same-facing neighbour, names the two materials and tests visibility (a ray from each
face along its normal: leaves the building, `exposed`; hits geometry, `covered`; a downward face at
grade, `below grade`). The attribution's totals are therefore equal or larger.

| Building | Probe z-fight m2 | Attribution m2 | Exposed | Below grade | Covered |
|---|---|---|---|---|---|
| `gondor_town_house_01` | 21.7 | 23.3 | 0 | 10.1 | 13.1 |
| `gondor_townhouse_01` | 47.2 | 67.8 | 0.10 | 11.6 | 56.1 |
| `gondor_watch_tower_01` | 60.2 | 89.4 | 0.08 | 8.8 | 80.5 |
| `gondor_barracks_01` | 53.1 | 78.1 | 0.17 | 17.2 | 60.7 |
| `gondor_hall_01` | 28.7 | 36.6 | 0 | 15.1 | 21.5 |
| `gondor_market_arcade_01` | 22.8 | 24.2 | 0 | 10.2 | 14.0 |

- **Covered** is mostly a panel's own coplanar top (the wall box and its stonebrick, one part)
  inside the string course or cornice, slab undersides against the wall bases between storeys, and
  trim overlaps inside the corner columns.
- **Below grade** is the floor slabs and wall bottoms at z 0, hidden by the terrain.
- **Exposed** is one pattern only, `M_Gondor_Stone_Trim_A1` against itself on the outer face
  (±3.27 m) of the `window2` panels, 1.73 to 1.92 m above their storey: both faces belong to the same
  panel (inferred: one material, one placement per bay), so it is authored in the kit.

## Deliberate choices

- **Trims raised by their measured cover.** A trim's top is a moulding: the 3 m trim has V-grooves
  between its blocks, the 6 m trim's top slopes. "Top flush with the storey line" left the wall top
  coplanar with it and open to the sky (8.2 m2 on the hall at a 1 cm lift). `trim_lift()` measures
  each trim by down-rays every 2 cm over the wall band and raises it by its deepest dip plus 5 mm:
  14.9 mm for `wall_trim_3m_b_clean`, 25 mm for `wall_trim_6m_b`. The projection from the wall face
  is unchanged (flush, Mike's Lond Cirion instruction).
- **Buttresses turned by their bounds.** The body lies wholly on one side of its mounting plane;
  `lc.wall()` orients by `detail_side()` (a vertex count) and put every buttress inside its wall (no
  buttress visible in the render, about 6 m2 of z-fight per buttress on the wall faces).
  `buttress()` turns the side the body lies on outward.
- **Corner columns 2 % wider in plan.** The column's cap faces and the 6 m trim's faces both sat at
  about 0.51 m from the corner line (0.48 m2 exposed per corner on the hall and arcade, measured).
- **Corner columns have no base or capital:** the kit has none for the square 1.04 m column. The
  round pillars have both.
- **All six gabled depths are 6 m:** `lc.gabled_roof()` seats one 3 m roof module each side.

## Known open

- **The Lond Cirion barracks** in `blockout/lond_cirion_buildings_a.fbx` places its buttresses
  with the same `lc.wall()` call, so they are probably inside the walls too (inferred, not probed).
- **Pillar capitals** stand 0.36 m past the arcade eave edge and 0.6 m past the hall cornice.
- **Triangle budget:** the base tiers run 18k to 91k. The placed window panels dominate (1,548
  tris for `window`, 2,700 for `window2`, 4,224 for the `window3` 6 m panel's tracery), then the
  stonebrick reliefs. No per-placement cut is applied: the Lond Cirion helpers have no equivalent of
  the Erebor assembler's `base_lod`, Mike's standing budget-cut decision, so adding one is owed.
- **The Erebor assembler and probe were not generalised** (the spec's ground rule): another session
  held uncommitted edits in both, so the Gondor assembler reuses the Lond Cirion one instead. Moving
  `lc.main`'s join loop, world-bake and material fold into shared functions, and one z-fight
  classifier for the probe and the attribution, are follow-ups.

## Review history

- 2026-10-15: built and probed; awaiting Mike's import in the Modding Kit (GATE 3).
- 2026-10-15, `/deep-review` (six lenses): the join test and prefab rebuild dropped bare-`.lod` and
  LOD-only sub-objects; the probe stopped 45 degree gables at the 26.57 degree line; a `.001` copy
  of a body replaced the real one in the catalog; two scripts could leave a stale DONE; the trim
  lift and corner columns left exposed coplanar faces; docs claimed "metres", a +Y door on the
  arcade, and an assert the code did not make. All fixed and re-measured; the export's triangle
  counts did not change. RCA: [rca-gondor-kitbash-2026-10-15.md](../reviews/rca-gondor-kitbash-2026-10-15.md).

## Pipeline

```
blender-launcher.exe -b -P tools/oneoff/blender_assemble_gondor_buildings.py -- --out E:\LOTRAOMAssets\_export\gondor\buildings
blender-launcher.exe -b -P tools/oneoff/blender_probe_erebor_building.py -- --fbx <gondor_buildings_a.fbx> --name <building> ^
    --expected <out>\<building>_expected.json --brief <out>\<building>_preview.json --out <out>\probe_<building> ^
    --ground <the bo_ tier's lowest z: -0.05, or -0.25 for the barracks>
blender-launcher.exe -b -P tools/oneoff/blender_zfight_attribution.py -- --fbx <gondor_buildings_a.fbx> --name <building> --out <out>\zfight
```

The assembler imports its part-family helpers from `blender_assemble_lond_cirion_buildings.py`.
Outputs in `E:\LOTRAOMAssets\_export\gondor\buildings\`: renders `<building>__{q1,q2,front,side,top}.png`,
the brief and layout JSON per building, `gondor_buildings_a_inspect.blend` (the exported file
re-imported, one collection per tier, the buildings 35 m apart), `zfight\<building>_zfight_*` (one
DONE file per building).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/kitbash/gondor/design-patterns.md](gondor/design-patterns.md)
- [docs/kitbash/gondor/README.md](gondor/README.md)
- [docs/kitbash/lond-cirion-buildings.md](./lond-cirion-buildings.md)
- [docs/kitbash/README.md](./README.md)

<!-- backlinks-end -->
