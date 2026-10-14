# RCA: orc and goblin locomotion (#791), TpacTool v0.5.1, armoury audit and Harad starter kits (#795), deep review (2026-10-14)

**Summary.** One session on 2026-10-14 built four things: item 1 of #791 (orc and goblin walk, run and idle
clips restyled from the vanilla human clips and bound into `as_orc_warrior` and `as_goblin_warrior` in the live
Armory), the switch of every `.ps1` tpac tool to ShortJake's TpacTool v0.5.1, an `/armory-audit` regeneration
(catalogue, class table), and the Harad starter-kit fix (#795). A `/deep-review` ran in two waves: lenses 1, 2,
5 and 7, then 3, 4, 6 and Tooling. Wave 1 found 2 HIGH defects, both in the starter-kit and armour work; wave
2 found 1 HIGH in the new binder's reinstall gate. The maintainer committed and pushed the change with the
wave-1 fixes as `776054c0c` (17:00). The wave-2 fixes, P1 and the improvements below are in the working tree,
uncommitted.

## Findings, part 1: data, engine claims, docs and records (lenses 1, 2, 5 and 7; lens 4's record findings)

| # | Sev | Bug | Category | Why missed | Preventive action | Status |
|---|---|---|---|---|---|---|
| W1-1 | HIGH | Removing the 12 duplicate aserai rosters from the vanilla override handed every Harad start vanilla's Calradian kit: Erkam's 37 aserai culture-default rosters (`08b30bfc4`) reuse vanilla ids without `_replaceWhileMerging`, so the engine appended their sets after vanilla's and the adapter took vanilla's first set (lenses 2, 5, 7) | Engine merge semantics | The tool and `StarterKitCoverageTests` counted a culture covered when its file defined the rosters, without asking whether they win the merge | `wire_starter_kit_rosters.py` flags the 37 rosters and refuses a stray flag; the C# test counts aserai only when each youth roster carries the flag (mutation-checked); lesson in `data-content-cultures.md` | Fixed in `776054c0c` |
| W1-2 | HIGH | The regenerated `armour_classes.xml` classed Harad's four camel bardings (`Type="HorseHarness"`, filed in `body_armors.xml`) as body armour, and 12 heavy Harad and Shaghana chests upgraded into the elite barding (lenses 5, 7) | Classify by container, not by data | Slot files had only ever held their own Type; the table's `--check` compares the table with the generator's own output | Type filter, first in `build_rows` (`776054c0c`), now in `build_armory_index` (H); live invariant test on every row and upgrade link; lesson in `build-tooling-workflow.md` | Fixed in `776054c0c`; moved to the index, uncommitted |
| W1-3 | MEDIUM | 24 more walk and run codes play the 16 restyled vanilla clips but stayed human (running idles, hand-shield, thrown, dagger, polearm-with-shield) (lens 5, then design P1) | Hand-listed scope | The binder's codes were a STANCES x KINDS grid, not derived from what plays the clips | P1 (Mike's approval): the binder derives the codes from Native; 42 per race | Applied live and to the snapshot at 17:08; repo part uncommitted |
| W1-4 | MEDIUM | "XSLT cannot bind these rows" was stated as a general rule; it holds only for the Armory's own stylesheet (lens 2) | Over-general engine claim | The merge order was verified for one module only | Docstring and doc reworded; lesson in `xslt-moduledata.md` | Fixed in `776054c0c` |
| W1-5 | MEDIUM | The binder, which rewrites the live Armory `action_sets.xml`, had no test (lens 1) | Missing test | Written and proven on a copy, not under a test | 11 tests with a live reinstall class; 20 now | Fixed in `776054c0c`; extended, uncommitted |
| W1-6 | MEDIUM | The starter-kit fix had no GitHub issue to carry its owed in-game check (lens 4) | Missing record | Done as a mid-turn request | #795 filed on Mike's word | Done |
| W1-7 | LOW | ShortJake's fork was named only in the provenance detail paragraph, not in the register row (lens 1) | Provenance | The row's tokens predated the fork | Register row extended | Fixed in `776054c0c` |
| W1-8 | LOW | "Six vanilla-mapped cultures", "256 rosters", "127 twins", "16 tests", "one file uses the flag" stayed in tests, tools and docs after the counts changed (lenses 1, 4, 5) | Stale counts | Counts restated by hand in many files | Two doc sweeps; counts replaced or re-measured (288, 128 = 40 + 38 + 50, five cultures, two files) | Fixed: sweep 1 in `776054c0c`, sweep 2 uncommitted |
| W1-9 | LOW | `tools/README.md` said the fork does not change the mesh writers; it changes `Mesh.Write`'s `SubVersion` (lens 1) | Unverified claim | Written from the release notes | Corrected from the decompiled DLL | Fixed in `776054c0c` |
| W1-10 | LOW | The feature doc's Naming section described a superseded whole-master export and named the wrong longest clip (lenses 1, 2, 5, Tooling) | Doc drift | The design changed after the section was written | Rewritten; longest clip `taom_goblin_walk_forward_1h_with_shield_left_stance` (51) | Fixed |
| W1-11 | LOW | Doc step numbers, a "draft" label on an approved spec, an unnamed Fab pack, an orphan feature doc, three tools missing from the README catalogue (lens 1) | Doc hygiene | Written during the build | Corrected; feature-map row; README rows | Fixed in `776054c0c` |
| W1-12 | LOW | `development-machines.md` repeated the README's API changes; `export_face_morphs.ps1 .NOTES` still named 0.4.0 (lens 1) | Duplication | | One home per fact | Fixed in `776054c0c` |
| W1-13 | LOW | The binder chose the newline by presence, not majority (lens 1) | XML I/O convention | | Majority rule | Fixed in `776054c0c` |
| W1-14 | LOW | With DualWield, orc and goblin `_dw` sets inherit the plain one-handed forward walk and run, because DualWield writes its forward clips only over vanilla ones (lens 2) | Cross-mod interaction | | Recorded in the feature doc's Testing list; visual only | Recorded |
| W1-15 | LOW | `-CloneByName` clears `FacialAnimationId`, so the two unarmed idles per race lost vanilla's `female_custom`; the doc said the recipes carry over unchanged (lenses 2, 5) | Clone recipe | The clear was written for the troll, which has no facial rig | Doc corrected; P2 (keep the id) rejected for now, re-weigh at item 5 or 6 | Recorded |
| W1-16 | LOW | No `{=starter_*}` twin name is registered for translation (128 of 128; pre-existing) (lens 7) | Localization | | `/localize` pass | Follow-up |
| W1-17 | LOW | 33 backup sidecars in the live Armory (lens 7) | Release hygiene | | `sweep_module_backups.ps1 -Apply` before packaging (release step) | Owed at release |
| W1-18 | LOW | Nothing ran the binder's `--check` after an Armory sync (lenses 4, 7) | Unversioned module gate | | `/armory-audit` step, `moduledata-validation.md` gate row, live test class | Fixed |
| W1-19 | LOW | `-TravelScale` is typed by hand and `-Verify` never reads `LoopDisplacement` (lens 5, Tooling L7, design P3) | Hand-typed parameter | | P3: `travel_scale` in each `clips_index.json` row, `-CloneByName` refuses a disagreeing value, `-Verify` checks the displacement | Follow-up |
| W1-20 | LOW | `audit_armory_refs.py` runs `generate_starter_kit.py --verify` without `--no-asset-repo`, so it reports the mirror's drift, which policy never clears (lens 5) | Gate reads the wrong copy | | Pass `--no-asset-repo` | Follow-up |
| W1-21 | LOW | `restyle_human_clip.py --only` overwrote `clips_index.json` and the report (lens 5) | Partial re-run | | `--only` merges | Fixed before `776054c0c` |
| W1-22 | LOW | The binder binds a clip whose package has no RuntimeDataCache entry (lens 5, Tooling L6, design P4) | Gate depth | | P4 rejected: `check_rdc_entries.py` is checklist step 6, and the outcome is the inherited human clip | Rejected |
| W1-23 | LOW | `UnknownClipName` still named in five docs and a skill (lens 5) | Doc drift | | Renamed to `BlendWithAnimation` | Fixed in `776054c0c` |
| W1-24 | LOW | The catalogue marks the 13 `lord_material_*` meshes unreferenced because `generate_armory_catalogue.py` scans only the Armory (lens 5) | Gate scope (pre-existing) | | Scan the repo module too | Follow-up |
| W1-25 | LOW | Records missing: the starter-kit in-game checklist and history, the snapshot README log, the stale #791 body, the extractor stall, the armour Type rule, the provenance DualWield rows, the feature doc's Dependencies and How-to (lens 4) | Missing record | | All written; a #791 comment posted on Mike's word | Fixed |

