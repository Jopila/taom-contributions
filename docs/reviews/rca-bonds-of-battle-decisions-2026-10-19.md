# RCA: Bonds of Battle decisions round (deep review, 2026-10-19)

## Top-line

The maintainer approved the six open Bonds of Battle (#861) decisions on 2026-10-19: exclude only an army the player
leads, count grants once per effective recipient, 1 point per 2 influence on Donate Troops with no floor, an alignment
filter on thanking notables, skip the side leader vanilla already thanks, and a relation seam that returns the change
that landed. A per-lord daily cap of 10 on the hand-over followed, after the builder found that a per-Done cap still
let a split gift pay more. The `/deep-review` of that diff ran six lenses in two waves on the uncommitted worktree
`E:\repos\taom-relations-politics` (1 Standards, 2 Engine compatibility, 4 Completeness, 5 Data flow, then 3 Efficiency
and 6 Design). XML and Tooling were not in scope. It found three MEDIUM defects, all confirmed by four lenses each, and
a set of LOW ones; a convergence pass on the fixes found two MEDIUM and four LOW defects (finding 7), also fixed. Every finding was checked against the code or the installed v1.5.5 engine before it was fixed.

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | The "side leader vanilla thanks" flag left out vanilla's last condition, `BattleRewardModel.GetPlayerGainedRelationAmount(...) > 0` (`PlayerEncounter.DoPlayerVictory`). On a win where the player contributed little, vanilla thanks and pays nobody, yet Bonds skipped that lord and, with Personal Relations off, his whole clan (Lenses 1, 2, 4, 5) | A vanilla predicate copied without its last clause, inside an adapter-derived flag | The flag was derived in the adapter, which no test reaches; the design read the dialog that pays, not the condition that opens it | The snapshot carries the raw facts (side leader, the player's army, who leads it, vanilla's amount) and the service decides, with tests. A REPEAT of the 2026-10-18 lesson "flags an adapter derives need a test that reaches the derivation" |
| 2 | MED | The hand-over day count lived in a process-lifetime singleton and was never cleared on a load or a new campaign, so a reload on the same day kept an abandoned timeline's count; the code comment and the doc said the opposite (Lenses 1, 2, 4, 5) | Per-campaign state in a singleton without a session reset | The MANDATORY session-reset rule (`csharp-architecture.md`) was not checked when an in-memory map was added late in the round | `ResetForNewSession()` called from the per-campaign behavior's constructor, with session-reset tests. A REPEAT of the session-reset lesson in `state-lifecycle-save.md` |
| 3 | MED | Splitting a gift still paid more: the -1 to +1 spread kept a floor of 1 at base 1, so five Dones of 2 influence averaged 6.7 points against 5 for one Done; the tests pinned the random roll at 0, so no test could see it (Lens 4) | A random term tested only at its centre | The split property was tested with the spread fixed, which proves only the deterministic core | No floor once a gift earns anything (the decision as approved), and a test that sums the gain over every roll. New lesson in `testing-qa.md` |
| 4 | LOW-MED | The daily cap counted per lord named, while the relation lands on the clan leader with Personal Relations off, so kinsmen each gave 10 to one relation; the cap also limits the ask, and Charm scales what lands (Lenses 2, 5) | A rate limit keyed on a different identity than the thing it limits | The battle path was rekeyed on the effective recipient, the hand-over path was not | The ledger keys on the relation owner, read at the Patch106 boundary; the wording says the cap stops after 10 landed and Charm can raise the last Done |
| 5 | LOW | Docs and hints out of step: the in-game check for the landed change used a barter (vanilla applies it, the seam never sees it); a stale "once per lord" hint; stale `GroupOrder` comments; missing Key Files, Dependencies and Changelog lines (Lenses 1, 4, 5) | Claims written before the code settled | The round changed six rules at once | Each sentence corrected against the code; the checklist replaces the barter item with a fight beside a lord at relation 100 |
| 6 | LOW | Clean-ups: a new day adapter duplicated an existing body (replaced by the engine's daily tick), a float read guarded a cast that cannot fire (now the engine's int), a repeated property read, a test name without its state segment, missing guard tests on the new reservation loop (Lenses 1, 3, 6) | First-draft shape | Builder self-check | Applied |
| 7 | MED | Convergence: no test pinned that the behavior factory builds a fresh behavior per campaign, which the session reset now depends on; the claim "splitting a gift never pays more" was still false at the daily cap (one Done of 24 averages 9.67, two of 12 always land 10) and its test again pinned the roll; plus a reservation gap for a non-lord side leader, two tests that could not fail and two doc lines | The fix round wrote the reset and the claim before their tests; the new lesson was not yet applied to the round's own test | A fresh-behavior pin; the claim is now "at most 10 a day however the gift is split" with a test over every split and roll; the reservation follows vanilla's predicate alone |

## Root-cause pattern

Findings 1 and 3 share a cause with the 2026-10-18 round: a rule was decided where no test could see it, either inside
an adapter-derived flag or behind a random term pinned at its centre. The fix is the same both times: hand the service
raw facts and test every value of every input that shapes the result.

## Why each agent missed these

- **The builder** derived the new flags in the adapter and tested the split property with the spread fixed, and did not
  revisit the session-reset rule when it added an in-memory map.
- **No lens missed a confirmed finding.** Four lenses found each MEDIUM defect independently.

## Feedback memories to codify

None. The lessons carry the rules.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/bonds-of-battle.md](../features/bonds-of-battle.md)
- [docs/reviews/rca-bonds-of-battle-2026-10-18.md](./rca-bonds-of-battle-2026-10-18.md)

<!-- backlinks-end -->
