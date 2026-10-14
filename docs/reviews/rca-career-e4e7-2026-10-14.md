# RCA: career forks engine, steps E4 to E7 and E12 (#769), 2026-10-14

## Top-line

The career redesign's first engine commit adds one take and refund rule (`CareerChoiceRules`), fork
groups (`max_picks`), per-career point levels, lock on leave, a paid retrain, a career-switch cost and
a tree-version migration. `/deep-review` ran seven lenses in two waves of at most four (standards,
engine, data flow and XML; then efficiency, completeness and design). Nothing was CRITICAL or HIGH.
Two findings were MEDIUM: a retrain price that wraps negative on absurd config and makes a paid
switch free, and a feature doc that the change made wrong. Every other finding was LOW or a nit. The
engine lens verified 19 engine claims on the installed v1.5.5 DLLs and found nothing incompatible.

Three of the patterns below have shipped before: a result message that describes a branch the code
did not take (`lessons/localization-ui.md`, 2026-10-01), living docs left behind by a change
(`lessons/build-tooling-workflow.md`), and a dormant destructive path that data can switch on (the
2026-10-08 career RCA, pattern 2).

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | `GetRetrainCost` added two config values in plain `int`; a huge `retrain_gold_base` or `retrain_gold_per_level` wrapped negative, the switch read a price of 0 or less as free and skipped its confirmation, and retrain became impossible | Derived value never validated | The parser checked each attribute for `>= 0` and nothing checked the product; the builder brief said "non-integer or negative warns" and named no ceiling. Standing rule "Config Providers MUST Validate" covers absurd values, and the brief did not carry it | `long` math clamped to `int.MaxValue`; tests for both huge inputs and for a switch at that price; lesson in `gamemodels-services.md` |
| 2 | MED | `docs/features/career-system.md` stated the old signatures, the old refund contract, three diamond tints, three keystone-rule consumers, and none of the new attributes | Living doc left behind | Neither builder brief named the feature doc; the plan listed docs as owed at the end, so nothing updated them while the code changed. Repeat of `build-tooling-workflow.md` "Document continuously" | Doc rewritten in this change; `docs/ai-includes/agent-teams.md` builder template gains a doc-sweep line |
| 3 | LOW | `MigrateTreeVersion` returned true whenever it stamped, so a player who held only the root read "your career choices were refunded" | Result message on a no-op branch | The brief said "when it returns true, show the message"; the message was written for the refund branch only. Repeat of `localization-ui.md` "A result message states what the action did on every branch" | Returns the refunded count; the message shows only when it is above zero |
| 4 | LOW, latent | `Stamp` and the migration only added `career_tree_vN` flags and `StoredVersion` takes the highest, so a flag from an earlier career could hide a later bump and certify picks made on another tree | Marker with no single writer | The version belongs to a career but is stored per hero; the brief described the add and never the replace | `Stamp` is the only writer and replaces every older version flag; tests for a v1 and a v2 career over a stale v3 flag |
| 5 | LOW | The retrain and switch confirmations kept Confirm enabled when the purse was short; the only feedback was a chat line, which the engine drops when the single-player chat box is off, and the switch picker closed | Feedback on an unreliable channel | The brief chose a message after Confirm; nobody checked how vanilla gates an unaffordable action. Vanilla perk reset disables its accept option instead | Confirm disabled through `InquiryData`'s `isAffirmativeOptionEnabled` with vanilla's "You don't have enough denars." (maintainer's decision) |
| 6 | LOW | The screen guessed why `SwitchCareer` failed from `Gold < cost`, so an ineligible switch by a short player showed the gold message | Caller infers a callee's failure cause | `SwitchCareer` returned a bool while `TryRetrain` returned a result enum; the two were briefed separately | `CareerSwitchResult` enum, mapped like `CareerRetrainResult` |
| 7 | LOW | `CloseScreen` pops the top state by level, and an inquiry callback can now reach it | Unguarded pop from a late callback | No path reaches it today (engine lens traced every one); the hardening costs one line | Returns unless the career state is the active state |
| 8 | LOW | The Retrain button's label did not dim when the button was disabled | Gauntlet state not propagated | `IsEnabled` dims only the button's own brush unless `UpdateChildrenStates` is set; this is the prefab's first bound `IsEnabled` | Attribute added; lesson in `localization-ui.md` |
| 9 | LOW | `ICareerSwitchService` and `ICareerLifecycleService` grew although nothing fakes them and each has one implementer | Interface kept past its ADR-002 test | The briefs extended the existing interfaces; ADR-002's 2026-09-24 amendment says such an interface goes when its file is touched | `ICareerSwitchService` deleted; `ICareerLifecycleService` kept because one of its two resolvers is in the single-owner `Main/SubModule.cs` (edit refused this session); `RefundAll` taken off it |
| 10 | LOW | Deleting the +/- commands left `IsActive` and `GroupId` on the group VM with no reader | Orphaned state after a deletion | The deletion brief named the commands, not the state they read | Both deleted; the older unbound members are a follow-up |
| 11 | LOW | Tests missed a career unknown to the registry (`TryRetrain`, `GetSwitchCost`) and an ineligible switch from a fork-holding career (charge order unpinned) | Skip-guard and ordering gaps | Sibling methods had the unknown-career test; the ineligible tests ran on a career with no fork, where the charge is never reached in either order | Three tests added |
| 12 | LOW | Nothing pinned the session-launch order (repair, then migration, then cache refresh) | Ordering contract unpinned | The order was right, so no test failed | Source-order pin test |
| 13 | LOW, latent | A future `max_picks` or `point_levels` without a `tree_version` bump would lock old saves' picks or shrink their budget with no refund | Dormant destructive path switched on by data | Each attribute is validated alone; nothing ties them. Repeat of the 2026-10-08 RCA pattern 2 | Shipped-data test: a career with a fork group or point levels has `tree_version >= 2`, and no career mixes fork and unlimited groups |
| 14 | LOW | The container test validated only two career services; both services now depend on the new purse adapter | Wiring unpinned | The 2026-10-08 preventive action (W1-09) named the services of that day | Lifecycle and switch services added to the container validation |
| 15 | LOW | No test checks `CareerScreen.xml` bindings, and this change added nine | Silent-failure class untested | Gauntlet ignores a bad binding; the repo has the test pattern for two other screens | `CareerScreenPrefabBindingTests` |
| 16 | LOW | Two harvest runs left two identical header blocks in `taom_module_strings.xml` | Tool side effect | `harvest_literal_loc_keys.py` writes its marker header on every run (`insert_rows`, by design), and each run also adds a blank line (open #781, which will not stop the second header) | Blocks merged by hand; a second header per run is a separate tool follow-up |
| 17 | LOW | Stale comments (the keystone rule's consumers, the toggle's gates, the lifecycle summary, `HeroCareerData.Flags`, a renamed test) and `docs/features/gui-sprite-system.md` still teaching the deleted hover and +/- bindings | Living doc left behind | Same as 2 | Updated in this change |
| 18 | NIT | `GetNextPointLevel` read the raw level while the budget clamps it to 1 | Two formulas, one input | Written separately | Same clamp |

## Root-cause patterns

1. **Briefs that described the happy path only** (1, 3, 4, 6). Each defect sits where the brief named
   what to do on success and named no bound, no replace semantics or no failure branch. The
   orchestrator wrote those briefs. A brief for a value from config states its range and the range of
   anything derived from it; a brief for a message states every branch; a brief for a stored marker
   states who replaces it.
2. **Living docs and secondary surfaces left behind** (2, 10, 17). The change deleted or changed
   members and nothing swept the docs, comments and state that described them. Third occurrence of
   the `build-tooling-workflow.md` pattern.
3. **A dormant destructive path waiting for data** (13). Second occurrence of the 2026-10-08 RCA
   pattern 2. The gate now exists before any data turns the path on.

## Why each lens caught or missed what it did

- **Standards:** caught 1 through "Config Providers MUST Validate", plus 6, 9, 10 and 11. It does not
  trace data to displays, so it missed 3.
- **Engine:** caught 5, 7 and 8 from the installed DLLs and confirmed every inquiry and gold claim. It
  reads engine contracts, not TAOM docs, so 2 was out of its lens.
- **Data flow:** caught 3, 4, 12, 13, 14 and 18 by tracing both directions, and confirmed 1 as an
  inconsistency of signs.
- **XML:** caught 8 and 16 and reproduced the localization gates in Python.
- **Completeness:** caught 2 and 15 and the ordering half of 11.
- **Efficiency and design:** found no defect; their proposals are listed below.

## Improvements applied (behaviour preserving)

The retrain button's state now comes from the walk that already computes each diamond's lock reason;
the switch reuses the creation handler's stamped assignment; one validated-integer parser replaces
three; the version flag parse uses the BCL; the tier-check delegate is built once per rebuild.

## Not applied

- `ICareerLifecycleService` deletion: one of its two resolvers (the other is `GauntletCareerScreen`)
  is in `Main/SubModule.cs`, a single-owner file whose edit was refused this session. It goes with the
  next `SubModule.cs` change.
- Follow-ups on code this change did not modify: the group VM's older unbound members (`Tier`,
  `IsExpanded`, `IsLocked`, `ExecuteToggleExpand`); one shared tier-row prefab for the three tier
  copies (worth doing before E8 and E9 add tooltip lines); `max_perk_points` validation; one shared
  purse adapter with the Armoury; a full rebuild on every click (only if a hitch is measured); the
  older stale parts of `docs/features/gui-sprite-system.md` (it still describes `ExtendablePanel`, pip
  art and `locked_chains`, none of which the prefab has had since before this change).

## Convergence pass

One reviewer checked every fix and improvement above against the code, the tests and the installed
v1.5.5 DLLs: all rows held, and behaviour parity held for every improvement. It found one LOW defect the
fixes had introduced and three LOW documentation errors, all fixed:

| # | Sev | Finding | Why missed | Fix |
|---|---|---|---|---|
| C1 | LOW | The disabled-Confirm gate returned "You don't have enough denars." on both branches, and the engine shows any non-empty reason as Confirm's hover hint (`SingleQueryPopUpVM.UpdateButtonEnabledStates`, v1.5.5), so a player who could pay read that they could not | The addendum's brief gave one string for the gate; vanilla returns an empty reason when the action is allowed. Third case of the result-text pattern in this change (finding 3) | The affordable branch returns `(true, string.Empty)` |
| C2 | LOW | The feature doc's test table put the registry tests under `CareerRetrainTests` and the migration tests under `CareerTreeVersionTests`, and three counts were stale | The docs agent described the files from the brief's summary | Rows and counts rebuilt from a grep of each file |
| C3 | LOW | This RCA said `SubModule.cs` holds the interface's only resolver; the screen is a second one | Written before the fix added it | Corrected |
| C4 | LOW | This RCA blamed the duplicate harvest header on #781, which covers the blank line only | The tool writes its marker on every run by design | Corrected; the second header is a separate tool follow-up |

Nits fixed with them: the changelog's "uncommitted", "drops every earlier version flag" (it drops every
one), a tint comment that missed the fork ring, a test comment citing a deleted test, the lesson's
vanilla count, and the template's doc-name placeholder.

