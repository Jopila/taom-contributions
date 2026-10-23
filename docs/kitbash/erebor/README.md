# Erebor dwarven kitbash catalog

Reference for the erebor dwarven kit under
`Modules/TAOM_Map/AssetSources/Scenes/erebor/` and its packaged form at
`Modules/TAOM_Map/Assets/Scenes/erebor/`.

## Files in this catalog

| File | What's in it |
|---|---|
| [textures.md](textures.md) | Raw `t_dw_*` textures (diffuse/normal/specular/height maps) |
| [materials.md](materials.md) | `m_dw_*` materials — what textures each one binds and what it's used for |
| [meshes.md](meshes.md) | Every piece measured: size, pivot, slope, openings, LODs, collision body |
| [design-patterns.md](design-patterns.md) | The frame rule, the tested join matrix and recipes from the authored buildings |
| [runes.md](runes.md) | Dwarven rune pieces — carved wall/trim meshes and their texture pipeline |
| [blender-build-prompt.md](blender-build-prompt.md) | Claude Code prompt: measure every piece in Blender, then assemble buildings, with review gates |

## Quick reference

- **Measured, not guessed.** Sizes, pivots, slopes and openings come from headless Blender
  (2026-10-15); the joins come from the authored TAOM_Map prefabs and were re-tested in Blender.
- **The frame rule.** The engine sees each Erebor FBX turned 180 degrees about Z from Blender's
  import. Convert a prefab offset (x, y, z, yaw) to (-x, -y, z, yaw) before using it in Blender.
  See [design-patterns.md](design-patterns.md).
- **Letters are per family, not kit-wide.** Wall `_A` is brown brick, `_B` brick with trim, `_C`
  dressed block with trim; roof letters and digits mean material or size depending on the
  sub-family. The full table is in [meshes.md](meshes.md) "Material letters". Within one
  building, keep one wall letter.
- **Grid and heights.** 3 m plan grid, 3 m storeys, roof pitch 26.57 degrees, eave on the wall
  centreline at the wall top, castle modules 20 m.

## Kit scope

| Family | Pieces | Notes |
|---|---|---|
| Ground tiles and floor trim | 10 | 3 m tiles, chamfer half tile, 6 m plinth block, seam trims, studs |
| Pillars | 4 | Hall scale, 11 to 46 m tall |
| Platforms | 14 | Terrace blocks: top at z 0, foundations to z -12 |
| Roof | 28 | Panels, flat deck plates, hip and valley corners, chamfer facets, gable wedges, strips |
| Wall overlays | 12 | Beams, columns, capitals |
| Walls | 24 | 3 m plain, chamfer corner, door and window walls in A, B, C |
| Decoration | 1 | Gold pile |
| Castle | 47 | Straight wall, gate, ramp, wall stairs, two towers, tower interior, bastion, 39 merlons |
| Unit-size props | 5 | Moria pillar, broken pillar, wall fragment, two statues; never placed in a prefab |
| **Total** | **145** | In the 12 source FBX files. `taom_erebor_kitbash.xml` also places shared `sm_nat_*` cliffs |

## Related files

- Kitbash prefab: `Modules/TAOM_Map/Prefabs/taom_erebor_kitbash.xml`: 184 entries, of which
  164 are single pieces (140 `sm_dw_*`, 24 `sm_nat_*`) and 20 are composed buildings such as
  `dwarf_house_a`, `dwarf_house_b`, `dwarf_barracks` and `dwarf_castle_wall_c`
- Source FBX: `Modules/TAOM_Map/AssetSources/Scenes/erebor/{meshes,walls,statues}/*.fbx`
- Packaged meshes: `Modules/TAOM_Map/Assets/Scenes/erebor/{meshes,walls,statues}/*_geo.tpac`
- Measure the kit: `tools/oneoff/blender_catalog_erebor.py`, then `tools/oneoff/erebor_catalog_to_md.py`
- Mine the authored joins: `tools/oneoff/mine_erebor_prefab_joins.py`
- Rebuild an authored prefab in Blender: `tools/oneoff/blender_render_erebor_prefab.py`
- Test join rules: `tools/oneoff/blender_test_erebor_joins.py`
- Assemble, export and probe whole buildings: `tools/oneoff/blender_assemble_erebor_buildings.py`,
  `tools/oneoff/blender_probe_erebor_building.py`, `tools/oneoff/fbx_frame_compare.py`; see
  [erebor-buildings.md](../erebor-buildings.md)
- Exported buildings: `Modules/TAOM_Map/AssetSources/Scenes/erebor/buildings/*.fbx`, packaged
  by the Modding Kit to `Modules/TAOM_Map/Assets/Scenes/erebor/buildings/*_geo.tpac`
- FBX mesh lister: `tools/list_fbx_objects_all.py`

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/kitbash/erebor/blender-build-prompt.md](./blender-build-prompt.md)

<!-- backlinks-end -->
