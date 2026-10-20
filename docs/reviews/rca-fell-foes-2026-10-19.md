# RCA: Fell Foes (#893), first deep review (2026-10-19)

**Summary.** Fell Foes adds three enemy battle tweaks: cornered routers turn and fight (and rally the routers beside
them), idle enemy archers pick the player, and the last stragglers of a big field battle are sent off the field so the
rout victory ends it and the campaign captures them. It was reviewed uncommitted in the worktree
`E:\repos\taom-battle-ai-missions` (branch `feat/battle-ai-missions`, base `2a7653f23`) before any in-game run. Seven
lenses ran in two waves (standards, engine compatibility, efficiency and data flow; then completeness and design),
then one convergence pass on the fixes.

- **Severity:** 0 CRITICAL; the first review found no HIGH and 8 MEDIUM. The convergence pass found 1 HIGH (row 27,
  a red test created by a doc edit) and 1 MEDIUM (row 28). The rest are LOW or INFO.
- **Mike's decisions (2026-10-19):** build the hit trigger and the rally as approved (row 1); give rallied men real
  morale and rally only while the battle is contested (rows 3 and 4); keep the co-op stand-down; ship the tweaks on,
  with the spike before the next release.
- **Outcome:** every confirmed finding is fixed in the working tree except row 15, which is recorded as deliberate.
  The in-game spike is owed.

Feature doc: [fell-foes.md](../features/fell-foes.md).

## Findings