## Findings, part 2: the tools (Tooling, Efficiency, Design)

| # | Sev | Bug | Category | Why missed | Preventive action | Status |
|---|---|---|---|---|---|---|
| W2-1 | HIGH | `bind_orc_goblin_locomotion.py --check` exited 0 on an Armory with no clips and no rows, the state a reinstall leaves (Tooling H1) | Gate checks nothing | The plan came from the clips on disk, so an empty disk gave an empty plan that matched | `--check` fails a set with no clips unless `--allow-no-clips`; test; recurrence noted on the "check must fail on inputs it could not read" lesson | Fixed, uncommitted |
| W2-2 | MEDIUM | `--apply` with a missing or wrong clips folder stripped every bound row and passed its own post-write check (Tooling M1) | Destructive default | The sibling hill troll binder's guard was not copied | Refuses to drop a bound row unless `--allow-shrink`; test | Fixed, uncommitted |
| W2-3 | MEDIUM | The restyle and the validator left an old `.DONE` and error log in place, so a crashed run could read as the previous verdict (Tooling M2) | Stale marker | | Both delete their markers once `main()` runs; the caller deletes `.DONE` before launching (docstrings, README rows; convergence C-2) | Fixed, uncommitted |
| W2-4 | MEDIUM | `ROSTER_RE` in `wire_starter_kit_rosters.rewire_text` lets a self-closing `<EquipmentRoster/>` swallow the next roster (latent: none in the file) (Tooling M3) | Regex over XML (pre-existing) | | `(?<!/)>` in the pattern, with the two probe cases as tests | Follow-up |
| W2-5 | MEDIUM | The restyle keyed each whole vanilla master into a Blender action and read it back frame by frame (about 25 s of 38 s per variant), and evaluated each clip's source four times (Efficiency F1, design F) | Redundant engine work | | `rt.json_pose_sampler`, sampled once per clip; the orc restyle took about 5 s, was about 31 s | Fixed, uncommitted |
| W2-6 | LOW | The compare loop re-assigned the target's action every frame (Efficiency F2) | Redundant work | | Deleted | Fixed, uncommitted |
| W2-7 | LOW | The validator's error handler wrote nothing with a missing `--out`; both scripts parsed `--out` unsafely in the handler (Tooling L1) | Error path | | `makedirs`; defensive parse (proven: headless runs with `--out=<new dir>` and a missing `--out` wrote their log and `.DONE`) | Fixed, uncommitted |
| W2-8 | LOW | The binder read Native with a regex that also reads 130 commented-out rows (Tooling L2, design B) | Comments read as data | | ElementTree; a commented row in the test fixture; `find_set` skips a commented-out copy too | Fixed, uncommitted |
| W2-9 | LOW | `--check` proved each derived set came after the warrior set, not after its own parent (Tooling L3) | Partial invariant | | Every link of each chain; test | Fixed, uncommitted |
| W2-10 | LOW | A missing set raised a traceback; input errors exited 1 like "problems found" (Tooling L4) | Exit codes | | A missing set is a problem; bad input exits 2; tests | Fixed, uncommitted |
| W2-11 | LOW | The binder ignored `BANNERLORD_GAME_DIR` (Tooling L5, design L) | Install path | | `_gamedir.game_dir` | Fixed, uncommitted |
| W2-12 | LOW | `load_spec` accepted unknown keys, so a typo ran at the default (Tooling L7) | Silent default | | Unknown keys refused (the `travel_scale` half is W1-19); both variant specs still load | Fixed, uncommitted |
| W2-13 | LOW | The roster marker flagged commented-out rosters (Tooling L8) | Comments read as data | | Comment spans skipped; test | Fixed, uncommitted |
| W2-14 | LOW | The folder test asserted one direction only (Tooling L13); `--clips-dir` was a test-only switch on a live writer (design C) | Weak test | | Fixtures build the real folders; both directions tested; switch removed | Fixed, uncommitted |
| W2-15 | LOW | TpacTool key-field check, empty-folder census, PSEdition guard, `--verify` skip exit, index-row validation (Tooling L9 to L12, L14); `key_source_from_json` keys in quadratic time (Efficiency F3) | Tool robustness (pre-existing) | | See Follow-ups | Follow-up |