## Lessons appended

- `lessons/gamemodels-services.md`: validate a derived value, not only its inputs.
- `lessons/state-lifecycle-save.md`: a version marker has one writer that replaces it.
- `lessons/localization-ui.md`: a bound `IsEnabled` needs `UpdateChildrenStates`; recurrence noted on
  the result-message lesson.
- `lessons/testing-qa.md`: recurrence noted for the data-gated destructive path.
- `lessons/build-tooling-workflow.md`: recurrence noted for living docs.

## Owed

In-game checks, listed in the commit body. Translation of the 15 new strings (paid, the maintainer's
word).

## Codex review (2026-10-14)

Codex (`gpt-6-astra`, `max`) reviewed `4f5b412d7..1e6a97c6d`, the three branch commits and the merge,
against the installed v1.5.5 DLLs. It disputed five of six suspects with evidence: the money paths (no
charge without a refund, no free respec, quoted and charged prices agree in single player), display and
click parity (330 level and count combinations against a parsed schedule), the shipped-data invariants
(probed with in-memory XML mutations through the real provider), the merge's party-size order for every
party type, and the v1.5.5 engine glue. It confirmed one finding:

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| X1 | P2 | The tree-version migration ran at session launch for `Hero.MainHero` only, while persistence restores career data for every saved hero and the passive cache walks them all. A co-op hero, or one the player takes over later, is never the main hero at launch, and `PlayerPossessionBehavior.CheckForPossession` returns early for a hero reconciled in an earlier session, so such a hero kept old-tree picks; its fork picks read as locked in, and its player would pay to retrain what the migration owes for free | Stale state / lifecycle (a per-record migration run over one record) | The plan said "per hero", but the brief placed the call beside the existing repair pass, which runs for the main hero only, and copied its scope. The data-flow lens checked the load order and the stamp at every assignment path, not which heroes hold career data when the session starts | `MigrateAllTreeVersions` migrates every hero in `GetAllData()` and returns the player's count (`59c1803d2`); two tests (a co-op hero beside the player, a player without a career); the order pin names the new call; lesson in `state-lifecycle-save.md` |

A second pass (`high`) on `59c1803d2` found no defect and confirmed the fix for every record present
at session launch. It left three observations, none a finding against this change:

- The foreign-choice repair still runs for the main hero only. A non-player record that already holds
  a foreign choice keeps it, and its passive still counts; the repair's documented producer (the
  character-creation ghost root) is a main-hero cause, and no normal path producing such a record was
  found. Older than this change; left as a follow-up.
- `OnCareerSelected` over a hero that already holds choices certifies them with the stamp. Every saved
  record is current once the session starts and tree versions change only between sessions, so no
  reachable stale-tree case was found.
- A co-op implementation that restores career records after session launch would miss the one-shot
  sweep. UNVERIFIED: no TAOM path does it.
