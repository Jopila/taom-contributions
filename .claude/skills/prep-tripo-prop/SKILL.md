---
name: prep-tripo-prop
description: Use when a Tripo AI FBX must become a Bannerlord prop or map icon, from decimation and UVs through a scripted Painter retexture to the Kit handoff.
argument-hint: [<tripo fbx> ...]
---

# Prepare a Tripo prop or map icon

A Tripo export is a dense triangle soup with a fragmented UV atlas and a texture nobody can paint.
This skill makes it a game asset: a decimated low poly with clean UVs, textures painted in Substance
Painter from mesh maps baked off the full-resolution source, packed into `t_<stem>_{d,n,s}`, then
handed to the Modding Kit. Every step is a script, so a run can be repeated and tuned.

**Owning docs:** the UV method data and the full path are the Tripo section of
[ue-to-bannerlord-asset-pipeline.md](../../../docs/reference/ue-to-bannerlord-asset-pipeline.md);
the Painter bridge and its traps are
[substance-painter-mcp-setup.md](../../../docs/reference/substance-painter-mcp-setup.md).
Tool flags are in each script's docstring.

## Before you start

- A GitHub issue for the asset (AGENTS.md "Documentation duty").
- Staging goes to `E:\LOTRAOMAssets\_export\<asset>\`. Writing into a live module's `AssetSources\`
  needs Mike's word, and never overwrites a file.
- Blender: `"%LOCALAPPDATA%\Microsoft\WindowsApps\blender-launcher.exe"`. With `-b` it detaches and
  prints nothing; each script writes a completion file (`_report\DONE.txt`, `preview_done.txt`).
- Painter: start `C:\Users\mikew\substance_painter_mcp\launch-painter-remote.bat`.
  `tools/painter/painter_run.py` needs only that bridge, not the MCP server.

## Phase 1: Analyze

1. Probe every candidate and the asset it replaces:
   `blender-launcher -b -P tools/blender/probe_fbx.py -- <in.fbx> <out.json>`.
2. Look at each `.fbm` texture. A Tripo atlas is noise: plan to retexture, not to paint on it.
3. Set the triangle budget from the reference asset. Map icon textures are 1K; prop textures are
   diffuse 2K and the rest 1K.
4. Report triangles, textures and atlas state per candidate before changing anything.

## Phase 2: Blender prep

1. Probe two UV methods on the decimated mesh, in parallel. Same arguments for both runs, then
   `--uv-method sharp_seams --probe-spreads 50` for one and `--uv-method chart_abf --probe-spreads 75`
   for the other:
   `blender-launcher -b -P tools/oneoff/blender_prep_tripo_prop.py -- --src <fbx> --dst <dir> --staging <dir> --name <mesh> --material t_<mesh> --scale-mode length --size <m> --decimate-tris <n> ...`
2. Pick from `_report\DONE.txt` with the targets in the pipeline doc (use ≥ 40%, texel density
   p95/p5 ≤ 2.5, folds < 1%). `sharp_seams` wins on a clean low poly; `chart_abf` 75 on a decimated
   Tripo surface. Never `chart`.
3. Full run with the chosen method. Add `--skip-bakes --export-high` when Painter will retexture:
   it writes `<mesh>.fbx` (with `bo_`), `<mesh>_low.fbx` and `<mesh>_high.fbx`. Without
   `--skip-bakes` the Tripo maps are rebaked onto the new UVs instead.
4. To let Mike pick between candidates, run them without `--skip-bakes`, then in GUI mode (no `-b`):
   `blender-launcher -P tools/blender/compare_prop_candidates.py -- <mesh> A=<dir a> B=<dir b>`.

## Phase 3: Retexture in Painter

1. `python tools/painter/painter_run.py tools/painter/painter_new_project.py --arg low=<dir>\<mesh>_low.fbx --arg high=<dir>\<mesh>_high.fbx`.
   Expect `bake: Success` and six maps (AO, Curvature, Normal, Position, Thickness, WorldSpaceNormal).
2. `python tools/painter/painter_run.py tools/painter/painter_stack_stone_frost.py [--arg key=value ...]`.
   The docstring lists the keys and what each does. For another look, copy it to
   `tools/painter/painter_stack_<look>.py` and keep it idempotent.
3. `python tools/painter/painter_run.py tools/painter/painter_export.py --arg out_dir=<dir>\painter --arg spp=<dir>\<mesh>.spp`.
4. `blender-launcher -b -P tools/blender/render_pbr_preview.py -- <dir>\<mesh>_low.fbx <dir>\painter\t_<mesh>_ <dir>\painter`.
   Read both previews before you show them. If a layer looks wrong, read the exported `baseColor`
   PNG: it shows which mask covers what.
5. Show Mike the previews and the `.spp`. Iterate with `--arg` until he approves.

## Phase 4: Convert and hand off

1. `python tools/oneoff/convert_tripo_prop_textures.py --src <dir>\painter --dst <dir>\painter\converted --stem t_<mesh> --max-size 1024`.
   "metal map is near-constant" is correct for stone.
2. Check `t_<mesh>_s.png` against the ORM map: R = metallic (ORM B), G = 1 − roughness (ORM G),
   B = AO (ORM R).
3. Check both names are free: `python tools/validate_mesh_refs.py --check-name <mesh>` and `t_<mesh>`.
4. With Mike's word, copy the FBX and the three textures into the Kit folder, new names only, and
   compare hashes after. A prop takes `<mesh>.fbx` (with its `bo_`). A map icon takes
   `<mesh>_low.fbx` renamed `<mesh>.fbx` (no `bo_`: the settlement entity has its own) into
   `TAOM_Map\AssetSources\Campaign Map\Map Icons\<faction>\`, textures in `textures\`; conventions in
   [map-icons.md](../../../docs/kitbash/map-icons.md). Read back in Blender, the FBX sits at rotation
   X 90 and scale 0.01: that is the Kit's proven format, not a fault.
5. Mike restarts the editor (it scans resources at startup only), imports the textures, then the
   mesh, and makes the material `t_<mesh>`. For a map icon he points the settlement entity's child
   `meta_mesh_component` in `SceneObj/Main_map/scene.xscene` at the new metamesh. Change only that
   one entity: a faction icon metamesh is shared (`gundabad_town` draws three settlements), so
   re-importing over it changes every one of them.
6. In game: right scale, no pink or white texture, siege and gate slots in place.

## Phase 5: Records

Progress comment on the issue with the numbers. A map icon gets a row in the registry in
[map-icons.md](../../../docs/kitbash/map-icons.md); a prop gets one in its kit's row of
[docs/kitbash/README.md](../../../docs/kitbash/README.md). Either names the mesh, the Kit files and the
staging folder, which is the source of truth because the live modules are unversioned. New lessons
go to `docs/reviews/lessons/build-tooling-workflow.md`, a better default goes into this skill.

## Top traps

| Symptom | Fix |
|---|---|
| Painter refuses every color ("non legacy color space") | The project is Legacy: create it with `painter_new_project.py` (OCIO) |
| Frost or another world-space mask covers every wall | Walls sit at 0.5 in that mask: raise `ws_balance` |
| Blotches that break at UV island edges | Generator grunge runs in UV space: `Use_Triplanar=1` |
| Thousands of islands, single-digit UV use | Sharp seams on a decimated mesh: use `chart_abf` |
| Converter finds no roughness or metallic | Painter 12 exports glTF ORM; the converter reads the packed map |
