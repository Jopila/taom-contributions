# RCA: Lost Footing (#894) phase 1 census, first deep review (2026-10-20)

**Summary.** Lost Footing phase 1 is a read-only census: it logs, per battle, the agents stranded off the walkable area
or standing still in water, so Mike can decide from real battles whether a rescue (phase 2) is needed. It was reviewed
uncommitted in the worktree `E:\repos\taom-battle-ai-missions` (branch `feat/battle-ai-missions`, base `06b1deaab`)
before any in-game run. Seven lenses ran in two waves (standards, engine compatibility, efficiency and data flow;
then completeness and design), then one convergence pass on the fixes.

- **Severity:** 0 CRITICAL, 2 HIGH, 9 MEDIUM, the rest LOW or INFO. The two HIGHs would have made the census measure the
  wrong things: nearly every soldier counted as a creature (row 1), and "far from destination" measured to the scene
  origin (row 2). The convergence pass found only LOW items.
- **Where the HIGHs came from:** the plan, not the build. Both errors are in the approved plan's acceptance list and
  engine table, and the builder implemented them faithfully.
- **Outcome:** every confirmed finding is fixed in the working tree. The census runs are owed and decide phase 2.

Feature doc: [lost-footing.md](../features/lost-footing.md).

## Findings

Severity is the finder's. "Lens" names the finder. Each row was re-checked against the source, the v1.5.5 cache or the
native decompile before it was entered here.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | HIGH | "Creature" was "carries a `BehaviorTreeAgentComponent`", but Race Abilities attaches that component to every soldier whose race or culture has a profile (on by default), so census lines would read `creature=N soldier=0`. Lens: data flow. | Presence of a shared component used as a type test | The plan's acceptance item 2 defined the kind by the component, from the creature features alone; nobody listed every site that attaches it. | Creature = a non-null tree that is not `RaceAbilityBehaviorTree`; the convergence pass checked all ten tree types. Lesson in `lessons/adapters-taleworlds-api.md`. |
| 2 | HIGH | `Agent.GetTargetPosition()` was read as the AI move destination. It is the movement-lock target (ladders, siege engines) and reads (0,0) for an ordinary soldier, so `toDest` was the distance to the scene origin and the water filter never filtered. Lenses: engine, data flow. | Engine getter used by its name | The plan's engine table named it without reading its body or its callers. | Reads `GetAIMoveDestination()`; the gate uses the formation slot first and logs both. Lesson in `lessons/adapters-taleworlds-api.md`. |
| 3 | MEDIUM | The scan read 64 agents per one-second pass, not per frame as the plan meant, so a sweep took 16 s at 1,000 agents and the "2 s" windows meant two sightings 16 to 24 s apart. Lenses: efficiency, data flow. | Plan constraint outside the acceptance list | "64 per frame" sat in the plan's shape section, not in the acceptance list the builder ticked. Repeat of the Fell Foes lesson on acceptance lists (`lessons/testing-qa.md`). | Per-frame slices; a test reads 640 agents over 64 ticks once each. Lesson in `lessons/testing-qa.md`. |
| 4 | MEDIUM | A removed agent stayed counted as stranded until pruned, up to about 48 s. Lens: data flow. | Stale state | As row 3: the long sweep hid it. | Per-frame slices prune within about 3 s; a test pins it. |
| 5 | MEDIUM | The engine snap was computed for unconfirmed agents, and `snapOnMesh` was described as "on the mesh" though it is a lookup seeded with the agent's own height. Lens: engine. | Measurement semantics | The plan assumed the pair repairs an agent; the engine applies it to the formation position. | Snap only for confirmed agents; the reading key says what the field measures. |
| 6 | MEDIUM | The per-mission wiring test runs engine code without `RequiresGame`, so the no-game run would fail; the Fell Foes twin on trunk had the same gap. Lens: standards. | Test category | Copied from the Fell Foes test, which already lacked it. Repeat of `lessons/testing-qa.md` near line 1656. | Both tagged on the method. |
| 7 | MEDIUM | No test fed a non-finite X or Y before the float-to-int cast of the hotspot cell. Lens: standards. | Engine-float gate untested | Repeat of the NaN-gate rule in `.claude/rules/csharp-architecture.md`. | DataRows for NaN and both infinities on X and Y. |
| 8 | MEDIUM | The owed census runs could not be carried out as written (scene ids instead of picker names, no Battle Size, no log file named, no cost check, no inert checks). Lens: completeness. | Owed checks not executable | Written from the plan's scene list. | Rewritten as executable steps, including `taom.print_battle_scene` and the log path. |
| 9 | MEDIUM | Nothing pinned the module's gate closure: `() => true` stayed green. The Fell Foes twin had the same gap. Lens: completeness. | Wiring unpinned | The gate itself was tested, not its wiring. | Source pins on both modules, proven RED with `() => true`. |
| 10 | MEDIUM | Two display-only engine reads (water level, formation slot) ran on every probe. Lens: efficiency. | Cost | | Read only for slow or face-missing agents. |
| 11 | LOW | Source pins missed the negations; the engine-type scan missed one Domain file; `DistToDestination` defaulted to 20 (passes its gate); enum sizes were hard-coded; the tunables were "validated" but never fed; a dead `|| IsInWater()` term; dead `PreviousSeen`; an unreachable `confirmed=pending`; fast agents had no formation distance; a toggle flip bridged paused time; the gate duplicated Fell Foes' term for term. Lenses: standards, efficiency, data flow, design. | Small defects and shape | | All fixed: pins with negations, a directory scan, NaN defaults, sizes from the enums, compiled defaults, `!IsOnLand()`, deletions, pause closes episodes, the Fell Foes gate reused. |
| 12 | LOW | The summary could not answer the phase 2 question per scene and per kind, or say how episodes ended, and the census cost was unmeasured. Lens: design. | Measurement incomplete | | Summary per scene with recovered, removed and atEnd by kind, a cost line, and the engine's `UnreachableViaNavMesh` flag on samples. |
| 13 | LOW | Doc citations (the cheap test, the snap wording, `GetTargetPosition`) and the issue body were stale. Lenses: engine, completeness. | Docs | | Corrected; #894's body rewritten. |
| 14 | LOW | Convergence: a failed tree build left a null tree, read as a creature. | Null case | | The creature test requires a non-null tree (RED proven). |
| 15 | LOW | Convergence: doc and comment wording (tick profiler needs a restart, what `t=` counts from, pass length at low and high agent counts, stale "validated" and "every scan pass", a settled UNVERIFIED bullet, one line number, and the engine side effects of `GetOrderPositionOfUnit`). | Docs | | Corrected. |

