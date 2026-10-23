# RCA: Lines of Descent (#905, 2026-10-23)

## Top-line

The tier 1 review of Lines of Descent (a read-only family tree popup and two buttons, built from view-model
mixins and prefab extensions with no Harmony) ran Codex at `ultra` and three Claude lenses (data flow, engine
compatibility, XML). None found a CRITICAL or HIGH, so no second wave ran. Together they confirmed 6 MEDIUM
and about 14 LOW findings, all fixed in the same session except two named limits. Three findings were found
independently by Codex and a Claude lens, which is the strongest signal in the set.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | Couple rows added spouse cards after the 300-card check, so the cap could be passed (Codex). | Cap checked at the wrong grain | The cap was written for one card per row; the row grew to a couple later in the design. | The whole row is admitted only when it fits; test with 150 married children. |
| 2 | MEDIUM | The relation search followed raw child and sibling lists, which the engine never prunes (`Hero.Father`/`Mother` setters append to the new parent and never remove from the old one), while the tree builder already rejected those stale links (Codex, lens 5). | Two consumers, two rules | The stale-child rule was found for the tree and not carried to the second reader of the same lists. | One "current link" rule shared by both; test with a stale child and a stale sibling. Lesson in `adapters-taleworlds-api.md`. |
| 3 | MEDIUM | The 5,000-visited cap was checked outside the loop that adds heroes (Codex). | Bound checked at the wrong grain | Same shape as 1: the guard sat at the outer loop. | Check before each insertion. Lesson in `testing-qa.md` (with 1). |
| 4 | MEDIUM | An only child under a married couple was drawn at the left with its connector line dropping into empty space: the child row had no `HorizontalAlignment`, and Gauntlet defaults to Left (lens 7, lens 2). | Gauntlet default alignment (repeat) | Vanilla's tree uses a fixed 150 px row that is never wider than a child; the wider TAOM card broke that assumption. `localization-ui.md` already records fixed-width children clustering left. | `HorizontalAlignment="Center"` plus a source pin. Lesson extended in `localization-ui.md`. |
| 5 | MEDIUM | Both mixin constructors called `OnRefresh()`, although UIExtenderEx 2.13.3 already replays the view model constructor's refresh on the new mixin; each clan row ran the relation search three times on open (lens 2, lens 5). | Framework lifecycle misread | The plan cited a precedent for the explicit call; the rule against it lives only in a code comment (`FiefNamingMixin.cs:46`). | Calls deleted, comments and doc corrected, a source pin. Lesson in `localization-ui.md`. |
| 6 | MEDIUM | Every adapter call resolves the hero with a linear `Hero.Find` (lens 2). | Cost, unmeasured | Hero lookups by id look free. | Named limit: timed in game on a late save before any cache is added (the simplicity rule rejects an unmeasured cache). |
| 7 | LOW | A spouse's first card was not recorded, so a later appearance drew a second full card (Codex, lens 5). | Visited set covers nodes only | The visited set was written for expansion, not for display. | Drawn and expanded tracked apart; test on the cousin-marriage fixture. |
| 8 | LOW | Equal-length relation paths followed list order, not birth order (Codex). | Spec not applied to the second reader | The brief said "children in birth order" for the tree; the search did not sort. | Sorted; test. |
| 9 | LOW | A throw while opening, before the layer was attached, skipped the layer's engine cleanup and could leave the popup stuck open (Codex, lens 5, lens 2). | Error path | Only the happy path was traced. | Attach first, clear the reference on failure, run the close callback in a `finally`. |
| 10 | LOW | The selected hero could be missing from its own tree when the root sits 12 or more generations up or past card 300 (lens 5). | Caps versus the user's focus | The caps were reasoned about the tree, not about the hero who opened it. | The climb stops one generation short and the path to the selected hero is drawn first; tests. |
| 11 | LOW | The close button released the movie from inside the layer's own input update, against the layer's stated rule; one close trigger could never fire (lens 2). | Stated invariant not checked against callers | | The close button raises the same flag as the other triggers; the dead trigger is deleted. |
| 12 | LOW | A dead prefab attribute, no gamepad navigation on the two new buttons, unread data fields, and several doc claims about engine behaviour that were wrong or imprecise (sprite category loading, drag scrolling, Escape, the clan relation box) (lens 7, lens 2, lens 5). | Polish and doc accuracy | Copied attributes and engine claims written from the design, not from the decompile. | Fixed; the in-game checklist gained the checks the code cannot prove. |

**Named limits (not fixed, recorded in the feature doc):** the clan-screen cost of finding 6, to be timed in
game; and with "Show all encyclopedia characters" off (on by default) the popup still shows relatives the player
has not met, because vanilla hides only the hero page's own family list.

## Root-cause pattern

Findings 1, 2, 3, 7 and 8 share one shape: a rule was applied where it was first written and not at the second
place the same data or bound is used (the row versus the card, the tree versus the relation search, the outer
loop versus the insertion). The engine fact behind 2 (parent setters never prune the old parent's list) was
known and handled once.

## Why each reviewer caught or missed what it did

- **Codex (`ultra`):** caught the bound and second-reader defects (1, 2, 3, 7, 8, 9) by tracing concrete
  counterexamples through the service; it did not look at prefab geometry.
- **Engine compatibility (2):** caught the mixin lifecycle (5), the cost (6), the geometry (4) and the doc
  claims, because it decompiled UIExtenderEx and the widgets.
- **Data flow (5):** caught the selected-hero gap (10) and matched Codex on 2, 7 and 9.
- **XML (7):** caught the geometry (4) from the widget's line code and the dead attribute and navigation gaps.
- The builders' own tests missed all of these because the fakes had one card per row, no stale links, and no
  layout.

## Feedback memories to codify

None. The lessons above carry the rules.
