# RCA: B4c, mission, CustomBattle-engine and WinForms types to TAOM.Client (ADR-013, #968), 2026-10-22

**Summary.** B4c moves seven types from `TAOM.dll` into `TAOM.Client.dll`: the warg rage-mode player
controller (`AutonomousMovementPlayerController`, a `[DefaultView]` MissionView), the battle action bar view,
the Host Review order provider, the parked shader walk's game manager and the shader notice's three WinForms
types. Core reaches them through five seams with null implementations, and the Warg code through the core
interface `IAutonomousMovementController`. The review ran at tier 1: Codex (gpt-6-astra, ultra, 227,090
tokens) and the Data flow and Engine compatibility lenses, in one wave. No CRITICAL or HIGH was found, so no
second wave ran. Two MEDIUM and four LOW findings were confirmed against the code and fixed in the same
commit. The pattern behind the two MEDIUMs: a source-text gate that checks order or presence with
`IndexOf` and never checks that the text it compares exists, or which way a condition points.

Sources: Codex output `docs/reviews/raw/codex-adversarial-taom-client-b4c-2026-10-22.md` (local, ignored),
prompt `docs/reviews/codex-adversarial-taom-client-b4c-2026-10-22.prompt.md`. Build plan (not in the repo):
`E:\Temp\claude\handoffs\b4c-build-plan.md`.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | `SubModuleMissionServerGuardTests` matched the word `IsDedicatedServer`, so an inverted condition (`if (!...IsDedicatedServer)`) still passed: TAOM would then skip its mission behaviors on every client and keep them on a server | Weak test oracle | The test was written to go RED on a missing guard, and nobody asked what wrong guard it would accept | The test pins the exact positive condition, one server check only, and a log before the return |
| 2 | MEDIUM | `WargControllerSwapTests.ClientController_SwitchesVanillasControllerOffBeforeItselfOn` compared two `IndexOf` results; a deleted `baseController.Disable()` gives `-1 < n` and passes, leaving vanilla's controller active during rage | Weak test oracle | Same as 1: the order check assumed both calls exist | The test collapses whitespace and pins each branch's two calls as one adjacent sequence |
| 3 | LOW | `CoopMissionRolePolicyTests.Table_CoversEveryBehaviorTheModAdds_NoMissingNoStale` counted only `AddTaomBehavior(new X(`; behaviors added through a factory variable were a hand list, so a third seam could skip the co-op role table (a missing row defaults to `Local`, which a session adds) | Gate scope | The two factory adds were special-cased instead of counted | The test counts `AddTaomBehavior(variable)` sites and requires that number to equal the hand list |
| 4 | LOW | `docs/features/warg-combat.md` (new text) said the explicit controller instance is harmless "because it ticks first and the others write after it" | Engine assumption | The plan inferred tick order from the behavior list and did not read when the mission screen registers views | Corrected: `MissionScreen.OnMissionAfterStarting` registers views before `Mission.AfterStart`, so the explicit instance has no `MissionScreen` and its pre-tick returns before it writes |
| 5 | LOW | Research problem row 7 still listed `HostReviewModule.OnPhase` as a ProcessLoad site that reaches client code; B4c settled it | Stale tracker | The doc update edited the B4c rows and missed row 7 | Name removed from row 7 |
| 6 | LOW | The server-guard log line said "TAOM added no mission behaviors", but the caller's `finally` still adds the loading-window closer | Inaccurate log | The comment stated the exception; the log text did not | Log text now names the closer |

Not changed, recorded (they exist at the base `fd2072a0a`, B4c neither causes nor fixes them):

- **The battle action bar never refreshes and takes no hotkeys.** `BattleActionBarMissionView` is added in
  `Mission.AfterStart`. `MissionState` calls `MissionScreen.OnMissionAfterStarting` first
  (`MissionState.cs:343,345`), and that method registers only the views already in the list
  (`MissionScreen.cs:4054-4065`). The view's `MissionScreen` therefore stays null and its
  `OnMissionScreenTick`, the bar's only driver, never runs. Lens 2 found no `[BattleActionBar] mission init`
  line in 34 TAOM logs. Boot check 3 of the plan will fail on the base too. A follow-up issue is owed on
  the maintainer's word.
- **The explicit controller instance is inert and leaks** its `EarlyStart` handlers each mission, for the
  same reason. Dropping the explicit add should change no gameplay (lens 2); it stays a separate change.

## Why each reviewer missed or found these

- Codex found 1 and 2 by mutating the production text in memory and re-running each test's predicate.
  Its own build failed in its sandbox (`MSB4184`, SDK discovery), so its evidence for the rest is source
  and decompile only.
- Data flow found 3 by asking what a future seam written the same way would do, and 5 and 6 by reading the
  tracker and the log text against the code.
- Engine compatibility found 4 and the two base defects by enumerating the firing set of every overridden
  member: `OnMissionScreenTick` dispatches only over the screen's registered views.
- The builder's RED run confirmed every new test failed for its stated reason, which does not show what a
  wrong implementation would pass. Neither lens reviewed test oracles; only Codex mutated them.

## Lessons

Appended to `docs/reviews/lessons/build-tooling-workflow.md`: "A source-text gate must fail on the wrong
code, not only on missing code". Lens 2's view-registration finding is an engine fact for the mission
lifecycle doc and the follow-up issue, not a review lesson.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/lessons/build-tooling-workflow.md](lessons/build-tooling-workflow.md)

<!-- backlinks-end -->
