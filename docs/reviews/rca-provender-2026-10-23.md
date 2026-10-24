# RCA: Provender-master (#908), deep review of 2026-10-23

## Top line

The tier 1 review of the Provender-master (Codex at ultra, then Codex at high on the fixes, and eight Claude lenses in three
waves) confirmed 2 HIGH findings, 10 MEDIUM and about 25 LOW, after the builder had already fixed 5 defects of its own
before the review. Every confirmed defect is fixed with a test; two LOW proposals were declined with reasons (below). The
two HIGHs share one root: an engine figure was used without reading how the engine builds it. One of them repeats a lesson
recorded three days earlier (Lost Footing, 2026-10-20), so this RCA asks for a stronger gate than another lesson line.

Final state (after wave 3): full suite 22,584 pass; the one failure is base drift (the branch predates trunk `e339df075`, which adds the
Cardolan and Rhudaur Hall of Fire rows).

## Findings

Severity is the highest any reviewer gave. "Self" is the builder's own check before the review.

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | The slaughter order measured `GetNumDaysForFoodToLast`, but `ItemRoster.TotalFood` counts each live head's meat, so a slaughter never moved the figure: the order would fire every day until each herd sat at its keep level | Engine aggregate read without its definition | The plan read `GetNumDaysForFoodToLast` and stopped; nobody opened `ItemRoster.OnRosterUpdated`. The test fixture faked days of food without a herd, a state the engine cannot produce | Lesson (campaign-mechanics); repeat of the Lost Footing lesson, so a gate proposal below |
| 2 | HIGH | The drink purchase quoted the first unit's price times the count, but a town's price rises with each unit inside one `SellItemsAction` call, so the line showed less gold than it took and the gate let a too-small purse through | Bulk engine action with per-unit semantics | The plan read the action's signature, not its loop; the fixture priced every unit flat, so no test could see a rise | Lesson (adapters-taleworlds-api); a rising-price fixture (`UnitPrices` fake) |
| 3 | MEDIUM | The food order could spend the whole purse, so the next wage tick failed (unpaid wages, morale loss) | Missing reserve in an automatic spend | The plan copied the reference rule ("paid from the leader's gold") without asking what an automatic spend should never take | Mike's decision: "Wage Days Kept" (default 3); lesson (campaign-mechanics) |
| 4 | MEDIUM | Whole engine days truncated the slaughter start (29 food at 10 a day read as 2 days) | Integer engine figure used as a measurement | Same as 1 | Fixed with 1 (food units) |
| 5 | MEDIUM | After the first fixes, a plan made up front at first prices starved a later food type that the remaining gold could still buy | Plan made before the real cost is known | The fix for 2 cut each line but kept the up-front allocation | The service plans each type against the gold really left; test `TopUpFood_ALaterTypeIsPlannedAgainstTheGoldReallyLeft` |
| 6 | MEDIUM | Adding fractional days per head in float took an eleventh head where ten were enough | Float accumulation in a stop rule | The fix for 1 kept a float sum | Meat is counted in whole units against the food still wanted |
| 7 | MEDIUM | The shipped-rations test mocked the race list it claimed to check against the live game | Test that cannot fail | The builder mocked `IsValidRaceName` with the same hand-copied list as the JSON | The test reads the installed `skins.xml` (`LiveInstall`) |
| 8 | MEDIUM | Issue #908's title and body still described the pre-build plan | Completion duty | The body was written at filing time and nobody owned updating it | Body and title rewritten to what ships |
| 9 | MEDIUM | The behavior's save path and the greyed reasons had no executing test | Coverage of Hooks | The builder's tests stopped at the flow; the Gift precedent's `GiftBehaviorTests` was not mirrored | `ProvenderBehaviorTests` (RequiresGame) |
| 10 | MEDIUM | `ReadOffer` had no caller after the quote moved to the exact cost | Dead adapter member | Left behind by the fix for 2 | Deleted |
| 11 | MEDIUM | The settings provider was missing from `CampaignHotPathSettingsProvidersTests` | House gate not joined | Nothing points a new provider at that hand-listed gate | Two rows and a counting test |
| 12 | MEDIUM | The food order priced each line 4 to 7 times (a binary search over a summed cost) | Redundant engine calls | The fix for 2 chose a search over a cost function | One pricing pass: `UnitPrices` and a prefix scan |
| 13 | LOW | A village with no trade town bought at its castle's prices (`TradeBound ?? Bound`) | Engine fallback misread | The plan's rule was "a village with a trade town"; the code added a fallback | `TradeBound` only |
| 14 | LOW | A village forced for supplies or volunteers still sold; the drink shortfall ignored war and raids | Two market checks, neither vanilla's | The food rule and the drink rule were written separately | One `IsOpenMarket` rule for both, from `VillageState` |
| 15 | LOW | Several small items: the dead `OrderOff` value, a stale settings count, a stray blank line in 12 language files, a missing `LiveInstall` category, a silent drop for a malformed race list, a missing NaN test for the new gate, a test asserting a castle that is also a town, int arithmetic and overflow in the cost, the midpoint overflow, comment and doc wording, the INDEX row | Hygiene | Ordinary slips across a large first build | Each fixed with its test or edit |
| 16 | MEDIUM | The market rule ignored whether the settlement lets the player in: `PlayerEncounter.Init` raises `SettlementEntered` before the gate menu, so a neutral town that bars a player with a crime rating still sold food | Rule written from the obvious cases, not from vanilla's | The plan listed castle, raid and war; nobody read `DefaultSettlementAccessModel` or when the enter event fires | `RefusesPlayer` fact from vanilla's own access model; tests for the food order and the drink shortfall |
| 17 | LOW | The economy diagnostics texts said `SellItemsAction` is AI-only; the Provender-master is the first player-side caller | Another feature's claim made wrong by this change | No reviewer of the first waves looked outside the feature's files | Three texts updated (ledger comment, patch comment, two docs) |
| 18 | LOW | A test fake quoted rising prices but charged a flat price, so a later type was planned against gold that did not exist | Test that cannot fail (pattern B) | The `Buy` fake kept its flat default when a test re-stubbed the prices | The test charges what its prices say and asserts no later purchase |
| 19 | LOW | Comment and doc wording after the round of improvements: stale save keys in the feature map, the castle-village reason, the save-load reason, two members with no pinning test | Hygiene | Ordinary slips in a refactor round | Fixed; `ListsFromStores` truth table and a two-site role-read pin |
| S1 | Self | Buying one unit per call would show one gold notice and coin sound per unit | Bulk engine action with per-unit semantics | Found by reading `DefaultNotificationsCampaignBehavior` before the review | One purchase per food type |
| S2 | Self | A single-player save hosted on a dedicated server could run the orders | Co-op gating | The plan gated on `ShouldDeferToHost` only, though the trap index says co-op loaded, authority and dedicated server are three questions | `IsDedicatedServer` gate |
| S3 | Self | The three morale lines shared text variables, and the conversation view resolves option text after all conditions | Dialog text resolved late | Found by reading `MissionConversationVM` | Per-line variables and a test that no two lines share one |
| S4 | Self | A closed morale line showed zeros; the drink line spent gold without saying so | UX data gap | Found in a self-read | Figures kept on closed quotes; a second, priced drink line |

