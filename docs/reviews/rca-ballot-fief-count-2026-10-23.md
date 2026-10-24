# RCA: Ballot fief count (deep review, 2026-10-23)

## Top-line

The `/deep-review` of the fief count on each fief-vote ballot card (#866) ran seven lenses on the uncommitted worktree
`E:\repos\taom-relations-politics`: 1 Standards, 2 Engine compatibility, 3 Efficiency, 4 Completeness, 5 Data flow,
6 Design and 7 XML. It found one HIGH defect, one MEDIUM and five groups of LOW findings. The HIGH one was a layout
bug: the hint widget inside the count row had `StretchToParent` on both axes under a horizontal `CoverChildren`
`ListPanel`, so Gauntlet measured the row to the whole space offered to the card (about 1370 x 845). Every fief-vote
card would have spilled out of its panel and pushed its win bar below the frame. Four lenses (1, 2, 5, 7) found it
independently. Every finding was re-checked against the code and fixed in the working tree; the full suite then passed
(24669 passed, 0 failed, 6 skipped) and the binding gate passed (728 passed).

Repeat check: `docs/reviews/lessons/` and `docs/reviews/rca-*.md` were searched for `StretchToParent` and
`CoverChildren`. Finding 1 is a related repeat, not an exact one. `localization-ui.md` already carries "A Gauntlet
screen with fixed-width children must be FIXED WIDTH and CENTRED, never StretchToParent" (near line 139) and an earlier
entry (near line 638, also summarized in `rca-supply-search-2026-09-13.md`) saying a `CoverChildren` button cannot hold a
`StretchToParent` overlay. Both warn about `StretchToParent` next to a content-sized parent, and the second is the same
parent and child pairing, but neither covers a hint widget in a stack panel, and the builder met neither because no
test reads size policies. No earlier lesson covers the other six findings. The lesson for this review is already filed
under #866 in `localization-ui.md` ("A StretchToParent child of a CoverChildren panel fills the whole offered space");
this RCA cites it and adds none.

## Findings and root causes

| # | Sev | Finding | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | HIGH | The `HintWidget` in the count row had `StretchToParent` on both axes inside a horizontal `CoverChildren` `ListPanel`. Gauntlet measured the row to the whole space offered to the card (about 1370 x 845), so each card spilled out of the panel and its win bar landed below the frame. A hint widget only relays its parent's hover and needs no size; vanilla's hints in the same prefab carry none (Lenses 1, 2, 5, 7) | Layout; size policy under a content-sized parent. A related repeat of the `localization-ui.md` entries on `StretchToParent` | The builder copied a stretched hint from a TAOM prefab where it sits inside a Fixed wrapper, which makes it harmless there. The tests checked bindings, XPath and brushes, never sizes | One `TextWidget` with the `HintWidget` as its child and no size policy (the Lens 6 shape, vanilla's own). A test asserts no inserted element is `StretchToParent`. Lesson under #866 in `localization-ui.md` |
| 2 | MED | The test that the card count equals the scoring input compared against the wrong method; scoring counted through `Build` (Lens 4) | Test shape; a test that checks the wrong thing | The test was written against the helper the builder had in mind, and no one traced which method scoring actually calls | `Build` now counts through `CountClanFortifications`. Because `Build` cannot run without a campaign, a source pin test holds the call |
| 3 | LOW | The card count dropped blank names while scoring counted them, so the two numbers could differ (Lenses 1, 2, 3, 5) | Data flow; two counts with two filters | The filter was added to the display path as tidiness and scoring was not compared | The filter is removed, and the summary struct that carried it went with it |
| 4 | LOW | The label leaves out the fief under vote, which the ruler's clan usually holds, so a ruler's number looked one short (Lens 5) | Completeness; unstated scope of a number | The label was written for the common case and the ruler case was not walked | The hint now names the fief under vote for that clan |
| 5 | LOW | The docs said the list is "sorted for the player's culture" while the game pins the invariant culture (Lenses 3, 5) | Docs claim not read from the source | The sentence described the intent, and the call that sorts was not opened | Sort is now invariant, and the doc says so |
| 6 | LOW | Records and tests: the rebalance MCM hint; wording of the disabled-card and vanilla-divisor claims; a log call that could never log; a test that could not fail; the brush test missing its `LiveInstall` tag; the XPath written three times; two tests duplicating a gate or the compiler; three copies of the prefab binding walker; a binding gate that accepted more than one XPath match; doc gaps (GitHub Issue section, smoke steps, diagram, INDEX); the #866 body still the plan; no #763 row (Lenses 1, 2, 4, 5, 7) | Test shape and doc drift | Builder self-check; strings and helpers were written once and not re-derived or consolidated | One shared walker replaces the three copies; the gate now requires exactly one XPath match; dead log, vacuous and duplicate tests removed, the tag added; claims reworded; doc sections, INDEX line and #763 row added; an as-built comment on #866 |
| 7 | LOW | A merge conflict with trunk in the 12 language files at the insertion point (Lens 7) | Merge; parallel inserts at one anchor | New rows went at the same spot trunk's rows went | Resolved at the merge, keeping both sets of rows |

## Root-cause pattern

Findings 1, 2 and 3 share one habit: a piece was built to look right and was never checked on the axis that breaks.
The hint was copied from a place where a stretch is harmless, and no test reads sizes. The count test compared against
a method that is not the scoring input, so it passed without proving the claim. The card count filtered names while
scoring did not, and nothing compared the two. The tests in this feature measured what was easy to read (bindings,
attribute names, brushes) and not what the feature promises (a card that fits, a number equal to the score's input).

Finding 1 shows a gap the layout lessons did not close. The earlier `StretchToParent` entries are written as rules for
screens and buttons, so a builder adding one hint widget to a vanilla card does not recognize them as the same
problem. The new #866 lesson names the exact shape and ships a test that pins it.

## Why each agent missed these

- **The builder** copied a stretched hint from a TAOM prefab where a Fixed wrapper contains it. The tests it wrote read
  bindings, XPath and brushes, so the unbounded row never showed. It compared the card count with a helper instead of
  with the method scoring calls, and it filtered blank names on one side only.
- **Lens 1 (Standards)** found the stretched hint (1), the blank-name filter (3) and the record and test drift (6).
- **Lens 2 (Engine)** found the stretched hint against the vanilla prefab (1), the sort culture (5) and the
  disabled-card and divisor claims (6).
- **Lens 3 (Efficiency)** found the blank-name filter (3) and the sort culture (5).
- **Lens 4 (Completeness)** found the count test against the wrong method (2) and the doc and record gaps (6).
- **Lens 5 (Data flow)** found the stretched hint (1), the blank-name filter (3), the missing fief under vote (4) and
  the sort culture (5).
- **Lens 6 (Design)** supplied the corrected shape for finding 1: one `TextWidget` with a size-less hint child.
- **Lens 7 (XML)** found the stretched hint in the prefab (1), the test and gate drift (6) and the language-file merge
  conflict (7).
- **No lens missed a confirmed finding.** Finding 1 was reached independently by four lenses, which is the reason it
  is rated HIGH with confidence.

## What proves it

- The full suite: 24669 passed, 0 failed, 6 skipped.
- The binding gate: 728 passed, with the gate now requiring exactly one XPath match.
- `DecisionOptionFiefCountPrefabTests` asserts that no inserted element is `StretchToParent` and that the
  `HintWidget` has no size policy.
- A source pin test holds that `Build` counts through `CountClanFortifications`, since `Build` cannot run without a
  campaign.

## Feedback memories to codify

None. The rule is in `localization-ui.md` under #866.
