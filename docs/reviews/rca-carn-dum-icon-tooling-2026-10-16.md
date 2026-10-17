# RCA: the Carn Dûm map icon tooling (rebuild, unique UVs, scripted Painter; 2026-10-16)

## Top-line

The Carn Dûm icon (`town_G3`) was rebuilt from solids because the Tripo model melted under
decimation, unwrapped into 7 texture sets by a new generic tool, and textured by scripted Substance
Painter recipes over the remote-scripting bridge. Texturing is not finished (Mike continues it).
Mike committed and pushed the session's files at 17:36 in `71fea53c8` while the review ran, so
every fix below lands in follow-up commits.

`/deep-review` ran seven `deep-reviewer` lenses in two waves (Standards, Engine compatibility, Data
flow, Tooling; then Efficiency, Completeness, Design) and no XML lens (no XML in scope). **No engine
API in scope**, but three engine claims in the docs were wrong. The Tooling lens checked every
builder primitive's winding by hand: all correct after the session's `obox` fix. The findings
cluster in five patterns, below. Every finding is fixed in this change or listed with its owner.

## Findings

| # | Sev | Bug | Found by | Category | Why missed | Preventive action |
|---|---|---|---|---|---|---|
| 1 | HIGH (lens rule) | `convert_tripo_prop_textures.py` defaulted `--dst` to the live `TAOM_Map\AssetSources\Scenes\Mordor\textures` and overwrote with no backup; this change added a `mkdir` and sent every prop through it | Tooling, Standards | tooling, defaults | Pre-existing default, never read when the script was extended | `--src` and `--dst` required, refusal to overwrite without `--force`; tests |
| 2 | MED | A multi-set Painter export without `--match` converted only the alphabetically first set (the 256 px openings) under the mesh's name and dropped six | Data flow, Tooling | data flow, handoff | The converter and the skill's Phase 4 were written for one set; Phase 2B sent seven into it | Without `--stem` every set becomes `t_<set>`; with `--stem` one set must match; tests |
| 3 | MED | Completion files could report a failed or stale run as success: the builder wrote `ok` before its export, three Blender scripts wrote only on success and never removed an old file, unwrap and prep removed the old one only after argparse, the probe never removed its JSON | Tooling, Design | tooling, evidence | Each script rolled its own report; the Store launcher detaches, so the file is the only signal | `tools/blender/blender_job.py` `job()` for the builder, unwrap, preview, probe and layout scripts; it caught a real failure on its first run (v11 folder missing); tests |
| 4 | MED | `painter_new_project.py` closed an open project without checking for unsaved work; a failed load or bake still exited 0 (any string RESULT is success) | Tooling, Design | tooling, data loss | Written for a clean session; failure was data, not an exception | Refuse unless `discard_unsaved`; raise on load and bake failure; painter_run documents "a script fails by raising". Not run in Painter yet (closed) |
| 5 | MED | A large part of the town and walls tiles (the lenses estimated 40% and a fifth) held buried geometry at full density: each terrace riser and the outer wall's faces ran from 1 m under ground in one quad, mostly hidden | Tooling, Efficiency, Design | geometry, texture budget | Visibility is judged per face; nothing reported hidden area kept at density | v11 seats each face on what hides it: town 9.7 to 11.9 px/m, walls 11.8 to 12.9; the unwrap now reports `visible_share` and `hidden_kept_m2` per part |
| 6 | MED | "10 to 15 px per metre ... what the map shows at full zoom" was an estimate stated as fact, and wrong: per `MapCameraView` that density matches the default distance (about 13 px per model metre at 1080p, the icon at 0.15 scale), full zoom is about 50 | Engine | evidence, engine | Written from a guess at the campaign camera, never checked against its code | Reworded as an estimate with the constants; whether 1K suffices is now a listed decision |
| 7 | LOW to MED | "The `_s` map's AO only dims ambient light" was wrong (it also scales the sun's specular), and the recipe paints the same occlusion into the albedo, so it darkens twice in game | Engine | evidence, engine | Stated from memory of other engines | Reworded with the shader citations; occlusion in one channel only is a listed decision |
| 8 | LOW | "The game culls back faces" was absolute; culling is per material (`MaterialFlags.TwoSided`) | Engine | evidence | Generalised from the Gundabad materials | Reworded in the lesson and the unwrap docstring |
| 9 | MED | The handoff: Kit materials must carry the FBX set names and `use_specular`; the rebuild's handoff FBX is `uv\<name>.fbx`, not `_low.fbx`; `validate_mesh_refs --check-name` sees metamesh names only; the `carn_dum_town` set converts to the Tripo version's texture names | Data flow, Engine, Completeness | handoff | The skill's Phase 4 predates the rebuild route | Skill Phase 4 and map-icons.md rewritten; the name clash is a listed decision |
| 10 | LOW | The frost recipe sent a linear value to a Substance material colour parameter | Data flow, Tooling, Design | colour space | The linear fix was applied to fills in one script, not to its sibling | One recipe file, `painter_stacks.py`, with `rgb_param()` for material parameters |
| 11 | LOW | `painter_run.py` reported a timeout as "cannot reach Painter", printed `None` for an unknown error dict, and timed out at 600 s against a 3000 s bake wait | Tooling, Data flow | tooling | Every `OSError` was one case | Connect and request handled apart, `--timeout 3600`; tests |
| 12 | LOW | Unwrap: tie order not repeatable, `--object` half wired, a mirrored transform baked without flipping normals, a missing `part` attribute silent, a dead Smart UV path, `--hidden-scale` never in effect, an estimate 1.2 to 1.8 times high, inverted faces not gating | Tooling, Design, Data flow | tooling | Grew by experiment within one session | Fixed; inverted faces over 0.1% of the area now fail the run; tests |
| 13 | LOW | `painter_export.py` took the shortest preset name with "metal" and "rough", treated a string `sets` as a substring test, rounded a non-power-of-two size down | Tooling | tooling | Edge cases untried | Exact preset name, list `sets`, power-of-two check |
| 14 | LOW | The stack scripts deleted old layers before looking up shelf resources, so a lookup failure left a half-deleted stack | Tooling | tooling | | Every resource resolved first |
| 15 | LOW | Builder: one gable end in roof material, flying-buttress piers 0.1 m above the wall, an unused key, the tiling route's box UVs and a second `carn_dum_town.fbx` | Tooling, Efficiency, Data flow | geometry | | Fixed in v11; the tiling route retired (planes script, tiling recipe, builder FBX) |
| 16 | LOW | Docs: Painter named 12.0.3 (12.1.5 installed since 2026-09-15), stale docstrings, "0.4 m²" (measured 0.71), "2,686 became 204" (206 measured), "no plain PBR preset" (one ships as a starter asset), a general MCP fact in the Painter doc, living tools under "One-offs", the skill's phase order and description | Standards, Data flow, Engine | docs | Written fast during a long session | Fixed |
| 17 | MED | No tests for the new pure logic | Standards, Completeness | tests | Offline art tools were treated as untestable | Four test files, 35 tests: `test_blender_job`, `test_convert_tripo_prop_textures`, `test_painter_run`, `test_unwrap_texture_sets` |
| 18 | MED | Documented steps whose only tool sat in the session scratchpad (layout measurement, albedo render, the paint loop) | Completeness | workflow | **Repeat within the session:** the lesson "Scratchpad scripts are not a workflow" was written the same day | `measure_tripo_layout.py`, `render_painter_preview.py --albedo`, the loop's commands in the skill |
| 19 | MED | No GitHub issue for the icon or its tooling | Standards, Completeness | process | Asked about at the start, never created | Owed: Mike's word |
| 20 | HIGH (process) | `71fea53c8` went out without the version label and with a body that is not a release note, and it carried other sessions' uncommitted work (the Erebor dress fix, KEYforce follow-ups, `translate_with_claude.py`) | Completeness | process | Most likely committed outside Claude Code, where no TAOM gate runs (#802); `check-commit-subject-version.sh` refuses this subject | This change's commit bodies carry the release-note paragraph for the icon tooling; Mike told which other work landed there |

Follow-ups not applied (measured or owned elsewhere): the towers' inner halves inside the outer wall
(1,677 m² hidden at density in v11), paving under parapets and the next terrace, seated box bottoms
(1,636 triangles), one shared flat normal for the 7 sets, the prep script's `chart` default and its
probe label mix-up, `uv_utilization` and `shelf_pack` reusing the island helpers.

## Convergence pass

One `deep-reviewer` read the diff of the fixes above and found nine more defects, all fixed in
this change:

| # | Sev | Defect | Fix |
|---|---|---|---|
| C1 | MED | The prep script still removed its old `DONE.txt` only after argparse (finding 3 named it; the fix missed it) | It clears the report before argparse (`blender_job.flag_value`), and the parser takes no abbreviations |
| C2 | MED | The recipe merge dropped the frost recipe's colour overrides (`grime`, `edge`, `frost`), and unknown ARGS keys passed in silence, so the skill's false-colour check did nothing | Those keys are back, `frost_stone` reads `stone`, an unknown key raises |
| C3 | LOW | `uv_report` was read without checking its status | A report that is not `ok` raises |
| C4 | LOW | A `sets` entry naming no texture set, or a run with nothing to paint, exited 0 | Both raise |
| C5 | LOW | The prep script's "each chart is one island" comment ignores non-manifold edges | Comment corrected (telemetry only) |
| C6 | LOW | The converter skipped files whose role word does not end the name, without a word; two docs still showed `--match` without the now required `--src` and `--dst` | It prints each skipped file; both docs fixed |
| C7 | LOW | Two new test files errored without numpy or Pillow | They skip |
| C8 | LOW | The Painter page's install table still said 12.0.3 and the lowercase MCP key | 12.1.5 (the exe says so) and `E:/repos/TAOM` |
| C9 | LOW | The unwrap docstring gave the lowest player tilts as the camera's elevation | Reworded with the untilted angle |

Still unproven until Painter runs again: the refusal and raise paths in the Painter scripts, the
preset name in `list_predefined_export_presets()`, which usage `find()` picks for `stone_slate`,
and the unwrap's mirrored-transform flip (never run on a mirrored mesh; Blender documents that
`Mesh.transform` inverts normals under a negative matrix, so one flip should restore them).

