# RCA: Hall of Fire (#826, deep review 2026-10-20)

## Top-line

Root cause, confirmed from the installed v1.5.5 code and the Native data (Mike accepted the static
proof): `DefaultMapConversationDataProvider` names a settlement conversation's backdrop
`conv_<settlement culture>_<location>_0`, and Native ships `conv_*` atmospheres only for the six
vanilla culture ids and the wilderness sets. For a TAOM culture the name does not exist, the native
`SetAtmosphereWithName` lookup finds nothing and keeps the scene's previous atmosphere, and the six
hidden lord's-hall interiors in `scn_conversation_tableau` (tagged with vanilla names) never show.
The fix wraps vanilla's provider once at the main menu through the public setter (no Harmony) and
swaps the culture part of the name for a vanilla set from a JSON map.

The deep review (six lenses in two waves, then one convergence pass) found no runtime defect and no
data gap: every name the swap produces exists, and every culture a settlement can hold, statically
or after a culture conversion, has a row or keeps a vanilla id. It found one MEDIUM finding and
fourteen LOW ones, all in tests, docs or shape, and the convergence pass found four more LOW slips in the fix round
(H16). All were handled in the same session.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| H1 | MEDIUM | Issue #826's body still read as the plan: every Files Changed row "(planned)", four changed files missing, an off switch that does not work ("emptying the JSON" loads the built-in rows), a third-party settings library named | Docs drift | The body was rewritten before the build and counted as done, though the lesson written that morning (Crown Guard) already said to rewrite it before the commit; fifth time in batch B01 | Body rewritten from what ships. A lesson alone did not hold the same day, so the check is now a step in `docs/ai-includes/completion-workflow.md` ("Before the commit") |
| H2 | LOW | The doc and two summaries said the provider runs per tick; it runs once per conversation from `FirstTimeInit`, which `OnTick` retries until it completes | Engine claim from the plan | Written from the plan's "inside OnTick" note, not from the tableau code | Corrected with the real never-throw reason (a throw would repeat every tick). The "callers from the IL" lesson now covers any engine-hook doc, not only patch docs |
| H3 | LOW | The doc said the wrapper's catch cannot be unit-tested; `MapConversationTableauData.CreateFrom` is public and an uninitialized `Settlement` takes a culture | Untested line | The builder took the private constructor as the end of the road | Wrapper tests: a Gondor settlement swaps, and a throwing config returns vanilla's tag and logs once over two calls |
| H4 | LOW | The doc called the first backdrop after a load "empty" and said another realm's town shows; the scene keeps its previous atmosphere, and the first one is the scene's own default | Unverified symptom | Written from the plan's guess | Reworded; the in-game check records what is seen |
| H5 | LOW | The culture coverage gate was LiveInstall only, so no CI run checked that a new kingdom culture has a row | Gate scope | The builder copied the live-settlements read from the brief; the conversion-target set was not named | CI test over `IVolunteerRecruitmentService.GetPooledCultureIds()` (the culture-conversion targets); the authoring docs name it |
| H6 | LOW | A shipped-config test read the provider's validated output, so its value check could never fail | Vacuous test | Same class as `testing-qa.md` "read the file, not the provider's output" | Deleted; the parse-without-warning and equals-default tests already fail on a bad row |
| H7 | LOW | A wiring assertion checked that a resolved substitute implements its own interface | Vacuous test | Copied shape | Deleted |
| H8 | LOW | The documented off switch (`"cultures": {}`) had no test | Untested claim | Written into the doc without a test | Test added |
| H9 | LOW | The dedicated-server installer test asserted less than its name | Weak test | Asserted the visible effect only | Also asserts the slot is not read |
| H10 | LOW | The service took an `isHideout` flag that repeated a decision vanilla already makes (a hideout gets a terrain tag, which the prefix rule never matches) | Over-structure | Copied from the plan's guard list | Parameter dropped; a test pins all five terrain words |
| H11 | LOW | A hand-written fake slot beside the substitute the file already used; three copies of the six vanilla ids in one test file; an `IList` cast | Shape | Builder habit | Substitute only; one list (`HallOfFireMap.AllowedSets`); plain `Contains` |
| H12 | LOW | The new test reference's `Private=False` had no comment, though `Private=True` breaks unrelated tests with duplicate types | Undocumented build choice | The reason lived only in the builder's report | One-line comment in the csproj |
| H13 | LOW | Two engine facts the feature depends on (the tableau reads the slot, `FirstTimeInit` passes the name to `SetAtmosphereWithName`) were pinned by nothing, so an engine update could stop the feature silently | Missing binding gate | No Harmony, so no patch-binding gate covers the seam | IL binding test; a LiveInstall test that each vanilla set ships its five backdrops |
| H14 | LOW | The culture promotion tool's printed checklist had no Hall of Fire step | Gate not routed | Sibling parity with Voice Accents not checked | Step added |
| H15 | LOW | Hand count ("45 atmospheres"), and no in-game control town | Docs | Repeat of the follow-ups F10 and Crown Guard F10 | Count dropped; a Rohan or Dale control and Khand added to the in-game check |
| H16 | LOW | Convergence pass: a test the `isHideout` removal had made a duplicate; two lesson edits without Source lines and with a Prevent line still scoped to patches; the issue body's Testing put game-only tests under "Unit"; the new workflow step named only the bug template's sections | Fix-round slips | The fix round was checked by its own tests, not re-read as a whole | Duplicate deleted; Source lines and the wider Prevent line added; issue body corrected; the step names both templates' sections |

## Root-cause pattern: the docs were written from the plan, again

H1, H2 and H4 repeat the pattern of the Crown Guard review the same day: statements copied from the
plan instead of read from the engine and the shipped diff. One lesson from that review was scoped too
narrowly to fire here (it named a "patch doc"; this feature has no patch), and is widened. The other
already said to rewrite the issue body before the commit and was not followed, so it is now a step
in the completion workflow rather than only a lesson.

## Why each agent missed these

- **The builder:** briefed with the plan, it wrote the doc from the brief, kept the brief's guard
  list (H10) and accepted the private constructor as untestable (H3).
- **The orchestrator:** rewrote the issue body before the build and did not plan a second rewrite
  before the commit (H1).
- **The lenses:** Standards found H7, H11, H12; Engine H2, H3, H4; Data flow H2, H3, H5, H7; Design
  H6, H10, H11 and the hall-geometry reason for the approach; Completeness H1, H6, H8, H9, H13 to H15;
  Efficiency found no cost on any path.

## Lessons recorded

- `lessons/build-tooling-workflow.md`: the issue-body lesson records the repeat and points at the new step.
- `lessons/harmony-il.md`: the caller-and-trigger lesson covers every engine-hook doc, not only patch docs.
- `docs/ai-includes/completion-workflow.md`: "Before the commit", the issue body checked against the diff.
