# RCA: Orc gait install and the swing probe default (2026-10-18)

**Summary.** The working tree on `bannerlord-1.5.x` (HEAD `d13e91658`) held a small change on top of the reviewed
animation variety commit: the swing alternative probe default flipped to on for Mike's tests, and the docs that record
the live install of the orc gaits (Mike's Kit import, 36 clips generated and Kit-saved, `as_orc_warrior_b` and `_c`
bound by `tools/bind_orc_goblin_locomotion.py --apply`, the snapshot refreshed). A tailored `/deep-review` ran in two
waves: Standards, Engine compatibility, Data flow and XML, then Completeness (Mike chose the tailored set; Efficiency,
Design and Tooling did not run, see the end). The live sweep since `d13e91658` (01:20:45) found 73 files, all this
change's: 72 orc packages and the live `action_sets.xml`, plus 37 RuntimeDataCache entries outside the sweep's folders.

- **No CRITICAL or HIGH.** 5 MEDIUM, 8 LOW and one wrong engine claim, all confirmed against the files; the
  convergence pass on the fixes added 2 LOW (rows 15 and 16).
- **Mike's Custom Battle at 09:41** ran during the review: both variant sets loaded and 125 orcs split 38 main, 37 B and
  50 C. The engine lens proved the 16 cache entries the old clips left behind are never read, so the run is valid.
