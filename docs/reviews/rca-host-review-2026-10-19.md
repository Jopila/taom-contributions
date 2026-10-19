# RCA: Host Review (#891), first deep review (2026-10-19)

**Summary.** Host Review adds a "Review the host" option to the Field Camp sub-menu. It opens a no-enemy mission
(`TaomHostReview`) with the healthy main party, nobody can be hurt, and the leave key exits. The change was reviewed
uncommitted in the worktree `E:\repos\taom-battle-ai-missions` (branch `feat/battle-ai-missions`, base `89374fc4f`)
before any in-game run. Seven lenses ran in two waves: standards, engine compatibility, data flow and XML first, then
efficiency, completeness and design.

- **Severity:** 0 CRITICAL, 0 HIGH, 5 MEDIUM, the rest LOW, NIT or INFO. No lens found an engine incompatibility: the
  engine lens checked about 84 members against the installed v1.5.5 assemblies.
- **Shape of the findings:** most rows are code written for a situation that cannot occur (a reinforcement switch-off,
  a general for the empty side, a redundant map-event read), or a vanilla list copied with an undocumented difference
  (the ironman flag, the face cache view, an incomplete list of dropped views). The two player-facing defects are text:
  "Hold Tab" ignores gamepads and rebinding, and two tooltips read badly.
- **Outcome:** every confirmed finding is fixed in the working tree except row 23 (owed paid translation).
  Three items stay UNVERIFIED until the in-game spike (U1 to U3).

Feature doc: [host-review.md](../features/host-review.md).

## Findings

