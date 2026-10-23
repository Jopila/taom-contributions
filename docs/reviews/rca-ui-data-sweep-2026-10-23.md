# RCA: UI and data bug sweep, #958, #923, #947 (deep review, 2026-10-23)

## Top-line

Session 5 of the bug sweep fixed three issues in the worktree `E:\repos\taom-s5` and found a fourth (#944) already
fixed on trunk by `6647f8f57`. Each fix was reviewed at its risk tier: tier 2, so Codex at `xhigh` plus the Data
flow lens, with the Tooling lens for the generator. The #947 review found a HIGH, which brought a second wave
(Tooling, Completeness, Design) and two Codex re-reviews at `high`. Every finding below was checked against the
code or the v1.5.5 decompile before it was fixed. The raw Codex outputs are in `docs/reviews/raw/` (ignored).

| Issue | Change |
|---|---|
| #958 | `SpecialResourceMapBarMixin` inserts its map-bar entry on the first `MapInfoVM.Refresh`, guarded by `Contains`, instead of skipping that refresh (`MapBarVM.Initialize` runs it before the view loads, and the next one is 2 s later) |
| #923 | `IArmouryPlayerAdapter.ChangeRelationWith` documents the real relation owner; `CommanderLordAdapter` stops passing the ignored `affectRelatives: true`; `AffectRelativesGateTests` keeps it out |
| #947 | `generate_new_faction_kingdoms.py` `upsert` replaces its marker region in place (re-appending it turned a block added after it into "drift"), and refuses a second region, a lone BEGIN or END, or a missing closing tag |

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | The first in-place `upsert` replaced only the first region (`count=1`). A stale second region then passed `--check`, where the old strip-all code had reported it; the engine loads the later row of a repeated id (`MBObjectManager.LoadXml`) (Tooling lens) | A side effect of a fix: the old code's incidental check was lost | The fix was written to stop one false positive and was not checked for what else the old behaviour caught | Count markers and refuse anything but zero or one complete region; three refusal tests. New lesson in `lessons/build-tooling-workflow.md` |
| 2 | HIGH | The marker guard counted only BEGIN markers, so an orphan END with stale rows passed (Codex re-review 1) | Same root as 1: a guard checked against one shape of damage | Tests were written for the case the first review named | END markers counted too; test added. Covered by the lesson for 1 |
| 3 | MED | With a region present, the closing-tag check came after `return text`, so a file missing `</Factions>` passed (Codex re-review 1) | Validation order | The check was added on the append branch only | The closing tag is checked first; test added |
| 4 | MED | The #958 source pin read raw text and checked one substring: a commented-out insert, or a new first-refresh skip, still passed (Codex and the Data flow lens) | A REPEAT of the B4c lesson "A source-text gate must fail on the wrong code" (2026-10-22) | Written to fail on the old code only | `ReadSource(stripComments: true)`, exactly three guard returns before the insert, and the no-resource guard ordered before it; all three tests fail on the old mixin |
| 5 | LOW | The new `ChangeRelationWith` summary said "his clan leader" with no exceptions; a clanless hero and a member of the player's clan own their own relation (`DefaultDiplomacyModel.GetHeroesForEffectiveRelation`) (Codex and the Data flow lens) | A doc rewrite that dropped a qualifier | The issue text was followed, then extended for Personal Relations, and its exception clause lost | Exceptions named. One-off |
| 6 | LOW | The gate stripped comments from all 3,330 files (about 20 s) where five mention `affectRelatives` (Codex) | Cost of a source scan | The repo's raw-match-first pattern (`MentionsOutsideComments`) was not reused | Raw match first, strip only the candidates |
| 7 | LOW | `gui-sprite-system.md` still named the deleted `_baseInitialized`; the comment and docs read as if v1.5.5 brought the 2 s delay (it is also on v1.4.8) (Data flow lens) | Docs not swept for the removed name | Only `special-resources.md` was updated | Swept with grep; wording fixed |
| 8 | LOW | Docs stated the old "must stay last" rule and over-claimed what the guard and the sync test catch (Tooling, Data flow and Codex re-review 2) | Docs describing the replaced behaviour | Written before the guard settled | Four docs and the 2026-10-20 lesson updated |
| 9 | LOW | An END comment right before the closing tag, with no newline, was refused (Codex re-review 1) | Pattern too strict | Not tested | Newline after END optional; test added |

## Not fixed here (rejected or follow-up)

- **A copy of the region's rows without the exact markers passes `--check` (Codex re-review 2, rated HIGH).**
  Rejected for `upsert`: no marker parser can see a copy without markers, and the old code saw it only because
  re-appending moved the real block, which is the false positive #947 removes. A repeated id is the real
  defect, and it is already caught: `SCHEMA_INVALID` validates these files against the engine XSDs, which
  declare `Faction`, `Hero` and `Kingdom` ids unique (`xs:unique`), and `DUPLICATE_NPC_ID` covers lords. This
  RCA first said nothing caught them; #974 was filed on that claim, and its review proved it wrong. #974 added
  `EngineIdUniquenessTests` to pin the existing check instead of a second gate.
- **`generate_arthedain_troops.py` `upsert_templates` has finding 1's defect** (second-wave Tooling lens). Latent:
  one region today. Fixed in #975 (`tools/_marker_region.py`).
- **`promote_borrowed_cultures.py` and `insert_new_factions.py` still strip and re-append.** Fixed in #976.
- **The #958 map-bar entry stays after a hero loses the resource** (Data flow lens; older than #958). Fixed in #977.
  Reachable when the hero leaves a resource kingdom with a culture that has none (goblin, mistymountainorcs,
  bluecraig, lindon), through vanilla heir succession, or the `campaign.set_hero_culture` cheat. Not through
  Player Switcher, as first written here: it runs only during character creation, before the map bar exists.

## Follow-up batch (#974 to #977, #979, same day)

The maintainer asked for the four follow-ups above to be fixed and closed. Each was reviewed at tier 2 (Codex
`xhigh` plus the Tooling or Data flow lens). Findings:

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MED | #974's premise was wrong: `SCHEMA_INVALID` already blocks a repeated `Faction`, `Hero` or `Kingdom` id at commit, because the engine XSDs declare the id unique. The three new duplicate-id schemas only reported each duplicate twice, and flagged commented-out rows the engine skips (#974 Tooling lens) | A gate added without checking for an existing one | The claim came from the #947 review (Codex re-review 2) and was relayed into this RCA, a lesson and two docs unprobed | Schemas removed; `EngineIdUniquenessTests` pins the engine check; docs corrected. New lesson in `lessons/build-tooling-workflow.md` |
| F2 | MED | #977's in-game test used Player Switcher, which runs only during character creation, before the map bar exists, so the test passed on the old code (#977 Data flow lens) | A verification route not traced to the code it exercises | The route came from the #958 review and was not checked against `PlayerSwitchRegistrationBehavior` | Issue and this RCA corrected: the `campaign.set_hero_culture` cheat, or heir succession. One-off |
| F3 | HIGH | Pre-existing, filed as #979: `promote_borrowed_cultures.py --apply` would write an unparseable `taom_module_strings.xml` and stop half-way, because it parsed each file after writing it and its strings extractor copies multi-line rows line by line (#975/#976 Tooling lens) | Write-then-parse | The script's `--check` compares bytes and never parses | Both writer scripts parse every output before writing any; the extractor fix stays open in #979. README convention bullet |
| F4 | LOW | Docstrings in `promote_borrowed_cultures.py`, `insert_new_factions.py` and the regions test still described strip-and-re-append, and the regions test misnamed the remaining drift (#975/#976 Tooling lens) | Docs not swept with the code | The callers' docstrings were outside the edited functions | Reworded |
| F5 | LOW | `gui-sprite-system.md` lacked the removal step (#977 Data flow lens) | A REPEAT of finding 7 above | Only `special-resources.md` was updated | Line added |
| F6 | MED | The first #979 guard in `insert_new_factions.py` left `SubModule.xml` outside it: built and written after the eight guarded files (Codex) | A guard placed before the list was complete | The guard was added where the old loop stood | `SubModule.xml` is built first and joins the guarded list; a source-order test pins it |
| F7 | MED | `EngineIdUniquenessTests` matched registrations as raw text, so a commented-out registration passed (Codex) | A gate test reading text instead of what the gate reads | A REPEAT of the B4c lesson "A source-text gate must fail on the wrong code" | Asserts the `(file, id)` pairs `validate_xml_schemas.registered_files` resolves |

Rejected: a check that a block passed to `replace_region` carries its own marker (#975/#976 Tooling lens, T4). Every
caller builds the block from the same marker variable, and a mismatch fails that caller's first test: a tiny win
for added code. Still open: #979's extractor fix (whole `<string>` elements, and which Arthedain liege title
wins), and two hero ids, `lord_6_23` and `lord_6_24`, that both `heroes.xslt` and `characters/heroes.xml`
define, so the XML's text wins the engine merge (#974 Data flow lens; needs the maintainer's choice of bio).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/lessons/build-tooling-workflow.md](lessons/build-tooling-workflow.md)

<!-- backlinks-end -->
