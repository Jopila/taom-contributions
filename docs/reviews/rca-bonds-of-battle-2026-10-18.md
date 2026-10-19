# RCA: Bonds of Battle (deep review, 2026-10-18)

## Top-line

The `/deep-review` of Bonds of Battle (#861) ran seven lenses in two waves on the uncommitted worktree
`E:\repos\taom-relations-politics`: 1 Standards, 2 Engine compatibility, 5 Data flow and 7 XML, then 3 Efficiency,
4 Completeness and 6 Design. Tooling was not in scope. It found one HIGH defect (bandit gratitude never fired after a
hideout clear), about six MEDIUM findings and a set of LOW ones. Every finding below was checked against the code or
the installed v1.5.5 engine before it was fixed. A convergence pass on the fix round found two MEDIUM and four LOW defects (finding 11), also fixed.

Six findings change who earns relation, so they are design decisions for the maintainer and stay open on #861:
the army exclusion (every member of an AI-led army the player joined earns nothing), the per-lord dedup (the engine
lands player relation on the clan leader), the floor of 1 point per Done on the Donate Troops screen (splitting a gift
farms relation), notables of enemy realms thanking the player, the side leader that vanilla already thanks also
getting the bonus, and grants that report success whatever the engine kept.

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | A hideout battle puts the hideout's own settlement `PartyBase` on the defender side (`PlayerEncounter.SetupFields(MainParty, Settlement.CurrentSettlement.Party)`, `MapEvent.Initialize`). It is not a bandit party, so "only bandits opposed" was false in every hideout battle (Lenses 5 and 2) | An engine-shape assumption: "every party in a map event is a mobile party" | The adapter derives the eligibility flags, and no test reaches the adapter; the service tests fed hand-built snapshots that could not contain a settlement party | The adapter skips parties without a `MobileParty`. Hideouts are skipped explicitly (`MapEvent.IsHideoutBattle`) because vanilla `HideoutCampaignBehavior.SetCleanHideoutRelations` already pays +2 to one notable per nearby village. Lessons in `campaign-mechanics.md` and `testing-qa.md` |
| 2 | MED | The barter path had no enlistment gate, although the MCM hint says the whole feature does nothing while enlisted (Lenses 1, 5, 2) | A binding rule applied per entry point instead of per feature | The plan named the gate for the battle path; the barter model was added later as a separate seam | `BarterOverpayService` returns null (vanilla) while enlisted, with a test |
| 3 | MED | The hand-over path had no co-op gate. The installed co-op module also replaces `PartyScreenLogic.DoneLogic` with its own bool prefix, and on a client `ChangeRelationAction` does nothing, so the client could show a "thanks" line for no change (Lenses 2, 5) | A co-op gate copied to one entry point only | `coop-interop.md` says to gate every handler that reaches the mutation; the feature had three entry points and one gate | `TroopHandoverService` defers to the host; `RelationGrantService` refuses on a dedicated server, so every later consumer of the seam inherits it. Host-only table row added |
| 4 | MED | No test drove `BondsOfBattleBehavior`'s gates (Lens 4) | Hook coverage | The wiring test only constructed the behavior | `BondsOfBattleBehaviorTests` (8 tests) on an uninitialized `MapEvent` under `RequiresGame` |
| 5 | MED | `Patch105` is also claimed by two other uncommitted worktrees and by an old MapViewRelease note (Lens 4) | Parallel sessions sharing a number space | Each session picks "the next free number" from the same base | Renumber at the rebase before merge; the same applies to MCM `GroupOrder 57` (also used in the B04 worktree) and the shared counts |
| 6 | LOW | Two log lines ("is grateful") cannot be gender-agreed in six languages, and the translator's placeholder gate stops a later fix (Lens 7) | Localization design | English text written without the translation pipeline's constraints in mind | Reworded to a neutral verb ("thanks you for") in the C# literal, the registration row and all 12 seeded rows before the paid run |
| 7 | LOW | A wiring test built a campaign behavior without `[TestCategory("RequiresGame")]`; one patch test asserted nothing; the player-clan rule lived in the patch, out of reach of tests (Lens 1) | Test hygiene | Builder self-check did not compare against sibling wiring tests | Split and tagged; the empty test deleted; the refusals moved into `TroopHandoverService` with tests |
| 8 | LOW | `affectRelatives` was carried through the new seam, but v1.5.5 `ChangeRelationAction.ApplyPlayerRelation` never forwards it (Lenses 2, 5, 6) | A dead parameter on a shared interface | Copied from the engine signature without reading the body | Removed from `IRelationGrantService` and `IHeroRelationAdapter` |
| 9 | LOW | Doc and comment errors: "the one place every relation change goes through" (three older callers and vanilla's barter write bypass it); "fighting beside a lord changes nothing in vanilla" (the thank-you conversation pays the side leader); "vanilla is 3" (Tribute makes 4); "bandits who fell" (casualties include wounded and routed); influence "only on Donate Troops" (the garrison screen too); `CampaignVec2.IsValid()` called a NaN guard (it is a nav-face terrain test); a stale settings count in two docs (Lenses 1, 2, 4, 5, 6) | Claims written before the engine read | The feature doc was drafted from the plan, not from the decompile | Each sentence corrected against the engine; `IsValid()` call deleted (the distance test already rejects NaN) |
| 10 | LOW | Parity-preserving simplifications: settings read twice per battle; a two-file perk adapter and a by-id relation read for values the barter model already holds; two redundant participant flags (Lenses 3, 6) | Over-built boundary | First-draft shape | Applied: one settings read, the model reads relation and Tribute at its boundary, the flags deleted |
| 11 | MED | Convergence pass: the radius doc claimed 25 units "never reaches across a border" (fortifications of different kingdoms lie 14 units apart on the live map, which makes the enemy-notable decision look moot); the new lesson stated the pending per-clan dedup as a standing rule; plus a test that could not fail, a stale diagram label, an inaccurate cost hint and three unused usings | A claim copied from another doc without checking its scope; a review outcome written as a rule before the maintainer decided | The radius claim quoted figures that army-targeting.md gives for three kingdom pairs only | Sentences corrected; the lesson now says the choice is open on #861; the test also asserts no error is logged |

## Root-cause pattern

Findings 1, 3 and the open design items share one cause: the feature was designed from its own event outward, not
from what vanilla already does at that event. Vanilla pays the hideout notables, pays the side leader the player came
to help, and lands every player relation change on a clan leader. Each was visible in the decompile and none was read
before the rules were written. The second cause is test reach: the eligibility flags were derived in an adapter that
no test reaches, so a green suite proved nothing about the engine's real map-event shapes.

## Why each agent missed these

- **The builder** wrote service tests over snapshots it built itself, so the snapshots matched its own assumptions.
- **No lens missed a confirmed finding**: every finding above came from the review. Lens 3 found no performance
  issue; the event, the model call and the patch all run at battle or user-action frequency.

## Feedback memories to codify

None. The lessons below carry the rules.
