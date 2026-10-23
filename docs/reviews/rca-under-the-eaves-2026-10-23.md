# RCA: Under the Eaves, forest shortens the player's sight (deep-review, 2026-10-23)

## Top-line

`/deep-review` of Under the Eaves (#849), the rule that the main party sees less far while it stands in forest, ran six
lenses in two waves (Standards, Engine, Data flow, Efficiency; then Completeness, Design) and a convergence pass. No
CRITICAL or HIGH finding; every confirmed finding was fixed before the commit. The code path was sound: the toggle reaches
the contributor, the composer floors the sum, every reader of the main party's sight gets the shorter range, and nothing
outside Main implements the changed adapter.

What was wrong was the feature's model of the world around it. The design and the player-facing hint said vanilla forest
already hides the player's party; it does not (vanilla only makes parties in forest harder for the player to see, and
forest costs the player 20 % speed). The doc promised the Old Forest, which the TAOM map paints as plain. The rule applied
inside the elven towns whose gates stand on forest. And the vanilla perk whose text promises exactly this relief, Keen
Sight ("sight penalty for traveling in forests"), did nothing against it. Mike's standing rule applied the reviewers'
recommendations: no penalty inside a settlement, Keen Sight halves it.

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | The MCM hint, the feature doc and the #849 design comment said the wood already hides the player's party in vanilla. The vanilla forest ratio has one caller, how far the player sees others; no AI path reads forest for spotting. | Engine claim from a method name (REPEAT: #614's same misreading, recorded only in a career procedure; the hidden-orders RCA's "wrong engine claim") | The design read `GetPartySpottingRatioForMainPartySeeingRange` and inferred both directions without listing its callers; the lesson it cited was read for its rule, not its mechanism. | Text corrected; lesson in `lessons/campaign-mechanics.md` with the caller list; trap-index line. |
| 2 | MED | Keen Sight's text promises half the sight penalty for traveling in forests, and vanilla applies it only to the target side; the new penalty ignored it. | Perk text not searched | The design searched the visibility model, not the perk texts that name forest sight. | Keen Sight halves the penalty (read only in forest); engine pin on the perk's -0.5. Lesson: before adding a penalty, search the perk texts for one that promises to relieve it. |
| 3 | LOW | 111 settlement gates stand on forest faces (Caras Galadhon, Rivendell, Thranduil's halls, Dol Guldur), so the rule applied while waiting inside them. | Position rule without the settlement case | A party inside a settlement sits at its gate (`Position = GatePosition`); the design never asked. | No penalty while `CurrentSettlement` is set. Lesson: a map rule keyed on position decides the inside-a-settlement case. |
| 4 | LOW | The doc promised the Old Forest and Fangorn; TAOM_Map paints the Eriador woods as plain (634 forest faces, from x 611 to 990). | Data not checked offline | The design deferred the painting to the in-game check; the review decoded `navmesh.bin` in an hour. | Doc lists the painted woods; the in-game check expects no change in the Old Forest. Lesson: check terrain painting offline before a doc names regions. |
| 5 | LOW | The doc credited the +0.15 forest feat to Dunland only; Khand (`battania`) keeps vanilla's `battanian_forest_speed`. | Culture ids (REPEAT of the orientation trap "Culture ids") | The design read TAOM's xslt for Dunland, not vanilla's feat holder. | Doc corrected. Recurrence note on the culture-ids lesson. |
| 6 | LOW | The setting was named `ForestConcealmentEnabled` for a rule that shortens the player's own sight; the name becomes a saved key at release. | Naming (ADR-009) | Taken from the issue title. | Renamed before release to `ForestSightPenaltyEnabled`. |
| 7 | LOW | "Adds no native lookup" was true only while the party's stored position had a warm face cache; after a load, until the party moves, each read repeats vanilla's lookup. | Cost claim | `Position` returns a copy, so a read never warms the stored cache. | Wording corrected; the in-game frame-time check names the profiler and the cold case. |
| 8 | LOW | Undocumented interactions: Field Camp ambush odds and reach, the Eye of Sauron hunt, creature bands and raid hosts spawning closer, shorter chases; three wrong co-op sentences; two summaries saying contributors only widen sight. | Docs | Readers of the main party's sight were listed from vanilla only. | All documented. |
| 9 | LOW | Tests: ten duplicates or vacuous pins (a test of a substitute's default, an allocation test that only reached the null exit, a self-checking Forest-is-4 pin, Far Watch rows using literals instead of Far Watch's constants). | Test hygiene (REPEAT of the hidden-orders guarantee lesson) | Built test-first by count. | Deleted or bound to the real constants; the new decision is a pure 8-row table. |

Design simplifications applied: the adapter answers `IsInForest(party)` instead of returning a terrain number; the rule
is a pure `Factor(inForest, inSettlement, keenSight)`; the contributor reads the perk only when the party is in forest.

## Root-cause pattern: the design read one engine method and assumed its neighbours

Findings 1 to 5 share one move: the design read the method it planned to sit beside and inferred the rest (who calls
the target ratio, which perk relieves forest sight, where a party in a settlement stands, how the map is painted, who
holds the forest feat). Each answer was one read away, and the reviewers found all five. The design's lessons table
cited the right RCAs; it did not list the callers and data each claim rests on.

**Preventive action.** A design that states what vanilla already does names the engine callers of the method it cites
(a grep of the decompile, one line each), searches the perk texts for one that names the same effect, and, when it keys
on map position, states the inside-a-settlement case and checks how the map data is painted. Recorded as one lesson in
`lessons/campaign-mechanics.md`, with recurrence notes in `lessons/testing-qa.md` (C1) and
`lessons/data-content-cultures.md` (finding 5).

## Why each lens caught or missed what it did

- **Standards (1)** found the setting name (6), the engine literal and the duplicate tests (9), and the cost claim (7).
- **Engine (2)** found the wrong hiding claim (1), the Old Forest (4, by decoding the navmesh), the settlement gates
  (3), Keen Sight (2) and the Khand feat (5).
- **Data flow (5)** found Keen Sight independently (2), the ambush and hunt (8), the co-op sentences (8), and confirmed
  the co-op classification and the Client build.
- **Efficiency (3)** found the cold-cache cost (7); no runtime defect.
- **Completeness (4)** found the trunk sync on the settings counts, the spawners (8), the issue state and the checklist.
- **Design (6)** gave the adapter, rule and contributor shapes and the test deletions (9).

## Convergence pass

One deep-reviewer re-checked the fixed tree against v1.5.5 and the live map data: the six gates match the brief and are
cheapest first; `HasPerk` resolves Keen Sight to the scout exactly as vanilla's own read; `IsInForest` gives vanilla's
answer for every face; the rename reached every reference (the server schema and the co-op fingerprint are built by
reflection); the navmesh decode, the 111 forest gates, the ambush table and the hunt ratios all reproduce. It found one
MEDIUM and five LOW, all fixed:

| # | Sev | Finding | Fix |
|---|---|---|---|
| C1 | MED | The contributor passed literal `true, false` to the pure rule, so six of its eight table rows tested unreachable code and the real settlement and forest decisions were early returns no test ran; a flipped `!` could switch the feature off with the suite green (three such mutations were never tried). | The contributor reads the three facts and passes them to the rule; the table pins the decision and exact source pins catch a polarity change in each fact read. |
| C2 | LOW | A worked example used a Scouting 100 scout with Keen Sight, a tier-9 perk that needs Scouting 225; the scout's own skill raises the base. | Examples recomputed (base at least 25.5, 21.7 in forest). |
| C3 | LOW | The settings provider's comment said a dedicated server uses the compiled default; it uses the operator's file. | Corrected. |
| C4 | LOW | The adapter's class summary still said every call is a native query. | Corrected. |
| C5 | LOW | The doc still said the feature keeps "forest concealment". | Reworded. |
| C6 | LOW | A wiring test carried a redundant clause. | Removed. |

**Why C1 got through.** The fix made the rule pure and fully tabled, and the table looked like coverage; nobody asked
whether production ever passes those rows. This is the hidden-orders guarantee lesson again (a check that names a
guarantee it does not test), now in a mutation log: the builder's own log showed three gate mutations caught only by a
text-order test, and that was read as "caught". Recurrence note on `lessons/testing-qa.md`.
