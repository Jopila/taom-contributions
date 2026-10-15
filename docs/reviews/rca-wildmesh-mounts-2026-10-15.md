# RCA: WildMesh3D mounts, Armory data and test riders (deep review, 2026-10-15)

## Top-line

`/deep-review` of #794 stages 10 and 11 as committed in Mike's `c3fa211d4` ("Add WildMesh3D mounts support to
LOTRLOME_Armory", pushed 2026-10-15): the live-Armory writer `tools/apply_wildmesh_armory.py` and its test, the five
CustomGame test riders, the `MOUNTED_DWARF` allowlist and ladder exemptions, the docs and ledger, the four live Armory
files the writer wrote, and the pipeline scripts that produced the live clips (`export_blend_pack.py`,
`measure_animalia_clips.py`, `gen_animalia_anim_clips.ps1`, from `776054c0c` and `ba6eb1d4d`). Nine `deep-reviewer`
lenses on Opus 5.5 at max effort in three waves: Standards, Engine compatibility, Data flow, XML; then Efficiency,
Completeness, Tooling (the writer), Tooling (the pipeline scripts); then Design. One Step 2c agent then measured the
vanilla loop convention to settle a disagreement.

**No CRITICAL, no engine incompatibility.** Engine verified 25 API uses against the installed v1.5.5 DLLs (4 native
behaviours UNVERIFIED, each an in-game check); XML ran 14 gates, and the two non-zero exits predate this change (7 Harad
`UPGRADE_SKILL_REGRESSION` errors, and `check_rdc_entries.py --types all` flagging material packages, which never get
an entry). **Two HIGH:** the writer accepts an existing Monsters file that defines none of its Monsters, and the Kit
dropped the closing frame of three WildMesh gait masters while every gate passed them.

**Four findings repeat lessons recorded for #646 on 2026-09-23** (the snapshot, the test riders' visibility, the
unregistered item names, a new writer without its sibling's refusal cases). Text alone did not stop them, so the
preventive actions below are gates where a machine can check the fact.

## Mike's decisions

| # | Question | Answer | Applied as |
|---|---|---|---|
| D0 | A provenance row for the WildMesh3D pack (Completeness M4) | "Dont worry about provenance row, as I bought it for commerical license" (2026-10-14) | NOT APPLIED |
| D1 | The test riders in the Custom Battle picker | Precedent: #646 D1 "Keep both visible", with the release gate (`/release` step 5) | Header corrected; no `is_obsolete` |
| D2 | The five item names | Precedent: #646 D3 "Register English only" | English rows; 12 languages owed (paid, his word) |

| D3 | P1: make the animal the writer's unit (also the vehicle for row 1) | "Approve" | Per-animal steps; partly present animals still refused; per-run backups |
| D4 | P2: move the riders to `ModuleData/test_riders/wildmesh.xml` and delete their exemptions | "Approve" | Moved; 5 ladder, 5 melee and the recruitment-test exemptions deleted |
| D5 | The clip pipeline (rows 2, 10, 11): tools only, or tools and a regeneration round now | "Tools and regen now" | Tools fixed with tests, then re-export, retarget, re-measure, Kit re-import and clip regeneration in this session |

## Findings

Merged across lenses. "Repeat" marks a pattern a lesson already recorded.

