# RCA: Oaths of Service (#904, 2026-10-19)

## Top-line

The seven-lens `/deep-review` of Oaths of Service (hire terms, conversation topics and a daily home anchor for
named companions) found 1 HIGH, 7 MEDIUM and 16 LOW findings. All were fixed in the same session. The
HIGH was a data label nobody had checked against the live map: Aragorn's spawn settlement `town_EN2`, documented
as Bree, is Carreglyn in Dunland, and the feature's new `stay_at_home` flag would have pinned him there every
day. The review also found that the plan had narrowed six items of the issue without saying so; Mike chose to
build all six in the same change.

Full lens report: kept in the session scratchpad; the findings that matter are below.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | `named_companion_config.json` homes Aragorn at `town_EN2`, which the live `TAOM_Map/ModuleData/settlements.xml:394` names Carreglyn (Dunland, `Culture.empire`). Bree is `town_AN3` (`:15911`). Four doc lines said "town_EN2 (Bree)". The new `stay_at_home` turned a wrong starting point into a daily pin. | Data never checked against the live module | The label predates this change and the repo's stale `settlements.xml` names the town "Diathma", so nothing in the repo could disagree. The builder reused the existing id when adding `stay_at_home` and did not re-check what the id is. | `NamedCompanionShippedDataTests` now checks every spawn and home settlement id against the live `settlements.xml` (`LiveInstall` category). Lesson in `data-content-cultures.md`. |
| 2 | MEDIUM | The daily anchor moved a named companion who had been promoted to lord. Promotion clears `CompanionOf` and gives the hero an own clan, and `IsRecruitedOrInParty` read only `CompanionOf` and the party. The same gap sat in `NamedCompanionService.EnsureCompanionsPlaced`. | Entity State Matrix incomplete | The matrix in `csharp-architecture.md` lists six states. "Was recruited, then left the player's clan by promotion" is not one of them, and the builder enumerated the list rather than the engine's transitions. | `IsRecruitedOrInParty` also tests `Hero.Clan != null` (an unrecruited named companion has none: heroes.xml uses `Faction.neutral`, which `Hero.Deserialize` never assigns). New matrix row; a source pin on the adapter's clan check. |
| 3 | MEDIUM | Vanilla `CompanionsCampaignBehavior.SwapCompanions` moves unhired wanderers on the same `DailyTickEvent` and runs after TAOM's listener, so it could undo the anchor the same day. | Listener order (repeat) | `state-lifecycle-save.md:10` already says campaign listeners run newest-first. The plan's research looked for vanilla wanderer movers but did not ask which of two daily listeners runs last. | The daily listener raises a flag and the next `HourlyTickEvent` runs the anchor. Tests pin daily-alone, hourly-once and hourly-twice. Lesson in `campaign-mechanics.md`. |
| 4 | MEDIUM | A hire-terms block with `gold: 0` whose only item failed to resolve at session launch, or `gold: 0` alone, rendered "My price is ." and hired the companion for free. | Two-phase validation | `Validate` (load) dropped empty terms, but `ResolveIds` (session launch) emptied the item list later and did not re-apply the rule. Each phase was tested alone. | An unresolved hire item now drops the whole terms block; `gold: 0` without items reverts to the vanilla price; the renderer never returns an empty price and the line falls through to vanilla. Lesson in `testing-qa.md`. |
| 5 | MEDIUM | Nothing failed if the ledger reset in the campaign behavior's constructor was deleted, or if `IOathFlagStore` and `OathLedger` resolved to two instances. The class comment and the doc both said a wiring test pinned the reset; it pinned only the inline `new`. | Claimed pin that did not exist | The wiring test was copied from a sibling that also pins the reset in a separate test, and the claim was written from the copied name. | Source pin on the constructor body, a `RequiresGame` container test (fresh behavior per Create, one ledger instance, flag gone after Create) and a SyncData round trip. Lesson in `state-lifecycle-save.md`. |
| 6 | MEDIUM | No test pinned the dialog wiring that keeps the Wanderer Allegiance gate: 105 between vanilla's 100 and WA's 110, both lines on `companion_hire`, both `Register` calls. | Missing wiring pins | The services had 100 percent coverage, so the hook layer looked covered. WA's own wiring test shows the shape, but nobody looked at it. | Source pins mirroring `WandererAllegianceWiringTests`. |
| 7 | MEDIUM | Topic thresholds read the engine's effective relation, which adds up to plus or minus 4 from personality traits, so `min_relation: 0` hid the topics from a dishonourable player. | Engine getter semantics | `Hero.GetRelation` reads like a stored number. Its body calls `GetEffectiveRelation`, and nobody opened it. | Samples changed (first topic open to all, second at 5); the doc states the trait term and that `relation_gain` is a base value the engine scales. Lesson in `adapters-taleworlds-api.md`. |
| 8 | MEDIUM | No shipped-data test checked that the config's text ids exist, contain no dot (the engine splits GameText ids at the first dot), or that its settlements exist. | Config keys unchecked | `.claude/rules/xml-data.md` asks for a test that every config key resolves; the shipped-config test ran only the load-time validator. | `NamedCompanionShippedDataTests`. |
| 9 | LOW | The lenient JSON parse swallowed syntax errors as warnings and let `"enabled": "no"` fail open. | Error handling | The handler's "empty path means syntax error" assumption was never tested against a syntax error inside an entry. | `JToken.Parse` first, so syntax errors reach the ERROR path; a wrong-typed `enabled` disables the entry; one summary warning. |
| 10 | LOW | Items came back after a hire that threw once the companion had already joined. | Partial failure | The refund test used a stub that threw before any change. | Refund only when the companion did not join. |
| 11 | LOW | The topic reply was the only NPC line on its token, so a false condition would stall the conversation with no exit. | Dialog dead end | Unreachable with today's data; nobody asked what the engine does with no matching NPC line. | Priority-1 fallback line. |
| 12 | LOW | The ledger codec existed on a false premise ("the save system may not define List or Dictionary"); `SaveableBasicTypeDefiner` defines both. | Engine claim not checked | Written from caution, not from the decompile. | Codec deleted; containers synced directly under new keys. |
| 13 | LOW | Topic prompts froze the language at session launch; topic gold rewards were silent; the WA re-check in the hire dialog was redundant by priority; several comments and docs went stale (the WA module comment, the modder guide, a nonexistent "Mithril Ore" example). | Polish and sibling docs | A feature sharing a vanilla dialog token did not update the sibling feature's comments and docs. | Fixed; lesson in `misc.md` on sibling features sharing a token. |

