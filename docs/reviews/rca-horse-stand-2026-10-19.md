# RCA: CultureDoctrine horse stand (#892), first deep review (2026-10-19)

**Summary.** The horse stand folds a "stand against horse" into CultureDoctrine: an AI defender of an opted-in culture
(`erebor`, `erebor_warriors`, `isengard`, `sturgia`), outnumbered by a mostly mounted enemy, takes ground the horse
cannot ride around, Dale's cavalry dismounts into the wall, and the wall favours polearms. It was reviewed uncommitted
in the worktree `E:\repos\taom-battle-ai-missions` (branch `feat/battle-ai-missions`, base `f25d8f5d6`) before any
in-game run. Seven lenses ran in two waves (standards, engine compatibility, efficiency and data flow; then
completeness and design), followed by one convergence pass on the fixes.

- **Severity:** 0 CRITICAL, 0 HIGH. The first review found 7 MEDIUM; the convergence pass found 2 more MEDIUM in the
  fixes themselves (rows 33 and 34). The rest are LOW or INFO.
- **Shape of the findings:** the stand issued a lasting engine order with no release (row 1), ported half of a vanilla
  mechanic (rows 2, 16, 33 to 35), and re-decided committed ground on every phase flip (row 4). The A/B could not
  prove its own claims (rows 5, 6, 21, 22). Each of these is a repeat of a pattern on file (see "Root-cause patterns").
- **Mike's decision:** chokes are held properly (row 2), not dropped.
- **Outcome:** every confirmed finding is fixed in the working tree except rows 3, 12 and 20, which are recorded
  below. The in-game A/B is owed; CultureDoctrine stays off until it passes.

Feature doc: [culture-doctrine.md](../features/culture-doctrine.md) "Horse stand".

## Findings