Severity is the finder's. "Lens" names the finder. Each row was re-checked against the source or the v1.5.5 cache
before it was entered here.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The start message said "Hold Tab to leave." Leaving is game key 4, which is Tab only on a keyboard with default bindings (`GenericGameKeyContext.cs:54`, gamepad `ControllerRRight`). Vanilla binds it: `{leave_key}` in `Native/ModuleData/module_strings.xml:2265`. Lens: XML. | Player text names a rebindable key | The builder wrote the default key from the engine's `Mission.OnTick` branch and no rule says to bind key names. | Text uses `{LEAVE_KEY}` from `HyperlinkTexts.GetKeyHyperlinkText(HotKeyManager.GetHotKeyId("Generic", 4))`, shown with `MBInformationManager.AddQuickInformation` as vanilla does. Lesson in `lessons/localization-ui.md`. |
| 2 | MEDIUM | The hook classes had no tests: menu controller, launcher, mission logic, views and order provider. `.claude/rules/tests.md` asks 80% for hooks. Lens: completeness. | Test coverage | The build brief listed service and supplier tests only, and the builder wrote "needs the game" for the rest. TAOM tests menus and mission wiring with source pins (`RefugeWiringTests.cs:102-143`). | Source pins for the menu slot, the `DisableDying` callback, the launcher's logic entry and one tooltip per block, plus a RequiresGame factory test. Lesson in `lessons/testing-qa.md`. |
| 3 | MEDIUM | Issue #891 still described the pre-approval design: an MCM toggle, IoC and SubModule lines, a deployment subclass and a bypass flag with its reset. Lens: completeness. | Process: issue drift | The plan approval changed the design, but the issue was filed earlier from the batch spec and nobody owned updating it. | A progress comment on #891 records the built design. Lesson in `lessons/build-tooling-workflow.md`. |
| 4 | MEDIUM | The owed in-game list folded the spike into "the scene opens" and missed the return to the wait menu, TAOM's own synthetic blows and the career-resource question the plan promised to answer. Lens: completeness. | Completeness of owed checks | The doc writer was briefed with the plan's verification list only. | The doc now opens with a named spike gate and lists the missing checks; the career question is answered from code. |
| 5 | MEDIUM | The queued rewording of three keys would have left 36 seeded rows on the old English, which the paid run treats as translated and skips forever (`translate_with_claude.py:352-354`). Lens: completeness. | Localization: reworded seeded row | A known trap (orientation.md "Loc keys"; `lessons/localization-ui.md` entries at about lines 389 and 472). It was caught before the reword landed. | The reword changed the C# literal, the source row and all 12 seeded rows in one edit. Repeat pattern: no new rule, the existing trap line applies. |
| 6 | LOW | A click on an option that became blocked between menu refreshes did nothing and showed nothing; `Launch()` returned `Blocked` and only logged. Lens: data flow. | Silent failure path | The consequence handled only `Failed`; the engine does not re-run the condition before the consequence (`MenuContext.cs:154-160`). | `Launch()` now returns the block reason, and the consequence shows it in red. |
| 7 | LOW | "Review the host" landed before Leave only because the feature module's `OnSessionLaunched` listener ran first; `MbEvent` listeners run newest first. A later move of Refuge into the module list would put it after Leave. Lens: data flow. | Order-dependent menu insertion | The architect walked both orders and found the result right; nobody asked why it was right. | The option registers from `OnAfterSessionLaunchedEvent`, which runs after every session listener (`Campaign.cs:754-755`). Lesson in `lessons/campaign-mechanics.md`. |
| 8 | LOW | A reinforcement switch-off ran on the tick after deployment. It could never act: the lineup is capped at the spawn logic's own battle size and the empty attacker side gives the defender all of it (`DefaultBattleMissionAgentSpawnLogic.cs:425-427`). Lenses: data flow, engine. | Defensive code for an effect that cannot occur | The plan said "no reinforcements"; the builder found that vanilla re-enables them and added a latch without asking whether anything was left to reinforce. | Deleted. Lesson in `lessons/adapters-taleworlds-api.md`. |
| 9 | LOW | `enemy.SetGeneral(characters[0])` named the player's hero general of the empty side. Nothing reads it: `IsUnderPlayersCommand` returns before it for the other side. Lens: engine. | Dead code | The builder guarded a null read it did not trace. | Deleted. Same lesson as row 8. |
| 10 | LOW | `HasEncounterOrMapEvent` read `MapEvent.PlayerMapEvent`, which is `MobileParty.MainParty?.MapEvent` (`MapEvent.cs:102`), the clause before it; the whole property repeated the shared `IEncounterAdapter`. Lenses: standards, data flow. | Reuse | The builder did not search `Main/Adapters` before writing engine reads. | The service uses `IEncounterAdapter.HasCurrent` and `IsMainPartyInMapEvent`. |
| 11 | LOW | The escape menu was built with `false`; vanilla passes `CampaignOptions.IsIronmanMode` (`SandBoxMissionViews.cs:396`), so an ironman player could quit to the main menu from a review without saving. Lens: engine. | Vanilla list copied with a changed argument | The view list was copied from vanilla with deliberate omissions, and the argument change was not compared. | Fixed. Lesson in `lessons/adapters-taleworlds-api.md` (diff a copied list against vanilla, entry by entry). |
| 12 | LOW | `MissionFaceCacheView` was dropped from the view list. Vanilla Battle and Custom Battle both include it; it caps unique face generation, and a review puts a full battle size on one side. The cost is unmeasured. Lens: engine. | Vanilla list copied with an undocumented omission | As row 11. | Added. |
| 13 | LOW | The views class comment and the feature doc listed five dropped views; the list dropped nine (eight after row 12), and `MissionPreloadView` must stay out because it reads `MapEvent.PlayerMapEvent` unguarded. Lens: engine. | Doc claim incomplete | As row 11. | The comment and doc now name every omitted view and the reason for `MissionPreloadView`. |
| 14 | LOW | The doc and a launcher comment credited `isInvincible` with "no experience". `PartyAgentOrigin.OnScoreHit` is empty either way; no XP is awarded because `BattleAgentLogic` is not in the list. Lens: engine. | Doc claim wrong | The builder inferred the reason from the origin's name. | Corrected in both places. |
| 15 | LOW | `ViewCreatorManager` reads `GetCustomAttributesSafe(typeof(ViewMethod))[0]` for every static method of a `[ViewCreatorModule]` type, unguarded, inside its static constructor (`ViewCreatorManager.cs:63-66`). A helper method added to the views class would break every mission view in the game. Lens: engine. | Engine trap, first use in TAOM | TAOM had no `[ViewCreatorModule]` before this change. | A generic gate test over the TAOM assembly, and one line in `.claude/rules/csharp-patterns.md` naming it. Lesson in `lessons/adapters-taleworlds-api.md`. |
| 16 | LOW | Two tooltips read badly: the co-op line used "host", which TAOM's co-op text and translations use for the session host, and the enlisted line ended "another lord's." with no noun. Lens: XML. | Player text | Written without reading the sibling lines (`taom_fcamp_reason_enlisted`). | Reworded to match the siblings. |
| 17 | LOW | The service clamped healthy counts that the adapter already clamps, and no test covered an empty roster (the adapter's read-failure result). Lens: completeness. | Test coverage, redundancy | As row 2. | Clamp deleted; empty-roster and absent-hero tests added. |
| 18 | LOW | `IHostReviewMissionLauncher` was declared in `IHostReviewPartyAdapter.cs`, and the adapter interface and implementation sat in different folders, unlike the sibling features. Lens: standards. | File layout | The builder followed the brief's file list, which had the split. | Own file for the launcher interface; the adapter pair moved to `Main/Adapters`. |
| 19 | LOW | `HostReviewMissionLogic` overrode `OnBehaviorInitialize` only to set `ForceNoFriendlyFire`, which added an entry to the lifecycle ratchet's allowlist. The flag is read only when a hit lands, so `AfterStart` is early enough. Lens: standards. | Avoidable exception to a gate | The brief named `OnBehaviorInitialize`. | The flag moved to `AfterStart`; the override and the allowlist entry are gone. |
| 20 | LOW | The `IsActiveIn` comment said other TAOM code gates on it; only the order provider calls it. Lens: design. | Comment claim wrong | Written ahead of any caller. | Corrected. |
| 21 | INFO | Two `null!` returns in the troop supplier, the only ones in `Main`; null is the real contract. Lens: standards. | Nullability | Style. | Declared nullable where it compiles. |
| 22 | INFO | Four test names missed the `MethodName_StateUnderTest_ExpectedBehavior` pattern. Lenses: standards, completeness. | Naming | Style. | Renamed. |
| 23 | LOW | Paid translation of the 8 keys is owed. Lens: XML. | Owed step | Paid runs need Mike's word. | Not applied: the seed rows hold English until Mike approves the run. |

## Root-cause pattern

Rows 8, 9, 11, 12 and 13 share one cause: the mission was assembled from vanilla parts by reading vanilla code for what
to copy, but not for what each part does in THIS mission's configuration (no map event, an empty side, a capped
lineup). Each addition was locally reasonable; none was checked against the configuration it runs in. Rows 1, 2 and 4
share a second cause: the build and doc briefs were written from the plan, and the plan did not carry the house rules
for player text, hook tests or owed checks. The briefs now name those rules.

