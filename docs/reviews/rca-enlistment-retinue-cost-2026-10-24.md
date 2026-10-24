# RCA: The cost of your own men (#839, deep-review, 2026-10-24)

## Top-line

`/deep-review` on #839 ran lenses 1, 2, 5 and 7 in a first wave and lenses 3, 4 and 6 in a second, then one
convergence pass on the redesigned change. It found **no engine incompatibility**: the number the line
states is `MobileParty.TotalWage`, the same value `DefaultClanFinanceModel.CalculatePartyWage` charges the
parked main party every day. It found **one MEDIUM data-flow defect** and **one MEDIUM design finding**
that two lenses reached independently; both were fixed before the first commit.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM (Data flow) | The board quoted the rank table's wage as "service pays M" (and as "Daily pay") while the column is lost, a state in which the daily tick pays nothing. | Display read beside the payout instead of from it | The builder read the rank table directly; `IEnlistmentWagePreview` exists so displays read the number the payment uses. | The board reads `GetDailyWage()`, 0 while the column is lost; a test pins that state. The wage preview's summary now names that case. |
| F2 | MEDIUM (Completeness, Design) | The line came in a message after "I swear it.", although the issue asks for the cost at the decision point; after the oath the only exit for 21 days is desertion. | The plan's wording ("on the oath line") followed instead of the issue's motive | The plan reused #837's term for the message after the oath. | The line is part of the commander's oath question, where declining is still free; its pay is the wage of the rank the oath grants, from one rank rule shared with the restore. The message, its service and its key are deleted. |
| F3 | LOW (Standards, Design) | Two keys carried one sentence; the pay was computed in a fifth copy of the rank-table lookup. | Duplication | Written per surface. | One key and one builder; the preview and the shared rank rule supply the pay. |
| F4 | LOW (Standards, Completeness) | Test names claimed more than they asserted (the log-once latch, the bound variables, "once", the template); stale summaries on the adapter, the refresh and the model. | Test and comment accuracy | Copied from sibling tests. | Pins tightened; summaries rewritten. |
| F5 | LOW (Engine compat, Data flow, XML) | The doc's in-game check forgot that companions draw a wage, compared N with a folded tooltip line, stated a fixed refresh rate, and the line had no currency unit. | Doc accuracy | Written from the plan, not the engine. | Companions, the main party wage line of the detailed gold tooltip, no fixed rate, the coin icon. |

## Convergence pass

One reviewer checked the redesigned change against the installed v1.5.5 sources: the dialog condition runs
before the line's text renders, the global it sets is rewritten before each render of its only consumer,
the `{=!}` composition keeps the translated oath question, and the coin icon renders in a dialog line as
it does in vanilla's. Verdict READY, with five LOW test and doc items and two nits, all applied: the
"never lower than the current rank" step is now one `StartRank` method both the quote and the restore
call, a test proves the condition returns true in both branches, the no-game tests no longer carry the
game category, the quote's snapshot and the adapter's negative guard are pinned, and the global carries
the `TAOM_` prefix.

Noted, not changed:

- The engine redraws the whole wait-menu text every frame (its TextObject has no attributes, so
  `GameMenuVM.IsMenuTextChanged` is always true), and each nested board line is rendered again on each
  pass. #839 adds one line to that tree; rendering the board to a flat string once per push is a
  follow-up to measure first.
- The menu link colour patch's regex runs on the same per-frame setter, not once per menu open as its
  comment says; a follow-up outside this change, to measure first.
- The board refresh budget is fed by two clocks; that is #841 (C2 of this batch).

## Root-cause pattern

F1 and F2 share one cause: the builder implemented the words of the plan, not the reason the issue gave.
The fix in both cases was to read the motive ("what the player is actually paid", "at the decision point")
and check the surface against it. Lesson appended to `lessons/gamemodels-services.md`.

## Feedback memories to codify

None; the lesson covers it.