| # | Sev | Bug | Found by | Category | Why missed | Preventive action |
|---|---|---|---|---|---|---|
| 1 | HIGH | `apply_wildmesh_armory.py:298-300`: when `lotr_monster_wildmesh.xml` exists and holds none of the five ids, `_presence` returns "none" and the step adds no report line, no problem and no write; `--apply` then writes five Horse items naming undefined Monsters. A 0-byte file (the shared `write()` truncates before writing) or `<Monsters />` passes; the sibling refuses any missing id (`apply_animalia_armory.py:361-368`). Its "part" message also says "restore the file from its backup", and a new file has none | Tooling (writer), Completeness | **repeat**: sibling's refusal not copied | The step was re-typed around a three-state `_presence` helper written for the edited files, where "none" means "insert"; for a file the tool creates whole, "none" is corruption. The sibling's test for it (`test_apply_animalia_armory.py:362`) was not ported, and the new test file was written fresh | RED test first; the fix per D-P1; lesson "port the sibling's refusal tests first" |
| 2 | HIGH | The Kit compiled `wildmesh_deer_runslow_f`, `_mountaingoat_hoppingslow_f` and `_warthog_trotfast_f_2` one frame short (21, 22, 21 frames for measured clips of 21, 22, 21; a good master holds measured + 1 for the rest frame, the boar walk 37 for 36). Vanilla gait clips end on a copy of their first pose (24 clips measured, 0.00 to 1.86 degrees); these wrap 13.9 to 22.3 degrees in one frame. `gen_animalia_anim_clips.ps1 -Verify` checks each clip against its own master (`:245`), never against the measure, so ok=96 | Tooling (pipeline), Step 2c | gate checks the wrong pair | Blender writes FBX key times truncated (`export_fbx_bin.py:2328`); for some frame counts the last key lands one tick under its frame and the Kit drops it [Likely]. No stage compares a master's frames with what was measured | Guard in `Get-MeasureProblem` and `-Verify` (master frames = measured n + 1) for the WildMesh pack; the Animalia moose walk is exempt (2c: its truncation removed a redundant second copy); remedy needs a Kit re-import |
| 3 | MED | No test fails when a reinstall drops the WildMesh Armory data: `LiveRecipeParityTests` skips when the Monsters file is missing (`test_apply_wildmesh_armory.py:252`) and compares only Monsters and items; the workflow's stage 10 gate is "wiring tests" (`quadruped-pack-to-horse-skeleton-workflow.md:37`) | Completeness, Data flow | gate skips on the case it exists for | The Animalia wiring is a C# LiveInstall test, Inconclusive only without the whole Armory; the WildMesh port became a Python parity test with a narrower skip and scope | The parity test fails (not skips) when the Armory is installed, and covers the sets, their twins, the registration and every bound clip |
| 4 | MED | The `MOUNTED_DWARF` allowlist gained three ids with no test: `MountedDwarfTests` covers only the rams (`test_validate_moduledata.py:1143-1144`), so neither the new passes nor the deer and moose staying refused is pinned | Completeness, Data flow | gate widened untested | The rule change was data-only in feel; #515's carve-out had landed with 9 cases | Two cases: a dwarf on `taom_wildmesh_boar_a` is clean, on `taom_wildmesh_deer_a` is reported |
| 5 | MED | The five `{=taom_wildmesh_*_a}` item names have no row in any of the Armory's 13 `loc_LOTRAOM_horses.xml` files; `check_external_loc_coverage.py:76-83` counts only rows that exist, so it passes | Standards, XML, Data flow | **repeat**: item names unregistered (#646 row 20, D3) | The sibling writer never wrote loc rows: #646 added them by hand at review, so the recipe the new writer copied had no such step | The writer writes the English rows as a step; the workflow's stage 10 row lists them; a gate for inline Armory keys with no root row is a follow-up |
| 6 | MED | `troops_wildmesh_test.xml:11-12` says "no campaign, save or culture roster ever sees them" and "nothing upgrades into or out of them". The picker lists every `IsSoldier && !IsObsolete` character (`ArmyCompositionGroupVM.cs:175`), and five `<upgrade_targets>` blocks (lines 56, 229, 402, 575, 655) copy the donors' edges, which repo tools that walk the upgrade graph count as second parents | Engine, XML, Standards, Data flow | **repeat**: CustomGame-only read as hidden (#646 row 3) | The header was written from intent, not from the workflow's own line (`:142` states the picker); the riders were cloned whole from `ironpass_ram_rider` and `mirkwood_rochenlas` | Delete the blocks; correct the header; the workflow's stage 11 carries the header text to copy and "strip `upgrade_targets`" |
| 7 | MED | The rider file sits in `troops/troops_*.xml`, so `generate_name_localization_strings.py:51`, `generate_troop_roster_page.py:175` and `rebalance_troops.py:1131` ingest the riders on their next run (the #646 riders' names reached all 12 languages that way; `rebalance_troops` would cut each elf rider 410 skill points and report a phantom culture) | Data flow, Completeness, Design | test data in a production glob | The exemptions were added per gate that failed; generators that do not fail were never asked | Per D-P2 |
| 8 | MED | The live `action_sets.xml` holds 15 `as_wildmesh_*` sets; the repo snapshot `docs/reference/lotrlome-armory-snapshot/action_sets.xml` is byte-identical to the pre-change backup and holds none, so the README's restore recipe drops them while the Monsters still name them | Completeness | **repeat**: snapshot not refreshed (#646 row 2) | The lesson ("A hand edit to live data is done when its replay script, snapshot and ledger say the same") is phrased for a hand edit; this was a script edit | Snapshot refreshed; the parity test also checks that the snapshot carries the writer's block, so a later drift fails a test |
| 9 | MED | The new test file did not port four sibling cases (a Monsters file missing a Monster, a missing `_map` twin, a wrong root exits 2, a dry run never asks whether the game runs); `test_each_override_keeps_the_vanilla_attributes` checks one action's `alternative_group` | Tooling (writer) | **repeat** (as row 1) | As row 1 | Ported; the override test compares every override with the native fixture |
| 10 | MED | `export_blend_pack.py:82` steps by `src/dst` from `s` and clamps at `e`: when the length is not a whole number of output frames, the last sample misses the source's closing pose. 5 of 17 gaits end 2.6 to 7.7 degrees off their first pose (vanilla: under 1.9) | Tooling (pipeline), Step 2c | fixed step on a closed interval | The round-trip check compares rest-pose heads only; the "pose within 0.07 degrees" figure in the lesson came from exact-length clips | Per D-P6; the round trip gains a last-frame check; the #794 lesson is corrected |
| 11 | MED | `measure_animalia_clips.py:110-111` takes the first threshold crossing as the plant, so a 2 to 4 frame toe scuff before the stance wins: boar `WalkMedium_F` RF 0.6 (stance at 0.9714), elk `Loco_WalkBack` LF and RF | Tooling (pipeline) | first match taken as the event | `Get-MeasureProblem` counts plants and never checks their spacing | `plant_start` (D5). The boar walk's clip is regenerated with it (RF 0.9714); the elk walk-back's two front values are NOT applied: the same re-measure records that source at 60 fps, so the new fps guard refuses the clip, and its plants wait for the Animalia follow-up (convergence finding 1) |
| 12 | MED | `export_blend_pack.py:224-252`: a stale `.DONE` "ok" survives a crash; `spec.split` and argparse errors leave no `.DONE`; a `--clips-regex` matching nothing writes "ok"; the round-trip numbers are never judged; the report is written twice (`:249-250`) | Tooling (pipeline), Efficiency | **repeat**: detached-run handling not copied | Its same-commit siblings (`restyle_human_clip.py:514-520`, `retarget_mannequin_to_human.py:1150-1176`) have it; the exporter was written for one run | Copied: stale verdict cleared, errors caught into `.DONE`, empty selection fails, `failed` recorded in the report |
| 13 | LOW | Parse failures and missing inputs show as raw tracebacks at exit 1, the code for a data refusal (`apply_wildmesh_armory.py:306, 321, 338, 359-360`) | Tooling (writer) | exit-code contract | Inherited from the shared helper | `PARSE FAILED: <file>` into problems; missing inputs exit 2 |
| 14 | LOW | The SubModule presence check is a bare substring (`:307`), and its anchor has no count check (`:311`) | Tooling (writer) | substring containment | The `/WARG` anchor beside it has one; this one was written first | Exact `path="..."` token; anchor count 1 |
| 15 | LOW | Stale allowlist text: the rule's "The two ids are pinned" (`moduledata-validation.md:185`), `taom_schema.py:979-982` ("the one carve-out is the war ram ... only for the ram itself"), `docs/features/moduledata-validation.md:236-241` (the two-id `frozenset`), `doc-lookup.md:93` | Standards, XML, Tooling (writer) | half-applied edit | The commit edited the constant and one rule row, not the other places that describe the set | Name the set, not its members; renamed `DWARF_MOUNT_IDS` (P4) |
| 16 | LOW | Two engine claims kept from older text in hunks this change rewrote: the ram is "built for the dwarf skeleton so the rider bone lines up" (`taom_schema.py:186-187`; every one of the five Monsters inherits the horse's `rider_sit_bone`, `Monster.cs:228-236`), and `Patch46` "enforces the same invariant at runtime" (rule row `:89`; it strips every dwarf's horse in tournaments only, race-keyed) | Engine | **repeat**: a cloned claim re-asserted | A rewritten sentence was checked for what changed, not for what it still asserts | Corrected; lesson recurrence note: an edited sentence is new text |
| 17 | LOW | "Falls, rear, kick, walking backwards, turns and jumps stay the horse's" omits the strafes, quick stops and head-turn additives; the in-game list lacks turning at each gait | Engine | incomplete list | Written from the rows the writer binds, not from the 29 native movement rows | List and checklist completed |
| 18 | LOW | The ledger's "Installed TAOM module" section says the installed `SubModule.xml` "was not replaced"; a full build deploy at 20:56 made it equal to the repo, and its `.bak-wildmesh-20261014` targets v1.5.4, so restoring it would undo the deploy | Engine, XML, Completeness | status from an earlier moment | Mike's build ran after the ledger was written | Ledger corrected, with "do not restore" |
| 19 | LOW | Stale docs: `feature-map.md` ("stages 1 to 8"), `INDEX.md:63`, `monster-size.md` (three sized Monsters; live has 8), `module-armory.md:46`, `file-catalogue.md`, `doc-lookup.md:74`, `items-mounts-and-harness.md:397`, `wildmesh-mounts.md` ("No unit tests", Key Files), the workflow's writer mentions (`:37`, `:126`, `:197-200`), the unrecorded `_2` export, the seat-check consequence | Standards, Completeness | drift | Each written before the next stage moved it | Updated |
| 20 | LOW | The writer cannot add a sixth animal to an applied Armory: every step reports a partial block and refuses (#794 item 7 plans three more) | Completeness, Design | batch as the unit | Written for one five-animal run | Per D-P1 |
| 21 | LOW | Three dwarf riders carry four rosters each, rosters 3 and 4 identical to 1 and 2 | Design | dead copy | Cloned from `ironpass_ram_rider`, which mixes two rams with two bardings | Deleted (P3) |
| 22 | LOW | Nothing pins each rider to its own animal and harness | Completeness | untested data | #646's pin was removed with its riders (`d85fe6dc3`) | A data test in the writer's suite |
| 23 | LOW | Pipeline smaller items: the measure's `fps` is never read by the generator (F5); a missing measure file exits 1, not 2 (F7); `_safe()` can map two clip names to one file (no collision today); the master scan is not filtered by pack prefix | Tooling (pipeline) | unchecked inputs | Single-run tools | Guards applied: fps, exit 2, collision refusal, prefix filter |
| 24 | LOW | Commit `c3fa211d4` has no version label, does not cite #794, and its body (the changelog entry) names live files the commit does not contain while omitting the riders, the allowlist and the exemptions | Standards, Completeness | changelog text | Committed from the editor, so the subject hook never ran | Pushed: flagged for `/release`, whose Phase 4 reads unlabelled commits |
| 25 | LOW | #794's body still says "pilot, not yet committed", names `taom_test_<animal>_rider`, and lists `audit_mount_parity.py`, which covers no reskin | Completeness, XML | public status drift | Public, so not updated without Mike's word | Asked |

**Not applied, with the reason:** the provenance row (D0). `is_obsolete` on the riders (D1 precedent). The art-drop
catalogue (`/armory-audit` commits; Mike's word). The ResourceWarning in `apply_animalia_armory.py`'s `read`/`write`,
the generator's duplicate read-back after `-Apply`, the market gate's `?? true` fallback (`ArmourMarketplaceGate.cs:26`)
and the stale `apply_animalia_armory.py` docstring: pre-existing code outside this change, follow-ups.

## Found by the Step 2c agent (not in this change)

**Nine Animalia gait masters hold their closing pose twice:** elk walk, trot, run, sprint and walkback; moose trot, run,
sprint and walkback. Frame S2 - 1 equals S2 (0.00 degrees), so under vanilla's convention each loop freezes one frame.
The cause (the Unreal export or the retarget) is unknown; `/investigate` is the route, and its answer decides between a
re-export and `Source2 = frames - 2`. Follow-up, not fixed here.

## Root-cause patterns

1. **A sibling's refusal paths do not travel with its code.** Rows 1, 9 and 12: the writer re-typed the sibling's steps
   around a new helper and wrote a new test file, and the exporter skipped the detached-run handling its same-commit
   siblings carry. The #646 lesson said a new writer "lands with a synthetic-tree test of its dry run and its refusal
   paths"; this one did, but its refusal paths were the new code's, not the sibling's. Porting the sibling's test file
   first, case by case, would have gone RED on row 1 before any code ran.
2. **A gate that checks the artefact against itself.** Rows 2, 3 and 10: `-Verify` compares a clip with its own master,
   the parity test skips when the thing it guards is gone, and the round trip compares rest poses. Each passes exactly
   the broken input it exists for. The fix compares each artefact with its upstream source (the measure, the recipe,
   the source clip's closing pose).
3. **Knowledge recorded where it is not read.** Rows 5, 6, 8 and 16 repeat #646 lessons. Each lesson sits in a category
   file that the next author did not open; the workflow template (`:142`) even held the right text. Where a machine can
   check the fact, the gate now does (the snapshot, the loc rows); where it cannot, the text moves into the workflow
   stage that does the work.

## Why each lens missed what it missed

- **Standards, Engine, Data flow, XML (wave 1)** read the writer's output and the live files, all of which match the
  recipe; row 1 is a path the live data never takes. Data flow flagged the parity test's narrow scope (row 3) and the
  generators (row 7).
- **Efficiency** measured cost only; no row is a cost defect.
- **Completeness** found rows 1, 3, 4 and 8 by running scratch trees and diffing the snapshot.
- **Tooling (writer)** found rows 1, 9, 13 and 14; it did not read the pipeline scripts, which the second Tooling lens
  owned.
- **Tooling (pipeline)** found rows 2, 10, 11, 12 and 23. Its F1 first counted the Animalia moose walk as short; the
  Step 2c measurement showed that master matches vanilla, and narrowed the guard to the three WildMesh masters.
- **Design** proposed P1 to P6; it did not look for defects.

## Engine behaviour still UNVERIFIED (in-game checks)

`act_horse_stand_for_movement_data` bound to a custom clip; a WildMesh forward clip blended with a vanilla strafe at
the same pace (hoof slide when turning); a child set declaring both `skeleton` and `base_set` (the war ram ships this
shape); whether the rider scales with the 150 moose (`Mission.cs:4044-4050` scales any agent whose slot 10 holds a horse;
two sibling Monster comments disagree). The owed list is in `docs/features/wildmesh-mounts.md`.

## Feedback memories to codify

None. The rules exist (TDD, evidence-over-claims, the #646 lessons); what failed is where they were read. The lessons
appended to `lessons/build-tooling-workflow.md` and the recurrence notes on `lessons/data-content-cultures.md` carry the
category, and the gates named above carry the facts a machine can check.

## Improvements (Step 4)

**Applied** (four builders, then checked by the orchestrator; tests first):

- Rows 1, 9, 13, 14, 20 and P1: `tools/apply_wildmesh_armory.py` per animal (`plan_monsters`, `plan_submodule`,
  `plan_action_sets`, `plan_items`, `plan_loc`); `MONSTER_FILE` keeps its sha256; pristine-tree output byte-identical
  except the corrected comment (row 17). 33 tests in `test_apply_wildmesh_armory.py`.
- Rows 3, 5, 8: the loc step, the strict parity test, the snapshot refreshed; the five English rows written live
  (backup `.bak-wildmesh-20261015-082540`).
- Rows 4, 6, 7, 21, 22 and P2, P3, P4: the riders in `test_riders/wildmesh.xml`, 11 exemption lines deleted,
  `DWARF_MOUNT_IDS`; three `MountedDwarfTests` cases and 14 tests in `test_wildmesh_test_riders.py`.
- Rows 2, 10, 11, 12, 23 and P5: `sample_times`, `.DONE` and round-trip verdicts, collisions, `on_ground` and
  `plant_start`, the SHORT and fps guards, the pack filter, exit 2; `fbx_exact_times.py` around the retarget's export.
  62 new tests in `test_gen_animalia_anim_clips.py` (12), `test_export_blend_pack.py` (33) and
  `test_measure_animalia_clips.py` (17), and 12 in `test_fbx_exact_times.py`.
- Rows 15 to 19: rule row and bullet, schema comments, the docs listed in row 19, the workflow's stages 1, 3, 8, 10
  and 11, the ledger, the snapshot README.

**Regeneration round (D5):** re-export, retarget of the 74 shipped clips with exact times, re-measure, Mike's Kit
re-import, 37 clips regenerated, one Kit open and close. Details and gates: `docs/features/wildmesh-mounts.md` "Tests".

**Not applied:** P6 (per-fcurve resample): the one-line `sample_times` fix already lands every clip on its closing
pose, and P6's per-frame parity was unverified without a Blender comparison run; the regeneration used `sample_times`.
Efficiency 1 (unclosed handles in `apply_animalia_armory.py`'s helpers) and 3 (the generator's duplicate read-back):
pre-existing code, follow-ups. The elk walk-back's new plants (row 11): the re-measure also recorded the source at
**60 fps**, so the new fps guard refuses that clip; the committed elk measure stays until the Animalia follow-up below.

## Follow-ups (pre-existing, not fixed here; issues on Mike's word)

1. Nine Animalia gait masters hold the closing pose twice (Step 2c); four more Animalia masters lost their last frame
   to the truncating exporter (elk vocalization bugling and jump-down, moose eating 01 and attack legs 02); the moose
   walk's lost frame was a redundant second copy, so it is exempt.
2. The Animalia elk `Loco_WalkBack` source is 60 fps and the moose deaths 25 fps, keyed one to one at 30.
3. `retarget_mannequin_to_human.export_clip` (the human and troll clips) exports through the same truncating code.
4. `ArmourMarketplaceGate.cs:26` falls back to `?? true` when the gate's records are cleared, so a failed gate init
   lets every non-merchandise item into culture markets; `apply_animalia_armory.py`'s docstring is stale on it.
5. `check_external_loc_coverage.py` cannot see an inline Armory key with no root row.
6. `CareerCultureCoverageTests` keeps its own two-ram list; it needs the three ids when campaign riders arrive.
7. The measure's in-place and stride-sample guards (pipeline F6); `retarget_animalia_to_horse.py` writes `.DONE` "ok"
   when some clips fail.
8. Commit `c3fa211d4` has no version label and an inaccurate body: write its release-note entry from the feature
   doc at `/release`.

## Verification

After the fixes and the regeneration (this tree): `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`
17,376 passed, 7 failed, 6 skipped, the same seven as the baseline; `python -m unittest discover -s tools/tests -t .`
3,861 run, 5 failures and 1 error, the same six as the baseline. Live gates: `apply_wildmesh_armory.py` dry run
"nothing to write"; `gen_animalia_anim_clips.ps1 -Pack wildmesh -Verify` ok=96 short=0, the Animalia default ok=54;
`wire_anim_master_skeletons.ps1` census ok=74; `check_rdc_entries.py --under creature/creatures` 0 without an entry;
`audit_action_set_parity.py` exit 0; `validate_moduledata.py` 7 errors (Harad, unchanged), 1,602 warnings, none naming
a WildMesh id or test rider; `validate_xml_schemas.py` on the rider file and `SubModule.xml` pass.
`check_external_loc_coverage.py` reported +5 untranslated per language, the item names it could not see before; the
baseline was updated in the convergence fix commit (Armory counts only) and the translations are owed (paid, Mike's
word). The installed TAOM module carries the moved rider file (a 10:24 build
deployed the tree) and the old installed copy is deleted. Not yet run: anything in game.

**Committed with the ModuleData commit gate overridden, at Mike's word (2026-10-15, "Just commit anyways and dont
worry about the hooks for now override them").** `check-moduledata-validation.sh` blocks any commit that stages
ModuleData XML while the validator reports an error anywhere, and the tree carries 7 `UPGRADE_SKILL_REGRESSION`
errors in `troops/troops_harad.xml` and 6 Harad career-kit drifts (`generate_career_kits.py --verify`), both from
`08b30bfc4` and untouched here; this change adds no validator error. The commits ran from a script by path, with the
staged list checked against this change's files. **The convergence pass** (Step 4.6) was still running at commit
time; its findings land in a follow-up commit.

**Convergence pass (one `deep-reviewer` on the fix diff, after the commits): 2 MED, 5 LOW, no regression.** It
confirmed the writer's pristine-tree output byte-identical but for the header comment, the validator's 1,609 issues
unchanged between HEAD and the tree, both packs' `-Verify` counts, the 17 regenerated gait clips against the new
measures, the exact-times patch points in the installed exporter, and the rider move. Dispositions:

| # | Sev | Finding | Disposition |
|---|---|---|---|
| C1 | MED | The elk `Loco_WalkBack` plant fix was not carried into `animalia_elk_clip_measure.json` | NOT APPLIED, pushed back with evidence: the same re-measure records that clip at 60 fps, so committing it makes the new fps guard refuse the clip; row 11 now says so |
| C2 | MED | The loc step turned `check_external_loc_coverage.py` red (+5 untranslated per language) | Re-baselined (`--update-baseline`): only the Armory counts move, +5 in each of the 12 languages, the five owed item names |
| C3 | LOW | `taom_schema.py:187` said "all five Monsters"; the five items are four Monsters | Fixed |
| C4 | LOW | The clip generator's header did not describe SHORT, the new BAD MEASURE conditions or the new exit 2 | Fixed |
| C5 | LOW | A malformed measure file made `-Verify` exit 1, not 2 | Fixed with a test |
| C6 | LOW | The writer's "only the forward and in-place rows are rebound" sentence ignored the stands, idles and hit reactions it also rebinds | Fixed |
| C7 | LOW | The exporter's stale-verdict clearing missed an abbreviated `--ou` | `allow_abbrev=False`, with a test |

Baseline during the review, before any fix: `dotnet test TAOM.Tests` 17,376 passed, 7 failed, 6 skipped (the seven:
`EveryUpgradeTarget_HasNoSkillLowerThanTheTroopItUpgradesFrom`, `AllNonMilitiaNonBossTroops_AreReachableFromARecruitmentPoolRoot`,
`AllPooledTroopIds_ResolveToRealTroops_NoTypos`, `EveryEnlistableCulture_ResolvesToItsOwnKit_ForEveryAssignmentAndRank`,
`NoRankChain_IssuesTheSameKitTwice`, `EveryConversionTargetCanReplaceEveryTierAndRole`,
`Commissions_RewardOnlyHeavyOrEliteKit`); `python -m unittest discover -s tools/tests -t .` 3,749 run, 5 failures and 1
error (clan heraldry specs x2, generator item refs x2, career kits, and the troop-name strings, which also count the
five riders on purpose). Another session pushed 17 commits after `c3fa211d4` during the review, none in this change's
files except three docs, so the baseline is re-run on the new HEAD before the fixes.