- **Outcome:** every finding is fixed in the working tree, except the follow-ups listed below. Nothing is committed.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | Plain `bind_orc_goblin_locomotion.py --check` passed when an orc variant had vanished with its clips; `/armory-audit` and `TestLiveArmory` ran it that way, so a reinstall could drop the per-soldier gaits with both gates green (XML M1, Data flow 2, Completeness N4). | Gate polarity | Yesterday's fix for the same gap (RCA 2026-10-17 row 19) added an opt-in `--require-variants` "once the variants are imported". The import happened today and nobody rewired the gate's callers; a strict mode behind a flag waits for someone to remember it. | Plain `--check` now fails an absent variant, like a race set with no clips; `--allow-no-clips` covers both for an install before the Kit import; `--require-variants` is gone. RED tests first. Lesson (repeat of "The safe invocation is the default invocation"). |
| 2 | MED | The documented rollback named only the 2026-10-17 backup folders and the `action_sets.xml` backup. It left the B and C folders, their 20 cache entries and the 16 new main-clip entries in place, and did not say that the Kit had re-cooked one old entry (`7F28D977`) at 08:28, so the live copy no longer matched the old package (Data flow 1, Completeness N3). | Stale procedure | The rollback was written before the import created new state, and nobody re-read it after the import. The 08:28 re-cook was invisible: no tool reports which entries the Kit rewrites. | The rollback now lists four steps and says to take the 16 entries from the backup. Lesson (cache entries). |
| 3 | MED | Eleven places still said the probe is off by default or that the variants are not installed: `INDEX.md`, `feature-map.md` (2 rows), `animation-variety.md` (6 lines), `IAnimVarietyProbeSettingsProvider.cs:8`, plus the restore steps, inventory and skip note in `orc-goblin-locomotion.md` (Data flow 3, Standards, Completeness). | Stale statements | The edit pass updated the lines it knew and did not grep the old state's phrases ("off by default", "not imported", "=missing", "64 packages"). Repeat of two lessons (`build-tooling-workflow.md`, "When a fact moves" and "A change's sweep has two halves"). | Fixed each hit. Lesson (repeat): a state change is a moved fact. |
| 4 | MED | The probe's "on while the swing clips are tested" default had no tracked exit. The setting is absent from v2.0.34, so the next `/release` ships it on; `EnableEnlistmentDiagnostics` went on "temporarily" on 2026-08-28 and shipped on in v2.0.34 (Completeness N1). | Missing exit | The default flip was treated as a local test convenience, and its exit lived only in a code comment, which no release step reads. | An **Owed** line in `animation-variety.md` (rename with the default off before the next release or before any `act_taom_*` code reaches the Armory), the same in the commit body; an issue only on Mike's word. Lesson. |
| 5 | MED | `/armory-audit` was not run after today's art drop (36 masters newer than the catalogue), so the ARMORY ART DRIFT banner would fire every session (Completeness N2). The audit itself reads CLEAN. | Skipped step | The Kit Import Checklist does not list it, and the 2026-10-14 install ran it from memory. | Run `/armory-audit regen`; the checklist names it. |
| 6 | LOW | The probe's hint said "per battle"; the behavior runs in every mission, towns included (Standards, Data flow 4). | Wrong wording | The hint was written from the battle use case. | Hint reworded; the three pinned substrings kept. |
| 7 | LOW | The new comments sat between the attributes and the member (setting and test), unlike their siblings, and cited "orientation.md trap index" instead of the row (Standards). | Convention | Written in place without reading the neighbours. | Moved above the attribute stacks; the row is named. |
| 8 | LOW | The doc said "a player who once ran the build keeps `true`"; MCM writes `TAOM.json` only on a settings save (Data flow 5). | Wrong mechanism | Inferred from the persisted-default trap rather than from MCMv5 `BaseJsonSettingsFormat.Load`. | Reworded; the commit body states why the changed-default tooltip rule does not apply (no saved value exists). |
| 9 | LOW | DualWield's `SpawnAgent` postfix replaces the set and step size of its three gait-race troops after `OnAgentBuild`; the summary still counts them under the drawn variant (Data flow 6, XML note). | Undocumented interplay | The interplay paragraph covered only vanilla's later swaps. | Documented; accepted by Mike (2026-10-18): only a few troops dual-wield. |
| 10 | LOW | The snapshot README row still gave `~3.9 MB` for a 4,466,896-byte file (XML L1). | Stale number | Pre-existing at HEAD; the row was edited without re-reading its size cell. | Fixed. |
| 11 | WRONG | The corrected `anf_cyclic` row said the flag is required on creature `quad_movement` gaits; 94 of 142 vanilla `quad_movement` clips have no cyclic, the horse, camel, cow, dog and cat gaits among them (Engine). Also `BipMovIkUsage` is a clip usage, not a flag. | Engine claim from a partial read | Repeat of the 2026-10-17 RCA pattern (d): the claim generalised from the elephant table and four human clips, not from the whole family. | Row rewritten with the counts. Lesson (repeat). |
| 12 | LOW | The 09:41 battle's evidence was not recorded; two docs still called the run owed and one said the step numbers came "not from a run" (Completeness N5). | Evidence not filed | The review started before the run was read. | Result lines in both docs. |
| 13 | LOW | #791 stopped at 2026-10-14 (Completeness N6). | Issue drift | Mike said no new issue for the C# half; the clip half's issue was not updated. | Progress comment on #791. |
| 14 | LOW | `animation-variety.md` said 8 wiring tests (9 at HEAD); the locomotion doc named the 00:02 and 00:51 copies, not the 01:15 copy that was imported (Completeness N8). | Stale numbers | Same as row 3. | Fixed. |

**Verified, no defect:** the 16 cache entries of the moved old clips are never read. The v1.5.5 client builds an `.rdc`
path in one function (0x484CC0) from a registered package's GUID and never lists the folder (Engine S1). Both orders of
`base_set` definition were traced: a child defined before its base gets set 0, and the live order is base first (Engine
S3, XML). All 36 clips copy their vanilla templates' flags (Engine S2).

## Root-cause patterns

- **(a) A state change is a moved fact** (rows 2, 3, 12, 14). The install changed three facts (the variants exist, the
  probe is on, the run happened), and each lived in more places than the edit pass touched. This is the third time
  the same lesson applies within a month.
- **(b) A strict mode behind a flag is a dead gate** (row 1). The flag was the right check, but every caller ran the
  lenient default. Yesterday's fix created the flag; today's import made it necessary; nothing connected the two.
- **(c) A temporary setting needs an exit that the release reads** (row 4), or it ships, as one did in v2.0.34.

## Why each agent missed or caught these

