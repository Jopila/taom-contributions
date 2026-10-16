# RCA: Erebor kitbash buildings (#797), deep review (2026-10-15)

**Summary.** One session on 2026-10-15 measured the Erebor kit (145 pieces), proved how its pieces
join and how the engine reads its FBX files, and built three merged buildings from it in headless
Blender: `erebor_dwarf_house_01`, `erebor_great_hall_01` and `erebor_barracks_01`. The house and the
hall were committed in `fe06128ea` and `83b946e55`; the barracks and everything below are
uncommitted. A `/deep-review` then ran six lenses over the session's scope: Engine compatibility (2),
Completeness (4), Data flow (5) and Tooling in the first wave, Efficiency (3) and Design (6) in the
second. Standards (1) and XML (7) were not in scope: no C#, harness file or XML changed. No finding
was HIGH. Four MEDIUM defects sat in the assembler and its docs and are fixed; three MEDIUM defects
and most LOW items sit in tools that a parallel Gondor session now holds and edits, so they are
handed off below instead of fixed here. A convergence pass on the applied fixes found no HIGH or
MEDIUM defect; its LOW findings are fixed (part 3).

**Re-run after the fixes (19:13).** All three buildings were rebuilt, exported, probed and saved as
inspect files with the fixed assembler. Placements (as a set), triangle counts per tier, bounds,
grounding, openings, gables, roof and floor results are the same as before the fixes. The three
live FBX files were rewritten with the same shapes; the Kit packages from the first exports stay
valid.

## Findings, part 1: the assembler, its tests and the docs (fixed in this session)

