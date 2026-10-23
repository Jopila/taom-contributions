# RCA: two Gondor field-battle scenes (deep review, 2026-10-17)

## Top-line

The change put Mike's two new Gondor scenes, `taom_gondor_battle_001_forceatmo` and
`taom_gondor_pelennor_fields_forceatmo`, into the campaign field-battle rotation through the `REGIONS` table of
`tools/build_battle_scenes.py`, and into the Custom Battle picker with registered English names. A 7-lens
`/deep-review` (Opus 5.5, max effort; wave 1: engine compatibility, data flow, XML, completeness; wave 2: tooling,
efficiency, design; Standards not in scope, since the change has no C# and no harness file) found no load or crash
defect in the data: every reference in both scenes resolves, both carry valid `spawn_path_NN` paths, and the
engine's equal-odds pick was confirmed in the v1.5.5 decompile. It confirmed **1 MEDIUM and 1 LOW defect**, two
design changes Mike accepted, and one HIGH pre-existing release gap. Each was re-read against the files before it
entered this table.

Proof after the fixes: `python tools/build_battle_scenes.py --check` exit 0; `python -m unittest
tools.tests.test_build_battle_scenes` 26 OK (the new region test RED before the table change, GREEN after);
`python tools/generate_name_localization_strings.py --check` exit 0; `validate_xml_schemas.py` PASS; the full
`TAOM.Tests` suite, run in an isolated copy of HEAD plus this change (the main checkout did not compile because of
another session's untracked files), 17,481 passed, with 5 failures that come from the copy (4 tests look for a
`.git` folder, 1 for `ModuleSounds`, which the copy left out), none in the battle-scene area.

## Findings and root causes

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | The two new name keys were registered in `taom_battle_scene_strings.xml` but no row reached the 12 language files, so `LanguageFileCoverageTests.EveryLanguage_DeclaresARowForEveryEnglishKey` went red (2 keys in each of 12 languages). | Registered but never propagated (repeat of #434) | Mike deferred the paid translation, and the builder read that as "skip `/localize`". The recipe's step 2 says to run `/localize`; its SKILL.md names the free route for exactly this case (`--sync-ids --seed-only --apply`). The test that would have shown it could not run: the main checkout's test project did not compile because of another session's work, and nobody built an isolated copy until the review. | Seeded with `--seed-only` (free, no provider call). Lesson in `localization-ui.md`: deferring the paid run never defers the seed. |
| 2 | LOW | The generated header of `sp_battle_scenes.xml` said "The eight taom_* scenes", and the file then held ten. The first rewording still carried a count ("the two Gondor ones") and an unmeasured claim ("with the same shader"). | Hand-kept count in generated text | `--check` compares the file to the generator's output, so a stale sentence inside the generator passes it. CLAUDE.md bans "counts nothing computes" in tiers 2 and 3 only, so nothing covered a generator's own header. | Header now names the class and points to the feature doc; the feature doc's count line points to the dry run. Lesson in `build-tooling-workflow.md`. |
| 3 | Design | Pelennor Fields, whose horizon shows Minas Tirith and Osgiliath, went on all five Pelennor and Anorien cells. Cell 90 also holds a Rohan castle (`castle_V6`) west of Edoras, about 30% farther from Minas Tirith than Edoras is. | Data chosen from names, not content | The builder offered cell choices from region names and the grid table without opening the scene. The table already gives place-scenes their own row (Fords of Isen, the Morannon). | Mike chose cell 85 only. `test_pelennor_scene_stays_on_its_landmark_cell` pins it. Lesson in `data-content-cultures.md`. |
| 4 | Design | `taom_gondor_battle_001_forceatmo` was named "[Gondor] Gondor Plains" from the id pattern. The scene holds cliffs, caves, two river bridges, a ruined tower and a cave camp, lit dark. | Same as 3 | Same as 3: the name followed `<region>_battle_001` to "<Region> Plains" without a look at the scene. | Renamed "[Gondor] Ruined Outpost" (Mike) before the paid run, so no translation goes stale. Same lesson as 3. |
| 5 | HIGH, pre-existing | No release gate checks that a channel's `TAOM_Map/SceneObj` holds every `taom_*` scene the TAOM module names. Patreon and public lack both new scenes and the Edoras siege scene (picker row since v2.0.32). | Cross-module pairing with no gate | Every scene gate (`--check`, `CustomBattleSceneLiveDataTests`, `audit_battle_scenes.py`) reads this machine's install. The release doc pairs Armory items and settlements, not scenes. | Recorded as a known limitation in `docs/features/battle-scenes.md` with the per-channel check. The planned gate is a refusal in `tools/package_release.py`'s dry run. No issue was filed: that needs Mike's word. |

## Root-cause pattern

Findings 3 and 4 share one cause: the builder chose data about a scene (its cells, its name) without reading the
scene. Findings 1 and 2 share another: a check that would have caught the defect existed, but it could not run
(the test project did not compile) or could not see the text (`--check` trusts the generator's own prose).

## Why each agent or gate missed these before the review

- **The builder's own gate run** used `generate_name_localization_strings.py --check`, which proves the English
  registration only. The language coverage test lives in `TAOM.Tests`, which did not compile in the main checkout.
- **`build_battle_scenes.py --check`** compares bytes with the generator's output, so it cannot judge the
  generator's comment text, and it reads this machine's install, so it cannot see a release channel.
- **The review lenses** caught all five. Lenses 4, 5 and 7 found finding 1 independently and Lens 2 the risk
  wording; Lens 6 found 3 and 4; Lens 5 and Tooling found 5.

## Follow-ups (not in this change)

- The release-channel scene gate in `tools/package_release.py` (finding 5), and widening `/release` item 6 from
  "settlement" to "settlement or scene".
- `missing_scene_folders` in `build_battle_scenes.py` accepts an empty folder; it should require
  `SceneObj/<id>/scene.xscene`, as `CustomBattleSceneLiveDataTests` does (Tooling F2, no exposure today).
- Retire `tools/audit_battle_scenes.py`, which reads the deployed copy and always exits 0 (already owed by the
  2026-10-03 RCA).
- `Mission.InitializerRecord`, looked up by name in `Mission_Initialize_Patch.cs`, has no row in
  `ReflectionSiteBindingTests` (Lens 2, pre-existing).
- For Mike, in the scene: Ruined Outpost carries 22,574 entities (about 18,700 from unpacked rock-pile pebbles), 241
  ambient sound emitters and 163 firefly systems with no day and night switch, and uses
  `color_grade_interior_dungeon`. Measure at the Custom Battle load test (250 v 250 against `battle_terrain_a`).

## Lessons appended

- `docs/reviews/lessons/localization-ui.md`: "Deferring the paid translation never defers the free seed".
- `docs/reviews/lessons/build-tooling-workflow.md`: "A generator's own header carries no count".
- `docs/reviews/lessons/data-content-cultures.md`: "Open a scene before choosing its cells or its name".

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/lessons/build-tooling-workflow.md](lessons/build-tooling-workflow.md)
- [docs/reviews/lessons/data-content-cultures.md](lessons/data-content-cultures.md)
- [docs/reviews/lessons/localization-ui.md](lessons/localization-ui.md)

<!-- backlinks-end -->
