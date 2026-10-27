# RCA: Patch123 LastSeenTextGuard deep review (2026-10-26)

**Crash:** player bundle 9b2a103e (TAOM v2.0.34, Bannerlord v1.5.5.124170, 2026-10-11). No issue yet;
filing it waits for Mike's word.
**Review:** `/deep-review`, risk tier 1 (a Harmony patch). Wave 1: Agent 5 Data flow and Agent 2
Engine compatibility (`deep-reviewer`, Opus 5.5 at max). Codex pre-review at `high` (prompt
`codex-prereview-lastseen-guard-2026-10-26.prompt.md`, output in `raw/`), then a Codex re-review of
the HIGH fix (`codex-fixreview-lastseen-guard-2026-10-26.prompt.md`).

## Top-line

The crash is a vanilla v1.5.x defect: `HeroHelper.GetLastSeenText` reads `partyBase.LeaderHero` and
`Army.LeaderParty.LeaderHero` with no null check, and vanilla lets a leaderless party (bandits) hold a
captured lord. The first guard was a finalizer that swallowed every `NullReferenceException` the
patched method raised. Codex proved that it also swallowed an NRE thrown by another owner's prefix,
so it hid unrelated faults, which the change's own goal forbade. The guard now swallows an NRE only
when the exception's first frame is the target's own body and the hero's state reaches one of the
unguarded reads. A Codex re-review of that fix proved it still too wide: a small foreign prefix the JIT
inlines, a foreign transpiler and an NRE inside the inlined `GameTexts.FindText` all throw in the same
replacement frame. The guard is now a prefix that answers only the leaderless state and swallows
nothing. The first version was committed in `76a395729` before the re-review; the prefix replaces it.
The rest were test and documentation gaps, all fixed.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH (Codex) | The finalizer judged by exception type alone. A finalizer wraps the whole Harmony replacement, prefixes and postfixes of every owner included, so a foreign NRE became a "successful" call logged as the vanilla defect. | Containment scope | I followed the Patch69 lesson to the letter ("swallow only the exception class it exists for") and the Patch49 and Patch62 precedents. For a class as generic as `NullReferenceException` on a method other mods can patch, the class says nothing about the cause. | First fix (`ThrewInOriginal` plus a state check) was refuted by finding 10. Final fix: a bool prefix on `LastSeenState.ReachesUnguardedLeaderRead`; lesson in `lessons/harmony-il.md` ("A finalizer cannot tell a vanilla NRE from a foreign one: guard the read with a prefix"). |
| 2 | MEDIUM (Codex; Agent 5) | The tests stayed green with the fallback text forced empty, and nothing exercised log-once. Every fault-path test passed a null hero. | Test strength | The tests checked "non-null result", which the `catch` returning an empty text satisfies on its own. I did not apply the two-edit test to them. | Fix: fallback-text tests against a real `GameTextManager` (text id and both variables), log-once and two-hero tests, and a mutation run (origin check forced true, fallback forced empty, log-once removed) that failed 5 tests. No new rule: `tests.md` already names the two-edit test. |
| 3 | LOW (Codex; Agent 2) | "A lord held by bandits always threw" overstated it: vanilla returns "never seen" first for a hero the player does not know, and the bundle does not name the captor. | Claim wider than evidence | I wrote the attribution from the mechanism, not from what the bundle proves. | Fix: wording in the patch comment and four docs. `evidence-over-claims.md` C already covers it. |
| 4 | LOW (Agents 2 and 5) | `taleworlds-api-snapshot/patch-targets.md` was not regenerated, so `engine_body_diff.py` would not diff the new target at the next engine bump. | Generated artifact not refreshed | No gate fails on a missing row; Patch122 needed the same fix-up (`f2e6efd23`). A repeat. | Fix: regenerated (339 patches, one new row), and `/verify` Step 2d plus the `api-snapshot` row in `.ai/verification.md` now run `snapshot_api_surface.ps1 -Check`, which exits 1 on a missing row (the script's help already said the verification step ran it; nothing did). |
| 5 | LOW (Agent 2) | The binding test did not pin the parameter name `hero` that Harmony binds, and never applied the patch through real Harmony. | Binding coverage | I pinned target and category only. | Fix: the parameter-name assertion and an apply test on the installed engine (`BindingVerification`, `RequiresGameIL`), the Patch122 shape. |
| 6 | LOW (Agent 2) | The comment said "No TAOM patch is on that stack"; TAOM patches were on outer frames. | Wording | Read from the inner exception only. | Fix: "on the throwing frames (GetLastSeenText, EncyclopediaHeroPageVM.Refresh)". |
| 7 | LOW (Agent 5) | The log line gave no throw location and said "showing the settlement text" even when the text was "never seen" or empty. | Diagnostics | Written before the fallback had three outcomes. | Fix: the line now names the text actually shown; the prefix design (finding 10) throws nothing, so there is no throw location to log. |
| 8 | LOW (Agent 5) | The feature-map Encyclopedia row cited only Patch119; the registry did not name `OnSubModuleLoad`. | Docs | No gate requires them. | Fix: both rows. |
| 9 | LOW (Agent 2) | The registry and comment said PatchShield also wraps the target; that is true only in solo play (the shield does not install under co-op). | Wording | Read `PatchShield` without `PatchShieldPolicy`. | Fix: both say "solo play only". |
| 10 | HIGH (Codex re-review) | The origin check still passed a small foreign prefix the JIT inlined, a foreign transpiler's inserted throw, and an NRE inside the inlined `GameTexts.FindText`: all throw in the replacement frame. | Containment scope (repeat of 1) | I narrowed the check without listing what else runs in the replacement frame; the synthetic test used a prefix that throws explicitly, which the JIT does not inline. | Fix: the prefix design; nothing is swallowed. Lesson above. |
| 11 | MEDIUM (Codex re-review) | `Capture` built the settlement link inside the try that decided eligibility, so a link failure discarded a leaderless classification and reopened the crash. | Classification mixed with formatting | One try block for two concerns. | Fix: the link is read separately in `Decide`; a failed read gives the never-seen text and still skips vanilla (`Decide_LinkReadThrows_StillSkipsVanillaWithTheNeverSeenText`). |
| 12 | MEDIUM (Codex re-review) | All 27 tests passed with the production finalizer forced to propagate: the synthetic test probed `ThrewInOriginal`, and the installed-target test never ran the fault path. | Test strength (repeat of 2) | I mutated the helpers, not the entry point's own decision. | Fix: the production decision is `Decide`; a mutation run (decision forced to run vanilla, fallback empty, log-once removed, link catch removed) failed 8 tests, then 2 tests for the last two alone. |
| 13 | LOW (Codex re-review) | The prose omitted the refreshed last-known-settlement precondition. | Claim wider than evidence | Repeat of 3. | Fix: the patch comment and registry name it. |

Found by the author before the lenses started, fixed in place: the rethrow marker passed `null`
instead of `__originalMethod` on a cold target; the log printed `armyLeaderHeroNull=True` with no army;
the tests depended on run order through the static once-per-hero set; a `sed -i` turned the test file
to LF.

## Root-cause pattern

Findings 1 and 2 share one cause: I verified that the guard catches the crash, and not that it
catches only the crash. The positive case (the vanilla NRE is contained, the result is non-null) was
tested and reviewed; the negative cases (a foreign NRE, a lost fallback text) were not asked. Codex
found finding 1 because its prompt stated the outcome as "stop that crash without hiding unrelated
faults", which turned the negative case into a requirement.

## Why each agent missed finding 1

- **Agent 5 (Data flow)** traced it and recorded "the swallow does not depend on which dereference
  threw" as a strength. My scope note asked it to look for more NRE sources inside the method
  (coverage), not for NREs from outside it (over-capture), and it answered the question asked.
- **Agent 2 (Engine compatibility)** verified Harmony's finalizer semantics, including that the
  wrapper runs other owners' patches, but its lens checks API and binding correctness, not the scope
  of a swallow.
- **Lenses 1, 3, 4, 6** were not run (SKIPPED BY TIER).

## Lessons recorded

- `docs/reviews/lessons/harmony-il.md`: "A finalizer cannot tell a vanilla NRE from a foreign one:
  guard the read with a prefix".
- `docs/reviews/lessons/adapters-taleworlds-api.md`: "An engine rewrite can add unguarded reads to a
  method TAOM never patched".

## NRE guard audit (2026-10-27)

Mike approved applying the audit of TAOM's other NRE containment finalizers (an `architect` agent,
read-only, verdicts spot-checked against the code before any edit):

