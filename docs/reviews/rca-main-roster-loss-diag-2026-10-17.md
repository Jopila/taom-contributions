# RCA: main-party roster loss diagnostic (deep review, 2026-10-17)

## Top-line

A player reported that about 60 non-hero troops left the main party right after an Elite Emissary
purchase (screenshot only, no log). `/investigate` found no TAOM path that removes a whole roster at
once, so a TEMPORARY, log-only diagnostic was built: a prefix on `TroopRoster.AddToCountsAtIndex`,
a per-hour LARGE LOSS burst, and a watchdog in `TroopCountDiagnosticsBehavior` that compared
`TotalRegulars` between campaign events. The `/deep-review` (7 agents in two waves, plus an
adversarial pass on the entry point) found 1 CRITICAL, 1 HIGH, 5 MEDIUM and several LOW findings.
The watchdog could not see the bypass it was written for, and the burst warning fired on routine
vanilla operations in exactly the shape of the report, so the diagnostic would have pointed the
next reader at the wrong code. Mike chose the stateless redesign: one DEBUG line per main-party
removal with `tick=`, the row, the regular count and a full-namespace caller chain, plus a sibling
prefix on `TroopRoster.SetElementNumber`. The watchdog and the burst logic were deleted.

Final state: full suite `Passed! - Failed: 0, Passed: 17527, Skipped: 5` (after the convergence
pass added a filter-before-cap test and four doc sentences); binding gate
`Passed! - Failed: 0, Passed: 599, Skipped: 0`.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | The watchdog compared `TotalRegulars`, a cached field (`_totalRegulars`) that only `AddToCountsAtIndex` and the load-time recompute write. `SetElementNumber` (v1.5.5 `TroopRoster.cs:494-503`) writes `data[index].Number` and the version and nothing else, so the bypass the patch comment and both docs said the watchdog covered was invisible to it. `PartyBase.MemberRoster` has a private setter set only in the constructor, so "roster replaced" could only ever mean `Campaign.MainParty` changed. | Detector over a cached value | The plan listed the methods that bypass the funnel and assumed a count comparison would see them. Nobody listed the writers of the field the comparison read. | Lesson in `harmony-il.md`: a check over a cached engine field sees only the writers that update the cache; list the field's writers before claiming coverage. Fixed by deleting the watchdog and patching `SetElementNumber` directly. |
| 2 | CRITICAL | The watchdog took `TroopCountDiagnosticsBehavior` from 138 to 190 class lines, past the ADR-002 ceiling, and gave the class a second temporary diagnostic with a different removal condition. Half of the watchdog's state lived in the entry point, untestable. | ADR-002 | The builder was told to put the watchdog in the existing behavior "beside" the old diagnostic; no gate counts class lines outside two GameModel files. | Fixed by deletion (the file has no diff now). A repo-wide ADR-002 size gate is a possible follow-up issue, on Mike's word. |
| 3 | MEDIUM | Vanilla's party screen binds the LIVE main roster; every Esc/Cancel runs `PartyScreenData.ResetUsing` (`PartyScreenData.cs:231-241`), which calls `RightMemberRoster.Clear()` and re-adds every row. `PartyScreenLogic.UpgradeTroop` also removes before it adds, and battles remove one man per death. Each of these crossed the 10-removal burst threshold and raised LARGE LOSS with every regular removed and heroes kept, which is the report's shape. Campaign time is frozen while a screen or mission is open, so the once-per-hour latch could then hide a real loss. | Removal-time judgement on rebuild-in-place | The plan expected "lines only for battle casualties" and never read what the party screen does to the roster it shows. | Lesson in `harmony-il.md`: an observe-only removal log on a live roster must expect rebuild-in-place; judge loss on the net change or not at all. Fixed by removing the judgement; the registry entry now lists the benign sources and says not to filter `ResetUsing` by name. |
| 4 | MEDIUM | `_previousRoster`, a strong `TroopRoster` reference in a process-lifetime singleton, kept the finished campaign's object graph alive until the next campaign's `OnSessionLaunched`. | Lifecycle retention | The `GarrisonCultureSwapAdapter` and #771 precedents exist, but the builder brief asked for a reference compare and nobody asked what the field pins. | Fixed by deletion. Existing rule (`csharp-architecture.md` singleton state) already covers it; no new rule. |
| 5 | MEDIUM | The prefix logged `Math.Min(-countChange, Number)` for an over-removal. The `TroopRosterElement.Number` setter throws `MBUnderFlowException` on a negative value (`TroopRosterElement.cs:33-38`) and `AddToCountsAtIndex` writes through it (`TroopRoster.cs:317`), so the engine aborts and nothing leaves, yet a removal was logged. Two reviewers (engine compatibility and adversarial) claimed the count goes negative instead; the setter read settled it. | Engine contract not read | The clamp was written defensively without reading the setter. | Fixed: the prefix skips a removal larger than the row, with the setter cited in the comment. Reviewer disagreement resolved by reading the code, per `evidence-over-claims.md`. |
| 6 | MEDIUM | Caller frames were formatted with `DeclaringType.Name`, which drops the namespace, so the `System.` filter could never match and the chain could not say whether TaleWorlds, TAOM or another mod made the call. The test passed only because it fed namespaced strings. | Repeat: frame naming | Second occurrence: `SpawnOriginFormatter.cs:17-19` and the #372 changelog entry record the same defect, but no lesson existed, so nothing loaded it. | Lesson in `harmony-il.md`: caller-chain frames use `DeclaringType.FullName`, and tests feed the production shape. Fixed. |
| 7 | MEDIUM | `LogInfo` is durable (synchronous drain and flush on the calling thread, `FileLogger.cs:85-136`); one line plus a full stack walk ran per main-party casualty, possibly on the agent-removal thread. | Hot-path I/O | The brief named LogInfo without checking what it does. | Fixed: the per-removal line is `LogDebug` (queued). |
| 8 | LOW | Additions between two snapshots masked an unseen drop (`prev - cur = S + U - A`); the burst reset at `OnSessionLaunched` ran after load-time removals; no line bracketed a save and reload. | Snapshot design | Consequences of finding 1's design. | Moot after deletion. The registry entry lists save and reload as not covered. |
| 9 | LOW | Docs and comments said `PartyBase.MainParty` "throws without a campaign" (it throws while a campaign has no main party), claimed roster-object replacement, and lacked Threads, PatchShield, Co-op lines, a read guide, the `patch-targets.md` rows and the feature-map clause. | Documentation completeness | The first pass wrote the registry entry from the plan, not from the engine. | Fixed in the rework. |
| 10 | LOW | Tests: the reset test proved only half its name, two service branches were untested, the wiring test used a private walker that goes Inconclusive instead of `RepoPaths.ReadSource`, a second public type sat in the service file, and `CheckSnapshot` returned null from `string`. | Test quality | Builder defaults. | Fixed: tests rewritten around the two pure functions, wiring test uses `RepoPaths.ReadSource(..., stripComments: true)` and checks both category attributes. |
| 11 | MISSING | No GitHub issue exists for the report. | Documentation duty | Creating an issue needs Mike's word. | Owed: ask Mike. |

