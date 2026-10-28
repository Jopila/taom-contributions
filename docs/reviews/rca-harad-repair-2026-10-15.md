# RCA: Harad repair after the 2026-10-10 tree replacement (deep review, 2026-10-15)

Issue #798. Repair committed in `44e9edd48`.

## Top-line

Erkam's `08b30bfc4` ("Some XML stuff which haterade will hate me for it 2", 2026-10-10) replaced the Harad troop tree:
4,044 changed lines in `troops_harad.xml`, 25 troops deleted, 45 new ones. It was committed with plain git, outside
Claude Code, so no TAOM gate ran on it, and neither did Mike's later ModuleData commit `776054c0c`. The first
Claude-driven ModuleData commit after them met the fallout: the commit gate `check-moduledata-validation.sh` refused it
for 7 `UPGRADE_SKILL_REGRESSION` errors and 6 career-kit drifts, and the C# suite carried 6 more red tests (pools naming
deleted troops, Shaghana without enlistment kits, an Aserai kit chain issuing one kit twice, garrison conversion with no
target, a commission reward outside the armour class table).

The repair (uncommitted at review time) fixed those through the existing generators wherever one exists, and on Mike's
decisions below. `/deep-review` then ran eight `deep-reviewer` lenses on Opus 5.5 at max effort in two waves (Standards,
Engine compatibility, Data flow, XML; Efficiency, Completeness, Design, Tooling). **No engine incompatibility, no
performance issue.** **One HIGH introduced by the repair** (archer kits issuing bows an enlistee cannot equip) and
**one HIGH from `08b30bfc4`** (the deleted troops; Mike accepted the break).

## Mike's decisions

