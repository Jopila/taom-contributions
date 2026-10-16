# Campaign map icons

What draws a settlement on the campaign map, where icon sources live, the conventions a new icon
follows, and a registry of the custom icons TAOM has made. The procedure for a new icon from a Tripo
model, decimated or rebuilt, is the [`/prep-tripo-prop`](../../.claude/skills/prep-tripo-prop/SKILL.md)
skill.

## How an icon is drawn

A settlement entity in `TAOM_Map/SceneObj/Main_map/scene.xscene` (named by its id, for example
`town_G3`) has a child entity whose `meta_mesh_component` names the icon metamesh. Settlement data
has no icon field: the `map_icon` attribute has no managed reader
([settlements.md](../modding/settlements.md)). The entity also carries the walls, `town_gate`,
`map_icon_siege` slots and its own `bo_town` collision, so an icon mesh needs no `bo_`.

A faction metamesh is shared. `gundabad_town` draws three settlement entities (2026-10-16:
`rg -c 'meta_mesh_component name="gundabad_town"'` on the scene file), so giving one settlement its
own icon means pointing only that entity's child at a new metamesh. Re-importing over a shared
metamesh changes every settlement that uses it.

## Where sources live

`TAOM_Map/AssetSources/Campaign Map/Map Icons/<faction>/`, one folder per faction (Dale, Dol Guldur,
Dunland, Erebor, Gondor, Gundabad, Isengard, Lothlorien, Mirkwood, Mordor, Others, Rhun, Rivendell,
Rohan, Umbar): the FBX at the top, textures in `textures/`. TAOM_Map is unversioned, so a reinstall
removes these files; the staging folder in the registry below is the source of truth, and the skill
rebuilds an icon from it.

## Conventions for a new icon

- **Mesh:** visual only, no `bo_` (the shipped `gundabad icons.fbx` has none). The prep script's
  `<name>_low.fbx` is that file: copy it to the faction folder as `<name>.fbx`.
- **Budget:** measure the faction's existing icons first (`tools/blender/probe_fbx.py`). The
  Gundabad set, as a reference:

  | Mesh | Triangles | Size (m) |
  |---|---|---|
  | `gundabad_town` | 91,089 | 60.5 × 62.4 × 29.2 |
  | `gundabad_castle_3` | 23,249 | 30.4 × 34.8 × 29.5 |
  | `gundabad_map_fortress` | 11,449 | 75.9 × 27.0 × 36.1 |
  | `gundabad_castle_2` | 9,158 | 30.1 × 34.5 × 26.0 |
  | `gundabad_village_warg_pit` | 5,874 | 6.6 × 6.1 × 1.2 |
  | `gundabad_village_camp` | 4,711 | 5.4 × 5.0 × 1.5 |

  The town entity draws its child at scale 0.1, so a 60 m model is the size of `gundabad_town`.
- **Textures:** 1K at most for a new icon (Mike, 2026-10-16), packed `t_<name>_{d,n,s}`; an icon
  with several texture sets gets one triple per set, and a small set (iron, openings) can be 256.
  The shipped faction sets use `<faction>_<set>_{d,h,n,s}` at 2K; a new icon does not need an `_h`.
- **Frame:** the FBX reads back in Blender at rotation X 90 degrees and scale 0.01 (centimetre mesh
  data); `carn_dum_town.fbx` probed that way on 2026-10-16. That is the Kit's proven Y-up format
  ([lond-cirion-buildings.md](lond-cirion-buildings.md), [gondor/design-patterns.md](gondor/design-patterns.md)):
  in the Kit it lands upright, turned 180 degrees about Z, which the entity's rotation absorbs.

## Rebuilding an icon from solids

A Tripo model is a sculpt: rounded corners and dripping walls. Decimated to 25,000 or 90,000
triangles, the Carn Dûm model read as melted wax in the Kit, so it was rebuilt instead
(2026-10-16). The hand-made Gondor icons are box-modelled the same way (`minas_tirith`, 63,597
triangles of flat planes and hard edges).

1. **Measure the Tripo layout** (heights, radii, tower positions) and write it into a builder script's
   layout table; `tools/oneoff/blender_build_carn_dum_icon.py` is the pattern. Every face carries a
   material slot and a `part` attribute (towers, walls, keep and so on).
2. **Spend the budget where the map shows it.** The icon draws LOD0, so Mike set the whole 90,000
   triangles; walls stay plain, the towers, keep and gate carry the detail.
3. **Unique UVs:** `tools/blender/unwrap_texture_sets.py` with a rules file
   (`tools/oneoff/carn_dum_texture_sets.json`) splits the faces into texture sets, gives hidden faces
   almost no space and reports inverted faces. Carn Dûm: towers, walls, town and paving at 1K, roof
   512, iron and dark 256; about 10 to 15 px per metre on visible faces, which is what the map shows
   at full zoom.
4. **Painter:** `painter_new_project.py` with `low_as_high` bakes AO, curvature, position, thickness
   and world-space normals off the mesh itself; `painter_stack_angmar_fortress.py` paints each set;
   `tools/blender/render_tiled_preview.py` renders the export.
5. **Convert and hand off** as in the skill. The Kit needs one material per texture set.

What 1K shows on a 55 m icon: features under 2 texels (about 18 cm) vanish, height relief tilts the
normal map instead of adding detail, and worn edges, soot, streaks and stone courses do not read
(Mike, 2026-10-16). Colour, broad tone and the baked occlusion do.

## Custom icons

| Icon | Settlement | Source | Kit files | Staging (source of truth) | State |
|---|---|---|---|---|---|
| `carn_dum_town` | `town_G3` Carn Dûm (Gundabad) | Rebuilt from solids (`blender_build_carn_dum_icon.py` v10) on the layout of Mike's Tripo model: 84,053 triangles, 54.8 × 55.9 × 40.8 m, 7 texture sets (unique UVs); Painter stone colour, tone and baked occlusion | Obsolete Tripo version still there: `Gundabad/carn_dum_town.fbx` (25,000 triangles) and `Gundabad/textures/t_carn_dum_town_{d,n,s}.png`, copied 2026-10-16 and imported by Mike the same day (`Assets/Campaign Map/Map Icons/Gundabad/carn_dum_town_geo.tpac`, material `t_carn_dum_town`), not placed: the map scene does not name it. The rebuild is not copied yet; its material names (`carn_dum_towers` and so on) do not collide with `t_carn_dum_town` | `E:\LOTRAOMAssets\_export\carn_dum_rebuild\v10\` (`uv\carn_dum_town.fbx` and `_uv.blend`, `carn_dum_fortress.spp`, Painter exports `paint_r14\`); the Tripo version in `carn_dum_icon_final\` | Texturing in progress (round 14 of the paint; the paving set still to repaint as stone). Then: conversion to `t_*_{d,n,s}`, the Kit copy (backup of the Tripo FBX), 7 materials, the `town_G3` child swap (`Gundabad`, now `gundabad_town`) and the in-game check |
