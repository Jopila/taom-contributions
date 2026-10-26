# Harvest Road

## Overview

Over a town's nameplate on the campaign map the base game shows small trade pop-ups, but only for caravans. Harvest Road raises the same pop-up when a villager party delivers its goods to a town and when an AI lord's party buys food there, so the grain of the Pelennor and Lossarnach visibly rolls into Minas Tirith. One MCM switch, default on. Display only: no trade, price, gold or stock changes.

## Why This Exists

- **Vanilla behavior:** `SellGoodsForTradeAction.ApplyInternal` raises `CampaignEventDispatcher.OnCaravanTransactionCompleted` only when the seller `IsCaravan` (`SellGoodsForTradeAction.cs:64-66`). Villager deliveries and AI food purchases (`PartiesBuyFoodCampaignBehavior.BuyFoodInternal`, `:56`) move goods and gold in town with no feedback on the map.
- **TAOM requirement:** the village economy is tuned (see [settlement-economy.md](settlement-economy.md)), and a player scouting a town should be able to see the supply arriving.
- **Without this feature:** the economy works unseen; only caravans announce themselves.

## What the player sees

Above a town's nameplate, the same pop-up that appears for a caravan trade, a portrait of the party's leader and a line such as "Sold Grain x12 Hides x4", now also appears for a villager's delivery and for a lord's food purchase ("Purchased"). Pop-ups follow the base game's own rules for which nameplates show them, how long they stay and when a second trade by the same party joins the first.

## Architecture

### Design Challenge

- The notice is a vanilla view-model listener (`SettlementNameplateNotificationsVM`, `OnCaravanTransactionCompleted`) that matches the town and the party and never checks `IsCaravan`, so raising the event is all it takes. The listener picks "sold" or "bought" from the sign of the FIRST line (`CaravanTransactionNotificationItemVM`), so every list raised carries one sign.
- Neither vanilla seam reports what it moved, so each is bracketed by a roster snapshot and a diff.
- The event is named for caravans. No TAOM listener exists, but a future listener must not treat it as caravan-only: the party may be a villager or a lord.

### Solution Approach

Two Harmony prefix and postfix pairs (`Patch120_HarvestRoad`, GameInit), one pure diff and one small service:

| Piece | Role |
|---|---|
| `TradeNoticeDiff.Compute` | `before - after` per key, generic, no engine types. Duplicate keys are summed, lines that net to zero are dropped, order is stable, a delta past the int range is clamped, a null side gives an empty result. Positive = left the roster (sold), negative = arrived (bought). |
| `HarvestRoadService` | the gates (`ShouldWatch`, `ShouldWatchFoodPurchase`) and the sign filter (`Sold`, `Bought`). A list with the wrong sign is trimmed; an empty list raises nothing. |
| `HarvestRoadPatches` | the two patch classes (`VillagerSalePatch`, `FoodPurchasePatch`), the roster snapshot (keyed by `EquipmentElement`, so two modifiers of one item stay two lines) and the raise call. Every body is in try/catch with a one-time warning: a fault never reaches a trade. |
| `HarvestRoadModule` | registrations, the category, `InitializeStatics`. One line in `Main/Composition/FeatureModules.cs`; `IoC.cs` and `SubModule.cs` are untouched. |

| Seam | Gate | Raises |
|---|---|---|
| `SellGoodsForTradeAction.ApplyByVillagerTrade(Settlement settlement, MobileParty villagerParty)` | switch on, not the main party, `settlement.Town != null` | the lines that left the villager, positive |
| `PartiesBuyFoodCampaignBehavior.BuyFoodInternal(MobileParty mobileParty, Settlement settlement, int numberOfFoodItemsNeededToBuy)` | switch on, not the main party, `settlement.Town != null`, amount above 0 | the lines that arrived, negative |

`BuyFoodInternal` runs hourly for every food-consuming AI lord party in a town or village (the gate drops villages), mostly with an amount of 0 (a stocked party), so the prefix takes no snapshot unless the amount is above 0. `int.MinValue` (what a `NaN` float cast gives) is not a purchase either. The villager seam's only vanilla caller is `VillagerCampaignBehavior.OnSettlementEntered` (`:324-330`), for towns only.

TAOM's Patch68 diagnostics prefix and postfix already sit on `ApplyByVillagerTrade`; both pairs coexist (Harmony runs each, neither skips the original).

**Castles.** The plan included castles. In v1.5.5 neither seam ever reaches one: the villager call is inside `if (settlement.IsTown)` and `TryBuyingFood` accepts `settlement.IsTown || settlement.IsVillage`, and a castle is neither. The gate is `Town != null`, so a castle would work if a later version or another mod sent one, but today only towns show the notices. Villages are out of scope.

## Configuration

