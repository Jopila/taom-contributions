# RCA: Bond of Hire (deep review, 2026-10-24)

## Top-line

The `/deep-review` of Bond of Hire (#867, a minimum mercenary contract; #868, the contract line in the kingdom Clans
panel) ran on the uncommitted worktree `E:\repos\taom-bond-of-hire`. Seven lenses reported one HIGH, which was a
decision the build did not apply, and a group of LOW findings. No engine API was wrong: Lens 2 verified 30 signatures
and firing claims against the installed v1.5.5 DLLs. Every finding was re-checked against the code and fixed in the
working tree; the full suite then passed (25283 passed, 0 failed, 7 skipped) and the binding gate passed (741 passed).

Repeat check: `docs/reviews/rca-*.md` and `docs/reviews/lessons/` were searched for `IsDedicatedServer`, dedicated
server, `CoverChildren`, wrap, `MaxWidth`, `PropertyChangedWithValue` and `IgnoreComments`. What repeats:

- **Finding 1 is a related repeat.** `campaign-mechanics.md` ("On a headless/dedicated host `Hero.MainHero` EXISTS but
  is nobody's character") scopes the dedicated-server gate to code that credits or charges the local player. The build
  copied the gate into a feature that only writes AI clans, where the server is the one peer that should act. The old
  entry says when to add the gate and not when to leave it out. New lesson filed.
- **Finding 2 has no earlier lesson.** The #866 entry in `localization-ui.md` covers a stretched child of a
  CoverChildren panel; nothing said that a CoverChildren TextWidget never wraps. New lesson filed.
- **The LOW group** is comment, doc and test drift covered by existing general rules; no entry added for it.

## Findings and root causes

| # | Sev | Finding | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | HIGH | The service skipped the dedicated server, so a server-hosted campaign never bound any clan and its `BondOfHireMinimumDays` key had no effect. The maintainer had decided the server binds (Lenses 2, 4, 5) | Co-op; a gate copied from a feature with a different subject | The plan listed "co-op peer and dedicated server skipped" as one test line, copied from features that credit `Hero.MainHero`. The decision to bind on the server was made after the plan and never reached the code, its test or the five doc sites | The gate is `ShouldDeferToHost` only, and the test now asserts the server binds. Lesson in `campaign-mechanics.md`: ask of each feature whether its subject is the local player before adding the dedicated-server gate |
| 2 | LOW | The panel line used `CoverChildren` width, which Gauntlet measures as one unbounded line, so a longer translation would run under the Fiefs column (Lenses 2, 7) | UI layout | The #866 lesson (no stretch inside CoverChildren) was applied, and the opposite case was not asked | Fixed width 560 with CoverChildren height, as vanilla's wrapping texts do. Lesson in `localization-ui.md` |
| 3 | LOW | Comments and the MCM hint said the date guards two departures; the engine reads it for three (debt, hired away, the daily leave consideration). One comment also said the engine never writes the date for a new mercenary; `ChangeKingdomAction.ApplyInternal` writes the caller's value (Lenses 2, 5) | Docs claim not read from the source | The doc was written from the full reader list and the comments from an earlier note | Every site names three; the hint test pins the third |
| 4 | LOW | The mixin comment said its own property sets raise `PropertyChangedWithValue`. UIExtenderEx's `SetField` raises plain `PropertyChanged`, and one test changed an int property that never reaches the handler (Lenses 2, 3, 6) | Engine claim; a test that proves nothing | The claim was reasoned, not read | Comment corrected; the test now changes a string property |
| 5 | LOW | Co-op limits were not documented: the installed co-op module disables two of the three readers on every peer, and the date is not synced to clients (Lens 2) | Docs | The co-op module's own patches were not read | Known limits and the co-op row say so |
| 6 | LOW | Test and record quality: a constructor try/catch and a second reference for a view model UIExtenderEx always supplies; source-text pins where a behavioural test can run; no test that the line recovers after a fault; no pin that `KingdomClanVM` declares the two hooked methods; the prefab test counted comments; a stale `#867` and `#868` body; no `#763` row; the checklist expected exactly 30 days (Lenses 1, 4, 5, 6) | Test shape and record drift | Builder self-check | Deleted the guard and the field; behavioural tests replace the pins; three new tests; the issue bodies, the `#763` row and the checklist are corrected |

## Root-cause pattern

Findings 1 and 2 both copied a rule from a sibling feature without its condition. The dedicated-server gate is right
for a feature that pays or charges the player and wrong for one that acts on AI clans. The no-stretch rule is right for a
child of a CoverChildren panel and says nothing about a text that must wrap. In both cases the copied rule was correct
where it came from, and the question that limits it (who is the subject; does this text need to wrap) was not asked.

## Why each agent missed these

- **Builder:** applied the plan's test list, which predated the decision in finding 1.
- **Lenses:** every lens that could see finding 1 reported it. Finding 2 needed the engine's `TextLayout` measure rule,
  which only Lenses 2 and 7 read.
- **Orchestrator:** recorded the decision for finding 1 in the plan's notes and did not add it to the builder's brief.

## Feedback memories to codify

None beyond the two lessons. The orchestrator habit (put a late decision in the builder's brief, not only in notes) is
covered by `evidence-over-claims.md` section A.

## Convergence note

The convergence pass found one defect the fix pass introduced: a new behavioural adapter test read `Campaign.Current`,
whose reference-stub body throws, without the `RequiresGame` tag, so the no-game unit run would have failed. It is
tagged now. The same pass tightened three tests and comments: the dedicated-server test also pins the service's
constructor, so a server check cannot come back unseen; a prefab test name now says it checks the height axis only; a
settings comment gives the right reason for the co-op fingerprint.