Severity is the finder's. "Lens" names the finder. Each row was re-checked against the source or the v1.5.5 cache
before it was entered here.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The Dismount order stayed on the team's formations for the rest of the battle. A formation passes its riding order to every unit that joins (`Formation.cs:2322`), the merge empties the old cavalry formation, and reinforcements prefer an emptied formation, so later cavalry dismounted on arrival, even after the stand. Lenses: engine, data flow. | An order with no release | The plan called the dismount "one way" and thought only of the riders present; it never asked which formations keep the order or who joins them. | `ReleaseDismount` in `OnCancel` sets Free on every AI formation of the team still holding Dismount (Free never remounts: `HumanAIComponent.cs:267`). Repeat of `rca-culture-doctrine-phase-c-2026-09-17.md` row 7. Lesson in `lessons/adapters-taleworlds-api.md`. |
| 2 | MEDIUM | A chosen choke was held at its centre in a wide line facing the nearest enemy, and the wall went Square at each charge, so the gap stayed open. Vanilla faces the choke's direction and sets the frontage to its width (`BehaviorDefend.cs:20-34, 65-78`). Lens: engine. | A vanilla mechanic ported in part | The plan ported vanilla's choke eligibility test and stopped there. | Mike chose to hold chokes properly: `StandGround.HoldOf`, facing and width carried to `BehaviorBracedDefend`, no Square while holding the gap. Lesson in `lessons/adapters-taleworlds-api.md`. |
| 3 | MEDIUM | `dismount: false` is decided per team by its majority culture, and the order acts on a whole formation, so a Dale-led side would dismount allied ram riders. Lens: engine. | Per-team rule, per-formation order | The troop check looked at each culture alone. | Not fixed: a formation order cannot skip single riders. Documented under "Left out". |
| 4 | MEDIUM | The stand verdict held only in the Defend phase, and each change reset the ground anchor. At every contact the non-stand race, which ignores horse, picked the ground again, so the wall could leave its choke as the horse arrived. The gate had no release band. Lens: data flow. | Committed decision reset by a phase flip | The plan tied the stand to Defend for the dismount and did not separate "dismount now" from "this is our ground". | The stand latches when it starts in Defend and ends only at cancel; `EndStand` resets the anchor then. Repeat of `rca-culture-doctrine-engagement-2026-09-17.md` row 1 and phase C row 2. Lesson in `lessons/adapters-taleworlds-api.md`. |
| 5 | MEDIUM | The A/B steps did not say which side the tester plays; the player's team never stands, so a tester playing Erebor would see a false failure, and the player-team step passed even with the rule deleted. Lens: completeness. | A/B step that cannot fail | Steps were written from the feature's view, not the tester's. | Every step names the side to play; the player-team check plays Dale defending. Repeat of `lessons/testing-qa.md` entries near lines 324, 1102 and 1187. Lesson in `lessons/testing-qa.md`. |
| 6 | MEDIUM | The A/B named no scene, and no Custom Battle field scene has a forest region without chokes. Lens: completeness. | A/B input not checked | Nobody scanned the scenes. | A scene table with counts read from the live scene files; the forest choice cannot be isolated there, and the doc says so. Same lesson as row 5. |
| 7 | LOW | The arming rule (switch, culture, defender, not the player's team) was inline glue with no test, and it alone kept the edge and the ground pick off a player's army. Lenses: standards, completeness, design. | Decision in glue | The installer grew the rule as one expression. | `HorseStand.PolicyFor` with four tests; the installer's parameter is required. |
| 8 | LOW | `TaomTacticBase` grew past its own ADR-002 exception paragraph. Lens: standards. | Exception text out of date | The paragraph was not reread. | The paragraph now names the stand members and why they live in the base. |
| 9 | LOW | `StandWeaponFavor.Multiplier` took an `isHuman` flag that production always passed as true. Lenses: standards, design. | Dead branch with a test | The guard lived in two places. | Parameter and its test removed. |
| 10 | LOW | `TeamDoctrineInstaller.Install` had an unused default for the switch. Lenses: standards, data flow. | YAGNI default | Added for convenience. | Parameter made required. |
| 11 | LOW | The GameModel registry and the stat-model header lists missed the new polearm post-pass. Lens: standards. | Registry drift | The registry was not on the builder's list. | Both registry rows, both model headers and `.claude/rules/gamemodels.md` updated. |
| 12 | LOW | `ICultureAggressionService` gained a member though nothing fakes it (ADR-002 amendment: remove it when next touched). Lens: standards. | Interface kept by inertia | Removing it needs `Main/SubModule.cs`. | Not fixed: `SubModule.cs` is single-owner for the batch sessions. Follow-up for its owner. |
| 13 | LOW | "Outnumbered" compared one team's members with the whole enemy side, so the player's AI ally team could stand when its side was not outnumbered. Lenses: data flow, design. | Team versus side counts | `MemberCount` and `EnemyUnitCount` read as a pair. | The gate uses `AllyUnitCount` (it includes this team, `TeamQuerySystem.cs:240-249`). Lesson in `lessons/adapters-taleworlds-api.md`. |
| 14 | LOW | Five texts said the polearm favour reaches dismounted riders; it reaches every AI soldier in a formation holding Dismount, i.e. Dale's whole standing wall. Lenses: engine, data flow. | Doc claim narrower than code | The wall formations get the order too, and the texts were written first. | All five reworded. |
| 15 | LOW | Docs said the switch is read once per mission; the tactics read it once per team at install and the favour on every stat update. Lenses: engine, data flow. | Doc claim wrong | Written from the installer alone. | Corrected. |
| 16 | LOW | The choke eligibility was called "vanilla's test made pure", but vanilla's distance clause was dropped. Lens: engine. | A vanilla check ported without all its clauses | As row 2. | The comment and doc say which clauses are kept and that the march cap and race replace the distance clause. |
| 17 | LOW | "Never the routed vanguard" was not true for the two wall splits, which pass no vanguard. Lens: engine. | Comment claim wrong | Copied from the plan. | Corrected. |
| 18 | LOW | Only the local player's team was excluded; a co-op remote human's formation could take the order. Lens: engine. | Co-op ownership | The local `IsPlayerTeam` was read as "every human". | `IsAIControlled` guard on both the dismount and the release. |
| 19 | LOW | The doc said bandit cultures resolve to default; `erebor_warriors` is a bandit culture with its own row and stand block. Lens: data flow. | Doc claim wrong | Generalised from the looters. | Corrected, and the test message with it. |
| 20 | LOW | The horses left by Dale's riders can be taken by the enemy's unhorsed riders, whose formations hold Mount. Lens: data flow. | Side effect on the other team | The plan reasoned about the stand team only. | Not fixed: documented under "Left out" and as an A/B watch item. |
| 21 | LOW | The looters step cannot run in Custom Battle, which drops bandit cultures. Lens: completeness. | A/B step not runnable | As row 5. | Moved to a campaign cell. |
| 22 | LOW | No A/B step for the ally team, no arm that isolates the polearm favour, no stand-specific OFF arm or pass criterion. Lens: completeness. | A/B incomplete | As row 5. | All four added. |
| 23 | LOW | The doc's "add a tactic" and "give a culture a doctrine" checklists, culture rows, limitations and binding-test list did not carry the stand. Lens: completeness. | Doc checklists stale | Only the new section was written. | All updated. |
| 24 | LOW | The riding-order engine facts were not recorded. Lens: completeness. | Engine reference gap | No reference doc was on the list. | Added to `docs/reference/engine/formations-and-team-ai.md`. |
| 25 | LOW | The MCM hint read "riders without mounts". Lens: completeness. | Wording | | Reworded. |
| 26 | LOW | Enlistment and sergeant mode never stand, because the player's team is never armed. Lens: completeness. | Scope note | | Documented. |
| 27 | LOW | The anchor's mover lists started below one team's formation count. Lens: efficiency. | One-time growth | | Capacity 10; the comment says several enemy teams can still grow it once. |
| 28 | LOW | The scene was classified twice, once to copy and once to count, with a region walk that only met duplicates. Lens: design. | Duplication | | One classification; counts read from the copied array. |
| 29 | LOW | The race was composed twice (stand picker and anchor). Lens: design. | Duplication | | `HighGroundRace.CanTake` used by both. |
| 30 | INFO | Only the Custom Battle chain of the polearm reset was pinned. Lens: engine. | Binding gap | | A campaign-chain binding test added. |
| 31 | LOW | Three test names missed the house pattern. Lens: standards. | Naming | | Renamed. |
| 32 | LOW | Issue #892 still described the pre-approval design. Lens: completeness. | Issue drift | The approved-design comment was posted, but the body was not edited. | Body rewritten to the built design. |
| 33 | MEDIUM | Convergence: on an insurmountable choke the picker raced the choke's centre while the anchor's re-check raced the hold point half a width further on, so a choke near the cap passed the pick and failed the first re-check, and the wall then held where it stood for good. | Two races on two points | Introduced by the row 2 fix (the hold point) meeting the row 29 fix (the shared re-check). | The picker races the hold point; `Pick_InsurmountableChokeWhoseHoldPointIsBeyondTheCap_IsNotPicked` was RED first. |
| 34 | MEDIUM | Convergence: the choke frontage was set once on arrival, and any later arrangement change made the engine recompute it from the file count (`Formation.cs:758-761`), so the gap opened or overhung. | A vanilla mechanic ported in part | The row 2 fix ported the order, not vanilla's per-tick re-issue. | `FormOrderCustom(width)` every tick at the position, as `BehaviorDefend.cs:55, 65-78` does. |
| 35 | LOW | Convergence: the facing on an insurmountable choke was frozen at decision time; vanilla faces the live enemy. | As row 34 | As row 34. | `FacingOrderLookAtEnemy` for an insurmountable choke. The hold point stays fixed on purpose, so the wall does not slide out of the gap; the doc records this. |
| 36 | LOW | Convergence: seven texts no longer matched the fixed code. | Docs written before the code settled | Fixes changed the code after the docs were written. | All corrected. |
| 37 | LOW | Orchestrator: after the latch, a cancel cleared the stand flag but left the anchor in stand mode, so a re-picked wall would favour chokes without the gate. | Two halves of one state | The latch was applied to the flag first. | `OnStandEnded` calls `HighGroundAnchor.EndStand` at cancel. |

## Root-cause patterns

**An order or a decision without its end (rows 1, 4, 37).** The stand issued a lasting engine order and made a lasting
ground decision, and in both cases the plan described the start and assumed the end. TAOM has hit this before:
VolleyControl's hold had no release on one exit (phase C row 7), and the first wall brace had no release time
(engagement row 1). The fix pattern is the same each time: the component that issues an order owns its release, at
the one event that ends its authority (here `OnCancel`).

**A vanilla mechanic ported in part (rows 2, 16, 33, 34, 35).** Holding a choke in vanilla is four things: the
eligibility test, the hold point, the facing, and a frontage re-issued every tick. The plan ported the first. Each
fix then ported one more part and the convergence pass found the next missing one. The rule: read the whole vanilla
behaviour that owns the mechanic (here `BehaviorDefend` with `TacticHoldChokePoint`) and port it as one unit, or
name each part left out.

**A/B steps that cannot fail or cannot run (rows 5, 6, 21, 22).** This is the fourth time on file (`testing-qa.md`
near lines 324, 1102 and 1187). The completeness lens caught it again, which is where it is caught; the doc's
protocol now names the side to play and the scene for each step.

## Why each lens caught or missed what it did

- **Standards** found the structure rows (7 to 12, 31). It does not judge engine behaviour.
- **Engine compatibility** found the engine-behaviour rows (1 to 3, 14 to 18, 30) by tracing riding orders,
  reinforcement placement and `BehaviorDefend` in the installed assemblies.
- **Efficiency** found nothing above LOW (row 27): the favour short-circuits when the doctrine is off and the AI-tick
  scoring allocates nothing.
- **Data flow** found the phase-flip reset (row 4), the team-versus-side gate (row 13), the horses (row 20) and
  confirmed row 1 from the reinforcement side.
- **Completeness** found the A/B rows (5, 6, 21 to 23, 26) and the issue drift (row 32).
- **Design** gave the fix shapes for rows 1, 2 and 4 (the sweep at cancel, the choke hold, the latch) and the
  simplifications in rows 9, 28 and 29.
- **Convergence** found rows 33 to 36, all created by the fixes: one more reason to run it.

## UNVERIFIED until the A/B

- Whether and how fast the native AI dismounts after the order, and whether a formation on Stop still dismounts.
- What `AiWeaponFavorMultiplierPolearm` does natively, and to shield carriers.
- The per-tick frontage and the live facing on a real choke (`taom_rohan_battle_001`).
- The cancel-then-re-pick path.

## Feedback memories to codify

None. The lessons below are rules for the repo.
