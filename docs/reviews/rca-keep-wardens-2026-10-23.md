# RCA: Keep Wardens, castle guard-house patrols (#888, deep-review 2026-10-23)

## Top-line

`/deep-review` ran seven lenses in two waves (1 Standards, 2 Engine, 5 Data flow, 7 XML; then 3 Efficiency, 4 Completeness,
6 Design) on the uncommitted #888 change in the `feat/recruitment-garrisons` worktree, against `247e04278`. All four
first-wave lenses found the same HIGH independently: turning "Castle Patrols" off mid-campaign could crash the hourly
campaign tick through a live castle patrol's refill. It was fixed before commit, with 3 MEDIUM and about 15 LOW findings.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | Vanilla `PatrolPartiesCampaignBehavior.ReplenishParty` (v1.5.5 :112) asks `GetPartyTemplateForPatrolParty` for a live patrol without asking `CanSettlementHavePatrolParties`. With the setting off, the castle plan refused, the model fell through to base, base returns null for every castle, and `FindAppropriateInitialRosterForMobileParty` threw at `partyTemplate.Stacks.Count`. TAOM's own size hook kept the refill reachable: under vanilla's limit of 0 the ratio check blocks it. | One gate applied to every model answer | The plan and the build treated the three model members as one decision gated by the setting. Nobody listed every engine caller of each member and what it assumes; the refill caller skips the "may have" check. | `Plan` now separates `Strength` (any castle with an owner-culture template) from `Allowed` (new patrols only: switch, loyal owner, built Guard House); tests pin switch off, level 0 and rebel owner. New lesson in `lessons/gamemodels-services.md`. |
| 2 | MED | `TaomPartySizeModel` resolved its new hook with `??= IoC.Resolve` inside the hot override body, on worker threads too. | Service locator in an override | The plan chose a lazy resolve to avoid a constructor parameter; a constructor-body resolve needs no `SubModule.cs` edit either. | Resolved once in the constructor, `readonly`; test pins it. |
| 3 | MED | Tests that construct `ExplainedNumber` had no `RequiresGame` tag and fail on a reference-assembly run. | Test category | The implementer ran only with the game installed. | Tagged. |
| 4 | MED | The issue's data test (every castle-owning culture resolves its patrol templates) was not written, and a fixture comment still said the patrol model requires `IsTown`. | Issue test list not reconciled | The plan said "no data work" and dropped the test with it. | `ShippedCastleOwnerCultureTests` (LiveInstall); comment updated. |
| 5 | LOW | The MCM hint said "a new patrol every 10, 8 or 6 days"; the engine keeps one patrol at a time and replaces a lost one after that delay. | Player text from the plan, not the engine | The plan described the spawn duration without reading its caller. | Hint, registered default and 12 seeds rewritten. |
| 6 | LOW | Docs: the town-window Patrol row is TAOM's Reeve's Ledger, not vanilla; new games spawn seeded castle patrols at once; `patrol_party_mordor` is TAOM data; castle patrols add no security until #889; patrol templates spawn at each stack's maximum, so many exceed or fall short of the 15/20/25 limits; BannerlordTogether not covered; "previous model" wording; "lazily" wording; the refill was called a daily check. | Doc claims | Wording written from the plan. | Fixed; the template head counts are a Known Limit and a pending follow-up. |
| 7 | LOW | A wiring test passed vacuously when `Plan(` was absent (IndexOf -1); no container validation of the new hook; a duplicated `-1` sentinel; untested adapter mapping; config overflow and path-throw paths and a NaN size case untested; test names without the method segment. | Test gaps | The brief listed decision tests, not the boundaries around them. | Each gap has a test now. |
| 8 | LOW | Design: a redundant `HasOwnerClan` field, template flags read separately from the template getter, hand-written structs, a self-assigning ternary. | Simplicity | First-draft shape. | Folded into one mapping and record structs. |

| 9 | LOW | Convergence pass: parking the module keeps the size hook alive without the model, which reopens finding 1's crash for live castle patrols; "daily" and "hourly" were swapped in two doc lines (one by the orchestrator's own blind replace); "calls base only when the plan has no strength" was false for two of the three overrides; head counts per level were understated; the castle-owner data test read clan cultures before `spclans.xslt`; a constructor-resolve test checked less than its name. | Doc and test accuracy | Text rewritten in the fix round without re-reading each caller; a mechanical replace applied without checking each hit. | Fixed; parking note added to the feature doc and the module comment. |

## Root-cause pattern

Finding 1 repeats the shape of #918's lesson from three days earlier (a gate must cover every engine consumer of the
resource): the design reasoned from the members TAOM overrides, not from every engine call site of each member.

## Why each lens caught what it did

- **Standards (1), Engine (2), Data flow (5), XML (7):** all four traced the refill path to the null template (finding 1).
  Standards added 2, 3 and 7; Engine and Data flow added 5 and 6; XML verified all 156 castles' templates and added 4.
- **Efficiency (3):** no hot-path cost beyond finding 2. **Completeness (4):** issue bodies, follow-ups, doc sections.
  **Design (6):** finding 8 and the shape of the finding 1 fix.

## Feedback memories to codify

None. The lesson carries the rule.
