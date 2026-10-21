# RCA: co-op gate doc test (#921) and harvest blank line (#922), deep review (2026-10-19)

**Summary.** Two follow-ups from the Watchful Holds RCA: `CoopGateDocCoverageTests` fails when a feature module's
campaign behavior has no row in `coop-interop.md` (#921), and `harvest_literal_loc_keys.py` should stop doubling the
blank line before `</strings>` (#922). Seven `/deep-review` lenses read the uncommitted tree. None found anything above
LOW; every finding below was re-read against the code and fixed before the commit. While this work was in review,
another session fixed the same harvest bug under a third issue, #919 (`337228885`, the same one-line change with its
own tests), so this branch's harvest commit was dropped at the rebase and #922 closed with #781.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | One bug was filed three times: #781 (open since earlier), then #919 and #922 on the same days by two sessions, and #702 already named it as its second defect. Two sessions also wrote the same fix in parallel. | Process: duplicate issue | Each issue was written from a review finding without searching the open issues for the tool's name. | Lesson below; the `/issue` skill now searches open issues for the same file or symptom before it creates one. |
| 2 | LOW | The test counted any backticked mention in the section as a classification, so a behavior named in passing (one siege-table row already names `CareerQuestCampaignBehavior` in its prose) would pass without a row of its own. | Test proves less than it claims | The first matcher was the simplest string check. | The test matches a name only where a table row or list entry starts. |
| 3 | LOW | Hard Questions' two behaviors were baselined, although the feature is new and both already gate on the co-op provider in code and in `hard-questions.md`. #921's own rule says a new behavior is never baselined. | Baseline used for new work | The baseline was built from a test run, not from each behavior's age. | Two rows added to `coop-interop.md`; the baseline holds ten pre-existing behaviors. |
| 4 | LOW | The doc sentence and the template bullet said the test fails for any behavior with no row, but the baselined behaviors and the behaviors added by hand in `SubModule.cs` are not checked. | Doc overstates a gate | Written from the intent. | Both lines now name the two exceptions. |
| 5 | LOW | The ratchet test was named `Baseline_OnlyShrinks` but checks only for stale entries. | Naming | The house name was not looked up. | Renamed `Baseline_HasNoStaleEntries`, as in the two existing baseline tests. |
| 6 | LOW | Issue #921 first listed 10 baselined behaviors; the test found 12, because the regex used to enumerate them missed two factory shapes. | Evidence | A grep stood in for running the code. | The list in #921 was corrected from the test's own output. |

## Root-cause pattern

Findings 1 and 7 are the same mistake: a fact was taken from a quick search (no issue search at all; a regex for
the declarations) instead of from the authoritative source (`gh issue list --search`; the test enumerating
`FeatureModules.All`).

## Lessons

- `docs/reviews/lessons/build-tooling-workflow.md`: search the open issues before filing one.