| Patch | Target | Result |
|---|---|---|
| Patch49 | `Army.FindBestGatheringSettlementAndMoveTheLeader` | Narrowed: swallows only the two proven dead ends (`IsKnownDeadEnd`); any other NRE now propagates instead of being filed as a siege dead end. |
| Patch20 | `SpawnNonHumanNarrativeMenuCharacter` | Finalizer replaced by a bool prefix that skips the spawn when vanilla would throw; no swallow, no half-built entity. |
| Patch50 | `Agent.CheckToDropFlaggedItem` | Finalizer kept and now logs the first swallowed NRE per process with its stack. The first stated reason ("the bite route has no `try`") was wrong: see finding 14. |
| Patch56 | `GauntletSceneNotification.OpenScene` | Kept (the random cast blocks a prefix); the swallow now logs a WARNING with the whole exception. |
| Patch69 | `TournamentVM.OnTournamentEnd` | Kept (a rethrow can hang the mission); the decision moved to the testable `Settle`, and its ERROR line carries the stack. |
| All four kept finalizers | | A non-NRE now goes back through `RethrowStackPreserver`; a bare `return __exception` made Harmony `throw` and erased the throw site. |

No other TAOM finalizer swallows NRE by accident: CrashReport, SaveShield and the FactionUI movie swap
swallow by design; PatchShield, Patch62, Patch65 and the rest are scoped to other types or are `void`.
Tests: `ContainmentFinalizerRethrowTests` (RED first: all four handed exceptions back bare),
`Patch49GatheringNreRuleTests`, `NarrativeMountSpawnGuardTests`; two new `ReviewedSafe` rows in
`CoopVetoClassificationTests`.

