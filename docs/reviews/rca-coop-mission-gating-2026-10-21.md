# RCA: co-op mission gating and per-player earning (#967 C1 + C7), 2026-10-21

**Summary.** C1 moved special-resource earning to the co-op authority and to the heroes the co-op
registry marks as players. C7 classed every TAOM mission behavior as Local, AgentOwner or Unsafe,
dropped the six Unsafe ones in a session and added ownership checks where a behavior acts on agents.
Both landed in `8ae036bc5`, an editor auto-commit whose subject and body named only the C1 tests. A
six-lens `/deep-review` ran on C7, a convergence pass ran on its fixes, and a Codex pass (gpt-6-astra,
xhigh) ran on C1 and C7 together. No CRITICAL defect was found. The review's main result was a
deletion: five of the new ownership checks repeated a decision an existing gate already makes, and
the commit's docs described the plan, not the code.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | Every ownership check in a session allocated a closure and a delegate (88 bytes); creature trees ask every frame (Efficiency lens, measured on the game's runtime) | Allocation on a hot path | The lambda captured `mission`; nobody read the compiled IL of a per-frame call | Fixed: a lambda that captures nothing; pin that the source has no `() => mission` |
| 2 | HIGH by the ADR table | `BannerBearerAssignmentMissionLogic.cs` grew to 155 lines (ADR-002) | Entry point growth | The check was added to a file already at 150 | Fixed: the check was deleted (finding 5) and the culture scan moved to `FormationBannerHeraldry`; 141 lines |
| 3 | HIGH, process | `8ae036bc5` carries C1 and C7 with no release note and no issue link | Commit outside the pipeline | The editor's Commit and Sync committed the working tree mid-review | The follow-up commit carries both release notes and #967; Mike asked not to use Commit and Sync during a run |
| 4 | MEDIUM | The creature-bandit rout backstop read the raw engine controller with no session gate, on a premise ("a dying agent may already be deregistered") the engine contradicts; it also skipped an owned mount during co-op's 2 s synthetic turn (Data flow, Engine lenses) | Wrong premise for a guard | The comment's reason was not checked against `Mission.OnAgentRemoved` | Fixed: deleted, because a puppet's origin has no supplier and `SetRouted` does nothing (`CoopAgentOrigin.cs:139-147`); pinned ungated |
| 5 | MEDIUM | Five checks repeated an existing gate: four morale paths already pass `DreadAgentGate.CanAffect` (AI-controlled human with a `CommonAIComponent`), Smart Cavalry reads only the player team, and banner promotion needs `IsAIControlled` (Design lens) | Check added without asking which gate already decides | Each site was gated by itself from the plan's table | Fixed: deleted; each invariant has a pin; lesson below |
| 6 | MEDIUM | An unused battle-host API was compiled in the same `try` as the ownership probe, so a host-bind failure would also switch off ownership (Standards, Engine, Data flow) | Dead code sharing a failure scope | Written "for the CultureDoctrine fallback", which nothing used | Fixed: deleted (YAGNI) |
| 7 | MEDIUM | Completeness gaps: ledger row C8 stale, six of eleven planned in-game checks missing, no co-op line in the gated features' docs | Paperwork not finished | The commit happened before the docs pass | Fixed: ledger, numbered owed list (10 checks), `siege-dismount.md`, `enlistment.md` |
| 8 | MEDIUM | The fix pass's docs reversed the failure behaviour ("a bind failure answers owned"), named three callers where there are eight, and gave the wrong reason for siege dismount (convergence pass) | Docs written from the plan, not the code | The docs agent wrote from the plan's wording while the code agent changed the behaviour | Fixed; lesson below |
| 9 | MEDIUM | `OnDailyTick` applied the earning policy only in a session, so a dedicated server with no session would tick its placeholder MainHero (Codex S4) | Policy applied on one branch | C1 moved the single-player branch verbatim and gated only the new session branch | Fixed test-first: `OnDailyTick_DedicatedServerWithoutSession_DoesNotProcess` |
| 10 | MEDIUM, follow-up | After a battle-host migration the new host adopts elephant and mumakil crew as AI with no seat link: crews are spawned and seated only on the owner, and co-op's spawn message has no seat identity (Codex F1) | Lifecycle across peers | The lenses asked which peer may act, not what an owned object keeps when its owner leaves | Recorded in `coop-interop.md` as a known limit and an owed check; the fix is a seat relay (#967 C3) |
| 11 | LOW | The binding test loads the real Coop DLLs into the shared test AppDomain, so a later test that assumes Coop is absent would see it (convergence) | Test isolation | The test was added without asking what it leaves behind | Fixed: handler removed in `ClassCleanup`; the absent-type test turns inconclusive when the type is loaded |
| 12 | LOW | Smaller items, all fixed: one process-wide warn latch (now per mission, re-armed only after the behavior list was read, Codex S3); doc claims about the tree cadence, heal-on-kill and `OnAgentBuild`; no binding test for the co-op members (now `CoopMissionBindingTests`, run against the installed v0.1.6); a missing `RequiresGame` tag on a class that hashes real agents; `CoopSessionProvider` version note; provenance cell with two values | Various | Various | Fixed in the follow-up |

## Root-cause patterns

**A check before the invariant (findings 4, 5, 6).** The plan listed every site that acts on an agent
and put a check at each one. It did not first ask what each site's existing gate already decides. Four
of the morale sites already required an AI-controlled human with a morale component, which a co-op
puppet never is. A check that repeats a gate costs a registry lock per target and hides the real
reason in a comment. The design lens found this by reading each gate before its new check.

**Docs from the plan, not the code (findings 8, 7).** The fix pass ran a code agent and a docs agent in
parallel, both from one plan. The code agent followed the code where the plan was wrong (a bind
failure falls back to the controller, not "owned"); the docs agent copied the plan. Parallel work from
one plan is fine for disjoint files, but a doc that states behaviour must be checked against the final
code before the commit.

**One branch of two (finding 9).** Moving a method verbatim and adding a new branch beside it left the
old branch without the new policy. The C1 earning paths for battles and raids used one `Eligible` gate
for both session states; the daily path did not.

## Why each agent missed what it missed

- **Standards (lens 1):** found findings 2 and 6; its scope does not include runtime cost or call-site
  invariants.
- **Engine compatibility (lens 2):** found finding 4's wrong premise and the mount window; it verified APIs,
  not whether a check was needed.
- **Efficiency (lens 3):** found finding 1 by building the call chain and measuring it.
- **Completeness (lens 4):** found findings 3 and 7; it does not read engine code.
- **Data flow (lens 5):** found finding 4 and the redundancy in finding 5, as LOW; it traced each site,
  not the shared gate.
- **Design (lens 6):** turned finding 5 into deletions.
- **Convergence pass:** found finding 8 and 11; it read the docs against the final code.
- **Codex:** found findings 9 and 10, and a latch detail. It walked one lifecycle no lens walked (the owner
  leaving mid-battle) and read past a policy to its call sites. It did not report the closure (finding 1),
  which needs the compiled IL.

## Lessons recorded

- `docs/reviews/lessons/campaign-mechanics.md`: "An ownership change lists what each owned object keeps
  only on its owner" and "Before adding a check at a call site, read the gate the site already has".
- `docs/reviews/lessons/testing-qa.md`: "A moved method with a new branch applies the policy on every
  branch" and "A doc that states behaviour is checked against the final code, not the plan".
