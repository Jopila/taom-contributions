# RCA: Watchful Holds deep review, #822 (2026-10-18)

**Summary.** Watchful Holds (#822) lets a hostile castle or town harass a moving lord party within 5 map units:
the player's party is disorganized and takes small losses, an AI lord's party is only disorganized. Seven
`/deep-review` lenses read the uncommitted working tree on `feat/war-armies` before its first commit. None found a
CRITICAL or HIGH defect. They confirmed five MEDIUM and a set of LOW findings, which the orchestrator re-read
against the code and the v1.5.5 decompile before entering them here. All were fixed in the working tree before the
first commit, except the two follow-ups at the end. One behaviour decision went to the maintainer: army cohesion
now drops by 5 per hit and never below 10 (it was 10 per hit with no floor).

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The co-op gate was `IsAuthority`. It fails open under BannerlordTogether, so every peer would roll its own losses on shared parties, and on a BannerlordCoop host the feature would disorganize remote players' parties. | Co-op gate (repeat) | The build brief said "co-op authority only", copied from sibling hourly behaviors, without reading the 2026-10-06 lesson for passes that change other parties' outcomes. The builder followed the brief. | Fixed: the behavior stands down while `IsSessionActive \|\| ShouldDeferToHost`, like Eye of Sauron, and `coop-interop.md` has the row. Lesson extended (see below); a machine check is proposed as a follow-up. |
| 2 | MEDIUM | The province gate read only `RealmTerritoryService.State`. After Realm Borders had drawn once, switching it off left the territory Ready, so the gate stayed on, against the doc. | State outlives its toggle | The territory's state machine was taken to be the feature's on or off answer. `RealmBorderService.OnMapFrame` returns early when disabled and nothing calls `Invalidate`, which only the data-flow lens traced. | Fixed: `RealmProvinceLookup` also reads `IRealmBordersSettings.Enabled`, with a test. Lesson added to `state-lifecycle-save.md`. |
| 3 | MEDIUM | The doc and the MCM hint said a disorganized AI lord is "only slowed" and loses "nothing else". The engine also gives its troops a -0.2 factor on starting battle morale (`SandboxBattleMoraleModel`), and the -0.4 speed factor is in `DefaultPartySpeedCalculatingModel`, not the impairment model the doc named. | Engine effect not traced to its consumers | The plan read the speed model and the duration model, then described the state from those two. Nobody grepped every reader of `IsDisorganized`. | Fixed in the doc and both hints. Lesson added to `adapters-taleworlds-api.md`. |
| 4 | MEDIUM | Each hit took 10 cohesion from an army the player leads, with no floor. `Army.CheckArmyDispersion` disbands a player army at 0.1, and every party leaving it is then disorganized; with about four hits a day an army could be disbanded by harassment alone. | Engine threshold not read | The number came from the plan, which did not read what the engine does at the meter's low end. | Maintainer's decision: 5 per hit, never below 10 (`cohesionFloor`, validated), with `WatchfulLossMath.CohesionAfterHit` unit tests. Same lesson as 3. |
| 5 | MEDIUM | The hourly handler ran the co-op probe, a timer and the service for every one of 2,000 to 2,500 parties, and the 234-fort scan used `foreach` over an `IReadOnlyList` (a boxed enumerator per eligible lord). Measured with the shipped Debug JIT, the proposed 1 ms hourly budget held only below about 110 to 160 eligible lords. | Hot path | The budget was proposed without a measurement, and the class comment promised "no allocation" without a test. | Fixed: lords and the player are filtered first in the behavior; the fort array is cached per campaign in the service and scanned by index; the province lookup runs once per party; the fort name is read only for the player's message. Lesson added to `campaign-mechanics.md`. |
| 6 | LOW | Four snapshot fields were written and never read (`Id`, `FactionId`, `OwnerFactionId`, `Name`), and the adapter returned a half-filled snapshot that repeated the service's filter. | Dead data | Fields were added for symmetry with the brief. | Deleted. |
| 7 | LOW | "Harass AI lords" off still costs AI lords inside the player's army their share; `lossMultiplier` scales only the fraction part, not the random extras; the size gate counts the leader party's own men. The hint, the JSON comment and the doc said otherwise or said nothing. | Text promises more than the code | The hint and comments were written from the intent, not from the loop that applies the losses. | Wording fixed in the hint, the JSON comment and the doc; behaviour kept (simplicity criterion). |
| 8 | LOW | Comments and the doc said both roster methods assert. Only `RemoveNumberOfNonHeroTroopsRandomly` asserts; `WoundNumberOfNonHeroTroopsRandomly` silently wounds fewer. | Engine claim from memory | The brief stated it from one read of the remove method. | Corrected; the clamp stays because it keeps the reported counts true. |
| 9 | LOW | The doc stated per-file test counts (123 in total) that did not match the files (128). | Evidence | The doc subagent counted by hand and the orchestrator did not re-count. | Counts removed from the doc; coverage is described instead. |
| 10 | LOW | Missing tests: the cohesion and extra-loss numbers were never varied from the config, the equal-distance tie-break never ran, and nothing checked the JSON culture keys against the culture registry. | Test coverage | The tests pinned the defaults only. | Added, including a shipped-JSON test against `CultureDataFixture.MainCultureIds()`. |
| 11 | LOW | The test helper class was `Rig` in `WatchfulHoldsRig.cs`, and a comment cited `AmbushCandidate.EngineParty` as the precedent for object handles, which is a different (protected seam) shape. | Standards | Naming and the precedent were picked quickly. | Renamed; the comment now cites the object-token interfaces of the cavalry charge and mission start guard features. |
| 12 | LOW | The message could read "0 killed, 0 wounded and 0 food lost" and never said the march was slowed. | Player text | The sentence listed only the counted losses. | New text names the slowdown; rows re-seeded in English. |

The single convergence pass on the applied fixes found no code defect and seven LOW text and test gaps, all fixed:
the feature map and a `CoopSettingsRelevance` comment still described the old authority gate; the MCM hint promised
losses to "each party in" the army, where only parties marching with it are hit; a source-regex test counted the two
gate statements anywhere in the file, so the hourly handler's gate was unproven (now a runtime test that fails with
the gate removed); an assert message named the wrong case; the field camp rule was listed as vanilla; and two
comments missed the fort list in the reset and the Realm Borders settings dependency. The cause is the one below:
fix 2 changed a gate, and the text that described the old gate was not searched.