## Batch deep review (2026-10-27)

Lens 5, lens 2 and Codex (`high`; prompt `codex-batch-nre-guards-2026-10-27.prompt.md`, output in `raw/`)
reviewed the audit batch. Every confirmed finding was re-read against the installed v1.5.5 decompile
before the fix.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 14 | MEDIUM (lens 5, lens 2) | I recorded that Patch50's bite route has no `try`, so a rethrow would hang `OnMissionTick`. `BehaviorTree.RunTree` catches every exception, and the warg, spider and signature-strike routes have their own catches. | Exception path traced one call short | I stopped at `BehaviorTreeAgentComponent.TickOnMissionThread` and did not open `RunTree`. `harmony-patches.md` already says to follow an exception to its next catcher before deciding a rethrow; I did not apply it. A repeat of an existing rule, not a new one. | Fix: the reason rewritten in the registry and the class comment. Mike chose keep-and-log: the finalizer now logs the first swallowed NRE per process with its stack. |
| 15 | HIGH (Codex) | Patch20's prefix let a null `Monster.ActionSetCode` through; `MBGlobals.GetActionSet` uses it as a dictionary key, which throws `ArgumentNullException("key")` after the mount entity exists. | Precondition list incomplete | I listed the reads in the target's own body and not the ones inside the callee it passes the value to. | Fix: `CanSpawnMount` takes the raw values, the action-set code included, so a dropped check fails its test (also closes lens 5's "dropping `Item2Id` passes"). |
| 16 | HIGH (Codex) | Patch49 still contained a foreign prefix's NRE while the army was in a dead end: Harmony opens the try block before the prefixes. | Containment scope (repeat of 1 and 10) | The narrowing checked state, not provenance. | Fix: a `Priority.Last` prefix sets `__state`; the finalizer contains only when the body was entered. The Harmony behaviour is pinned through real Harmony. |
| 17 | MEDIUM (lens 5) | Patch123's repeat of vanilla's write was untested: deleting it, moving it or dropping its gate passed every test. | Boundary code untested | The mutation runs targeted `Decide`, not `Capture`. | Fix: the gate moved to the pure `LastSeenState.Complete`, tested for when it refreshes and that it refreshes before the settlement read. |
| 18 | MEDIUM (Codex) | The `api-snapshot` row in `.ai/verification.md` followed a Release build, but the tool reads the Debug test bin whenever it exists. | Doc vs tool mismatch | I wrote the row from the `/verify` flow, which builds Debug. | Fix: the row names the Debug build it needs. |
| 19 | LOW (lens 5, lens 2) | No gate checked Patch20's `____characterScene` injection or its `TargetMethod` name: `HarmonyFieldInjectionNamingTests` skips a bare `[HarmonyPatch]` class. | Binding coverage | I pinned only the prefix's own parameter names. | Fix: both rows in `ReflectionSiteBindingTests` and `reflection-sites.md`. |
| 20 | LOW (lens 5) | Patch56 set its close flag after logging, so a failed log line would leave the input soft-locked. | State after I/O | Added the heavier log line without moving the flag. | Fix: the flag is set first (`Patch56_LoggerThrows_TheCloseIsStillRequested`). |
| 21 | LOW (lens 5, lens 2) | Stale or wrong text: Patch69 promised full containment (it contains NREs only); Patch56 said its cast is random; Patch20 said a missing type fails silently; Patch49 claimed vanilla guards `AiBehaviorObject` downstream; `SubModule.cs` called Patch123 a finalizer. | Wording | Each was copied forward from older text. | Fix: all corrected; Patch69's dump-failure branch now has a test. |

**Merge damage repaired.** Merge `a99a8cb1c` resolved two same-place insertions by keeping one side:
it dropped the `api-snapshot` row, and in the settings counts it kept `616cf12f1`'s numbers over the
other session's removal of two Auto-Resolve Diagnostics toggles. The `api-snapshot` row is restored
here. The settings counts were fixed on trunk by `c72bf6ef7`, so this change keeps that version of
`SettingsFingerprintTests`, `ServerSettingsSchemaTests`, `coop-interop.md` and
`bannerlord-together-compat.md`. Merge `921091938` lost nothing.
