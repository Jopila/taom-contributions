# RCA: Tokens of Esteem (deep review, 2026-10-19)

## Top-line

The `/deep-review` of Tokens of Esteem (#863, gifts to companions and lords for relation) ran seven lenses in two waves
on the uncommitted worktree `E:\repos\taom-relations-politics`: 1 Standards, 2 Engine compatibility, 5 Data flow and
7 XML, then 3 Efficiency, 4 Completeness and 6 Design. Tooling was not in scope. It found no HIGH or CRITICAL defect,
about ten MEDIUM findings and a set of LOW ones; a convergence pass on the fixes found six LOW defects (finding 11), also fixed. Every finding was checked against the code, the live data or the
installed v1.5.5 engine before it was fixed.

Three lessons written for this same batch on 2026-10-18 and 2026-10-19 recurred one build later: key a throttle on the
relation owner, a test fake that passes the shared relation rule straight through hides what the engine does, and the
engine shapes that reach a dialog token must be listed. Those lessons sit in category files that nothing loads for a
feature that only calls `IRelationGrantService`, so `docs/features/relations.md` now carries a short "rules for
callers" paragraph next to the seam.

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | The cooldown was keyed on the recipient while the relation can land on his clan leader (Personal Relations off, or a co-op host), so every lord of one clan took a gift each cooldown into one relation (Lenses 5, 2, 4) | Throttle keyed on the wrong identity; a REPEAT of the relation-owner lesson | The rule lived in `adapters-taleworlds-api.md`, which the gift build never loaded | The service takes the ledger and keys gate and stamp on the owner the adapter reads (`GetHeroesForEffectiveRelation`); "rules for callers" in `relations.md` |
| 2 | MED | All 22 Armory creature mounts (wargs, war rams, elk, boars, spiders, mumakil) carry `item_category="war_horse"`, so the Rohirrim horse taste prized a warg; the orc taste named 2 of 7 orc races (207 of 557 orc-race lords) (Lenses 2, 5, 7) | Category-keyed data not proven against TAOM's shipped items and races | The catalog was seeded from vanilla category names and two race names, not from a count of the live data | Rohirrim taste dropped from `war_horse`; all seven orc races listed; the shipped-catalog test reads the live skins.xml. Instance of the `data-content-cultures.md` lesson on proving a filter against shipped data |
| 3 | MED | Prisoners and own-clan lords reached the lord gift line; vanilla's "Can I talk to one of your prisoners?" leads a captive to the diplomacy branch (Lenses 2, 4) | Engine shapes that reach a dialog token not listed; a REPEAT of the `testing-qa.md` engine-shapes lesson | The plan read the token, not every vanilla line that leads into it | Hidden gate for prisoners and the player's own clan, with tests |
| 4 | MED | The rule that keeps a refusing lord's line clickable lived in the behavior, with no test; six early exits in the service had no test; the flow's tests all sat behind one class-level `RequiresGame` tag (Lenses 1, 4, 6) | Rule in the entry point; coverage hidden by a test category | The flow was added to keep the behavior short, and its tests stayed in the behavior's class | The rule moves into the flow; untagged flow tests; tests for every early exit; priority-order pins |
| 5 | MED | The Large purse (10000) gave the same relation as the Medium (2000), because the companion value band stops at 1000 (Lens 5) | Two numbers from the issue not reconciled | The purse sizes and the value band were specified separately | A purse band of 1 point per 500 gold up to 10, with purse ranges 500 to 5000 derived from it |
| 6 | MED | The doc and code said Escape on the party screen hands a gift back; the engine never compares the item roster on reset, and the real leak is a later troop change plus Cancel (Lens 2) | Engine claim not read; a REPEAT of the party-screen lesson | The claim was copied from a related feature | The text and checklist step 8 describe the real case; the commit already prevents it |
| 7 | LOW | The cap limits what a gift asks for, while Charm can add to what lands; with the scaling switch off a hero at relation 100 still took a gift that landed nothing; the -1 refusal was silent (Lenses 2, 5) | A fake that passes the shared rule through; a REPEAT of that `testing-qa.md` lesson | `GiftFixture` returned every delta unchanged | The wording states what the cap limits; the scaling switch is removed (scaling is a rule in #863); the -1 shows vanilla's relation notice |
| 8 | LOW | Six lines were gendered in Russian and Polish although the doc called them neutral (Lens 7) | Wording checked in English only | No localization lesson covers speaker or player gender | Neutral rewording before the paid run, with the 12 seeded rows re-seeded. New lesson in `localization-ui.md` |
| 9 | LOW | Hero facts were read by walking the whole hero list, even with the feature off; the hero name was rendered on every read (Lens 3) | Repeated expensive lookup on a dialog condition path | The conversation partner was turned into an id and looked up again | Check the conversation partner first; read the switch first; render the name only in the notice |
| 10 | LOW | `GroupOrder 61` is also used on another unmerged branch while the comment said it was free everywhere; a stale settings count; an unchecked day cast; no drop-summary warning; misnamed tests; small structural clean-ups (Lenses 1, 4, 6) | Claims and shape | Builder self-check | Corrected; the comment says provisional until the merge rebase |
| 11 | LOW | Convergence: a test that could not fail (an empty list made the missing guard invisible), a checklist step left over from the removed scaling switch, a comment that called the log-line notice a popup, wording that still said "per hero" after the owner keying, an orphan comment in the strings file, and a misnamed test | Fix-round residue | The round changed the key, the switch and the notice at once | Each corrected; the test now holds a good so the guard is load-bearing |

## Root-cause pattern

Findings 1, 3 and 7 are repeats of lessons from the two earlier features of this batch. The lessons were right but
placed where a later feature's author never looks. The fix is to put the rule next to the seam a feature calls (the
"rules for callers" paragraph in `relations.md`), so the next relation feature meets it at the point of use.

## Why each agent missed these

- **The builder** worked from the plan, which named the tokens and the seams but not the engine paths into the tokens,
  the live item categories, or the owner rule from the earlier features.
- **No lens missed a confirmed finding.**

## Feedback memories to codify

None. The lessons and the "rules for callers" paragraph carry the rules.
