# RCA: Personal Relations (deep review, 2026-10-19)

## Top-line

The `/deep-review` of Personal Relations (#862) ran six lenses in two waves on the uncommitted worktree
`E:\repos\taom-relations-politics` (on top of 2fdbf1399): 1 Standards, 2 Engine compatibility, 4 Completeness and
5 Data flow, then 3 Efficiency and 6 Design (both resumed after a usage-limit stop). XML and Tooling were not in scope.
It found one HIGH defect (the larger disband cost could remove a player companion for good), five MEDIUM findings and a
set of LOW ones. Every finding below was checked against the code or the installed v1.5.5 engine before it was fixed.
The maintainer approved six behaviour changes. A convergence pass on the fix round found two MEDIUM and two LOW
defects, also fixed.

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | The member disband cost (13 to 17) could remove a companion: AI armies can call player-clan companion parties (`DefaultArmyManagementCalculationModel` excludes only the main party, the faction leader and `!CanJoinArmy`), `DisbandArmyAction` charges every non-main party leader, and `CompanionRolesCampaignBehavior` removes a companion when a negative change leaves relation below -10 (Lens 2) | A negative relation change routed to heroes the plan never listed | The plan read the cost's only caller, not who can be in the army it loops over, nor what listens to a negative change | Members keep vanilla's -1; only the army leader (never a player-clan hero) pays 28 to 32. Lesson in `campaign-mechanics.md` |
| 2 | MED | Seeding then succession counted the clan's relation twice: `ChangeClanLeaderAction` gives the heir his raw relation plus 0.7 x the old leader's, for every hero including the player (Lenses 5, 2) | A one-time data migration not traced through later engine writers of the same store | The seed was designed against readers of the relation store, not its writers | `GetRelationChangeAfterClanLeaderIsDead` returns 0 for the player while active. Lesson in `state-lifecycle-save.md` |
| 3 | MED | Co-op: seeding reached only the host's hero; with the co-op mod loaded even solo nobody was seeded; the co-op module patches `IsHumanPlayerCharacter` into a party scan, which the override evaluated before the switch on every relation read (Lenses 3, 4, 5, 2) | Hot-path override reading an engine property another module patches | The plan assumed the vanilla getter's cost | The feature engages only in single player after the save is seeded (one volatile flag set on the main thread); the switch is read first; the player is compared with `Hero.MainHero` |
| 4 | MED | Turned on mid-session, personal writes before the next daily tick defeated the "exactly 0" seed test; a new game set the flag on day 0, so a save played with the feature off was never seeded (Lens 5) | Gate order between a migration and the rule it prepares | The seed and the pair rule were gated separately | The rule engages only once seeded; a switch seen off re-arms the seed |
| 5 | MED | Clan-level writes (blood feud -100, execution penalties, raid and battle aftermath) still name only the clan leader, so the rest of a feuding clan keeps its relation (Lenses 2, 5) | Engine code that uses a clan leader to stand for the clan | Not visible from the two overridden methods | Documented as a known limit, with `OnBloodFeudStateChangedEvent` as the follow-up path; B08 told that its clan penalty shrinks |
| 6 | MED | The pair rule's "involves the player" decision sat in the model with no test (Lens 1) | Rule 4 of `gamemodels.md` | A one-operator decision looked like boundary conversion | `Resolve(effective1IsPlayer, effective2IsPlayer)` in the service with a full matrix test; IL pins on both overrides |
| 7 | MED | Convergence: a co-op host still seeded during a live session, against the docs; a test helper caught `FileNotFoundException`, which `tests.md` forbids (convergence pass) | A gate copied from an older order; a workaround that broke a written rule | The first guard was not rewritten when "single player only" was approved; the namespace filter did not work under RefAsm and was patched over | First guard is `!IsSinglePlayer`, with host tests; the test is tagged `RequiresGame` as the rule says |
| 8 | LOW | Emissary and companion dealings still landed on the clan leader (Lens 2) | The rule tested the original heroes, not the effective ones | Read from the issue text, which names only the player | The rule tests the effective heroes and un-maps only the other side |
| 9 | LOW | Wrong claims: "worker threads" (every relation read is on the main thread in v1.5.5), "no allocation" (false under co-op), "Off restores vanilla" (seeded raw values stay and vanilla's raw readers use them), the votes and defection sentence, "when a clan leader dies" (also a clan change and a king election), a sign error (Lenses 1, 2, convergence) | Claims written before the engine read | The plan's risk text was copied into code comments | Each sentence corrected against the engine |
| 10 | LOW | Parity-preserving clean-ups: one gate read first, one fault boundary in Patch106, three duplicate tests, a roll clamp that guarded only a fake, the seed written through `Hero.SetPersonalRelation` (Lenses 1, 6) | First-draft shape | Builder self-check | Applied |

## Root-cause pattern

Findings 1, 2 and 5 share one cause: the feature changed who owns a relation, and the plan checked only the two
methods it overrode. Every engine path that writes or reacts to a relation (a negative-change listener, a succession
loop, a feud) kept its old assumption. Finding 3 is the same blind spot for cost: an engine property looked cheap
because its vanilla body is cheap, while another installed module replaces it.

## Why each agent missed these

- **The builder** worked from the plan, which named the overrides and their single callers but not their downstream
  listeners and writers.
- **No lens missed a confirmed finding.** The convergence pass caught the two defects the fix round introduced.

## Feedback memories to codify

None. The lessons carry the rules.