## Root-cause patterns

**The plan carried the errors (rows 1, 2, 3).** In features 1 to 3 most defects came from the build; here the two HIGHs
and the cadence came from the approved plan, and the builder ticked the acceptance list exactly. The architect read the
creature features and the formation code, but not every attach site of the component or the body of the getter it named.
The fix is on the planning side: an engine member or a TAOM type used as a measurement is read at its definition and at
every writer before it goes into an acceptance list.

**Copied tests copy their gaps (rows 6, 9).** The census followed the Fell Foes shape closely, and two test gaps came
with it. Both were fixed in both features.

## Why each lens caught or missed what it did

- **Standards** found the test category, the NaN cast test and the small shape rows (6, 7, 11).
- **Engine compatibility** found the getter (row 2) from the native decompile and settled the cheap off-mesh test.
- **Efficiency** found the cadence and the display-only reads (rows 3, 10) and proved the cheap test does not re-validate.
- **Data flow** found the creature misclassification (row 1) by following the component to every attach site, plus
  rows 2 to 4.
- **Completeness** found the run instructions and the unpinned wiring (rows 8, 9).
- **Design** gave the fix shapes and the summary contents (rows 10 to 12).
- **Convergence** found rows 14 and 15.

## UNVERIFIED until the census runs

- Whether an active agent ever holds movement mode None.
- What `GetAIMoveDestination` holds in water once the formation frame is disabled.
- Whether native ever sets `UnreachableViaNavMesh`.
- The census cost per frame (the summary's cost line measures it).

## Feedback memories to codify

None. The lessons below are rules for the repo.
