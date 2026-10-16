# RCA: Gondor kitbash catalog and six buildings, deep review (2026-10-15)

**Summary.** One session on 2026-10-15 generalised the Erebor kitbash tools with `--kit` (a shared
table and part rules in `tools/oneoff/kitbash_kits.py`), measured the Gondor kit (330 kit parts, 166
props and references), proved its frame rule, mined and tested its joins, and, on Mike's word,
assembled six buildings into one FBX (`Scenes/Gondor/buildings_taom/gondor_buildings_a.fbx`) with
a new assembler on the Lond Cirion helpers. Nothing is committed. A `/deep-review` ran six lenses:
Engine compatibility (2), Data flow (5), Tooling and Completeness (4) in the first wave, Efficiency
(3) and Design (6) in the second. Standards (1) and XML (7) were not in scope (no C#, harness file
or XML). No finding was HIGH. Three lenses found the same MEDIUM data gap independently (F1). Two
more defects (F5, F13) surfaced only while the fixes were being verified. All defects in this
change are fixed and re-measured; the export's 24 tier triangle counts did not change.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action | Status |
|---|---|---|---|---|---|---|
| F1 | MEDIUM | The prefab renderer and the join tester kept their own name filter and dropped a bare `.lod` and LOD-only sub-objects, so the 45 degree gable was tested and rebuilt without its infill plate; the doc's "rule 5 holds" and "gable join clean" rested on it (completeness F1, tooling 1, data flow F1) | Duplicated rule set | `--kit` was added to each loader, but each kept a local copy of the part rules beside the tested `group_parts` | `kitbash_kits.base_names` is the loaders' only rule; the assembler asserts the Lond Cirion selection agrees with it on every placed part; lesson appended | Fixed: gable join re-measured with plates (z-fight 0, butt 2.65 m2), wallhouse rebuild 125 meshes |
| F2 | MEDIUM | The probe's gable band was a fixed 26.57 degree line and never read the `ridge_z` the assembler writes, so the top half of the two 45 degree gables was never tested while the doc said "0 open rays of 732" (completeness F2, data flow F2) | Written field without a reader | The field was added for the probe in one step, the probe in none | The probe reads `ridge_z` (lines added beside the other session's pitch hunk); lesson appended | Fixed: 1,624 rays per 45 degree gable building, 0 open |
| F3 | MEDIUM | A Blender `.NNN` copy replaced the plain name in the `bo_` and LOD slots: the catalog kept the 36-tri `bo_gondor_castle_wall_tower_L1_A_int.001` over the real 1,166-tri body and listed 9 copies as `stonebrick` LODs of `gondor_wall_6m_window3_a_dirty` (tooling 5, data flow F3, design 1) | Rule applied to one slot | The duplicate rule was written for the base slot only | One pre-pass drops a copy whenever its plain name exists, for every slot, with a warning; tests both name orders | Fixed: catalog re-run, `meshes.md` regenerated |
| F4 | MEDIUM | `blender_zfight_attribution.py` and `blender_kit_frame_probe.py` did not delete their DONE file before running (one `zfight_DONE.txt` served six buildings), and the catalog validated `--folder` before deleting its DONE (tooling 2 and 4, design 4) | Stale completion marker | **Third occurrence** in two days: the rule existed as an uncommitted lesson written the same day by the Erebor session; these scripts came from a template that predates it | Per-building DONE removed before the run; the catalog clears DONE before any check and writes `failed`; per-folder runs get their own DONE and log. A shared helper or gate is proposed below | Fixed |
| F5 | MEDIUM | Coplanar faces were left open to the sky: "top flush with the storey line" put trim tops level with the wall tops, a fixed 1 cm lift still left 8.2 m2 on the hall (the 6 m trim's top slopes, the 3 m trim has V-grooves between blocks), and the 6 m trim's faces shared a plane with the corner columns' caps (0.48 m2 per corner) | Assumed geometry | The bbox top was taken for the top surface; z-fight was read as an area, never as visibility. Found by the visibility test added to the attribution during Step 4 | `trim_lift()` measures each trim's cover by down-rays every 2 cm (14.9 mm and 25 mm); corner columns 2 % wider; the attribution reports exposed, covered and below-grade area; lesson appended | Fixed: exposed area 0 to 0.17 m2 per building, all of it the `window2` panels' own surround |
| F6 | MEDIUM | The per-plane z-fight table judged only the top of each list (caps of 12 and 15 planes, several outward-facing planes unjudged) and the probe's `grounding_ok` failed on all six without the doc saying so (completeness F3, data flow F5 and F6) | Partial evidence presented as complete | The table was written from the capped outputs | Every plane written; the doc gives exposed, covered and below-grade totals per building and names the tool behind each number; `--ground` is each building's planned lowest point | Fixed: grounding passes on all six |
| F7 | LOW | The probe and the attribution classify a face sandwiched between an opposite-facing and a same-facing surface differently, and the doc mixed their numbers (data flow F4, design 5, tooling 3) | Two counters | The attribution was written beside the probe, not into it | The doc labels each tool's numbers; one classifier is a follow-up (the probe is pre-existing and shared) | Documented, follow-up |
| F8 | LOW | Docs were wrong: the export "stores metres" (it stores centimetre values), every front "faces +Y" (the arcade's door faces -X), the assembler "asserts" the triangle sum (it warned), "1,548 to 3,852 tris" per window (3,852 is a panel no building places), 53 + 29 of 88 shipped-building frames named, decal counts (engine W1 and W3, data flow F10) | Record written before or without its evidence | Each sentence was written from memory of the code, not from the code | Corrected from the outputs; the assembler now does assert (it stops before writing) | Fixed |
| F9 | LOW | Completeness gaps: no `tools/README.md` rows for four new tools, stale docstrings (Gondor join table "empty", "every tool takes --kit"), Phase 6 not done for `design-patterns.md`, deviations from the spec not recorded, pure helpers without tests, Lond Cirion knowledge only in the Gondor docs (completeness F4 to F9, tooling 6 and 10) | Missing record | The work stopped at the user-facing report | Rows, docstrings, a "Placement rules learned" section, deviations in "Known open", 15 new tests, a pointer in `lond-cirion-buildings.md` | Fixed |
| F10 | LOW | Unused or unreachable table fields: `out` read by nothing, the Gondor `catalog_json` never produced so `erebor_catalog_to_md.py --kit gondor` failed on a missing file (data flow F8) | Dead configuration | Fields were added for symmetry | `out` deleted; a clear error names the per-folder file | Fixed |
| F11 | LOW | Prefabs carry `rotation_mat` and `non_orthogonal_rotation_mat`, which the miner and the rebuild read as unrotated (1,400 of the 51,491 Gondor kit placements; 9 in the wallhouse) (engine W2; the Erebor RCA's H1) | Engine reader copied from one sample | Pre-existing in the Erebor tools | Follow-up: fixing it changes the Erebor mined numbers that the Erebor session's uncommitted docs quote | Open, recorded in `design-patterns.md` |
| F12 | LOW | The probable Lond Cirion barracks defect (buttresses inside the walls, same `wall()` call) | Same trap, older file | Pre-existing; the barracks was judged by renders | Pointer in `lond-cirion-buildings.md` "Known-open"; an issue on Mike's word | Open |
| F13 | LOW | The catalog's results depended on the import order: Blender's native FBX importer creates objects in a varying order, so two runs listed decals differently and "first base wins" was order-dependent (found while verifying F3) | Non-determinism | The Erebor parity runs happened to be stable | `group_parts` sorts prefix-kit names first; a test proves order independence; two runs of `walls` now give identical JSON | Fixed |

## Convergence pass (Step 4.6)

One `deep-reviewer` checked the applied fixes for defects and parity. No HIGH or MEDIUM; five LOW
gaps in the fixes themselves, all fixed and re-run (tests 47 passed; the six buildings rebuilt with
identical triangle counts and zero warnings):

- **C1:** the z-fight tool cleared its DONE file after `argparse`, the exact case of the DONE lesson
  (F4 was only partly closed). It now clears the marker from the raw arguments first.
- **C2:** one line of `design-patterns.md` still said the kit stores "centimetres" (F8 partly closed).
- **C3:** the `kit_part_names` test imported `audit_fbx_lods` only because pytest adds `tools/`; it
  now adds the path itself and passes as a plain script too.
- **C4:** the `.NNN` pre-pass moved the duplicate-base branch out of every test; a test now pins it
  in both input orders.
- **C5:** a wall missing from the catalog stopped the assembler only after the live export; the
  check now runs before anything is written.

## Erebor hand-off (rca-erebor-kitbash-2026-10-15.md, part 2)

The Erebor session recorded 13 findings for the tools this session edits. Status after this change:

- **Fixed here:** G2 (the probe reads `ridge_z`), H5 for the catalog (DONE cleared before any check),
  H11 in part (an `--only` or `--folder` run writes its own JSON, DONE and log; views and sheets
  are still shared), H13 in part (`kitbash_kits.base_names` and `FBX_EXPORT` are shared; the import,
  colour, camera and log code is not yet).
- **Open (pre-existing, not changed here):** H1 (matrix rotation forms, F11), H2 (negative scale read
  as yaw 180), H3 (no probe verdict block), H4, H5 for the probe, joins and render, H6 to H10, H12.
  They change Erebor numbers that the Erebor session's uncommitted docs quote, so they belong in one
  follow-up change made with that session's docs.

## Not applied

- **Design 5 and Efficiency 1 (one z-fight classifier: fold the attribution into the probe).** The
  probe's `coincident()` is pre-existing and holds another session's uncommitted hunk; the doc labels
  each tool's numbers meanwhile. Follow-up.
- **Design 6 and 7 (the Lond Cirion assembler on `kitbash_kits` rules; shared assembler plumbing).**
  Pre-existing code; the agreement assert covers the risk for the six buildings. Follow-up.
- **Tooling 7 (export only with `--apply`).** The whole assembler family writes the live file
  unconditionally; changing one assembler alone would split the convention. The catalog is now read
  before any write. Follow-up for the family.
- **Tooling 8 (a status key in the catalog JSON).** It would change the Erebor JSON, which must stay
  byte-identical; a placed wall missing from the catalog now stops the assembler instead.
- **Efficiency 2b (bulk material-index copy).** The gain was not measured; part 2a (no per-vertex
  copies) was applied and proved pixel-identical on 67 renders.
- **Importer choice (efficiency cross-lens note).** The assembler keeps the legacy importer that
  produced the verified export (comment added); the catalog and probe agree with it on names and
  apertures (measured).

## Root-cause patterns

**1. A rule kept in more than one place.** F1 (loader filters beside `group_parts`), F3 (the
duplicate rule in one slot of three) and F7 (two z-fight counters) are one pattern: the same
judgement implemented twice, with no test that the copies agree. The 2026-09-29 lesson "A tool kept
in two places drifts" named it; the generalisation added parameters to each copy instead of
deleting the copies.

**2. Measuring the wrong thing with confidence.** F2 (a band tuned on a 26.57 degree roof), F5 (a
bbox top taken for a surface, an area taken for visibility) and F6 (capped lists read as complete)
all reported a clean number that did not cover the question. Each was fixed by making the check
reach the real geometry: the apex, the moulding, every plane.

**3. A lesson written in parallel does not reach a template in flight.** F4 is the third stale-DONE
defect in two days; the rule was written the same day in another session's uncommitted lesson.

## Why each lens's earlier passes missed these

No earlier deep review covered this change. Within this review:

- **F1** was found by three lenses (completeness, tooling, data flow) from three directions: the
  doc claim, a replay of the filter on the kit names, and the joins JSON.
- **F2** needed the probe code and the assembler output side by side; only completeness and data
  flow read both.
- **F5 and F13** were found by none: they showed only when the fixes were verified with a new
  visibility test and a second identical run.
- **Engine compatibility** confirmed the frame rule and the Kit import assumptions from the native
  importer, which no earlier pass had decompiled.

## Lessons

Appended to `docs/reviews/lessons/build-tooling-workflow.md`:

- A Blender loader that filters names itself drops what the shared part rules keep.
- A probe must read every layout fact the builder writes.
- A trim's top is a moulding: measure its cover before calling it flush.
- Sort what a parallel importer creates before grouping it.

The buttress lesson written earlier in this session was corrected (the side is read from the
bounds). No feedback memory: each lesson is a rule for this kind of tool.

## Preventive action for the repeat offender (F4)

A third stale-DONE defect means the docstring rule is not enough. Proposed (Mike's call, not done
here): one small `done_marker(out, name)` helper used by every detached Blender tool, plus a test
that greps `tools/oneoff/blender_*.py` for a DONE write without the helper.

## Owed

- **Mike:** the tracking issue (none filed: no answer yet); the GATE 3 Kit import; an issue for the
  Lond Cirion barracks buttresses (F12) and for the follow-ups above; whether
  `docs/kitbash/gondor/blender-build-prompt.md` (another session's file) is committed with this
  change, since its docs link to it.
- **Commit hygiene:** `blender_probe_erebor_building.py`, `tools/README.md` and
  `docs/kitbash/README.md` hold other sessions' uncommitted hunks; stage this change's hunks only.
