# Codex pre-review: Harvest Road (#912), 2026-10-26

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` and `.ai/review-reference.md` first. Scope is the uncommitted working tree of this
worktree on top of `069962aa4` (`git diff` and `git status`; new files are untracked: 
`Main/Features/HarvestRoad/`, `TAOM.Tests/Features/HarvestRoad/`, `docs/features/harvest-road.md`).
Engine source for v1.5.5 is in `C:\Users\mikew\.taom-src\v1.5.5\` (for example
`TaleWorlds.CampaignSystem.Actions.SellGoodsForTradeAction.cs`,
`TaleWorlds.CampaignSystem.CampaignBehaviors.PartiesBuyFoodCampaignBehavior.cs`,
`SandBox.ViewModelCollection.Nameplate.SettlementNameplateNotificationsVM.cs` and
`CaravanTransactionNotificationItemVM.cs`; find the exact file names with a directory listing).
Mark anything you cannot prove from code as UNVERIFIED.

## The change

Vanilla raises `CampaignEventDispatcher.OnCaravanTransactionCompleted(party, town, items)` only for
caravans (SellGoodsForTradeAction.cs:64, CaravansCampaignBehavior). Its only listener is the
settlement nameplate notification list, which shows a "sold" or "bought" pop-up over the town.
Harvest Road (Patch120) raises the same event for two more trades:

- `SellGoodsForTradeAction.ApplyByVillagerTrade(Settlement, MobileParty)`: a prefix snapshots the
  villager's item roster into `__state`, a postfix diffs it and raises the lines that LEFT the
  roster (positive counts).
- `PartiesBuyFoodCampaignBehavior.BuyFoodInternal(MobileParty, Settlement, int)` (private): the same
  snapshot and diff; raises the lines that ARRIVED (negative counts). Gated on amount > 0.

Both are gated on an MCM toggle (default on), not the main party, and `settlement.Town != null`. The
notice item picks "sold" or "bought" from the sign of the first line, so each raised list carries one
sign. Files: `Main/Features/HarvestRoad/Hooks/HarvestRoadPatches.cs`, `HarvestRoadService.cs`,
`TradeNoticeDiff.cs`, `HarvestRoadModule.cs`, settings and tests. Co-op: no gate; BannerlordCoop
forwards this event from the server to its clients.

## Questions to answer with evidence

- Listeners: is every subscriber of `OnCaravanTransactionCompletedEvent` (engine, SandBox, StoryMode,
  TAOM Main and Client) safe with a non-caravan party (villager, lord party, bandit, a party with no
  owner, no leader hero, no map faction)? Does any listener or the notification item dereference
  something that is null for these parties?
- Signs and merging: the nameplate list merges a new transaction into an existing notification for
  the same party (`GetUpdatableNotificationByPredicate` / `AddNewItems`). Can a merge mix a sold list
  and a bought list in one pop-up and show a wrong word or a wrong count? Is that worse than vanilla's
  own caravan behaviour (a caravan sells and buys in the same visit)? A caravan also buys food through
  `BuyFoodInternal`: does that now duplicate or contradict a vanilla caravan notice?
- Diff correctness: does the roster diff equal what the vanilla call moved, with item modifiers kept
  apart, for each path (partial sale, whole stack, a new stack, a stack topped up)? Can anything else
  change the roster between prefix and postfix (a nested call, an event handler inside
  `SellItemsAction.Apply`, a TAOM patch on the same methods such as Patch68)?
- Exceptions and Harmony: can a prefix or postfix throw into the trade, or skip vanilla? Are the
  parameter names bound correctly (`villagerParty`, `mobileParty`, `settlement`,
  `numberOfFoodItemsNeededToBuy`)? Is `__state` correct with `out` in the prefix and a plain parameter
  in the postfix?
- Cost: `TryBuyingFood` runs hourly for every party; what does the gate cost when it fails, and when
  it passes? Any allocation on the hot path when the toggle is off?
- Co-op and dedicated server: any path that crashes on a dedicated server (no nameplate VM) or that a
  client runs locally? Any client-only assembly named in core code (ADR-013, ServerJitGateTests)?
- Settings, registration and counts: does any count gate, classification or test break?

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact, and the
proving code or a reproduction. Say PASS for a question you checked and found sound.
