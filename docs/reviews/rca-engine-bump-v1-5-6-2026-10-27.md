# RCA: Bannerlord v1.5.6 engine bump deep review (2026-10-27)

## Top line

The tier 1 `/deep-review` of the v1.5.6 bump (#1036), the Hidden Hosts field-parley veto (#1037) and, after
it, the turbo-click fix (#1040) ran a Codex pre-review at `ultra` and four Claude lenses (5 Data flow,
2 Engine compatibility, 7 XML, Tooling) in one wave. Nothing reached CRITICAL or HIGH, so no second wave ran.
The review confirmed four MEDIUM and a run of LOW findings, every one re-read in the source before it was fixed.
A fix-loop re-review (Codex xhigh and lenses 5 and 2) found no CRITICAL, HIGH or MEDIUM defect it was decided to
fix: it added four LOW rows (10 to 13), one of them a MEDIUM-rated limitation (the one-frame latch lag) left unfixed.
Two findings changed what Mike decided: the parley veto's refusal text, and the scope of the Ctrl click conflict,
which turned a release-note line into the #1040 fix in this release.

The engine side came through clean: 15 API usages verified on the installed DLLs, 0 incompatible; every native
pin reproduced from the binaries; all 18 transpilers and all 12 `TargetMethods()` classes still bind.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|------------|-------------------|
| 1 | MEDIUM | `HiddenHostsService.MasksParley` delegated to `IsNameHidden`, which has no visibility gate, while the tooltip title masks only a party drawn on the map. A tracked enemy lord out of sight but inside seeing range showed its name in the tracker tooltip and lost vanilla's parley hint. Found by Codex (F1) and lens 5 independently. | Parallel predicates drifted | The build brief said "the same verdict the nameplate uses", and `IsNameHidden`'s doc comment claimed the tooltip title followed that one rule; it does not (`HiddenHostsService.Tooltips.cs`, the `s.IsVisible` gate). The only parity test compared `MasksParley` with its own body, so it could not fail. | Fixed: `s.IsVisible && IsNameHidden(s)`, red test `MasksParley_HostOutOfSight_IsFalse`, and parity tests against each consumer's real verdict (`Evaluate(s).HidesName` from a fresh service, `EvaluateTooltipParty`). The false comment is corrected. Lesson in `lessons/testing-qa.md`. |
| 2 | MEDIUM | The impact record and the question put to Mike said a Ctrl click on an enemy lord now parleys. In fact, with `MapParleyModifier` held, a click on any party or settlement goes to `HandleParleyClick` and issues no move order, so a Ctrl + Space turbo click on a friendly town stopped moving the party. Found by lens 2. | Engine claim relayed unread | I took the scope from a reviewer's summary and wrote it into a decision question without reading `MapScreen.HandleMouse`. `evidence-over-claims.md` A4 already says to spot-check a subagent's load-bearing claim before relaying it; I did not. A repeat of an existing rule. | Fixed: Mike was asked again with the corrected scope and chose a fix in this release (#1040, Patch124 on `MapScreen.IsParleyClickReleased`). The record now states the full scope. Lesson in `lessons/adapters-taleworlds-api.md`. |
| 3 | MEDIUM | The `/engine-bump` caveat on `check_engine_member_refs.ps1` named overrides but not a moved enum or const value, which the compiler bakes into the caller. `BattleEnvironment` lost `None`, so a v1.5.5 build's `Land` reads as `Naval`, and neither the checker nor a compile reports it. Found by the Tooling lens. | Gate scope understated | The caveat was written from the one blind spot the bump hit first. | Fixed in the skill. Tool change is tracked: FOLLOW-UP #1038 item 7 (record enum literal values in the engine index). |
| 4 | MEDIUM | `/engine-bump` Phase 3 demands `unparsed=0`, but two rows fail every run (`CoopRecruitAttributionPatch` UNPARSED, `MissionScreen.IMissionSystemHandler.UpdateCamera` UNRESOLVED). A gate that always fails teaches people to ignore its exit code. Found by the Tooling lens. | Gate that cannot pass | The skill edit covered three of the four #1038 gaps and skipped this one. | Fixed: the skill names the two rows and says any other one is a failure. FOLLOW-UP #1038 item 4 (teach the tool both forms). |
| 5 | MEDIUM | A new in-game check expected a tracked host out of sight to keep its parley hint, but vanilla refuses any party past the main party's seeing range. Found by the fix-stage skeptic. | Check written from the rule, not the range | The check followed finding 1's fix without the vanilla range gate. | Fixed: the check names a host hidden by forest but still inside seeing range. |
| 6 | LOW | The veto's refusal text told a masked at-war lord host apart from every other masked party. Found by lenses 5 and 2. | Masking text differs from the unmasked-equivalent case | The #1037 design replaced the text without comparing it with what vanilla prints for the other masked parties. | Fixed per Mike: the explanation is null, so every masked party prints vanilla's "Cannot parley with this party."; the TAOM key and its 12 rows are gone. One-off. |
| 7 | LOW | The record said a later TaleWorlds definition of TAOM's two compatibility string ids "cannot clash", because the first-loaded variation wins. The GameText XML is merged first, matched by `@id`, and the later module's attributes overwrite the earlier ones, so TAOM's row would replace vanilla's text and translation key. Found by Codex (F2); lens 7, lens 2 and an earlier skeptic said the opposite. | Precedence read after the merge | The three Claude readers stopped at `GameText.DefaultText` and `AddVariationWithId`, which run on the already merged document; `LoadGameTexts` builds that document with `MBObjectManager.GetMergedXmlForManaged` and `MergeElementAttributes`. | Fixed in the record, the XML comment above the rows, and an `/engine-bump` Phase 4 line (delete the rows when vanilla defines the ids). Lesson in `lessons/xslt-moduledata.md`. |
| 8 | LOW | Five of the six new `IsTurboActive` tests passed against a getter that always returns false, and the latch's red run was never shown. The `HandleMouse`-only caller scan skipped generic methods without a signal. Found by the turbo skeptic. | Test cannot fail | The close tests never asserted that the latch was open first. | Fixed: each close test asserts the open state, the deferral test opens the latch first, both shown red against `=> false`; the scan covers `MapScreen` and its nested types and fails on a skipped candidate. Lesson in `lessons/testing-qa.md`. |
| 9 | LOW | Wording and stale facts in the new docs and comments: four SkeletonBuffer patterns match once and two foreign shapes match zero (not "six once"); the base `PartyNameplateWidget.IsPositionOutsideScreen` rule changed; the parley hint row is not Ctrl-gated; the `OnSubModuleLoad` abort half is [Likely]; the `audit_mbproj_registration.py` comment said the native side was re-read; "hours" was 44 minutes; the archive-copy condition; the prefab header's edit list; the census scope; a leftover em dash; a stale test count. Found across lenses 2 and 7, Tooling, Codex (O2) and the two skeptics. | Wording | Text written from the plan or an earlier pass, not from the final tree. | Fixed. One-offs; `evidence-over-claims.md` C already covers them. |
| 10 | LOW | Patch124 reads the previous frame's turbo latch. `MapScreen.HandleMouse` runs inside the game tick before the submodules' `OnApplicationTick`, where TAOM updates the latch, so a click released on the exact frame Space is pressed still parleys. Real, one frame per transition. Found by Codex xhigh (F1), who rated it MEDIUM; lenses 5 and 2 judged it not worth a change. Lens 5 first claimed `HandleMouse` runs from a separate engine callback; the decompile refutes it (`Module.OnApplicationTick` to `GameManagerBase.Current.OnTick` to `MapState.OnMapModeTick` to `IMapStateHandler.BeforeTick` to `HandleMouse`). | Tick order between a postfix and the state it reads | The patch was designed around the predicate, and the tick order of the latch update was not compared with the click handler's. | Not fixed under the simplicity criterion: the fix would duplicate `OnTick`'s gates in a second predicate for a one-frame window. Recorded as a known limitation in `time-acceleration.md` and the registry. |
| 11 | LOW | Patch124 applied at GameInit left the installed Coop's early `HandleMouse` patches (`MapSiegeHoverCursorPatch`, `DisableGameMenuPausePatches`) as a path that could mark or inline the method before TAOM's mark. Found by lens 2. | Apply phase chosen for the first map frame, not for the first foreign patch | The timing note considered only when the JIT first compiles `HandleMouse`. | Fixed: the category is declared at ProcessLoad, with the phase pinned by `TurboClickBindingTests` (red, then green). |
| 12 | LOW | The in-game check said "Ctrl + Space", but `MapParleyModifier` is LeftControl plus ControllerRUp only and TAOM's latch accepts either Ctrl, so a Right Ctrl run passes even if Patch124 never runs. Found by lens 2. | Check cannot fail | The check was written from the turbo gesture, not from the parley binding. | Fixed: the check names Left Ctrl and adds a control step (Right Ctrl + Space moves even without the patch). |
| 13 | LOW | Wording: "every turbo click tried to parley" was imprecise, and the parley icon fix was costed as two nameplate view postfixes when one postfix on `CanMainHeroDoParleyWithParty` would do (and would also drop the tooltip hint). Found by lens 2. | Wording | Text written from the plan, not from the click handler. | Fixed in the docs and the patch's XML doc. |

Pre-existing gaps the review found in code this change did not modify (tracked, not fixed here):

- FOLLOW-UP #1038 item 10: `PrefabCloneWidgetReferenceTests` checks that a widget-reference attribute exists but never resolves its path (lens 7).
- FOLLOW-UP #1038 item 11: `check_handbook_attributes.py` exits 0 on a missing `--docs`, and its test file's `unittest.main()` hides 9 tests from a direct run (Tooling).
- FOLLOW-UP #1038 item 9: the snapshot keeps one `TargetMethods()` target and prints `out` as `ref` (Tooling, lens 2).
- FOLLOW-UP #1038 item 12: `taom-src.ps1` resolves `SandBox.*` types to the stale `Modules\Coop` copies (lens 2).

## Root-cause pattern: the reader stopped one step short of the consumer

Findings 1, 2, 5 and 7 share one shape. Each claim was true of the thing read and false of what the player or
the engine actually consumes: `IsNameHidden` instead of the tooltip's own verdict; a reviewer's summary instead of
`HandleMouse`; the masking rule instead of the vanilla range gate in front of it; the post-merge `GameText`
object instead of the XML merge that decides precedence. Finding 8 is the test-side form of the same thing: a test
that reads the method under test instead of the state that should change.

The rule: **prove a claim at the consumer that acts on it.** For a predicate, compare it with each consumer's real
decision. For engine behaviour, read the call path to the effect before writing it down or asking Mike to decide on it.

## Why each agent missed what it missed

- **Lens 2 (Engine compatibility)** verified every signature and found the Ctrl scope, but repeated the
  first-variation claim (finding 7): its question was the post-merge lookup, not the merge.
- **Lens 5 (Data flow)** found finding 1 and the refusal text, but did not trace the click route, so it missed
  finding 2; its brief listed the parley callers, not the click handler.
- **Lens 7 (XML)** checked every widget path and string row, but read GameText precedence from `GameText.cs` only
  (finding 7).
- **Tooling** found findings 3 and 4; findings 1, 2 and 7 were outside its files.
- **Codex** found findings 1 and 7 and confirmed the rest of the change; it did not reach the click route
  (finding 2), which no prompt suspect named.

## Feedback memories to codify

None. The lessons above go to the category files; the one existing rule broken (finding 2,
`evidence-over-claims.md` A4) needs no new memory, only following.