| # | Question | Answer | Applied as |
|---|---|---|---|
| D1 | The 7 skill regressions | "Clamp only" | `rebalance_troops.py --fix-monotonicity`: 43 values on 23 Harad troops; no exemption-list entry |
| D2 | The Pezarsan camel line (`harad_pz_levy`), in no pool | "Aserai and Abanissa" | Weight 2 beside levy 7 and noble 3 |
| D3 | Erkam's 10 hand-written Aserai enlistment kits | "Minimal: sergeant only" | Only the duplicate archer sergeant replaced |
| D4 | Bows for the archer kits (finding 1) | "Steppe ladder" | `composite_bow`, `steppe_heavy_bow`, `composite_steppe_bow`, `steppe_war_bow` |
| D5 | The 25 troops `08b30bfc4` deleted | "Keep them removed, remove them from all code, etc. They are redone" | `harad_noble_merc` deleted too; 26 name keys and 312 language rows removed; comments, docs and the roster page updated. **Known limitation:** a v2.0.34 save holding an old Harad troop is not supported |
| D6 | Two tool fixes for `08b30bfc4` fallout | "Yes, both" | Findings 10 and 11 |
| D7 | Abanissa enlistment kits through the Aserai tree (#431) | "Now, with the bow fix" | `TREE_ALIASES` `'abanissa': 'aserai'`, 15 kits seeded, bows per D4 |
| D8 | The elephant and Mumakil riders as volunteers | "The mumakil should be special resources and not able to be recruited"; the elephant "Emissary only, too"; Mumakil price "2800 War Drums" | Both leave every pool; `clan_aserai_1` has no pool now; the Emissary sells the Mumakil at `merchant_cost="2800"` |
| D9 | With the riders out of every pool, no AI party fields them either (finding 14) | "Yes, nobody recruits them" | Recorded in `mumakil.md`, `elephant.md` and `volunteer-recruitment.md`; no party template change |
| D10 | Deleting `harad_noble_merc` moves 4 Aserai career slots by the career rule (finding 13) | "Apply the rule" | Cavalry and infantry careers carry `khuzait_lance_1_t3`, the Harad militia spearmen's lance |

## Findings

| # | Sev | Bug | Found by | Category | Why missed | Preventive action |
|---|---|---|---|---|---|---|
| 1 | HIGH | The generated Shaghana archer kits (recruit, veteran, sergeant), the hand-copied Aserai archer sergeant and later the four Abanissa archer kits issued `ladder_harad_bow_t2/t3`, `difficulty="100"`. The kit goes to the inventory and `CharacterHelper.CanUseItem` (v1.5.5) refuses it below Bow 100 | XML, Tooling, Design | data, tooling | `generate_enlistment_rosters.py` copies a donor troop's launcher with no difficulty rule, while its siblings `generate_career_kits.py` (lowest difficulty) and `generate_starter_kit.py` (difficulty 0) both have one. The author copied the generator's row by hand and trusted it. `audit_enlistment_roster_coverage.py` only requires a ranged weapon in an archer kit, and `RANGED_DAMAGE_CEILING` checks damage, not difficulty | `EnlistmentArcherLauncherDataTests` (LiveInstall): every Aserai, Shaghana and Abanissa archer kit launcher at difficulty 50 or less, and no rank weaker than the one below. Follow-up: a difficulty cap in the generator and a validator check over every player-applied roster (it would also flag 18 older kits above difficulty 50: Lindon, Lothlorien, Mordor, Rivendell, Sturgia, Umbar and the two Rivendell ranged careers) |
| 2 | MED | The Aserai archer sergeant's bow (48) and sword were weaker than the veteran's (52) | XML | data | The hand copy took the generator's sergeant row, whose floor is the generator's own chain, not Erkam's hand-tuned lower ranks | The same test pins non-decreasing launcher damage by rank |
| 3 | HIGH | `08b30bfc4` deleted 25 troops that v2.0.34 uses as basic and elite troops and in 36 party templates. A save holding one keeps a hollow `CharacterObject`; three engine paths read its null `UpgradeTargets` (#670) | Engine, Data flow | save compatibility | **Repeat** of `rca-keyforce-lamedon-port-2026-09-25.md` finding 4: `.claude/rules/troops.md` says never delete a troop, and still nothing machine-checks that a shipped troop id survives. The commit ran no gate at all (pattern A) | D5: Mike accepts the break; the commit body records the known limitation. Follow-up: a shipped-troop-id baseline gate (second occurrence) |
| 4 | MED | All 9 Shaghana clan party templates field the Serpent line (`harad_serp_*`); none fields Shaghana's own Red Serpent line, although its pool, culture troops, tavern and kits now do | Data flow | data (pre-existing) | `08b30bfc4` added the Red Serpent line without templates; no gate compares a culture's pool roots with its party templates | Follow-up for Erkam and Mike |
| 5 | LOW | `harad_noble_merc` kept "for saves" with a comment that still said a culture referenced it, and a Mercenary troop nobody sells shows in the encyclopedia | Engine, Data flow | data | The author kept one id for save reasons and did not reread its comment | Moot under D5: the troop is deleted |
| 6 | LOW | A second "TAVERN MERCENARIES" banner in `troops_harad.xml` | XML, Design | tooling | `generate_tavern_mercenaries.append_copies` writes the banner on every run | The duplicate went with `harad_noble_merc`; tool behaviour left as is (one-off tool) |
| 7 | LOW | Stale names and prose: tests named `HaradLevy`/`HaradNoble` asserting other troops, the enlistment test summary, the `CustomBattleService` comment, and ten feature docs (pool weights, rider recruitment, mercenary counts, roster counts, deleted ids as examples) | Standards, Completeness, Data flow | docs | The repair changed data and did not grep its changed ids through prose and test names | Fixed. Habit already in `.claude/rules/working-discipline.md`; no new rule |
| 8 | LOW | The enlistment generator left `taom_enlistment_equipment.xml.bak-enlist-*` inside `Main/_Module`, and the build deploys it | XML, Tooling | tooling | `write_xml` writes its backup beside the target | Both backups deleted. Follow-up: write backups outside `Main/_Module` |
| 9 | MED | `generate_enlistment_rosters.py --seed-missing` had never finished a run: its marker comment contained `--`, illegal inside an XML comment, so `parse_written` refused every seeded file | orchestrator (while seeding) | tooling (pre-existing) | The marker arrived in `b1852a7ae` (2026-08-04) with no test of the seed path, and `b7a3f5931` (2026-09-01) added the parse check that refuses it; the header beside it already explains the double-hyphen rule | `SEED_MARKER` constant plus `SeedMarkerTests` (RED on the old text, GREEN after) |
| 10 | MED | `generate_tavern_mercenaries.py` could not finish a run: its `mistymountainorcs` row named a troops file deleted in `f577bd547` | Tooling, Design | tooling (pre-existing) | One-off tool with no test, re-run never needed until now | Row now names `goblin_fighter` (what the data binds); a stubbed-write simulation of `main()` adds 0 copies and its one write equals the bytes on disk |
| 11 | MED | `generate_name_localization_strings.py` would register 40 vanilla keys from `08b30bfc4`'s `taom_vanilla_troop_obsolete_overrides.xml`; its shipped-tree test was red at HEAD | XML, Tooling, Design | localization (pre-existing) | The `character_name` category globs `characters/*.xml`, so any new file there becomes a source | The file is excluded, with a test; the category now adds only Erkam's 6 real TAOM keys |
| 12 | LOW | The clan pool tests rolled only 0, so they could not see a dropped camel line or a wrong total | Standards, Completeness | testing | Pre-existing test shape | DataRow tests over every Abanissa and Shaghana clan that roll into the last bucket |
| 13 | HIGH | Deleting `harad_noble_merc` (D5) turned `generate_career_kits.py --verify` red: it was the only low Aserai troop carrying `aserai_lance_1_t5`, so the rule moved 4 career slots to `khuzait_lance_1_t3`, and the commit hook denies a commit on that | convergence pass | data, verification | The career check was run before the deletion and not again after it: a deletion changes every generator that derives from troops, and the author re-ran only the validator. The full C# suite cannot see it (`StarterKitCoverageTests` asks only that some Aserai troop carries the item) | D10: rule applied. Habit: after any troop add or delete, re-run every generator `--verify`/`--check` the commit hook runs, not only the validator |
| 14 | MED | With both riders out of every pool, no AI party fields an elephant or a Mumakil, and nothing recorded it | convergence pass | design | D8's question was asked about the player's route; AI lords recruit from the same pools | D9: Mike accepts it; recorded in three docs |
| 15 | LOW | Text that drifted from the data during the fixes: a tavern-mercenaries count (24 defined, 23 offered) and date, a playability sentence the docs pass rewrote wrongly, the elephant checklist line, a test summary and comment, a "not yet committed" note | convergence pass | docs | Prose written from the brief rather than re-read against the data | Fixed |

## Root-cause pattern

**A. TAOM's gates run only on commits Claude Code makes.** `08b30bfc4` and `776054c0c` reached trunk with plain git.
The commit hooks fire only inside Claude Code, and the GitHub CI workflows were removed on 2026-10-05 (`c8d69a0dc`), so
nothing ran on either commit and trunk carried red gates for five days. The same gap shows in
`rca-eye-of-sauron-2026-10-14.md` and `rca-wildmesh-mounts-2026-10-15.md`. Every one of the 08b30bfc4 findings above is a
gate that existed and did not run. A preventive action needs a gate that runs for every committer: a repository-level
git hook (`core.hooksPath`), a required check on the GitHub remote, or CI again. That is Mike's decision; it is owed as
an issue. (Update 2026-10-28: Mike chose the git hook for the commit rules: `.githooks/commit-msg` checks every
committer's subject label, length and attribution. The data gates in these findings still run only for Claude.)

**B. Generators that build player kits disagree on launcher difficulty.** The career and starter generators protect the
player from an unequippable bow; the enlistment generator does not. Finding 1 is that gap reaching shipped data.

## Why each agent missed these

Every finding above was caught in review, none in game. The author's own verification (validator, career-kit check,
full C# suite) passed with finding 1 present, because no gate reads launcher difficulty.

| Agent | Missed | Why |
|---|---|---|
| Standards | 1, 2, 3, 13 | Its rules cover C# and prose shape; the bows, the deletion and the career drift are data facts |
| Engine compatibility | 1, 2 | It verified how the engine loads the rosters, not whether a player can equip what a roster issues; `CanUseItem` sits on the inventory path, outside the brief's engine questions |
| Efficiency | all | Nothing in the change is on a hot path; correct to find nothing |
| Completeness | 1, 2 | It checks what a change lacks (issue, docs, tests), not item stats |
| Data flow | 1, 2 | It traced the kits to the quartermaster and the inventory, and stopped at "issued", not "equippable" |
| XML | 3 | Its scope is the changed files; the deleted troops were outside the diff and it flagged them as outside scope |
| Design | 3 | Proposals only, by design |
| Tooling | 3 | Scripts only |
| All eight | 13, 14 | Both arose from fixes applied after the lenses reported (D5, D8); the Step 4 convergence pass exists for exactly this, and found them |

## Lessons recorded

- `docs/reviews/lessons/data-content-cultures.md`: a player kit copied from a troop inherits its launcher's difficulty.
- `docs/reviews/lessons/build-tooling-workflow.md`: gates that only run in Claude Code leave plain-git commits unchecked
  (second and third occurrences).
- `docs/reviews/lessons/localization-ui.md`: a name generator that globs a folder registers whatever lands in it.

## Follow-ups (issues owed, on Mike's word)

1. Launcher difficulty: a cap in `generate_enlistment_rosters.py` and a validator check over every player-applied
   roster (finding 1).
2. A shipped-troop-id baseline gate (finding 3, second occurrence) and #670.
3. Shaghana party templates and the Red Serpent line (finding 4).
4. A gate for every committer (pattern A).
5. The Harad tier-7 bowmen have no ranged-ladder cell (`tools/ranged_ladders.json` lists tiers 2 to 6), so
   `rebalance_ranged_ladders.py` refuses to run and `docs/reference/ranged-troops.html` still lists the old
   `harad_archer`.
6. Three generators still write retired Harad item ids (`GENERATOR_RETIRED_ITEM_REF`).
7. `tools/data/armor_roster_tiers.json` is a 2026-09-25 snapshot that still names the deleted troops; regenerate it in
   its own change.
8. Enlistment generator: backups outside `Main/_Module`, and a `--seed-missing` dry run that lists what it would add.
9. The translation run for the new Harad name keys (paid).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/starting-equipment-tuning.md](../features/starting-equipment-tuning.md)
- [docs/reviews/lessons/build-tooling-workflow.md](lessons/build-tooling-workflow.md)
- [docs/reviews/lessons/data-content-cultures.md](lessons/data-content-cultures.md)
- [docs/reviews/lessons/localization-ui.md](lessons/localization-ui.md)
- [docs/reviews/rca-keyforce-harad-javelins-2026-10-16.md](./rca-keyforce-harad-javelins-2026-10-16.md)

<!-- backlinks-end -->