- **Standards** caught rows 6 and 7 and the interface comment of row 3. It does not read gate callers or procedures.
- **Engine compatibility** proved the cache question (the one risk to Mike's test) and caught row 11. It read the
  native code, not the docs' other statements of the same rule (lines 27 and 232 of the flags doc state the creature
  convention, which it judged harmless, not wrong).
- **Data flow** caught rows 1, 2, 3, 6, 8 and 9: it traced the rollback and the reinstall path as data flows, which is
  what found the two procedures.
- **XML** caught rows 1 and 10 by simulating a reinstall in memory against the gate.
- **Completeness** (wave 2, briefed with wave 1) added rows 4, 5, 12, 13 and 14, and proposed the default flip in row 1
  from the existing lesson.
- **Not run (Mike's tailored set):** Efficiency (the C# change is a default flip; its cost was traced by Data flow),
  Design, and Tooling (the binder change came from the review itself and gets the convergence pass; the scripts that
  wrote the live packages are unchanged since their review in `d13e91658`).

## Decision record: the probe default (addendum to RCA 2026-10-17)

RCA 2026-10-17 rows 21, 27 and 32 recorded "default off" as the fix for the probe's per-frame cost (E1), and as the
reason efficiency items E2, E4, E5, E7, T1 and T4 were not applied. On 2026-10-18 Mike chose the default on for the
swing tests. With no `act_taom_*` code loaded (0 in Native, the Armory and DualWield today) the probe costs 256 silent
lookups and one log line per mission; once such codes exist, every mission pays the per-frame sweep those rows
describe. The Owed line (row 4) restores the off state before either a release or the first `act_taom_*` code.

## Convergence pass

One reviewer checked the fix diff for defects only. It confirmed the binder fix by simulation (a reinstall with only
the main clips: plain `--check` exit 1 naming both variants, `--allow-no-clips` exit 0; a fresh install: exit 0 with
`--allow-no-clips`; `--require-variants` refused) and found two LOW doc defects in the fixes, both fixed:

| # | Sev | Finding | Why missed | Fix |
|---|---|---|---|---|
| 15 | LOW | Rollback step 2 said `check_rdc_entries.py` prints each package GUID; it prints only packages without an entry, and it cannot reach folders step 1 had already moved (the lesson repeated the claim). | The tool's behaviour was stated from its name, not from a run. Same class as row 11. | The step names `check_rdc_entries.package_guid`, runs before the folders move and is marked optional (the entries are inert); step 4 re-binds with `--apply --allow-shrink` instead of restoring the whole file. |
| 16 | LOW | The skip-rules bullet named the goblin variants as skipped; the binder's `VARIANTS` table has no goblin row. | Written from the plan, not from the table. | "None today", with the reason. |

## Follow-ups (not fixed here)

- `docs/reference/bannerlord-animation-system-map.md:595` says only Native and the Armory contribute action sets;
  DualWield registers `soln_action_sets` too (Engine). Pre-existing.
- `docs/ai-includes/creature-mount-authoring.md:269` and `:486` and the creature rows of the flags doc set `cyclic` on
  creature gaits; the engine does not require it, but the mid-charge crash behind those lines is still UNVERIFIED, so
  they stay. Pre-existing.
- `tools/validate_xml_schemas.py:291-292` tells a reader that an mbproj native-data file is never read (XML L2).
  Pre-existing.
- 742 orphan cache entries in the Armory, 1,320 in `TAOM_Map` and 12 in `TAOM`, plus 2,712 `.rtemp` leftovers: inert;
  Mike asked for a cleanup on 2026-10-18, done separately with a backup.

## Lessons recorded

- `docs/reviews/lessons/build-tooling-workflow.md`: "An opt-in strict flag is a dead gate once the state it waits for
  arrives" (row 1), "A state change is a moved fact" (rows 2, 3, 12, 14), "A temporary default needs an exit the
  release reads" (row 4).
- `docs/reviews/lessons/animation-skeleton.md`: "A cache entry without its package is inert; a restored package needs
  its own entry back" (row 2 and Engine S1), "Count the whole clip family before writing a flag rule" (row 11).