No config file. MCM "Map UI/Settlement Nameplates" (`GroupOrder = 40`), "Harvest Road notices" (`EnableHarvestRoadNotices`, `Order = 7`, default on, `RequireRestart = false`). The provider takes the MCM object once through a lazy accessor (a row of `CampaignHotPathSettingsProvidersTests`) and reads it live, so a change applies at the next trade. Text keys `taom_hroad_mcm_enable` and `taom_hroad_mcm_enable_hint` are registered in `taom_module_strings.xml` and seeded in English in all 12 language files; the paid run (#763) translates them.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/HarvestRoad/HarvestRoadModule.cs` | feature module: registrations, category `Patch120_HarvestRoad` at GameInit |
| `Main/Features/HarvestRoad/TradeNoticeDiff.cs` | the roster diff (pure, generic) |
| `Main/Features/HarvestRoad/HarvestRoadService.cs` | gates and sign filter (pure; a concrete class, nothing fakes it) |
| `Main/Features/HarvestRoad/IHarvestRoadSettingsProvider.cs`, `HarvestRoadSettingsProvider.cs` | the MCM switch |
| `Main/Features/HarvestRoad/Hooks/HarvestRoadPatches.cs` | the two patch classes, snapshot helper, raise call |
| `Main/Features/TaomSettings.cs` | `EnableHarvestRoadNotices` |
| `Main/Features/CoopInterop/CoopSettingsRelevance.cs` | the setting is classified as presentation |

## Dependencies

- `IModLogger` for the one-time fault warning.
- `TaomSettings` (MCM) for the switch.
- Engine: `CampaignEventDispatcher.OnCaravanTransactionCompleted`, `SellGoodsForTradeAction`, `PartiesBuyFoodCampaignBehavior`, `ItemRoster`. Names only `TaleWorlds.CampaignSystem` and `TaleWorlds.Core` (ADR-013), so a dedicated server loads it.

## Co-op

No gate. Coop already forwards `OnCaravanTransactionCompleted` from the server to every client, so the server's switch decides for all peers; a client's own switch changes nothing it can see. The patches write no campaign state and skip nothing (`CoopVetoClassificationTests` needs no entry), and the setting is classified as presentation in `CoopSettingsRelevance`. See [coop-interop.md](coop-interop.md). The forwarding was read in the Coop source when the plan was written; no co-op session has run this build.

## Tests

- `TAOM.Tests/Features/HarvestRoad/TradeNoticeDiffTests.cs`: unchanged roster, partial sale, whole stack sold, new item bought, stack topped up, duplicate keys summed, zero lines dropped, null sides, stable order, modifiers kept apart, comparer, int range.
- `TAOM.Tests/Features/HarvestRoad/HarvestRoadServiceTests.cs`: every gate alone (switch, main party, no town, amount 0, negative, `int.MinValue`), the sign filter both ways, empty and null diffs.
- `TAOM.Tests/Features/HarvestRoad/HarvestRoadPatchTests.cs`: a body that cannot do its work leaves the trade alone and warns once.
- `TAOM.Tests/Features/HarvestRoad/HarvestRoadBindingTests.cs` (`BindingVerification`, `RequiresGame`): both targets resolve with the names Harmony injects by, the patch methods' parameter names, the dispatcher entry point shape, every engine member the bodies name, the category attaching one prefix and one postfix per target and the JIT accepting them, the bodies still reaching the raise and snapshot helpers.
- `TAOM.Tests/Features/HarvestRoad/HarvestRoadWiringTests.cs`: module listed once, category and phase, services resolve, `IoC.cs` and `SubModule.cs` untouched.
- `TAOM.Tests/Features/HarvestRoad/HarvestRoadSettingsProviderTests.cs`: default on, the MCM attributes.
- Binding gate: `HarmonyPatchBindingTests`, `PatchCategoryNumberTests`, the regenerated `taleworlds-api-snapshot/patch-targets.md`.

## How to Retune

Nothing to tune. To silence the pop-ups, turn the switch off. A saved `TAOM.json` keeps its own value for a setting; a fresh install gets on.

## Engine bump

Re-check, against the previous version: that `SettlementNameplateNotificationsVM` still matches on `town.Owner.Settlement` without testing `IsCaravan`, that `CaravanTransactionNotificationItemVM` still reads the sign of the first line, the two target signatures and their parameter names, and whether `TryBuyingFood` now admits castles.

## Changelog

- 2026-10-26: first version (#912).

## How to verify in game

Owed; none of these has been run.

- Watch a large town (Minas Tirith) for an in-game day: villager deliveries show a "sold" pop-up, and lords buying food show a "bought" pop-up. The figures match the party's roster change.
- A caravan's own trades still show exactly once (no duplicate).
- Switch the setting off: no villager or food pop-ups, caravans unchanged.
- The player's own party buying food shows nothing new.
- A large map-event or many parties in one town: no frame hitch from the hourly check.
- No exceptions in the logs (`[HarvestRoad]` lines).
- Co-op: the pop-ups appear on a client from the server's trades (not yet run).

## GitHub Issue

- **Issue:** #912, settlement trade notices for villagers and AI food buyers
- **Status:** Open until the in-game checks are done.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/features/settlement-economy.md](./settlement-economy.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/harmony-patch-registry.md](../reference/harmony-patch-registry.md)

<!-- backlinks-end -->
