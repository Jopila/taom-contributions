# Provender-master

## Overview

The companion the player names Quartermaster in the clan screen becomes the party's Provender-master and takes standing orders in conversation. Two orders run on their own once given: buy food whenever the party enters or leaves a market, and slaughter livestock when the stores run low. A third choice lifts the host's spirits on the spot, with pay for every soldier or with drink (or meat) shared out, in a settlement or a ready [field camp](field-camp.md).

## Why This Exists

- **Vanilla behavior:** the Quartermaster role only adds its holder's Steward perks. Food is bought by hand at every market. Livestock is slaughtered by hand in the inventory screen; the game kills a beast on its own only when a day's meal has used up every other food, and then only that meal's shortfall, as meat with no hides. Morale moves only with battles, food variety and events.
- **TAOM requirement:** Middle-earth's map is large and its hosts are big, so feeding one is a chore repeated at every town. The role should carry real work, and a lord should be able to reward the host the way the peoples of Middle-earth would: ale for the Rohirrim and the Dwarves, wine for the Elves, meat for an orc warband.
- **Without this feature:** the player buys food by hand at every stop and has no way to spend gold on the host's spirits.

## Architecture

### Design Challenge

- `MobileParty.EffectiveQuartermaster` falls back to the party leader when the role is empty, which is the player. The officer is read from the role itself (`GetRoleHolder(PartyRole.Quartermaster)`) and must be in the party, alive, free and not the player.
- `SellItemsAction.Apply` checks neither the market's stock nor the buyer's gold, so the adapter sizes each purchase to the stock and to what the gold covers at the current price. It makes one call per food type, because every call the player pays shows a gold notice and a coin sound (`DefaultNotificationsCampaignBehavior.OnHeroOrPartyTradedGold`). The price can rise a little with each unit inside the call; `GiveGoldAction` never takes more gold than the player holds, so an overrun is a small discount, never a debt. XP and the message follow what was actually paid.
- The conversation view resolves every option's text after all the conditions have run (`MissionConversationVM` builds its items from `CurOptions`), so each morale line sets its own text variables; a shared name would show the last line's figures on every line.
- The engine has no callable slaughter helper: `FoodConsumptionBehavior.SlaughterLivestock` is private and runs only for a meal that every other food could not cover, and `InventoryLogic.SlaughterItem` needs the inventory screen's rosters. The adapter applies the same yield (`MeatCount` meat and `HideCount` hides per head) to the party roster.
- The engine counts each live head's meat as food (`ItemRoster.TotalFood`), so its days of food (`GetNumDaysForFoodToLast`) do not move when a head becomes meat. The slaughter order measures the food in the stores without the herd: the food items plus the part-eaten unit the engine carries (`RemainingFoodPercentage`), divided by the daily burn.
- A conversation opened from the party screen is reverted by Escape, so every morale purchase commits the open party screen after its writes (the Tokens of Esteem precedent).

### Solution Approach

A feature module with one campaign behavior; no Harmony patch, no GameModel.

- **Food:** on `SettlementEntered` and `OnSettlementLeftEvent` for the main party, at an open market: a town, or a village with a trade town (a castle's village with no trade town in range has none), never a castle, never a village out of its normal state (raided, being raided, or forced for supplies or volunteers), never a settlement that denies the player full access (vanilla's access model: a crime rating at a neutral town; `SettlementEntered` fires before the gate menu shows), and never a settlement at war with the player (the vanilla war test, not alignment: an Evil town at peace still trades). These follow vanilla's trade screen, which also refuses a village with no gold of its own (a sale to the player pays the village, so that rule does not apply here) and lets a disguised hero with Smuggler Connections trade (refused here, to stay on the safe side). The order pays the price `SellItemsAction` charges, with no merchant, as the AI does: at a village that is a little less than the trade screen asks, and at a same-faction town it misses the trade perks' discounts. Each food type the market has in stock is topped up to the eaters (members plus half the prisoners, as the engine counts them) divided by the number of types in stock, rounded up. The budget is the purse less Wage Days Kept days of the party's daily wage, so buying food cannot leave the troops unpaid. Types are bought in market order, each planned against the gold really left and cut to the market's exact total, because a town's price rises with each unit it sells. The officer earns Trade XP of gold/100 and Steward XP of gold/200.
- **Slaughter:** on the daily tick, only when the food in the stores, not counting the herd, lasts fewer than Low Food Days. One head at a time, always from the herd with the most to spare, until it lasts Low Food Days again, and each livestock type keeps Livestock Kept Per Type heads. The gain is meat in the stores before it is needed (food variety), hides, and a smaller herd. The officer earns 10 Steward XP per head.
- **Morale:** in a settlement or a ready field camp. Pay gives every soldier but the player the small or the large tier's gold for its morale, and does not disorganize the party. Drink shares out the ration the host prefers from `rations.json` (the majority troop race first, then the leader's culture, then the default), one item per so many troops, takes it from the stores first, buys any shortfall at the market the party stands in, then adds its morale and leaves the party disorganized. When part of the ration must be bought at an open market, a second drink line states the purchase and the market's exact total before anything is spent. A closed option still lists with its figures, greyed with its reason.
- **Gates:** the MCM switch, a co-op client (`ShouldDeferToHost`) or a dedicated server (its main hero is an idle world-gen hero, and a single-player save can arrive with the orders on), an enlisted player (the lord feeds the host), and a valid officer. Both orders start off and are saved per campaign.

### Component Diagram