## Step 4 improvements (design and efficiency proposals)

- **Applied:** P1 (Mike's approval), B, C, L (binder); G (sway docs); H (Type filter in the index); I+J (one
  roster-id reader, one vanilla check); K (C# test); completeness 7 and 8 (Blender pure-part tests, the
  three-argument `Save` guard); E (one `two_bone_ik` in `retarget_mannequin_to_human.py`, used by `_leg_ik` and
  the restyle), F and F1 (the JSON pose sampler, the source sampled once per clip).
- **Rejected:** P2 (keep the facial id: needs a new switch and four regenerated packages after an in-game
  check that passed), P4 (RDC in the binder: duplicates `check_rdc_entries.py`).
- **Follow-up:** P3, M (the same Type guard in `rebalance_armor.process_file`, whose keyword path never
  consults the index), Efficiency F3.

**Parity of the restyle batch (W2-3, W2-5 to W2-7, W2-12, E, F).** All 16 clips per variant were restyled into a
scratch folder and validated. Against the shipped reports, `restyle_max_deg` is identical on all 32 clips;
the other values move only by a rounding step or an argmax-frame tie (for example `identity_max_deg` 0.47 to
0.46), and `closed_pose_jump_deg` on `run_forward_1h` moves between 0.0 and 0.04. All 32 clips pass the
validator, and `clips_index.json` is identical. One retarget clip (`anim_run_forward_twohanded`, human source)
gives the same report as HEAD's tool, `leg_ik` included. Whether the new code reproduces the hill troll's
historical `leg_ik` block is UNVERIFIED: that report does not record its command line.

