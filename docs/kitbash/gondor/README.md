# Gondor kitbash catalog

Reference for the Gondor kit under `Modules/TAOM_Map/AssetSources/Scenes/Gondor/`. It repeats the
Erebor catalog ([../erebor/](../erebor/)) on a kit built from **part families**.

## Files in this catalog

| File | What's in it |
|---|---|
| [meshes.md](meshes.md) | Every part measured: size, pivot, LODs per sub-object, collision body, sub-objects |
| [design-patterns.md](design-patterns.md) | The frame rule proof, the tested join matrix, the Lond Cirion rules re-checked, recipes, roles, the proposed building list |
| [blender-build-prompt.md](blender-build-prompt.md) | Claude Code prompt: measure every part in Blender, then assemble buildings, with review gates |

## Quick reference

- **Measured, not guessed.** Sizes, pivots and LODs come from headless Blender (2026-10-15); the
  joins come from 51,491 authored placements in the TAOM_Map prefabs and were re-tested in Blender.
- **A piece is a part family.** `<prefix>` plus sub-objects (`.wall`, `.stonebrick`, `.window`,
  `.decalleak`, ...), LODs per sub-object, one `bo_<prefix>`. In the engine a meta-mesh is every
  object sharing the prefix, so a prefab names the prefix.
- **The frame rule is the Erebor one.** The engine sees each kit FBX turned 180 degrees about Z
  from Blender's import. Convert a prefab offset (x, y, z, yaw) to (-x, -y, z, yaw). Proved two
  ways; see [design-patterns.md](design-patterns.md).
- **Grid and heights.** 3 m plan grid, 3 m storeys, 0.5 m wall thickness (centred on the wall
  line), `roof_a_30_*` pitch 26.57 degrees, `roof_a_45_*` pitch 45 degrees.
- **Exclude decals** from every bound and anchor: 161 `.decal*` objects, some hanging 1.47 m
  below their part.

## Kit scope (measured)

| Folder | Parts | Role |
|---|---|---|
| `meshes` | 263 | **The kit**: wall panels, trims, roofs, pillars, buttress, stairs, paving, railing, bridge, castle pieces, bricks and rubble |
| `walls` | 67 | **The kit**: castle walls, towers, ramps (the Lond Cirion walls use them) |
| `statues` | 26 | Props |
| `Comprehend` | 49 | Props from another pack; no LODs, no bodies; 16 of them carry 492,032 tris each |
| `ships` | 3 | Harbour props |
| `buildings` | 88 | Shipped buildings, already merged: references and whole props, not parts |
| `blockout` | not catalogued | Ours: the Lond Cirion exports |

`meshes/gate bak.fbx` (a backup) and `meshes/VEG_Grass_Tussock_A.fbx` (vegetation) are excluded.

## Related files

- Source FBX: `Modules/TAOM_Map/AssetSources/Scenes/Gondor/{meshes,walls,statues,Comprehend,ships,buildings}/*.fbx`
- Kit table shared by every tool: `tools/oneoff/kitbash_kits.py` (`--kit gondor`)
- Measure the kit: `tools/oneoff/blender_catalog_erebor.py --kit gondor --folder <f>` (writes
  `gondor_catalog_<f>.json`; poll `_report\gondor_catalog_<f>_DONE.txt`, read
  `_report\gondor_catalog_<f>_log.txt`), then
  `tools/oneoff/erebor_catalog_to_md.py --kit gondor --json <file>`
- Mine the authored joins: `tools/oneoff/mine_erebor_prefab_joins.py --kit gondor`
- Rebuild an authored prefab: `tools/oneoff/blender_render_erebor_prefab.py --kit gondor`
- Test join rules: `tools/oneoff/blender_test_erebor_joins.py --kit gondor`
- Frame probe: `tools/oneoff/blender_kit_frame_probe.py`, then `tools/oneoff/fbx_frame_compare.py`
- Six buildings in one FBX, probed: [../gondor-buildings.md](../gondor-buildings.md)
  (`tools/oneoff/blender_assemble_gondor_buildings.py`, `tools/oneoff/blender_zfight_attribution.py`)
- Measured outputs (desktop, not in git): `E:\LOTRAOMAssets\_export\gondor\` (`catalog\`,
  `joins\`, `frame\`, `buildings\`)
- Earlier Gondor work: [../lond-cirion-buildings.md](../lond-cirion-buildings.md),
  [../lond-cirion-walls.md](../lond-cirion-walls.md)