## Why each lens caught or missed what it did

- **Standards** found the layout, reuse and lifecycle rows (10, 18, 19, 21, 22). It does not judge engine behaviour.
- **Engine compatibility** found the vanilla-copy rows (8, 9, 11 to 15) by diffing the lists against the installed
  assemblies. It answered the nine spike questions from code; three stay UNVERIFIED (U1 to U3).
- **Data flow** found the silent click (6), the order-dependent slot (7), the moot switch-off (8) and the
  redundant map-event read (10). Rows 6 and 7 are ones no static lens could see without
  simulating the listener order.
- **XML** found the text rows (1, 16, 23). Its gates passed; the defects were wording, not data.
- **Completeness** found the coverage, issue and owed-check rows (2 to 5, 17, 22), and the follow-through that made the
  wording fixes safe (row 5).
- **Efficiency** found no defect. It confirmed that the team AI is best left as it is (two ways to stop it are unsafe,
  the third saves too little for its cost).
- **Design** proposed the shape of the row 6 fix and four simplifications (Launch returns the block, one campaign
  behavior, the hero check from the roster, one healthy-count formula).

## UNVERIFIED until the in-game spike

- **U1:** whether native code reads `PlayingInCampaignMode` from the marshalled record. Only the managed reader was
  checked.
- **U2:** the burn tick action writes `Health` and calls `Die` directly, bypassing `DisableDying` (`Mission.cs:3705-3720`).
  No managed code queues it on a land scene.
- **U3:** the player team's AI re-scores tactics every 5 seconds with no enemy for the whole review. The weights read
  are float maths; three tactic position searches were not traced. The spike runs five minutes with a hero of Tactics
  50 or more.

## Feedback memories to codify

None. The lessons below are rules for the repo, not for one client's memory.