## Findings, convergence pass

One `deep-reviewer` on the diff of the improvements. Parity held on the binder, the wiring tool, the armour
filter, the Blender move and the docs; it found two MEDIUM and four LOW defects, all fixed.

| # | Sev | Bug | Fix | Status |
|---|---|---|---|---|
| C-1 | MEDIUM | The new `Save` guard passed `$pkg.save($out, 2)` (PowerShell binds method names in any case) and a call after a `#` inside a string | Case-insensitive match; the comment cut skips quoted text; both probes in the test (RED first) | Fixed, uncommitted |
| C-2 | MEDIUM | W2-3 was partial: the markers are deleted in `main()`, so before `main()` runs, or after an import-time failure, the old `.DONE` still reads as this run's | The caller deletes `.DONE` before launching (both docstrings, both README rows) | Fixed, uncommitted |
| C-3 | LOW | This RCA held placeholders | Filled in | Fixed |
| C-4 | LOW | The binder exited 1 with a traceback on a non-UTF-8 live file or a typeless Native row, and its docstring said a missing set exits 2 under `--check` too | `UnicodeDecodeError` caught, a typeless row is an `InputError` (test first), docstring corrected | Fixed, uncommitted |
| C-5 | LOW | The wiring row in `starting-equipment-tuning.md` still named SandBox's file as the only vanilla source | "both vanilla SandBox and SandBoxCore" | Fixed, uncommitted |
| C-6 | LOW | Three docs still said a shipped mesh carries eight NUL bytes | Past tense, with `escape()` still guarding the case | Fixed, uncommitted |

## Why these escaped the build

- **The same root twice: a gate that checks against its own input.** The class table's `--check` compares the
  table with the generator's output (W1-2); the binder's `--check` compared the rows with a plan built from the
  same disk (W2-1). Both read "current" on a wrong state. The fix in both is an invariant on the output (every
  row is character armour of one Type; every race has clips), not a re-derivation.
- **Coverage counted by presence, not by effect.** W1-1 and W1-3 are the same shape: the code counted "the
  culture has rosters" and "these 18 codes are bound", and never asked which roster the engine takes or which
  codes play the restyled clips.
- **Engine claims verified for one case and stated in general** (W1-4, W1-15).

## Follow-ups

None is filed: GitHub issues are public and need the maintainer's word. Each is ready to file.

1. P3: `travel_scale` in `clips_index.json`, `-CloneByName` refuses a disagreeing `-TravelScale`, `-Verify`
   checks `LoopDisplacement` (`gen_troll_anim_clips.ps1`).
2. M: the Type guard in `rebalance_armor.process_file`.
3. W1-20: `audit_armory_refs.py` passes `--no-asset-repo` to the starter-kit verify.
4. W1-24: the catalogue's referenced flag reads the repo module too.
5. W2-4: `ROSTER_RE` refuses a self-closing roster.
6. W2-15: Tooling L9 to L12 and L14, Efficiency F3, the stale-marker gap in the sibling Blender scripts
   (`retarget_mannequin_to_human.py`, `reskin_to_human_skeleton.py`, `export_blend_pack.py`,
   `export_rig_for_kit.py`).
7. W1-16: `/localize` for the 128 `{=starter_*}` names.
8. `extract_human_anims_tpac.ps1` stalls under v0.5.1 (cause unknown; `/investigate` if the old route is
   needed again).

## Lessons

- `docs/reviews/lessons/data-content-cultures.md`: "A TAOM roster that reuses a vanilla id appends to
  vanilla's unless it carries `_replaceWhileMerging`".
- `docs/reviews/lessons/xslt-moduledata.md`: "A module's XSLT runs before that module's own XML merges".
- `docs/reviews/lessons/build-tooling-workflow.md`: "Class an item by its own `Type`, never by the file it
  sits in"; "TpacTool v0.5.1 binds `Save($out, 2)` to `useOriginalChecksum = true`, silently"; and a
  recurrence line on "A check must fail on inputs it could not read".

## Owed in game

- #795: `./build.ps1`, a full restart, then a Harad start with and without a career (battle kit and civilian
  outfit), the Harad parent, childhood and education screens, Abanissa and Shaghana if reachable (#570).
- #791: the 24 codes bound at 17:08 (thrown, dagger, hand-shield and polearm-with-shield walks and runs; the
  idle of a halted running formation).