| # | Sev | Bug | Category | Why missed | Preventive action | Status |
|---|---|---|---|---|---|---|
| A1 | MEDIUM | A failed `--export` deleted the live FBX: the backup was an `os.replace` (a move) before anything was built, and a missing piece raised only after it (Tooling 1) | Destructive write order | The Lond Cirion assembler it was adapted from writes once, at the end, with no backup; the adaptation added a backup and made it a move before the build. No run had failed, so nothing showed it | `export()` checks pieces and the probe layout first, builds every tier, refuses on any problem, stages the FBX in `--out`, copies (never moves) the old live file to `.bak-<mtime>`, then copies the new one in. Test: `ExportRefusalTests` (live folder byte-identical, no `.bak`) | Fixed |
| A2 | MEDIUM | The export's own checks (tier triangles against the sum of the parts, `.NNN` materials) only logged a warning; the status stayed "ok" while the doc said the export "fails" (Tooling 2, data flow I1, completeness L3) | Warning as a check | Inherited from the Lond Cirion assembler; the doc described the intent | `export_problems()` names every problem, and the export raises on any; a missing collision body is a problem too. Test: `ExportProblemTests` | Fixed |
| A3 | MEDIUM | A previous run's `DONE.txt` read as this run's verdict: the marker was deleted only after argument parsing, and no doc said to delete it before launch (Tooling 3, data flow G5) | Stale marker, **second time in two days** | The orc and goblin RCA (W2-3, 2026-10-14) fixed it per tool in docstrings and README rows; no lesson or template carried it, and these tools were adapted from Lond Cirion, which never had it | `main()` clears `<out>\DONE.txt` before parsing; the docstring, the prompt's Ground rules, the buildings doc and the `tools/README.md` row all say to delete it before launch; lesson below | Fixed in the assembler; the probe, joins, render and catalog scripts are in the hand-off |
| A4 | MEDIUM | The 12 m gable of the barracks, a new join, never reached `design-patterns.md`; its "Ridge" row covered only a 6 m span (completeness M4) | Phase 6 skipped | The rule was written into the barracks section only | "Our 12 m gable" recipe and a ridge row for both spans | Fixed |
| A5 | LOW | A placement with no collision body vanished from the `bo_` tier silently; `Castle_Wall_A1_Ramp_Mrln_01` (truncated body name) would have, in the planned castle section (data flow G3, completeness M6) | Silent skip | The documented exception list had 16 entries and the code covered 15 | `BO_STANDIN` maps it to `bo_SM_DW_Castle_Wall_A1_Ramp_Mrln_0`; a missing body refuses the export. Test: `BodyTests` | Fixed |
| A6 | LOW | `LAYOUT` was a second, hand-kept source of eave, half sizes and chamfer (data flow I8, design 3) | Duplicate source | Written for the probe before the spec carried enough to derive them | `probe_layout()` derives them from the wall and chamfer placements; `ROOF` keeps only roof form, ridge axis and rise, and the layout now carries `ridge_z`. Test: `ProbeLayoutTests` asserts the old declared values | Fixed |
| A7 | LOW | `--preview --export` ran only the export (Efficiency 2) | Flag logic | | Both run. Test: `MainModeTests` | Fixed |
| A8 | LOW | A bare `.lod` name went to an LOD slot no ladder picks; mirrored kit nodes were baked without a normal flip (design 1, Tooling 6) | Naming rule; latent | No Erebor piece has either, so nothing showed | `split_kit_name()` makes a bare `.lod` the base (Lond Cirion rule 3); `load_kit` flips normals when the determinant is negative. Test: `KitNameSplitTests` | Fixed in the assembler; render and joins in the hand-off |
| A9 | LOW | Every export rewrote `frame_probe.fbx` from two hard-coded pieces, after the live write (design 8) | Repeated one-off check | | `--frame-probe` mode, once per kit | Fixed |
| A10 | LOW | The import note pointed at the wrong doc and gave fixed LOD prose that missed the `base_lod` floor cut and the prop `_lod2` base (completeness L7, design 9) | Generated text drift | Prose written for the first building | The note points at `erebor-buildings.md`, lists each tier's levels from data, and words the heading as a prediction | Fixed |
| A11 | LOW | The inspect file came from a scratch script, not a repo tool (completeness L6, data flow I6) | Unrepeatable step | | `--inspect` mode | Fixed |
| A12 | LOW | Spec rules (columns with a base and a cap, pilasters, courses) were repeated per building (design 6) | Duplication | | `column()`, `pilaster()`, `courses()`; the three specs are identical as placement sets before and after (scratch parity check). Test: `SpecTests` | Fixed |
| A13 | LOW | `fbx_frame_compare.py` decoded every array, never closed its file, split `.fbx:` only in lower case, and ignored pivots, offsets, post-rotation, geometric transforms and parents without a word (engine F5, Tooling 10, Efficiency 4 and 6) | Silent partial model | It was written for 15 Blender-written files that use none of them | Decodes only `Vertices`, `with open`, `split_arg()` ignores case, every row lists `unsupported` transforms. Tests: compressed arrays, unsupported flags, argument split | Fixed |
| A14 | LOW | The Euler test could not tell `Rz Rx Ry` from `Rz Ry Rx`; the test file leaked temp folders (engine F5, Tooling 9, Efficiency 6) | Weak test | The test used y = 0 | A case with X and Y both 90 degrees; `addCleanup` | Fixed |
| A15 | LOW | Docs claimed what nothing proved: "the editor import confirms" the heading (none had happened), Kit imports "owed" after the Kit had written both packages, "693 of 693" floor rays from an older probe margin, "140" exact `bo_` twins (129), "twenty" node transforms (18), all props Y-up (three of five), every roof piece with wood (not the gable wedges), "1 slate" for `Cor_Tri_C1` (stone), "1,692 placements" (pair observations), "almost every placement is Z-only" (trims tilt in more than half) (engine F2 to F4, completeness M1, M2, L1, L2, L10, data flow I2 to I5, I7) | Claim without evidence | Status lines and counts were written in the same pass as the work, and later runs changed them | Each line rewritten from a script output read this session; probe results moved into one section per building; the heading is "expected" until the editor reports | Fixed |
| A16 | LOW | The prompt still carried "one material family, never mixed" after the README replaced it with "one wall letter"; the status header did not record the barracks taking the keep's place (completeness M5) | Superseded rule | | Status header and Phase 3 updated | Fixed |
| A17 | LOW | Deviations from the prompt were not recorded: no 1e-4 m snapping, five renders and no close-up, no risky joins in the briefs; no post-import package check (completeness L4, L5, L11) | Missing record | | "Deviations" section; `check_rdc_entries.py` row in the pipeline (3 of 3 packages with an entry) | Fixed |

## Findings, part 2: tools held by the Gondor session (handed off)

The Gondor session (started from `docs/kitbash/gondor/blender-build-prompt.md`) now edits
`blender_probe_erebor_building.py`, `mine_erebor_prefab_joins.py`, `blender_render_erebor_prefab.py`,
`blender_test_erebor_joins.py`, `blender_catalog_erebor.py` and `erebor_catalog_to_md.py` for both
kits. Editing them here would mix two sessions' hunks in one working copy, so these findings are
recorded for that session and were not applied.

