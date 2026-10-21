# RCA: co-op safety nets, Batch A (#965), 2026-10-21

**Summary.** Batch A shipped five co-op "safety net" changes (commit `6ce20f6ed`, pushed before the
review finished). The six-lens `/deep-review` found that two of them could freeze a co-op battle,
and that three rested on premises v1.5.5 does not support. Four were reverted, and the fifth (R2)
was narrowed. Every change came from rows of the gap ledger
([coop-compat-gap-ledger.md](../research/coop-compat-gap-ledger.md)), which was copied that morning
from an outside register written against Bannerlord 1.4.8. Nobody checked those rows against
TAOM's own RCAs or the v1.5.5 engine before building.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | Under co-op, R4 (SaveShield `SpawnTroop`, `SetMissionMode`) and R5 (Patch69) rethrew from inside `OnMissionTick`. `Mission.OnTick` sets `tickCompleted = false` (`Mission.cs:3749`) before the behavior loop, and only the agent tick sets it back (`:3622`), so the next frame's `WaitTickCompletion` (`:3594-3599`) waits forever. A cosmetic fault became a hang | Guard tightening without tracing the landing frame | The ledger action said "rethrow", and the builder brief repeated it. Nobody asked where the exception would land. The `tickCompleted` hazard was already written down (`lessons/harmony-il.md`, the 2026-10-03 updates), but phrased for a swallow, not for a swallow turned into a rethrow | New lesson in `harmony-il.md`; one line in `.claude/rules/harmony-patches.md` (Finalizer) |
| 2 | MEDIUM | R1 retried a failed co-op bind every 5 s. BannerlordCoop loads GameInterface and Common in its own `OnSubModuleLoad` (`NoHarmonyLoader.cs:74-80`, `CoopMod.cs:103,176`), so the first campaign-time bind succeeds, and a bind fails only on an API rename, where a retry never heals. Under BannerlordTogether it added a lock and a clock read to every authority check for the whole process | Defensive code for an unevidenced case | The ledger row took the outside register's timing rule ("IsServer is false at OnSubModuleLoad") as a load-timing fact for the assemblies too. Nobody read Coop's loader | Reverted. Ledger header now says to check every row against TAOM's RCAs and the installed engine |
| 3 | MEDIUM | R9 removed pass-1 PatchShield finalizers when pass 2 saw co-op, on the premise that pass 1 runs before the module list is filled. On v1.5.5 `Module.Initialize` fills it (`Module.cs:301`) before `LoadSubModules` (`:307`); the live log shows pass 1 scanning 12 active modules | Refuted premise re-imported | `docs/reviews/rca-coop-veto-surface-2026-08-01.md:115-119` had already called the premise superstition, but the comments at `Dependencies/SubModule.cs:221-224` and `:284-286` still state it, and the ledger sweep read code comments, not RCAs | Reverted. Stale comments: fixed in the same commit |
| 4 | MEDIUM | R5's rationale (a swallowed tournament-end NRE hides a co-op divergence) is wrong: winner, prize, leaderboard and `OnTournamentFinished` all run before the event (`TournamentBehavior.cs:245-264`), so the swallow costs only the local winner panel | Rationale not checked against the caller | Same as 3: the register's claim was taken on trust | Reverted |
| 5 | MEDIUM | R2 rethrew type-load failures for co-op assemblies under any co-op module, matched generic names (`Common`, `Missions`), and missed co-op's runtime `AutoSync` assembly (`AutoSyncBuilder.cs`), which co-op scans with no catch (`AutoSyncPatcher.cs:35,48`) | Incomplete enumeration of the target set | The builder listed assembly names from the csproj files, which cannot show an assembly compiled at runtime | Narrowed: BannerlordCoop id required, `AutoSync` added, stack trace kept, `SaveDefinerCollisionGuard` keeps partial types. Residual: the narrowing did not remove name matching, so while the BannerlordCoop module is enabled another mod's assembly named `Common`, `Missions` or `GameInterface` also loses the swallow |
| 6 | LOW | Both new rethrows returned `__exception` bare, which erases the throw site (Harmony emits `throw`, not `rethrow`) | Known rule not applied | `harmony-patches.md` states the rule, but the builder brief did not point at it | R2 uses `RethrowStackPreserver`; the other rethrow is gone |
| 7 | LOW | The rewritten "IsServer missing" bind branch had no test | Missing branch test | The new tests covered only "both types present" or "neither" | `CoopSessionProviderBindTests` now holds four bind tests: IsServer missing, a finder that throws, a failed bind is not rescanned, and both types present with no warning. The branch where `TryGetContainer` itself is missing still has none |

## Root-cause pattern

Findings 1 to 5 share one cause: **a defect claim from an outside source was treated as verified
input.** The register was good evidence of what someone saw on 1.4.8. It was not evidence of what
v1.5.5 does, and in one case TAOM had already disproved it. The ledger was written as "verified
against our code", but the check confirmed that the code matched the claim's description. It did
not check that the claim's premise held.

Finding 1 adds a second cause: **a change that makes a guard stricter has a blast radius at its
landing frame, not at its own line.** "Make the failure visible" is only safe when the next catcher
shows it to the player. Here the next catcher was Patch37 on `Module.OnApplicationTick`, which
swallows after the tick window has been left in a state that hangs the next frame.

## How the review lenses found these

Nothing was missed by the review. These lenses found the defects that the build phase missed:

- **Data Flow (5)** and **Engine compatibility (2)** each traced the rethrow to its landing frame
  independently and found the hang (1). Engine compatibility also refuted the R9 premise (3) and
  the R5 rationale (4), and found the `AutoSync` gap (5).
- **Completeness (4)** found the R9 contradiction with the TAOM RCA (3) and the stale ledger.
- **Efficiency (3)** found the cost of R1 and showed that it fell only on sessions where the retry
  cannot succeed (2).
- **Standards (1)** found the bare rethrows (6) and the untested branch (7).
- **Design (6)** applied the simplicity criterion per item and gave the revert and narrow shapes.

The build phase missed them because the orchestrator's builder brief passed the ledger's actions on
as specifications. It did not pass them on as hypotheses to check first.

## Feedback to codify

- **Lesson** (`lessons/harmony-il.md`): before a swallow becomes a rethrow, follow the exception to
  its next catcher and name the frame it leaves.
- **Lesson** (`lessons/misc.md`): an outside defect register is a list of hypotheses. Check each
  premise against TAOM's RCAs and the installed engine before planning a fix.
- **Rule line** (`.claude/rules/harmony-patches.md`, Finalizer): the landing-frame check above.

## Process note

Commit `6ce20f6ed` was made outside the session that built Batch A, while its review was running. It
bundled the Batch A files with 33 banner packages and other sessions' harness files, and its subject
has no version label. It is pushed, so it stays. The corrections ship in a new commit that carries
the release note for #965.
