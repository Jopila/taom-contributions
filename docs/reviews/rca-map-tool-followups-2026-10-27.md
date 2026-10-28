# RCA: map-tool follow-ups (#1042), deep review 2026-10-27

## Top line

#1042 reset 4,633 live language rows of renamed map settlements that still held their pre-rename name, wrote 102
new village descriptions, added a map-wide name check to the two map writers, made the bulk renamer write only real
renames and evict the translation cache, and stopped the faction settlements generator from rewriting live rows.
Mike committed it as `244c15d27`. `/deep-review` ran at tier 1 (live `TAOM_Map` data) with lenses 5 Data flow, 2
Engine compatibility, 7 XML and Tooling; no lens found a CRITICAL or HIGH, so no second wave ran. Codex did not run:
no paid dispatch was authorised for this change. The live data was correct: every row byte-exact, kept by a Modding
Kit scene save at 20:24 (which re-reads the file and rewrites positions only) and by another session's map write at
20:42. The review confirmed 7 MEDIUM and 9 LOW findings, all in how the tools fail, how they protect the repair, and
data they left behind; all 16 are fixed here, and the older problems the review also found are in #1043. One
convergence pass on the fix diff found 1 MEDIUM and 4 LOW more (rows 17 to 21), all fixed.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | `Apply-MapVillageNames.py` wrote the master first and evicted the cache last, so a run that failed part-way left a master that matched the table: the re-run said "nothing to do" with 11 languages and the cache stale. A missing cache file crashed it after all 13 XML files were written | Write order | The convention covers parse-before-write, backups and dry runs, not which file a re-run reads to decide what is left | The cache goes first, the master last; the same order in both one-offs (the German witness last); `evict_cached_names` reads a missing file as empty; tests make a file read-only and prove the re-run finishes. Lesson in `build-tooling-workflow.md` |
| 2 | MED | Both one-offs called themselves idempotent, but a re-run after #763 turns every translated row back into English | Retired one-off without its guard (REPEAT) | Idempotency was checked against the state right after the first run, not after later writers. The same review day's Lindon RCA (finding 4) already named the missing `--rerun-retired` guard | Both refuse without `--rerun-retired`, tested before any file is read. Lesson in `build-tooling-workflow.md` |
| 3 | MED | The renamer, the third writer of settlement names, had no map-wide name check | Check added to some writers of a field | #1042 named "the map writers" and the fix went to the two table writers | The renamer refuses a clash, checked against the renamed master so two ids can swap names. Lesson in `build-tooling-workflow.md` |
| 4 | MED | The new guards in the writers' `main()` had no tests: eight deliberate breaks (no BOM, no backup, no parse, wrong cache field, three refusals removed, the generator rewriting every row) all passed the 103 tests | Pure functions tested, entry points not | `test_live_map_writers_main.py` had the temp-tree pattern; the new tests stopped at the helpers | Temp-tree `main()` tests for every guard; 19 breaks now each fail a test (harness in the session scratchpad, control run green) |
| 5 | MED | No drift gate protected the live repair: after a reinstall every check passed, and the coverage gate called the stale rows an improvement | Unversioned module without a gate | `module-map.md` asks for a repo gate with every live edit; the one-offs' dry runs exit 0 | `write_village_descriptions.py --check` (exit 1 on a reverted `text=`, a missing own-key row or a returned orphan row) and `LiveDriftTests` in the Python suite; a row in the gate table of `.claude/rules/moduledata-validation.md` and a pre-publish check in `release-process.md` |
| 6 | MED | 15 of the 16 borrowed description keys had no reader after the re-key, leaving 180 orphan rows, and the docstring said the rows "stay for the village that owns them" | Shared text re-keyed without listing its readers | The owner was assumed to exist | The 8 keys whose village is not on the map are deleted (`ORPHANED_KEYS`, refused while still read); the 7 villages that own the other keys but had no `text=` now get descriptions under them. Lesson in `localization-ui.md` |
| 7 | MED | Four Dunland fortifications outside the renamer's table (Galtrev, Carreglyn, Lhan Tarren, Methedras) showed old names in 11 languages, and six new descriptions name them | Repair scoped by the tool, not by the defect | The stale-row repair took its ids from `NAMES` | `EXTRA_IDS` in the reset one-off; the same witness rule |
| 8 | LOW | The generator could still append a castle at the head of new rows, breaking the save-order rule | Latent writer hazard | Not in the #1042 scope | Retired behind `--rerun-retired`; refuses `save_order_findings` and name clashes |
| 9 | LOW | The renamer exited 0 when it found none of its ids, and with a traceback when the master was missing | Silent success on the wrong input | Not tested | Exit 2 on both, tested |
| 10 | LOW | #763's run sequence lacked the BR and IT re-tag steps | Records not swept | The issue body was written before the reset | Steps added to #763 |
| 11 | LOW | No release channel had the repair, and no pre-publish check looked for it | Distribution step missing | The live module was treated as what players get | Pre-publish check in `release-process.md` |
| 12 | LOW | `feature-map.md:208` still said the renamer does not evict | Prose not swept | One file missed in the doc pass | Fixed |
| 13 | LOW | `settlements.md` "Takes effect: next save load" is true for English only | Engine claim not checked | Older line under the changed recipe | Fixed: the 12 translated languages at the next game start or a language switch (`MBTextManager.ChangeLanguage`) |
| 14 | LOW | Eight descriptions did not name the bound fortification as the map does ("Barad-dûr" for "Barad Dûr") | Text not checked against data | No gate compared them | Aligned; `LiveDriftTests` checks every description names its fortification |
| 15 | LOW | The new-factions doc still sent Blue Craig castles to the generator | Prose not swept | Same as 12 | Fixed |
| 16 | LOW | Weak tests: an mtime test whose result depends on timing, a misnamed test, fixtures without a BOM, one cache shape | Test quality | Two-edit test not applied to each | mtime set to 0 first; renamed; BOM and LF fixtures added |
| 17 | MED | Convergence pass: the new orphan refusal in `write_village_descriptions.py` read the master BEFORE the rewrite, so on a reinstalled package (where the 16 re-keyed villages still read their borrowed keys) the repair that `--check` prints refused every time | Guard checked against the input, not the output | The refusal was tested only with a reader the run itself re-keys, so the test locked in the wrong state | The check runs on the rewritten master; tests for a reader the run leaves (refuses) and one it re-keys (succeeds) |
| 18 | LOW | Convergence pass: the gate row called `--check` a check of the names, and the pre-publish greps passed while `--check` failed | Gate claimed beyond what it reads | Written from the intent, not the code | The rule row says descriptions and orphan rows; the release check runs `--check` on the channel and greps DE for "Usanc" and "Epicrotea" |
| 19 | LOW | Convergence pass: "the other 11 languages" where TAOM_Map has 12 translated languages | Count not re-measured | English counted among the 12 | Fixed in `settlements.md` and here |
| 20 | LOW | Convergence pass: the new-factions doc still said re-running the generator reproduces the live file | Prose not swept (same as 15) | A third mention of the generator sat in an older paragraph | Fixed |
| 21 | LOW | Convergence pass: the renamer's docstring (its `--help`) still named only the two map writers' refusal, and the lesson said eight stale languages where a replay gives nine | Docstring and lesson not re-read after the fix | Same as 20 | Fixed |