| # | Sev | Finding | File | What to do |
|---|---|---|---|---|
| H1 | MEDIUM | The engine reads `rotation_mat`, then `non_orthogonal_rotation_mat`, then `rotation_euler`; the tools read only `rotation_euler` (checked: neither file names the matrix forms), so about 1,500 kit placements count as unrotated (the lens counted 1,531; `grep -c` finds the form on 1,569 lines of `shogun_dwarf_prefabs.xml` and 97 of `taom_gondor_buildings.xml`) (engine F1) | miner `local_frame`, render `walk` | Read the 9 floats as columns s, f, u before the euler angles; one reader shared by both; a test with the Fink sample transform |
| H2 | MEDIUM | A negative scale is read as yaw 180: the second ramp of `dwarf_tower_ramp_a` is at scale (-1, 1, 1), no rotation (checked in the XML). The join test places it at yaw 180, which puts its battlement on the inner face (data flow G1) | miner `walk`, joins `tower_ramps` | Flag det < 0 and keep it out of the pairs; re-test the join as a mirror; the doc rows are corrected and marked for re-test |
| H3 | MEDIUM | The probe writes no verdict; a check that did not run writes zeros (`bo_miss` 0 without a body, gables 0 of 0 on a hip roof, `--eave` 3.0 by default) (Tooling 4) | probe | `bo_miss: None` without a body; a `verdict` block; refuse a run with no eave |
| H4 | LOW | The floor rays never test the `bo_` body, though the docstring says so, and a floor ray passes anywhere above z -0.05, not at grade (data flow G4, completeness M3) | probe | Down-rays as one function run per body (design 5); a grade window |
| H5 | LOW | The probe clears `probe_DONE.txt` only after parsing arguments (data flow G5) | probe, joins, render, catalog | Clear before parsing, as the assembler now does |
| H6 | LOW | Facade rays assume the building is centred on 0; a pass depth of 0.5 m only works for walls under 1 m thick (Tooling 5a, 5c) | probe | Use the bounds centre; carry each wall line's depth. Before the castle section |
| H7 | LOW | The shared-face area depends on triangle order: the same hall placements (247, same triangle counts) gave 30.625 m2 before the spec helpers reordered them and 30.362 m2 after (found while re-running for this RCA) | probe `coincident()` | Make the match order-independent, or report the area as an estimate (the doc now does) |
| H8 | LOW | The probe builds its base BVH twice; `centre` is always 0.0; an unclosed file at 205-206; the pre-layout fallbacks can go now that all three Erebor expected files are in the dict format (re-exported 19:13) (Efficiency 5 and 6, design 4 and 11) | probe | Delete as proposed |
| H9 | LOW | Pairs are bucketed in 5 m cells, so `--radius` above 5 drops pairs silently; a dead `before`/`continue` (Tooling 10, design 11) | miner | `cell = max(5.0, args.radius)`; delete the dead lines |
| H10 | LOW | A join with a missing piece scores as clean; no 12 m gable case; no normal flip for mirrored nodes (Tooling 6 and 7, completeness M4) | joins | Status "partial"; add the case; flip on det < 0 |
| H11 | LOW | A partial or `--only` catalog run overwrites the full outputs; per-triangle allocation; the render mesh is rebuilt by hand (Tooling 8, Efficiency 3, design 10) | catalog | As proposed in the review |
| H12 | LOW | `AXIS` is an identity map; an unclosed file at line 76 (design 11, Efficiency 6) | catalog_to_md | Delete; `with open` |
| H13 | LOW | Five Blender scripts each carry their own import fallback, kit loader, colour, camera and log code (design 2) | all | The Gondor session's untracked `tools/oneoff/kitbash_kits.py` may already be the start of this; settle one shared loader with the bare `.lod` rule (A8) |
| H14 | LOW | The Gondor assembler still backs up its live FBX with `os.replace` (a move) directly before `export_scene.fbx` (lines 633 to 638), so a failed FBX write leaves no live file (convergence pass; not reviewed further) | `blender_assemble_gondor_buildings.py` | Stage, check, copy the old file, then copy the new one in, as `export()` here now does |

`G2` (the probe's gable test height was a constant tuned on the house, so the barracks' 3 m rise
was half-checked and the Gondor 45 degree gables were reported closed on about 4 of 9 m2) is
**fixed jointly**: the Gondor session's probe hunk reads `ridge_z` from the layout, and this
session's assembler now writes it. The barracks probe ran with it: 0 of 3,250 gable rays open, up
to the ridge.

## Findings, part 3: the convergence pass on the applied fixes (fixed in this session)