## Root-cause pattern

Findings 3, 4, 7 and 8 share one cause: text and numbers were written from the plan and from one or two engine
reads, not from every place the engine and TAOM consume the state. Finding 2 is the same mistake across a feature
boundary: another feature's state machine was read as if it were that feature's toggle.

## Why each lens caught or missed what it did

- **Standards (1)** found 11, the precedent comment and the helper name, and flagged the wrong test counts outside its lens.
- **Engine compatibility (2)** verified all 31 engine calls and found 3, 4 and 8 by reading the consumers of
  `IsDisorganized` and the army's hourly tick.
- **Data flow (5)** found 1, 2, 6 and 7 by tracing the territory's lifecycle and the co-op tick split.
- **Efficiency (3)** found 5 with a micro-benchmark built in the shipped Debug JIT mode.
- **Completeness (4)** found the missing tests (10) and the issue body's drift, which was corrected.
- **XML (7)** found the missing culture-key guard (10) and the harvest tool's extra blank line (follow-up 2).
- **Design (6)** proposed the per-campaign fort cache and the single province lookup, both applied in 5.

## Follow-ups

1. **A machine check for co-op gates.** The co-op lesson was not read for this feature, the second miss of its
   kind after the stuck-battle sweep. A test that every feature module's campaign behavior appears in
   `coop-interop.md`'s gated table would have stopped the build at the first test run. Not built here; an issue on
   the maintainer's word.
2. **`tools/harvest_literal_loc_keys.py:157-163`** inserts its block above the file's trailing blank line instead
   of replacing it, so every harvest leaves two blank lines before `</strings>`. The extra line was removed by hand
   here, as in `2410aec51`. Fix: `lines[at:close] = block`. Pre-existing tool code, outside this change.
