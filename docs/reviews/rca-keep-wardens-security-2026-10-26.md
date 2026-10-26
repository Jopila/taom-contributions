# RCA: Keep Wardens part 2, castle patrol security, roster fit and Guard House priority (#889, #991, #992; deep-review 2026-10-24 to 2026-10-26)

## Top-line

`/deep-review` ran seven lenses (1 Standards, 2 Engine, 3 Efficiency, 4 Completeness, 5 Data flow, 6 Design, 7 XML) on the
uncommitted #889 change in the `feat/recruitment-garrisons` worktree, then one convergence pass after the fix round. Nothing
was HIGH. The first round found 2 MEDIUM and about 25 LOW findings plus one design proposal; the fix round also added a
castle Guard House upgrade listener that Mike approved, which removed a Known Limit carried since #888. The convergence pass
found no runtime defect, two LOW doc errors and a few wording nits, all fixed before commit.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | No test pinned `PatrolAlive: settlement.PatrolParty != null` in the castle patrol adapter. Deleting that line would set every castle's security bonus to 0 with every test green. | Adapter field without a pin | The security tests fed the snapshot by hand, so the adapter line that fills the new field was never exercised. | Adapter source pin plus service tests for "no live patrol, no bonus" at every level. Lesson in `lessons/testing-qa.md`. |
| 2 | MED | The bodies of #889, #991 and #992 still described the plan (#889 promised a "+1" the build does not add). | Issue not reconciled | Issue bodies are written at plan time and nothing re-reads them at merge. | As-built comments on all three issues at merge. |
| 3 | LOW | Design: `GuardHouseIf(town, bool pick)` existed only to do `pick ? Find : null`. | Simplicity | First-draft shape. | Deleted; the model is one ternary over `PicksGuardHouse` and `GuardHouse(town)`. |
| 4 | LOW | A full castle patrol kept its old size limit after a Guard House upgrade (`PartyBase.PartySizeLimit` is cached on `MemberRoster.VersionNo`, and vanilla bumps it only for the town Guard House). | Engine cache not refreshed | #888 accepted it as a Known Limit under a "models only" premise. | `CastleGuardHouseUpgradeBehavior` bumps the version for the castle Guard House (Mike approved 2026-10-26); the convergence pass traced the refill it triggers and found it safe. |
| 5 | LOW | `KeepWardensModule`'s parking comment was stale; followed literally, it left a hook whose resolve throws. | Parking note | The note was written for #888's one model and not updated as models and hooks were added. | The comment lists every model, hook, switch, key and the behavior that must park together. |
| 6 | LOW | Engine wording: the stale value is `PartyBase.PartySizeLimit`; vanilla's line is "Patrolling Parties"; town formulas; the new-game build pass asks `GetNextBuilding` for every fortification, not only AI fiefs; culture-feat limits 25/41/57. | Doc claims from the plan | Written before the engine was read for each caller. | Each corrected against the v1.5.5 cache. |
| 7 | LOW | Stale docs that still said castles have no patrols or 15/20/25 limits: `bandit-management.md`, `party-templates.md`, `party-template-sizing.md`, `troops-and-parties.md`, `INDEX.md`; convergence found one more, `troop-weight-system.md`, and a wrong head count (11, 14 and 14, not 11 at every level) in `keep-wardens.md`. | Doc sweep | The sweep grepped for the feature's own names, not for the facts it changed. | Fixed; lesson below. |
| 8 | LOW | Config parsing rolled its own finite checks instead of `FiniteFloatValidator.IsFiniteInRange`. | Shared helper | Copied from the #888 config. | Switched. |
| 9 | LOW | Tests: a level test named for the wrong level, no `AreSame` for an unchanged roster, no `int.MinValue`, no `{0,5,3}` shape, missing config cross-field cases, a "Both" name, a test text saying 48 templates and 16 families (the data has 54 and 18), two test names that claimed more than they checked. | Test gaps and names | The brief listed decisions, not boundaries. | Each added or renamed. |
| 10 | LOW | Gate wording ("a castle with a patrol or a built Guard House") did not match `ShowsPatrolRow`; the feature-map row, `gamemodels.md` row and the Castle Patrols hint did not mention the roster hook or the security bonus; co-op notes for `GetNextBuilding` and BannerlordCoop's security sync were missing. | Doc completeness | Written from the plan. | Fixed; co-op items marked UNVERIFIED where no evidence exists. |

## Root-cause pattern

Findings 6 and 7 repeat #888's finding 6: docs and comments written from the plan, then swept by name rather than by fact.
A changed fact (castles now have patrols, limits are 25/40/56) lives in docs that never mention the feature.

## Why each lens caught what it did

- **Standards (1):** 8, parts of 9 and the design hint for 3. **Engine (2):** 6. **Efficiency (3):** no issue; one optional pin.
- **Completeness (4):** 1, 2, parts of 7, 9 and 10. **Data flow (5):** 5 and the doc drifts in 7 and 10.
- **Design (6):** 3, and the follow-up that became 4. **XML (7):** strings and config passed.
- **Convergence:** the last stale doc and head count in 7, and wording nits.

## Feedback memories to codify

None. The lessons carry the rules.
