# RCA: Enlistment seven-rank ladder (#936, deep-review, 2026-10-19)

## Top-line

`/deep-review` (seven lenses in two waves, then one convergence pass) on #936 found **no runtime
defect on the upgrade path**: a save at ranks 0 to 3 keeps its pay, service XP, kit, duty access and
rank bonus, and every reader of the rank handles 0 to 6. It found **4 MEDIUM and 8 groups of LOW**
findings (the table below), all fixed before the first commit. The review covered the kit-tier rename commit too
(`EnlistmentRank` became `EnlistmentKitTier`), which had no review of its own.

The finding that matters: the Leadership gates were sized from the engine's nominal XP curve, and the
feature doc claimed the hero's learning rate "only makes a gate easier". It does not. Every passive
Leadership grant goes through `HeroDeveloper.AddSkillXp`, which multiplies it by the learning rate,
and that rate falls to zero a few levels past the learning limit. A new hero (Social 2, no Leadership
focus) earns 22.5 XP a day up to Leadership 10, then less, and stops at 18; a Social 1 hero with no
focus stops at 4. The shipped gates still hold for a normal hero, but the old gates of 20 and 50 were
out of reach for one without a Leadership focus, and the first test claiming reachability only pinned
literals.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM (Engine compat, Data flow, XML) | The doc said the learning rate only makes a gate easier; above the learning limit it slows the grant to zero. | Engine claim from a nominal model | The plan sized gates from `GetXpRequiredForSkillLevel` and the grant size, without reading how `AddSkillXp` scales the grant. | Doc rewritten with the real rate and a Social 1 known limitation. Lesson appended to `lessons/campaign-mechanics.md`. |
| F2 | MEDIUM (Completeness) | `DefaultLadder_LeadershipGate_IsReachableByEachStepsDayThreshold` pinned four literal arrays; nothing computed reachability (#521 asks for exactly that). | Test name claims more than it checks | Written to the plan's wording. | `ShippedLeadershipGates_AreReachedByEachStepsDayThreshold` reproduces the v1.5.5 curve and learning rate from the shipped config; a raised gate turns it red. |
| F3 | MEDIUM (XML, Data flow) | The reused `taom_enlist_rank_sergeant` row kept translations chosen when it named the top rank (senior NCO words in KO and zho-HK); the paid run would translate the six new ranks around it. | Reused key, changed meaning | "English unchanged" was taken to mean "translations stay valid". | Reset to English in 12 language files and removed from 12 caches, so the ladder is translated as one set. |
| F4 | MEDIUM (XML) | The 3 retired rank rows stayed in all 12 language files; no tool ever removes them. | Orphan rows | `--sync-ids` only adds rows; no gate checks the reverse. | Rows deleted; the rank-key test now fails if they return to any language file. |
| F5 | LOW (Data flow, XML) | A player's own pre-#936 `enlistment_duties.json` with old rank names throws, and the loader then offers no duties, with one log line. | Renamed enum names in data | Name-parsed enums were treated as code. | Both parse-error logs add a rank-rename hint; a test proves the hint stays out of other errors. No enum aliases (an old "Sergeant" would read as the new one). |
| F6 | LOW (Standards, Design) | The top kit tier was written twice (`3` in the mapping and `EnlistmentKitTier.Sergeant` in the behavior). | Single source of truth | Two authors' habits. | `EnlistmentKitTiers.Best`; the policy decides the top tier. |
| F7 | LOW (Design) | The status board computed the wage itself instead of `WagePolicy.DailyWageForRank`. | Duplicate logic | Pre-existing; touched by this change. | One function. |
| F8 | LOW (Design) | The rank-bonus cap was proven by a source-text scan. | Form over value | Cheaper to write. | Behaviour tests at both call sites (8 and 12). |
| F9 | LOW (Standards) | A test hand-copied the rank enum as a dictionary (repeat of the `RankTokens` lesson in `lessons/testing-qa.md`). | Parity copy | Extended instead of removed. | Uses `Enum.IsDefined` and `Enum.Parse`. |
| F10 | LOW (Completeness) | Two tests' Leadership setups became inert after D2 lowered the early gates. | Stale test setup | Values changed under the tests. | Moved to the Staff Sergeant step. |
| F11 | LOW (many) | Stale comments, log text calling a kit tier "rank", stale counts, release note and index rows with old names. | Doc drift | Wide rename. | Fixed; the kit-tier member renames follow as their own refactor commit. |
| F12 | LOW (Standards) | The kit-tier rename commit message claimed the two enums had "Sergeant" at different positions; at that commit both were 3. | Commit body claim | Written ahead of the ladder. | Message amended before push. |

## Convergence pass

One reviewer checked the applied fixes against the installed v1.5.5 sources. It confirmed every
formula the reachability test reproduces (curve, learning limit, learning rate, over-limit factor,
floor, and the order of operations per day), and that raising the Sergeant Major gate to 17 or the
Staff Sergeant gate to 15 turns the test red. It found six LOW items, all fixed: two stale doc facts
(a roster count and an audit date), a missing negative test proving the rank-rename hint stays out of
other parse errors (now proven RED with the hint forced on, GREEN after restore), stale rank names in
test comments, a redundant assertion, and a test lookup that could match `AlreadyDrawn` inside
`AlreadyDrawnBest`. It noted that the reproduction copies engine formulas, so an engine bump that
changed them would not turn it red; the repo has no registry of such reproductions.

## Root-cause pattern

F1 and F2 share a cause: a number derived from an engine table was trusted without reading the path
the engine applies to it. The XP table was right; the learning rate between the grant and the table
was skipped, and the test then pinned the derived numbers instead of re-deriving them. F3 and F4 are
the localization version of the same thing: a key whose English did not change was assumed to keep a
valid translation, though its meaning on the ladder changed.

## Why each lens caught or missed these

- **Standards:** F6, F9, F11, F12, and the stale-test findings.
- **Engine compatibility:** F1, by decompiling `AddSkillXp` and `CalculateLearningRate`.
- **Data flow:** F1 independently, F5, and the full rank-consumer trace (26 flows, no runtime gap).
- **XML:** F3, F4, F5, and confirmed all 27 duty gates keep their ordinal.
- **Efficiency:** no findings.
- **Completeness:** F2, F10, the doc gaps, and the #521 and #835 follow-ups.
- **Design:** F6, F7, F8, and the kit-tier member renames.

## Feedback memories to codify

None; the lesson below covers the learning-rate trap.