## Process finding: the plan narrowed the issue

Issue #904 asked for a maximum relation, a random chance, item give and take on topics, negative relation
changes, a separate home settlement, and refusing an entry whose item id does not resolve. The approved plan
dropped all six without listing them, and the plan review did not compare the plan with the issue. The
completeness lens caught it. Mike chose to build all six in this change. Lesson in `build-tooling-workflow.md`:
compare the plan with the issue, line by line, before asking for approval, and list every narrowed item.

## Root-cause pattern

Findings 1, 4, 7 and 12 share one shape: a value was trusted for what it looked like (an id's documented name,
a validated block, a getter's name, a cautious comment) instead of what the source says. Each had a cheap
check one file away: the live `settlements.xml`, the second validation phase, the getter's body, the save
system's type definer.

## Why each lens caught or missed what it did

- **Standards (1):** caught the unpinned reset (5) and the redundant WA re-check; its rules do not cover data labels or engine semantics.
- **Engine compatibility (2):** caught the promoted-lord state (2) and the listener order (3), because it read vanilla's daily movers end to end.
- **Efficiency (3):** no defects in its domain; its reorder of the anchor's reads is applied.
- **Completeness (4):** caught the narrowed issue scope and the missing dialog pins (6).
- **Data flow (5):** caught the two-phase gap (4) by tracing a renamed Armory id through both phases.
- **Design (6):** independently found the listener order (3) and the false codec premise (12).
- **XML (7):** caught the HIGH (1) by reading the live map, and the effective-relation thresholds (7).

The builder's own tests missed all of them because each sat at a seam the unit tests fake: the live map, the
engine's event order, the engine's getter, and the second validation phase.

## Feedback memories to codify

None beyond the lessons above. The listener-order trap is already a written rule; the fix is to apply it at plan
time (the lesson says so).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/lessons/adapters-taleworlds-api.md](lessons/adapters-taleworlds-api.md)
- [docs/reviews/lessons/build-tooling-workflow.md](lessons/build-tooling-workflow.md)
- [docs/reviews/lessons/campaign-mechanics.md](lessons/campaign-mechanics.md)
- [docs/reviews/lessons/data-content-cultures.md](lessons/data-content-cultures.md)
- [docs/reviews/lessons/misc.md](lessons/misc.md)
- [docs/reviews/lessons/state-lifecycle-save.md](lessons/state-lifecycle-save.md)
- [docs/reviews/lessons/testing-qa.md](lessons/testing-qa.md)

<!-- backlinks-end -->