| # | Sev | Bug | Fix |
|---|---|---|---|
| C1 | LOW | `--inspect` beside another mode ran only the inspect, of the previous live FBX, and said "ok" | `--inspect` runs alone; a mixed run fails. Test: `MainModeTests.test_inspect_runs_alone` |
| C2 | LOW | `--out=<dir>` kept the old `DONE.txt` through the run: the early clear matches only `--out <dir>`, and the after-parse clear had been removed | Both clears. Test: `test_a_stale_verdict_is_cleared_for_either_out_spelling` |
| C3 | LOW | `--out` equal to the live folder staged over the live FBX and backed up the new bytes | `export()` refuses it first. Test: `ExportRefusalTests.test_staging_in_the_live_folder_is_refused` |
| C4 | LOW | `test_each_problem_is_named` checked only that a problem was reported; `run_main` left `OUT` and `LOG` set | The test asserts each name; `OUT` and `LOG` are patched |
| C5 | LOW | Docs: A1's root cause (above, corrected), "two" handed-off MEDIUM defects (three), a lesson that blamed the rebuilder for the scale and named `ridge_z` as derived, the statues' node "like every `SM_DW_*` piece" (15 differ), "6 x 6" sheets (up to 36 pieces, laid out by count), the tower row's numbers without their yaw 180 caveat, "a placement has no collision body" (the `BO_NONE` trims are by design), "Phases 4 and 5" (3 to 5) | Each line corrected |

## Not applied

- **Design 7 (`fbx_frame_compare.py` imports `audit_fbx_lods.py`'s private parser).** It edits a
  gate tool's parser and makes a one-off depend on a private function to save about 60 lines.
  The one-off now decodes only `Vertices`, which was the measurable cost.
- **Efficiency 1 (load only the pieces a spec needs).** The lens timed an Erebor import-only
  launch at about 2 s; the cost matters for the Gondor kit (the lens counted 111 FBX, 1.1 GB) and
  belongs to the shared loader of H13.
- **Design 14 (the build procedure as a skill, the tool contract as one shared doc).** It needs a
  CLAUDE.md row and a `/security-scan`, and the kit-neutral command line is still settling in the
  Gondor work. Follow-up on Mike's word.

## Root-cause patterns

**1. Failure paths were never run.** A2 and A3 came with the Lond Cirion assembler the Erebor one
was adapted from: the warn-only checks and the missing marker rule. A1 was new: the adaptation
added a backup and made it a move before the build. The adaptation changed what the tool builds
and never exercised how it fails. The same happened in the probe: it kept constants tuned on the
first building (G2, H6).

**2. A record written in the same pass as the work.** Every A15 line was true or intended when
written and became false when a later run, a review fix or the Kit import changed the facts. The
worst case, "the editor import confirms it", described an import that had not happened. The rule
exists (AGENTS.md "Evidence, never invention"); the miss was writing the record before its
evidence, the trap `evidence-over-claims.md` C1 names.

**3. An engine reader copied from one sample.** The transform tools match the managed
`ApplyEulerAngles`, which is right for `rotation_euler`, but the native reader has two matrix forms
first and a scale that can be negative. The samples first read (`taom_erebor_kitbash.xml`) use
neither form except the one mirrored ramp, which then read as a turn (H1, H2).

## Why each lens's earlier passes missed these

No earlier deep review covered this change. The two commits went in on Mike's word before this
review; they touch Python tools and docs, for which the commit gate asks no review. Within this
review:

- **Engine compatibility** found H1 by decompiling the native reader; Data flow found H2 by reading
  the XML. Neither alone saw both, because one read code and the other read data.
- **Tooling** found A1 by running a stubbed export with a missing piece. Nothing else exercised a
  failing export.
- **Completeness and Data flow** both found the stale records (A15); the numbers needed the probe
  JSON on disk, which only those two lenses read.
- **H7** was found by none: it showed only when the same placements were exported twice in a
  different order, which happened in Step 4.

## Lessons

Appended to `docs/reviews/lessons/build-tooling-workflow.md`:

- An export refuses on its own failed check, and never moves the live file before the new one
  exists.
- Clear a detached tool's DONE marker before parsing arguments, and delete it before launch
  (second occurrence).
- Read a prefab transform the way the native reader does: matrix forms first, and keep the sign of
  the scale.
- Take a probe's test region from the layout the assembler writes, never from a constant tuned on
  the first building.

No feedback memory: each lesson is a rule for this kind of tool, not a preference.

## Owed

- **Mike:** the barracks Kit verdict; the editor heading check (door side +Y at rotation 0) on any
  of the three; a word on updating the #797 issue body and comment (public write); a word on the
  skill (design 14).
- **The Gondor session:** the hand-off table above.
