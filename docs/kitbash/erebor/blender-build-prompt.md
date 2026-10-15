# Erebor kitbash: understand it, then build with it in Blender

Paste everything below the line into a fresh Claude Code session in `E:\repos\TAOM`.

**Status (2026-10-15, #797):** Phases 1 and 2 are done; their results are in
[meshes.md](meshes.md) and [design-patterns.md](design-patterns.md), and the tools exist in
`tools/oneoff/`. A new session resumes at Phase 3 for the next building and re-runs Phase 1 only
if the kit FBX files change. Built so far: [erebor-buildings.md](../erebor-buildings.md).
Settled since this prompt was written: the frame rule (the engine reads each Erebor FBX turned
180 degrees about Z from Blender's import), the prop scale (x3) and **no below-grade skirt**: a
building's floor slab sits on the terrain.

---

You will learn the Erebor dwarven kitbash piece by piece, then assemble buildings from it in
headless Blender. Work in phases. Stop at every GATE and wait for Mike's word.

## Inputs

- Source FBX (read-only, never modify or re-save):
  `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\AssetSources\Scenes\erebor\`
  `meshes\` (8 files), `walls\` (2 files), `statues\` (2 files). Textures are in `textures\`.
- Existing catalog, to treat as HYPOTHESES, not facts: [README.md](README.md),
  [meshes.md](meshes.md), [design-patterns.md](design-patterns.md), [materials.md](materials.md).
- The proven method for a sibling kit, to reuse and adapt (do not start from zero):
  [lond-cirion-buildings.md](../lond-cirion-buildings.md) (seven placement rules, deviations,
  ray-probe stack), [lond-cirion-walls.md](../lond-cirion-walls.md) (material `.NNN` trap, export
  transform), `tools/oneoff/blender_catalog_parts.py`, `tools/oneoff/blender_dump_fbx_inventory.py`,
  `tools/oneoff/blender_assemble_lond_cirion_buildings.py`.

## Ground rules

- Blender runs headless through the MS-Store launcher, which DETACHES:
  `"%LOCALAPPDATA%\Microsoft\WindowsApps\blender-launcher.exe" -b -P <script> -- <args>`.
  Each script writes `DONE.txt` and `log.txt` in its output folder; wait for `DONE.txt`, then read
  the log. Use `log()`, not `print`.
- New scripts go in `tools/oneoff/` (`blender_catalog_erebor.py`,
  `blender_assemble_erebor_buildings.py`, `blender_probe_erebor_building.py`). Write them with the
  Write tool, never a Bash heredoc. Use `python`, never `python3`.
- All scratch, renders and JSON go on E: under `E:\LOTRAOMAssets\_export\erebor\`.
- Textures do not resolve in a headless preview; materials fall back to flat colours. Judge
  geometry with numbers and rays, never with preview colour.
- Every number you write in a doc comes from a script output you read this session. Mark each
  catalog claim `measured` or `inferred`.
- No commit, push or GitHub issue without Mike's word. Before Phase 3, ask Mike whether to file
  the tracking issue.

## Phase 1: measure every piece

Write `blender_catalog_erebor.py` by adapting `blender_catalog_parts.py`. Import each FBX in its
own clean scene. For every object, record in `erebor_catalog.json`:

- source file, object name, part prefix and sub-objects (`.wall`, `.lodN`, bare `.lod`, decals)
- LOD siblings present, and the `bo_` twin (or "orphan")
- import transform (expect rot X +90, scale 0.01: confirm, do not assume)
- bounding box min and max in metres, size X/Y/Z, tris, materials
- origin position relative to the bbox, classified (base-centre, centre, corner, top edge, other)
- facing: the side that carries the detail, from area-weighted face normals
- for roof and ramp pieces: slope angle from the dominant face normal, and rise/run
- for doors, windows and gates: aperture width and height, from rays through the piece
- for stairs and ramps: which end is high, in local coordinates
- grid fit: whether the footprint is a multiple of 3 m, and the off-grid remainder

Render numbered contact sheets (10x10, three-quarter view) plus front, side and top ortho views
per piece, with a 1 m grid and the origin marked.

## Phase 2: understand how the pieces join

From the Phase 1 data, not from names:

1. Give each piece a role (floor, wall, corner, aperture wall, overlay trim, column, capital,
   platform, stair, roof slope, ridge, eave, hip, valley, gable, tower, merlon, battlement,
   statue, decoration, nature).
2. Define its connection points: where it meets its neighbours, in local coordinates.
3. Build a join matrix: which pieces meet on which edge, at what offset and rotation, and with
   what gap or overlap. Test each claimed join with a two-piece Blender placement and a
   coincident-plane check.
4. Resolve every open unknown in `design-patterns.md` (roof pitch, ridge Z, castle wall module
   width and height, tower height, corner-wall behaviour, merlon seating).
5. Check each Lond Cirion rule against this kit: decal trap, bare `.lod` trap, origin-anchored
   parts, gable seating, straddling trims, gripping the structural body. Record which apply.
6. Catalog the uncatalogued files (`kys_dw_*`, `dw_moria_pillar_01`, both statues).

Then correct the docs in place: `meshes.md` gets measured sizes and pivots per piece;
`design-patterns.md` gets the join matrix, the resolved unknowns and the corrected rules; fix or
remove the stale `tools/build_erebor_kitbash.py` reference in `README.md`.

**GATE 1.** Show Mike: the catalog summary table, the contact sheets, the join matrix, every
place the old docs were wrong, and every piece you could not classify. Stop.

## Phase 3: design brief per building

Order: dwarf house, great hall, multi-storey keep, castle wall section with tower and gate,
forge or market. For one building at a time, write a brief: purpose, footprint on the 3 m grid,
storeys, roof form, the piece list with counts, one material family (A, B or C; never mixed
within one building), elevations as simple text or blockout renders, the expected tri budget,
and the risky joins. These are exterior shells for scene dressing: closed or blind doors are
acceptable, no interiors.

**GATE 2 (per building).** Mike approves or changes the brief. Stop.

## Phase 4: assemble and export

Write `blender_assemble_erebor_buildings.py` by adapting the Lond Cirion assembler:

- place pieces from the Phase 2 connection points, with coordinates snapped to 1e-4 m
- remap Blender's `.001` material duplicates to the original names before any join
- join per LOD tier (base, lod tiers as the kit provides) plus one `bo_` body built from the
  pieces' `bo_` twins; assert `tris == expected_sum` per tier
- export to `...\TAOM_Map\AssetSources\Scenes\erebor\buildings\<name>.fbx` with the same export
  settings as the Lond Cirion exporter; do not touch `bake_space_transform`
- render five ortho views, two three-quarter views and one close-up of the riskiest join

## Phase 5: verify the exported FBX

Write `blender_probe_erebor_building.py`. It reads the EXPORTED FBX, not the scene. Check:

- closure: rays across every gable, wall and roof plane; only planned apertures pass
- no doubled planes: a coincident-triangle scan
- grounding: the base sits at Z 0 (or the planned skirt depth) on every tier
- `bo_` body: down-rays and side-rays hit it where the visual mesh is
- footprint: outer size matches the brief within 0.01 m
- tri counts per tier against the brief's budget

Write `<name>_import.md` beside the FBX: materials to bind, expected size, origin, tiers.

**GATE 3 (per building).** Show Mike the renders and the probe table. Mike imports into the
Modding Kit. Do not start the next building until Mike reports on this one.

## Phase 6: record what you learned

After each building, add its recipe and any new rule to `design-patterns.md`, and add a section
to a new `docs/kitbash/erebor-buildings.md` modelled on `lond-cirion-buildings.md` (sections
table, rules, deviations, verification). Append review lessons to
`docs/reviews/lessons/<category>.md`. Run `python tools/lint_docs.py`.