```
provender/rations.json            TaomSettings "Provender-master" group
        |                                    |
RationCatalogProvider          ProvenderSettingsProvider (clamps)
        \                                    /
         \                                  /
          ProvenderService (gates, orders, morale) -- ProvenderRules (pure arithmetic)
           |                   |
   ProvenderWorldAdapter    IHeroSkillXpAdapter, IPartyItemRosterAdapter
   (party, market, writes)
           |
   ProvenderBehavior (events, SyncData) -- ProvenderConversationFlow -- ProvenderDialogLines
```

## Configuration

### Config File: `Main/_Module/ModuleData/provender/rations.json`

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | The ration's own id, for log lines |
| `item` | string | The item handed out; an unknown id drops the ration |
| `troopsPerItem` | int, 1 to 1000 | How many troops one item serves |
| `morale` | int, 1 to 30 | The morale it adds |
| `default` | bool | The ration everybody else is given; at most one |
| `races` | string[] | Skin names from `skins.xml` that prefer it; an unknown race is dropped |
| `cultures` | string[] | Culture ids that prefer it; an unknown culture is dropped |

### Current Values

| Ration | Troops per item | Morale | Preferred by |
|--------|-----------------|--------|--------------|
| beer | 7 | +15 | the default; the race `dwarf` and the culture `vlandia` (Rohirrim) |
| wine | 20 | +20 | the race `elf` |
| meat | 5 | +15 | the orc, uruk and troll races |

There is no mead item in the game, so there is no mead ration. Beer and meat are also food, so a handout of either eats into the stores.

### MCM: "Provender-master" (GroupOrder 70)

| Setting | Default | Range |
|---------|---------|-------|
| Enable Provender-master | on | |
| Low Food Days | 3 | 1 to 30; days of food in the stores, not counting the herd |
| Livestock Kept Per Type | 2 | 0 to 20; the game itself still slaughters when every other food is gone |
| Small Pay: Gold Per Soldier / Morale Gained | 50 / 5 | 10 to 500 / 1 to 30 |
| Large Pay: Gold Per Soldier / Morale Gained | 100 / 10 | 10 to 500 / 1 to 30, never read below the small tier |
| Wage Days Kept | 3 | 0 to 30; the food order never spends this many days of wages (0 = the whole purse) |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/Provender/ProvenderService.cs` | Gates, the two orders and the morale options |
| `Main/Features/Provender/ProvenderRules.cs` | Pure arithmetic: food targets, purchase and slaughter plans, XP, ration choice |
| `Main/Features/Provender/ProvenderOrders.cs` | The two standing orders |
| `Main/Features/Provender/ProvenderConversationFlow.cs` | The dialog conditions and consequences |
| `Main/Features/Provender/Hooks/ProvenderBehavior.cs` | Campaign events and `SyncData` (two bools: `_taom_provender_autoFood`, `_taom_provender_slaughter`) |
| `Main/Features/Provender/Hooks/ProvenderDialogLines.cs` | The conversation tree; its hub token `taom_provender_orders` takes further orders |
| `Main/Features/Provender/RationCatalogProvider.cs`, `ProvenderSettingsProvider.cs` | The validated rations and MCM values |
| `Main/Features/Provender/Models/ProvenderFacts.cs`, `Models/RationCatalog.cs` | The party and settlement facts, the morale quote and block reasons; the validated rations |
| `Main/Features/Provender/ProvenderSettings.cs`, `IProvenderSettingsProvider.cs`, `IRationCatalogProvider.cs` | The clamped settings record and the provider seams |
| `Main/Features/TaomSettings.cs` | The "Provender-master" MCM group |
| `Main/Features/Provender/ProvenderModule.cs` | Registration (one line in `Main/Composition/FeatureModules.cs`) |
| `Main/Adapters/IProvenderWorldAdapter.cs`, `ProvenderWorldAdapter.cs` | Party and market facts, purchases, slaughter, pay, morale, messages |
| `Main/_Module/ModuleData/provender/rations.json` | The rations |

## Dependencies

- `IHeroSkillXpAdapter`, `IPartyItemRosterAdapter` (Enlistment registrations): officer XP, ration stock
- `ICampService` (Field Camp): whether the player's camp is ready
- `IRaceManager`: race names for troop counts and ration validation
- `ICoopSessionProvider`, `IDedicatedServerProvider`, `IEnlistmentStateQuery`: the co-op, dedicated-server and enlistment gates

## Tests

- `TAOM.Tests/Features/Provender/ProvenderRulesTests.cs`: the arithmetic, including zero, overflow and non-finite food burn
- `ProvenderServiceFoodTests.cs`: buying and slaughter, one test per skip state
- `ProvenderServiceMoraleTests.cs`: pay and drink, every closed state, short writes give no morale
- `ProvenderConversationFlowTests.cs`, `RationCatalogProviderTests.cs` (its shipped-file test reads the installed `skins.xml`: `LiveInstall`), `ProvenderSettingsProviderTests.cs`
- `ProvenderBehaviorTests.cs`, `ProvenderWorldAdapterTests.cs` (`RequiresGame`): the save round trip and the greyed reasons; no engine object kept in a singleton
- The settings provider is a row in `CampaignHotPathSettingsProvidersTests`
- `ProvenderWiringTests.cs`: module registration, the per-campaign behavior, dialog ids and keys, the role read
- The co-op row is in [coop-interop.md](coop-interop.md).

## How to Add a Ration

1. Add an entry to `rations.json` with an item id that exists in the game.
2. List the races (skin names) and cultures that prefer it.
3. Restart the game. No code change is needed; an unknown id is dropped with a log line.

## Changelog

- 2026-10-23: first version: food and slaughter orders, pay and drink morale options (#908).

## GitHub Issue

- **Issue:** #908, [Provender-master orders](https://github.com/haterade22/TAOM/issues/908)
- **Status:** Open