Not applied, with reasons: a map-wide duplicate-name line in `--check` (needs the hideout families excluded; #1043),
and the three older duplicate names (the new names are Mike's call; #1043).

`FOLLOW-UP` #1043: Sardol's shared vanilla description, the three repeated names, 99 vanilla descriptions, nine `{==`
keys, a map-wide duplicate report. The dedicated-server stage still holds the old map (owed, as in the Lindon RCA). Also owed: the loc coverage
baseline, after the Ered Luin branch merges (both change it): this change adds 11 untranslated rows per language
(the 7 new descriptions and the 4 Dunland names; PL 7), so run `python tools/check_external_loc_coverage.py
--update-baseline` then.

## Root-cause pattern

**A fix checked against the moment it was written, not the states that follow (1, 2, 5).** The renamer was right
after a full run, wrong after a crash; the one-offs were idempotent until #763 runs; the repair held until a
reinstall. Before calling a writer safe, name the next three things that will touch its output (a failed run, a
later legitimate writer, a reinstall) and test each.

**A change scoped by the tool instead of by the defect (3, 6, 7).** The name check went to the writers in the issue
title, not to every writer of names; the re-key assumed its readers; the reset took its ids from one table. List
every writer and reader of the field first.

## Why each lens found what it found

- **Tooling** found 1, 2, 3, 4, 8, 9 and 16 by breaking each guard on purpose and failing writes in temp trees.
- **Data flow** found 5, 7, 10, 11 and 12 by replaying the translator's discovery and probing the release channels.
- **XML** found 6 and 14 by diffing every live file against its backups and reading the bound fortification of each row.
- **Engine compatibility** found 13 and confirmed the Kit save path, the module load order and that neither text
  field is saved, so existing saves show the new text.
- **Codex** did not run. Standards, Efficiency, Completeness and Design were skipped by tier (no HIGH).

## Feedback memories to codify

None beyond the lessons entries: `lessons/build-tooling-workflow.md` (write last the file a re-run reads; retire a
one-off whose re-run destroys later work; a check goes to every writer of the field) and `lessons/localization-ui.md`
(list a shared key's readers before re-keying it).