## Root-cause pattern

Findings 1, 3 and 5 share one cause: the diagnostic was designed from a list of engine METHODS and
an expectation of what players do, not from the engine's STATE. The plan asked "which calls bypass
the funnel" but not "which code writes the field I compare", and asked "when do troops leave" but
not "what does vanilla do to the live roster when nothing leaves". A diagnostic that judges what it
sees inherits every gap in that model, and here the gaps produced false positives in the exact
shape of the bug being hunted, which is worse than no diagnostic. The stateless redesign avoids the
class: it records facts with their caller and leaves the judgement to the person reading the log,
with a read guide that names the benign sources.

## Why each agent's pass was needed

- **Standards (1):** found the ADR-002 overrun no test gates, and the dead `System.` filter.
- **Engine compatibility (2):** verified the bindings and the funnel, found the `SetElementNumber` cache gap and that "roster replaced" means a main-party switch; wrong on over-removal (corrected by reading the setter).
- **Efficiency (3):** found the durable flush per casualty and the campaign pin, and first flagged the party-screen clear.
- **Data flow (5):** traced the cache writers, the Esc clear, the frozen-hour latch and the over-removal phantom.
- **Completeness (4):** found the missing issue, read guide, removal checklist and the half-proving test.
- **Design (6):** proposed the stateless trail with `tick=` grouping that removed findings 1, 2, 3, 4, 7 and 8 together.
- **Adversarial:** confirmed the CRITICAL and argued for deletion over relocation.

The builder (Sonnet) and the plan (this session) did not catch any of them before review: the
plan's own claims (findings 1 and 3) were the source.

## Lessons appended

`docs/reviews/lessons/harmony-il.md`: three entries dated 2026-10-17 (cached-field writers, rebuild
in place on a live roster, full-name frames).