## Root-cause patterns

1. **Success was self-reported.** A report file, an exit code or a RESULT said "ok" without proof:
   findings 3, 4, 11. The fix makes failure the default path (`job()`, raise in Painter scripts).
2. **One-item steps took many items.** Findings 2 and 9: a converter and a handoff written for one
   texture set silently took the first of seven.
3. **Estimates written as facts.** Findings 6, 7, 8 and 16: the density, AO, culling and version
   claims were memory or arithmetic, not readings. `evidence-over-claims.md` already forbids it.
4. **Defaults nobody re-read.** Finding 1, and the prep script's `chart` default.
5. **Paid-for geometry nobody sees.** Finding 5: per-face visibility hides a partly buried face's
   cost.

## Why the authoring missed these

This was the first review of the change. Everything was written in one long session that moved
from a Tripo retexture to a rebuild to unique UVs, and each tool grew by experiment. The renders
looked right at every step, and Blender's tone mapping and back-face drawing hid three defects (the
colour space, the normal tilt, the box winding) that only measurements showed.

## Feedback memories to codify

None new: the patterns map onto existing rules (`evidence-over-claims.md` for 3, the XML I/O
convention's "dry-run default and backups" for 4 applied to textures). Five lessons in
`docs/reviews/lessons/build-tooling-workflow.md` record the cases.
