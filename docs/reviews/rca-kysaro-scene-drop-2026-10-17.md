# RCA: Kysaro's scene drop wiring (deep review, 2026-10-17)

## Top-line

The change wired Kysaro's scene drop (lotraom-assets `db387f23f`, live in `TAOM_Map` at 18:00 to 18:03) into
TAOM: 11 `<Location>` lines in the live, unversioned `TAOM_Map/ModuleData/settlements.xml` (Pelargir, Serelond town
and arena, Amon Gelin, Lothgobel, Eastern Moria, and five Mordor towns on one shared scene; Minas Morgul and Calembel
left out on Mike's word), 7 Custom Battle picker rows, `taom_rohan_battle_002_forceatmo` on the Rohan campaign cells,
and 7 name keys seeded into 12 languages. An 8-lens `/deep-review` (Opus 5.5, max effort; wave 1: engine
compatibility, data flow, XML, completeness; wave 2: standards, tooling, efficiency, design) confirmed the wiring
itself: one settlements file loads, nothing overrides it, saves pick it up on their next load (scene names are not
saved), every mission path reads the edited slots, the edit is byte-faithful, and all 165 binaries of the drop are
live and real. It confirmed **2 HIGH and 2 MEDIUM defects in the delivered scenes**, which stay as Kysaro's fixes
by Mike's decision, and **1 MEDIUM and 3 LOW defects in this change**, all fixed. Each was re-read against the files
before it entered this table.

Proof after the fixes: `SettlementSceneLiveDataTests` 2/2 live, and it fails against the pre-edit backup and
against the patreon channel; `build_battle_scenes.py --check` exit 0 (96 scenes); `python -m unittest
tools.tests.test_build_battle_scenes` OK; `generate_name_localization_strings.py --check` exit 0;
`audit_scene_names.py` 0 TAOM_Map suspects; `validate_moduledata.py` 0 errors; `lint_docs.py` 0 dead links, 0
dashes; the full suites are quoted in the commit.

## Findings and root causes

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH (scene) | `taom_mordor_town_01_forceatmo`, wired as the center of five Mordor towns including the start town Barad-dûr, has no `sp_player`. A night walk, a second walk in one stay or a disguised entry spawns the player at the world origin (`SandBoxHelpers.SpawnPlayer` falls back to `MatrixFrame.Identity`), outside the walk area, and the mission retreats. | Delivered scene lacks an engine-required tag | The builder checked that each scene folder existed and that siege rows declared their levels, and took the artist's "siege and civilian working" as the rest. Neither check opens the scene for the tags a town visit reads. | Kept on Mike's word, recorded as a known issue in `battle-scenes.md`; fix requested from Kysaro. Lesson: audit a delivered scene's contents before wiring it. |
| 2 | HIGH (scene) | Pelargir, Serelond (town and arena) and Lothgobel place 69 confirmed copies of `Lotraom_comprehend_*` prefabs no loaded module defines: `Decoration_014` to `_016` exist nowhere, `House_12` and `House_50` only in the unloaded `Prefabs_Unused`. The engine logs each and loads an empty entity, so buildings and decorations are missing. | Scene depends on art outside the shipped set | The artist's Kit loads prefabs the shipped `Prefabs/` folder parked on 2026-07-24 to stay under the editor's entity cap; nothing compares a scene's prefab references with the loaded set. | Kept on Mike's word, recorded; Kysaro to break the copies or supply the definitions. Lesson as 1. |
| 3 | MED (scene) | Serelond's town has 0 usable siege rock piles (it replaced a scene with 3 and 20 barrels), two outer gates, and an arena with no tournament fight set that carries the whole town terrain. | Scene content regression | Same as 1. | Recorded for Kysaro. |
| 4 | MED (scene) | Load cost, unmeasured in game: Pelargir and Serelond are Dol Amroth-sized, their guard points spawn about 180 to 200 guards at their starting prosperity (about 280 to 300 once prosperous) that Battle Size does not cap, and Lothgobel holds about 13,000 rock-pile entities and 198 sheep. | Scene cost | Same as 1. | Recorded for Kysaro; the in-game measurement is in the efficiency notes. |
| 5 | MED | The 11 live edits had no in-repo record or gate. A `TAOM_Map` reinstall or an artist copy-port reverts them, and every channel and the mirror still hold the old values; `audit_scene_names.py` stays quiet because the old scenes exist. | Unversioned-module trap not followed | The orientation trap "Unversioned modules: land an in-repo gate with it" names fixes; the builder read a scene wiring as data entry, not as a fix to protect. | `SettlementSceneLiveDataTests` (LiveInstall) pins the 11 rows and every `taom_*` scene the live map names. |
| 6 | LOW | The doc said each Rohan scene comes up "in 1 battle of 9" on all Rohan cells; cells 80 (12) and 88 and 89 (10) are shared. | Claim written past its evidence | The dry run printed the shared cells; the sentence was written from the region's size. | Per-cell odds in the doc. Lesson below. |
| 7 | LOW | Doc gaps: no settlement list, a "not this file" with no referent, no Tests line for the gate, "shipped" for a change no channel carries, and the release check missed the settlement wiring. | Doc accuracy | Written in one pass before the review. | Corrected; the release check is now a runnable `dotnet test` against a channel, proven to fail on patreon. |
| 8 | HIGH, pre-existing | No release channel holds any of the 8 scenes or the wiring (testing included). | Cross-module pairing, no gate | Recorded on the previous change; this drop widens it. | Known limitation updated with the runnable channel check. |

## Root-cause pattern

Findings 1 to 4 share one cause: a scene was wired from its name, its folder and the artist's word, without reading
what the scene contains. The checks that ran (folder exists, siege levels declared) are necessary and far from
sufficient for a town center. Findings 5 and 6 are the builder writing past the evidence in hand: a known trap read
too narrowly, and an odds figure computed from a region size while the tool's own output listed the exceptions.

## Why each agent or gate missed these before the review

- **`audit_scene_names.py`** checks only that a named scene's folder exists, so it passes every finding above.
- **`CustomBattleSceneLiveDataTests`** checks existence and siege levels for picker rows; it never sees a settlement
  or a prefab reference.
- **The review lenses** found them: Lenses 2 and 5 the missing spawn point, Lenses 7, 5 and 2 the prefabs, Lenses 4
  and 5 the siege props and the missing gate, Lenses 2, 4, 5 and 7 the odds, Lens 3 the cost.

## Follow-ups (not in this change)

- A scene-content gate for settlement scenes: every `taom_*` scene the live map names resolves every prefab it places
  in the loaded `Prefabs/` set and, for a town center, carries `sp_player`. Today it would fail on findings 1 and 2.
- `tools/oneoff/assign_gondor_scenes.py`: an `--apply` re-run would revert Amon Gelin and Lothgobel (the new test
  catches that) and re-deal three villages nothing checks; its docstring should say it is historical.
- The same missing `sp_player` already shipped in `taom_dwarven_moria_w_forceatmo` (Western Moria).
- The release-channel scene gate in `tools/package_release.py` (carried from the previous RCA).

## Lessons appended

- `docs/reviews/lessons/data-content-cultures.md`: "Audit a delivered scene's contents before wiring it to a
  settlement".
