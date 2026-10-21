# RCA: Arthedain troop special-resource costs (#963), 2026-10-21

**Summary:** Mike ruled that Arthedain troops cost no special resource. The fix deletes the four
Arthedain rows from `troop_resource_costs.xml` and adds `NoArthedainTroop_CarriesASpecialResourceCost`.
A seven-lens `/deep-review` found no CRITICAL, HIGH or MEDIUM defect. It found three LOW items caused by
the change (two doc gaps and one career passive that lost its target) and four LOW items that were
there before it.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | `arthedain.md` "Tests" and "GitHub Issue" sections did not name the new test or #963 | docs | The plan named two places to edit (the culture table row and the changelog) and the build stopped there, without a pass over the doc's other sections | One-off. The completion workflow already asks for it; fixed |
| 2 | LOW | `special-resources.md:291` said "5 tests" for a file that now has 6 | docs, hand-kept count | The change added a test to a class whose feature doc counts its tests by hand | Repeat of `lessons/misc.md` "drop hand-kept counts rather than patching one" (plan 025). Fixed by dropping the count, not by writing 6 |
| 3 | LOW | Career pick `knight_of_arthedain_t3_a_p4` (-10% special-resource upgrade cost) now has no Arthedain troop to discount | data, cloned culture | The plan traced every reader of the cost rows (`GetTroopCost` and its callers) but not the modifiers of the same mechanic. The pick is a verbatim clone of Gondor's `knight_of_belfalas_t3_a_p4`, made when the culture was cloned | New lesson in `lessons/data-content-cultures.md`. The pick itself is Mike's call (keep it, documented, or replace it) |
| 4 | LOW | Row counts in `special-resources.md` (198, 264, 392: "85 rows, 43 upkeep") were already wrong at HEAD (93 and 51, now 89 and 47); line 21 lists Castar for Gondor only; `troops.md:394` and `SpecialResourceTroopBadge.cs:11` say 77 rows; `file-catalogue.md:312` names `analyze_troop_balance.py` as a writer of the file | docs, pre-existing | Hand-kept counts, the same class as finding 2 | Follow-up: drop the counts in one docs pass |
| 5 | LOW | The party-screen upgrade clamp (`PartyCharacterVM_InitializeUpgrades_Patch.cs:45-48`) computes affordability from the raw `UpgradeCost`, while the charge uses the career-discounted cost. With a balance between the two, the screen can grey out an upgrade the discount would pay for. Read from code; not reproduced in game | code, pre-existing | #174 fixed only the charge side | Follow-up issue on Mike's word |
| 6 | LOW | No gate checks that each cost row's troop id is a defined troop; `validate_moduledata.py` cannot, because the ids are bare | gate gap, pre-existing | The Config ID cross-reference rule (`xml-data.md`) asks for a resolve test; this file never got one | Follow-up: one test that every `CostRows()` id is defined by a repo character |

## Why each lens did or did not catch these

- **Standards (1):** caught finding 1 and the count part of finding 4, under the documentation duty.
- **Engine compatibility (2):** no finding was in its scope. It verified the nine engine members the
  charge paths use and that a missing row is free everywhere.
- **Efficiency (3):** none in scope; the change only makes the map bar and daily tick cheaper.
- **Completeness (4):** caught finding 2, which wave 1 missed because no wave 1 lens counts tests in
  a second feature doc.
- **Data flow (5):** caught finding 3 (flow 10) and finding 5, by tracing modifiers as well as readers.
- **Design (6):** no proposal; the row deletion is the simplest correct fix.
- **XML (7):** caught finding 6.

## Lesson recorded

`docs/reviews/lessons/data-content-cultures.md`: "Taking a mechanic away from a cloned culture strands
the career passives it cloned with it (#963, 2026-10-21)".
