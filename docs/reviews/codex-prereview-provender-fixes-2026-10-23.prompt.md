# Codex fix review: Provender-master (#908) review fixes, 2026-10-23

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` first. Scope is the uncommitted working tree of this worktree on top of `c2988ac3d`
(the feature files are untracked; read them whole). Engine source for v1.5.5 is in
`C:\Users\mikew\.taom-src\v1.5.5\`. Mark anything you cannot prove from code as UNVERIFIED.

An earlier pass (`docs/reviews/raw/codex-prereview-provender-2026-10-23.md`) and three other
reviewers found these defects. Verify that each fix is right and complete, and that no fix broke
something else. Do not re-review unrelated parts of the feature.

## The fixes

1. **Rising town prices (HIGH).** New `IProvenderWorldAdapter.CostOf(settlement, item, count)` in
   `Main/Adapters/ProvenderWorldAdapter.cs`: for a town it sums
   `TradeItemPriceFactorModel.GetPrice(element, main, null, false, InStoreValue - k * item.Value,
   Supply, Demand)` for k = 0..count-1 from `town.MarketData.GetCategoryData(item.GetItemCategory())`;
   for a village it is the first price times the count (a village sale moves no price). -1 when the
   market cannot sell that many. `ProvenderService.QuoteDrink` quotes `CostOf`, and the food order cuts
   each line with a binary search (`AffordableCount`) to what the budget left covers.
2. **Slaughter measured the herd as food (HIGH).** `ItemRoster.TotalFood` counts each live head's
   meat, so `GetNumDaysForFoodToLast` never moves on a slaughter. The facts now carry `EdibleFood` =
   the food items + `RemainingFoodPercentage / 100f`, and `ProvenderRules.PlanSlaughter` takes food
   units and the daily burn (no int truncation).
3. **Wage reserve (MEDIUM, the maintainer chose it).** New MCM `ProvenderWageDaysKept` (default 3,
   0 to 30); `ProvenderRules.FoodBudget(gold, dailyWage, days)` with `MobileParty.TotalWage`.
   Morale purchases are not limited. Settings counts bumped to 415 / 296 / 445.
4. **Market rule.** One `IsOpenMarket` for the food order and the drink shortfall: town, or village
   with `TradeBound` (no `?? Bound` fallback), never a castle, no village whose `VillageState` is not
   `Normal` or that is under raid, not at war with the player.
5. **Smaller:** an inactive or captive main party reads as no party; the order toggles re-check the
   gate; the dead `ProvenderBlock.OrderOff` is gone; the shipped-rations test checks races against
   the installed skins.xml files.

## Questions

- Is `CostOf` exactly what `SellItemsAction` will charge (merchant null, isSelling false, the same
  category data, float vs int in-store value, the item category extension, village vs town)? Any
  overflow or performance problem for large counts (the binary search calls it log2(n) times)?
- Is the edible-food figure right for the main party (signs, the hunger carry, livestock exclusion)?
- Does any path now spend more than its budget or quote, or refuse a purchase it should allow?
- Did the `TradeBound`-only market break a village that should trade (town-bound villages)?
- Any test that now asserts a state the engine cannot produce.

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact and proof.
Say PASS for each fix you verified.
