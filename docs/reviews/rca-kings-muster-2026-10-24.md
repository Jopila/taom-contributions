# RCA: The King's Muster deep review, #874 (2026-10-24)

**Summary.** The King's Muster makes an AI kingdom ruler's army calls cheaper in influence. Six `/deep-review` lenses in two waves read the uncommitted tree before its first commit. They found no CRITICAL or HIGH defect, one MEDIUM and a set of LOW findings, each re-read against the code and the v1.5.5 decompile, plus four design proposals. All were applied before the first commit. The MEDIUM removed half the feature: the planned household rule (AI armies never draft the player's clan) duplicated a per-party engine control and overrode the player's choice in it, so it was deleted, and the feature doc points players at the clan screen's "Allow joining other armies" box instead.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The household rule filtered player-clan parties out of `CanLordCreateArmy`'s list after the base call. Vanilla's own filter on that line (`DefaultArmyManagementCalculationModel.cs:131`) already skips any party whose leader has `CanJoinArmy` false, a saved per-party flag the player sets in the clan screen. The TAOM filter removed parties the player had left allowed, and lost vanilla's slot refill and strength check. | Engine control duplicated | The plan quoted line 131 and listed only two of its sixteen conditions; the orchestrator read the same line and also missed `!CanJoinArmy`, the seventh. **Repeat:** the #862 lesson in `campaign-mechanics.md` already names `!CanJoinArmy` on this filter. | Rule deleted (scope change recorded in #874). Lesson extended. |
| 2 | Design proposal | A hook, an adapter interface and an adapter, plus a copied config loader and its own JSON file, for one multiplication. | Simplicity | The feature was shaped like its siblings before the household rule went. | A static facts reader (as Help Unlooked-For), the factor in `army_targeting.json` (as Hold the Walls), the engine's `Hero.IsKingdomLeader`, the factor checked once. |
| 3 | LOW | The discounted cost is also vanilla's candidate ranking key (`:156-159`); a factor of 0 would make a ruler keep the weakest parties. | Engine consumer missed | Only the charging callers were listed. | Range raised to 0.25 to 1; the reorder is documented. |
| 4 | LOW | The player-as-ruler exclusion lived in the adapter with no test; the module's parking comment was backwards; an unused parameter; a gendered MCM hint (a repeat of the #863 to #865 lessons); "exactly the base game's cost" ignored the culture feats; a duplicated override-list test in Red Arrow; stale doc rows; a balance run with nothing to measure it. | Assorted | Written ahead of the review. | Decision moved to the service and tested; comments, hint and docs fixed; balance check rescoped to hand readings. |
| 5 | LOW | The ruler-discount check read the co-op locks before the constant factor. | Ordering | | Reordered. |

## Root-cause pattern

Finding 1 makes this the second feature to misjudge who `DefaultArmyManagementCalculationModel.cs:131` lets into an AI army. The prevention is to quote every condition of an engine filter in the plan, not the ones that look relevant.

## Lessons

- `docs/reviews/lessons/campaign-mechanics.md`: the #862 entry gains a repeat note: before adding a filter over an engine candidate list, quote every condition of the engine's own filter, and check `PartyConfiguration` for an existing per-party player switch.
