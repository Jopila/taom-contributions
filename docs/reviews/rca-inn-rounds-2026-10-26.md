# RCA: Inn Rounds (#901, deep-review, 2026-10-26)

## Top line

`/deep-review` ran seven lenses in two waves, then a convergence pass, on the tavern rounds and on the refactor that moved the #899 reveal into a shared `HoldsRevealService`. The refactor kept the notable reveal's behaviour line for line. The review found no HIGH finding, three MEDIUM findings and a set of LOW findings; all of them were fixed before the commit. Mike accepted one commit for the refactor and the feature together, instead of the two that decision 1 planned, because the wave 1 fixes touched both.

The pattern behind the MEDIUM findings: the build read vanilla's behaviour from the call it hooks, not from every place that call can be reached. The tavern keeper also stands in the town centre by day. The skill-XP call scales its input. And the trunk had moved the single-player gate into one method the same day.

## Findings

| # | Sev | Finding | Why missed | Fix |
|---|---|---|---|---|
| 1 | MED | The house round priced the tavern's list of characters even when the keeper was met in the town centre. Vanilla spawns a keeper there by day (`TavernEmployeesCampaignBehavior.cs:118-123`), and 3 live TAOM centre scenes have the spawn point. | The plan assumed that a keeper conversation means the player stands in the tavern. | The room is counted only when `CampaignMission.Current.Location` is the tavern (vanilla's own test); otherwise the house line hides. |
| 2 | MED | The Charm notice, and Notable Services' Roguery notice, printed the XP that was asked for. The engine credits raw XP times the XP multiplier times the learning rate, so "+40" could land as about +2. | Repeat of the "show the value that landed" lesson (#899 relation notice); the XP grant was not treated as a value that can change. | A new `AddSkillXpLanded` reads the skill XP before and after the call. Both notices show the difference and are skipped at 0. |
| 3 | MED | The co-op single-player gate was written out inline, in this feature and in Notable Services, after the trunk had moved it into `CoopSessionPolicy.IsSinglePlayerAuthority`. | Copied from a sibling that the trunk refactor had missed. | Both services call the shared method. A new scan test fails on any four-term copy outside `CoopSessionPolicy.cs`. |
| 4 | LOW | A failed reveal made the keeper say "Anything else?" twice: the reply went to `tavernkeeper_pretalk`, whose only line says it again. | The reply's output token was not checked against what the next state plays. | The reply goes to `tavernkeeper_talk`, as vanilla's own turndown does. A source pin checks the token. |
| L | LOW | Other LOW findings, all fixed: <br>- no NaN tests for the moved gates; <br>- a computed `MainParty` getter; <br>- a value checked twice; <br>- missing MCM hint-default tests; <br>- comments that repeat what tests pin; <br>- a wrong log tag; <br>- dead fields; <br>- no-game pins inside a game-only class; <br>- a weak test; <br>- doc drift (the maid's meal, what the morale notice measures, the shared adapter docs). | Hygiene. | Fixed. |
| C | LOW | The convergence pass found two tests that need the game but carried no category: the morale clamp pin reads engine IL, and the per-campaign behavior test builds a behavior. A no-game filtered run would fail on both. | The class-level category covered the binding file, but not the IL read; the wiring file holds pure source pins, so its one game test was missed. | `RequiresGameIL` and `RequiresGame` on the two tests. |
| V | LOW | The verify run after the rebase found that `promote_borrowed_cultures.py` clones every `.gondor` string row into Arthedain, Cardolan and Rhudaur, so it would add a second `str_taom_round_drink.<culture>` row; its `--check` failed for all three. | No lens ran the tools suite: the change touched no script, so the Tooling lens did not launch, and a data row's effect on a generator was not in any lens's scope. | The generator skips the drink rows, as it skips the liege titles; `test_promote_string_rows.py` pins it. |

## Accepted limits

- The price cap of 35 is below the value of one unit of beer (50) or wine (90), so most towns charge the cap whatever the drink. The doc records it; the cap is in the JSON.
- In the player's own town the owner relation is 0, so a `revealMinRelation` above 0 would make the player's own keeper refuse.

## Lesson

`lessons/adapters-taleworlds-api.md`, "Before keying a feature on a conversation partner, list every place vanilla spawns that partner".