Severity is the finder's. "Lens" names the finder. Each row was re-checked against the source or the v1.5.5 cache
before it was entered here.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The approved cornered rule (a router who LOSES HEALTH near 1 to 3 of the player's men turns, and NEARBY ROUTERS RALLY with him) was built as "a router near 1 to 3 men turns", with no hit trigger and no rally, and nothing recorded the change. Lens: completeness. | Approved scope dropped silently | The plan brief described the rule only in the issue comment; its own Decisions list deferred "Captain's rally" (a different, hero-led rally), and the builder read the base rally as part of that deferral. | Built as approved: health drop since the last pass, a rally of up to 4 panicked men within the cornered radius. Lesson in `lessons/testing-qa.md`. |
| 2 | MEDIUM | After the battle result, the cornered rule kept stopping enemy routers (they turned on cheering troops; captures became kills): the gate never read `Mission.MissionEnded`, though the mission keeps ticking until the player leaves. Lens: data flow. | Mission tweak not ended with the battle | The gate reused a battle allowlist that only reads mode, team-AI type and combat type. | The gate closes at `MissionEnded`. Lesson in `lessons/adapters-taleworlds-api.md`. |
| 3 | MEDIUM | A rallied router kept the 0.02 morale `StopRetreating` leaves; one nearby death re-panicked him, so he fled and turned each second, and each re-flee drained the morale of the men around him. Lenses: data flow, design. | Engine call used outside its purpose | `StopRetreating` is vanilla's battle-over path, not a rally. | Morale raised to the agent's `RecoveryMorale`. |
| 4 | MEDIUM | Rallied men clear `IsRunningAway`, so vanilla's rout victory (every enemy running away for the reinforcement interval plus 3 s) waited for them; sieges and small battles had nothing to make up for it. Lens: data flow. | Tweak fights the engine's end condition | The plan checked the straggler cleanup against `BattleEndLogic`, not the cornered rule. | Rally only while somebody on the enemy side still stands on his own; once the side breaks, the turned men run again. |
| 5 | MEDIUM | The debug line "held N, targeting M", the spike's acceptance check, compared holds after the pass with targets read before it, so it showed a failure on every second that started a hold. Lens: data flow. | Metric compares two populations | The line was written from the state, not from what the scan read. | Both numbers counted over the same checked archers ("aiming X of Y checked"). |
| 6 | MEDIUM | Three engine claims in the doc were wrong: the 15 m reason (it belongs to another native function), "may count mounts", and "no vanilla caller of the target pair" (`TaskForceDetachment` uses it). Lens: engine. | Docs written ahead of evidence | The builder could not resolve `TaskForceDetachment` by its short name with `taom-src` and wrote the absence as a fact. | Corrected from the native decompile and the dump; the doc cites the evidence. |
| 7 | MEDIUM | Two rules built a hash structure over every enemy each second to look up a handful of tracked handles (about 70% of the feature's per-second garbage). Lens: efficiency. | Allocation sized to the army | Written for clarity first. | Sets sized to the tracked handles; indexed loops; cached delegate; early returns. |
| 8 | MEDIUM | Nothing tested that each mission gets a fresh service; a shared one would carry the peak count, the sent-off set and holds into the next battle. Lens: completeness. | Untested lifecycle invariant | The wiring test resolved services but never ran the factory. | `EveryMission_GetsItsOwnBehaviorAndItsOwnState`. |
| 9 | LOW | The wiring test meant to guard "release held archers when the gate closes" counted `Release()` and stayed green with that line deleted. Lens: standards. | Guard test that cannot fail | The count included the method's own declaration. | The gate moved into the service, where the release is unit-tested. Repeat of `lessons/testing-qa.md` near line 365. Lesson below. |
| 10 | LOW | The behavior's gate clock added `dt` unchecked; one NaN would freeze the gate's last answer. Lenses: standards, data flow. | Engine float gate | Two clocks, only one checked. | One clock, in the service, after the finite and positive check. |
| 11 | LOW | Creature bandits are team members that never rout, so they hold the rout victory open, and the straggler rule (humans only) ignored them. Lens: engine. | TAOM-specific agents not counted | The scan filtered `!IsHuman` for the soldiers it acts on and so lost them for the count. | `OthersStanding` blocks the cleanup and counts toward "standing". |
| 12 | LOW | A router already fading out at the map edge could be stopped. Lens: engine. | Native state not checked | | `StopRout` refuses `IsFadingOut()`. |
| 13 | LOW | A hold and a release in the same frame could lose the release (the hold is queued natively, the release is immediate). Lens: engine. | Native ordering | The failure path released at once. | A failed pass releases on the next pass. |
| 14 | LOW | A rallied man later sent off stayed in the cornered set and was unsteadied, undoing the send-off's morale hold. Lenses: engine, data flow. | Crossed rule sets | | A sent-off man leaves the cornered set. |
| 15 | LOW | Turned men are not released when the switch is turned off, the pass fails or the gate closes. Lens: data flow. | Missing release (deliberate) | | Not changed: an unsteady at mission end would turn captures into routs, and under co-op any write is forbidden. Recorded in the doc with the remaining risk. |
| 16 | LOW | The per-pass release loop was not isolated per handle; one throw stranded the rest with targeting off. Lens: data flow. | Error isolation | `ReleaseAll` had per-handle tries, the pass loop did not. | One release helper with a try per handle. |
| 17 | LOW | "Archers" included javelin and throwing troops (`HasRangedWeapon` admits any ranged usage). Lenses: engine, data flow. | Engine predicate wider than the name | | `IsRangedCached` (bow or crossbow with ammunition), read before the native `ImmediateEnemy`. |
| 18 | LOW | `FellFoesMissionLogic` derived from `MissionBehavior` with a comment explaining why not `Logic`. Lens: standards. | Naming | | Derives `MissionLogic`. |
| 19 | LOW | Two accessors (`Standing`, `IsStanding`) for one flag. Lens: standards. | Simplicity | | One `internal Standing`. |
| 20 | LOW | A failed doctrine tactic keeps `Standing` latched, so that team's archers were skipped all battle. Lens: data flow. | Stale latched flag | | The probe reads `Standing && !Failed`. |
| 21 | LOW | `TeamAIComponent._currentTactic`, read by name, had no reflection-sites row. Lens: engine. | Registry gap | Pre-existing; Fell Foes added a second reader. | Row and `[DataRow]` added. |
| 22 | LOW | `coop-interop.md` quoted 143 excluded settings (real: 146) and the compat doc "249 values"; the doc test checked only the total and covered figures. Lens: data flow. | Doc counts not machine-checked | | `EveryDocQuotingTheSettingsCounts_AgreesWithReflection` now checks the excluded figure. |
| 23 | LOW | The settings provider was missing from `HotPathSettingsProvidersTests`. Lens: completeness. | Hand-kept list | Repeat of `lessons/misc.md` near line 547. | Added. |
| 24 | LOW | A failed `SendOff` was never rolled back, against the rules' own summary. Lens: completeness. | Contract drift | | Rolled back and tested. |
| 25 | LOW | One request set served two purposes (foe counts and debug target reads); the target read ran even with Debug off; the archer range rule sat in the adapter. Lenses: efficiency, design. | Shape | | Request split into `Cornered` and `Held`; the range rule is in the pure rules. |
| 26 | LOW | Stale comments and doc lines (GroupOrder list, the shared gate's consumers, the co-op reason, Key Files, Performance, issue titles, "game-tested"). Lenses: standards, completeness. | Docs | | All corrected. |
| 27 | HIGH | Convergence: `ReflectionSiteBindingTests` went red: a summary line added to `TeamTacticProbe.cs` in the doc pass moved the reflected field from line 29 to 30, and the row's label still said 29. | Line labels shift with unrelated edits | The doc pass ran only the settings test, not the binding gate. | Label updated. Lesson in `lessons/build-tooling-workflow.md`. |
| 28 | MEDIUM | Convergence: a rallied man was admitted for being within 5 m of the hit man but kept only while a foe was within 5 m of himself, so most rallied men ran again after about a second. | Admission and retention use different tests | The fix list said "follow the same re-flee rule" without checking it against the admission rule. | Retention uses a hold radius of twice the cornered radius (clamped to 15 m), so everyone admitted starts inside it. Lesson in `lessons/adapters-taleworlds-api.md`. |
| 29 | LOW | Convergence: three tests could not fail (the stop rollback, the fresh-service test, the request-set test). | Guard test that cannot fail | As row 9. | Rewritten to fail without the guarded line (RED proven by deleting it) or deleted where another test pins the invariant. |
| 30 | LOW | Convergence: switching the cornered rule off and on left old health readings, so the first pass could count an old drop as a hit. | Stale state across a toggle | | Health maps cleared while the pass does not run. |
| 31 | LOW | Convergence: the MCM hint, four comments and four doc lines described the pre-fix rule. | Docs written before the code settled | | Corrected. |
| 32 | LOW | Convergence: the spike's flicker check had no per-man stop count. | Missing measurement | | `restopped` in the debug line. |


## Root-cause patterns

**Approved behaviour lost between plan and build (rows 1, 28).** The approved design lived in the issue comment; the
plan brief summarised it and listed a deferred, similarly named tweak. The builder built from the brief. The fix is
on the plan side: a brief restates every approved behaviour as an acceptance list, and the review checks each item.

**Rules that stop at the start (rows 2, 4, 15, 28, 30).** As with the horse stand, the plan described when a tweak
starts and not when it must stop: at the battle's result, when the side breaks, when the rally loses contact, when a
switch flips. Each fix named the event that ends the rule.

**Tests that cannot fail (rows 9, 29).** Four tests passed with the line they guard deleted. This is a repeat
(`lessons/testing-qa.md` near line 365). The builder now proves each guard test RED by deleting the guarded line.

## Why each lens caught or missed what it did

- **Standards** found the naming, the accessor, the unchecked `dt` and the guard test that could not fail (rows 9, 10,
  18, 19, 26).
- **Engine compatibility** found the engine rows (6, 11 to 14, 17, 21) and verified 30 members, including that
  riderless horses have no team.
- **Efficiency** found the army-sized structures and the small allocations (rows 7, 25).
- **Data flow** found the end-condition rows (2, 3, 4, 5, 15, 16, 20, 22).
- **Completeness** found the dropped scope and the untested lifecycle (rows 1, 8, 23, 24, 26).
- **Design** gave the fix shapes (the gate in the service, the morale buffer, the contested rule, the request split).
- **Convergence** found rows 27 to 32, two of them created by the fixes.

## UNVERIFIED until the spike

- Whether a stopped or rallied router without a formation fights.
- Whether the archer's target sticks.
- When `IsRunningAway` flips for a sent-off straggler.
- The tick cost in a 1,000-agent battle.

## Feedback memories to codify

None. The lessons below are rules for the repo.