Not applied, with reasons: plural item names in the drink lines (markup in 13 files for a small wording gain; translators
can phrase around it), splitting `ProvenderFacts.cs` by type (a file-layout preference with precedent for bundles), dropping
the first-price cap (it bounds the price scan; the gain rests on an untested property of the price model), and removing the
settings provider's internal constructor (the hot-path counting test now uses it).

## Root-cause patterns

**A. An engine figure used without reading how the engine builds it (findings 1, 2, 4, 13).** Days of food, a sell action's
total and a village's market town were each taken from a name and a signature. The definitions were one file away:
`ItemRoster.OnRosterUpdated` adds herd meat to `TotalFood`; `SellItemsAction.ApplyInternal` re-prices each unit and the town
re-stocks each unit through `RosterUpdatedEvent`; `Village.TradeBound` already returns the bound town when it is a town.
Finding 1 is the second instance in three days of the rule "read an engine getter's body and its callers before measuring
with it" (`lessons/adapters-taleworlds-api.md`, Lost Footing, 2026-10-20).

**B. Test fakes that cannot reproduce the engine (findings 1, 2, 7, 15).** A flat-price `Buy` fake, days of food without a
herd, a mocked race list and a castle that is also a town all made a wrong rule pass. A fake for an engine quantity must be
able to take every shape the engine gives it.

**C. A rule checked against the plan's list instead of the engine's rule (findings 14, 16).** The market rule was
reviewed three times against "castle, raided, at war" before anyone read the vanilla access model and the order in which
the enter event and the gate menu run.

**D. A fix that kept the old shape (findings 5, 6, 10, 12).** Each fix for a HIGH solved its case and left a new defect in
the shape it kept. The fix-pass Codex run and the second wave caught them, which is the case for re-reviewing HIGH fixes.

## Why each reviewer missed what it missed

- **Codex (ultra):** found 2 and the truncation part of 1, but not that the herd is counted as food; it read
  `GetNumDaysForFoodToLast` and not `ItemRoster`.
- **Lens 5 (Data flow) and Lens 2 (Engine):** found 1 independently; Lens 5 rated 2 MEDIUM. Neither saw 5 or 6, which the
  fixes introduced after they reported.
- **Lens 7 (XML):** found 7; the arithmetic was outside its files.
- **Second wave (Lenses 1, 3, 4, 6):** ran on the fixed code and found 8 to 12 and most of 15; they were not in scope for
  the first wave by tier.
- **Third wave (Lens 5 and Lens 2 on the HIGH fixes, and the convergence pass):** found 16 to 19. Finding 16 predates the
  fixes: every earlier reviewer checked the market rule against the plan's list, not against vanilla's access model.

## Preventive actions

- Lessons appended: `lessons/campaign-mechanics.md` (herd counted as food; an automatic spend keeps a reserve),
  `lessons/adapters-taleworlds-api.md` (a bulk action prices and notifies per unit; conversation text resolves after all
  conditions), `lessons/testing-qa.md` (a fake for an engine quantity must take every shape the engine gives it).
- **Applied on Mike's word (2026-10-24):** because pattern A repeated within three days, it is promoted from a lesson to
  a standing rule: a row in `.claude/rules/csharp-architecture.md` "Non-Negotiable Rules": for every engine figure a rule
  measures or gates on, the plan cites the line that builds it (not only its getter) and one vanilla caller.
