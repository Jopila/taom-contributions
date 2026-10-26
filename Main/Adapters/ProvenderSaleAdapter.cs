using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.Execution;
using TAOM.Features.Provender;
using TAOM.Features.Provender.Models;

namespace TAOM.Adapters;

/// <summary>
/// The campaign side of the Provender-master's remote sale (#909, docs/features/provender.md "Remote selling"). Keeps no engine
/// object between calls (a process singleton); one open screen keeps its own town in its closure. Main thread only.
/// </summary>
/// <remarks>
/// The screen is vanilla's trade screen (the shape of <c>InventoryScreenHelper.OpenScreenAsTrade</c>) with an empty merchant
/// roster, so the player can only sell, and buy back what was sold this session at the price it fetched. Gold, from
/// <c>InventoryLogic.DoneLogic</c> (v1.5.5): the player is paid from nothing (:552); inside a settlement the engine then calls
/// the screen's listener, which debits the chosen town (:585-587, <c>CurrentSettlementComponent</c> is
/// <c>Settlement.CurrentSettlement</c>, :393); outside one the engine debits no one when no encounter is live (:589-600, the
/// service refuses a camp with an encounter), so the close step debits the town by what the player gained. The sold goods
/// then go into the town's own stock.
/// </remarks>
public sealed class ProvenderSaleAdapter : IProvenderSaleAdapter
{
    private readonly IModLogger _logger;

    public ProvenderSaleAdapter(IModLogger logger) => _logger = logger;

    public SideIds PlayerSide => FactionSides.OfPlayer();

    public IReadOnlyList<SaleTownFacts> ReadTowns()
    {
        var towns = new List<SaleTownFacts>();
        try
        {
            var main = Campaign.Current?.MainParty;
            var playerFaction = Hero.MainHero?.MapFaction;
            if (main == null)
                return towns;
            foreach (var town in Town.AllTowns)
            {
                var settlement = town.Settlement;
                if (settlement == null)
                    continue;
                var distance = DistanceHelper.FindClosestDistanceFromMobilePartyToSettlement(
                    main, settlement, MobileParty.NavigationType.Default, out _, out _);
                towns.Add(new SaleTownFacts
                {
                    Id = settlement.StringId,
                    Name = settlement.Name?.ToString() ?? settlement.StringId,
                    Distance = distance,
                    Gold = town.Gold,
                    IsUnderSiege = settlement.IsUnderSiege,
                    AtWarWithPlayer = playerFaction != null && settlement.MapFaction != null && settlement.MapFaction.IsAtWarWith(playerFaction),
                    RefusesPlayer = ProvenderWorldAdapter.RefusesPlayer(settlement),
                    IsCurrent = settlement == main.CurrentSettlement,
                    Side = FactionSides.Of(settlement.MapFaction),
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Provender] ReadTowns failed: {ex.Message}");
            towns.Clear();
        }
        return towns;
    }

    public bool OpenScreen(string townId, int feePercent, Action<SaleOutcome> onClosed)
    {
        var manager = Campaign.Current?.ConversationManager;
        if (manager == null || string.IsNullOrEmpty(townId) || Settlement.Find(townId)?.Town == null)
            return false;
        // Open the screen once the talk has ended (the vanilla pattern at VillagerCampaignBehavior.cs:501-505), so it never
        // opens over a conversation that is still running.
        manager.ConversationEndOneShot += () =>
        {
            try
            {
                Push(townId, feePercent, onClosed);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Provender] Opening the sale screen for '{townId}' failed: {ex.Message}");
            }
        };
        return true;
    }

    private void Push(string townId, int feePercent, Action<SaleOutcome> onClosed)
    {
        var settlement = Settlement.Find(townId);
        var town = settlement?.Town;
        var main = MobileParty.MainParty;
        var player = Hero.MainHero;
        if (town == null || main == null || player == null)
            return;

        var state = Game.Current.GameStateManager.CreateState<InventoryState>();
        // Load-bearing: any other mode leaves the map bar's inventory exit open, and that exit runs DoneLogic without the close
        // step below (GauntletInventoryScreen ApplyChanges), so in a camp the player would be paid and the town never debited.
        state.InventoryMode = InventoryScreenHelper.InventoryMode.Trade;
        var sold = new ItemRoster();
        var logic = new InventoryLogic(settlement!.Party);
        logic.Initialize(sold, main.ItemRoster, main.MemberRoster, isTrading: true, isSpecialActionsPermitted: false,
            CharacterObject.PlayerCharacter, InventoryScreenHelper.InventoryCategoryType.None, new FeeMarketData(town, sold, feePercent),
            useBasePrices: false, state.InventoryMode, settlement.Name);
        var listener = new TownSaleListener(town);
        logic.SetInventoryListener(listener);
        var goldBefore = player.Gold;
        state.InventoryLogic = logic;
        state.DoneLogicExtrasDelegate = () => onClosed(Settle(town, sold, listener.Settled, player.Gold - goldBefore));
        Game.Current.GameStateManager.PushState(state);
    }

    /// <summary>
    /// Settles the town's gold when the engine did not, and moves the sold goods into the town's stock, one stack at a time, so
    /// one failure cannot stop the rest. A stack that cannot be moved is logged as lost: the player has been paid for it, so it
    /// belongs to the town, and handing it back would let the same goods be sold again. The party screen under the
    /// conversation is committed whatever happens, or Escape there would bring back the goods the player was paid for.
    /// </summary>
    private SaleOutcome Settle(Town town, ItemRoster sold, bool engineSettled, int playerGain)
    {
        var units = 0;
        try
        {
            var change = ProvenderSaleRules.TownGoldChange(engineSettled, playerGain, town.Gold);
            if (change != 0)
                town.ChangeGold(change);
            foreach (var element in sold.ToList())
            {
                if (element.Amount <= 0 || element.EquipmentElement.Item == null)
                    continue;
                if (Deliver(town, element))
                    units += element.Amount;
            }
            sold.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Provender] Settling the sale at '{town.Settlement?.StringId}' failed after {units} unit(s): {ex.Message}");
        }
        finally
        {
            PartyScreenCommit.CommitIfMainParty(_logger, "Provender");
        }
        return new SaleOutcome(units, playerGain);
    }

    /// <summary>Moves one sold stack into the town's stock; when that throws, logs the paid stack as lost.</summary>
    private bool Deliver(Town town, ItemRosterElement element)
    {
        try
        {
            town.Settlement.ItemRoster.AddToCounts(element.EquipmentElement, element.Amount);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Provender] Moving {element.Amount} {element.EquipmentElement.Item?.StringId} to '{town.Settlement?.StringId}' failed; the sold stack is lost: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// The town's own prices, with the Provender-master's cut taken off what the town pays; buying is untouched. In a vanilla
    /// trade screen the merchant roster is the town's own, so every unit sold raises the category's store value and lowers the
    /// next price (<c>TownMarketData.OnTownInventoryUpdated</c>). This screen sells into a roster of its own, so the value of
    /// what it has taken so far is added here, and the price falls exactly as it would in the town. The rest mirrors
    /// <c>TownMarketData.GetPrice</c> (v1.5.5 :124-133), whose item overload drops the merchant party.
    /// </summary>
    private sealed class FeeMarketData : IMarketData
    {
        private readonly Town _town;
        private readonly ItemRoster _sold;
        private readonly int _feePercent;

        public FeeMarketData(Town town, ItemRoster sold, int feePercent)
        {
            _town = town;
            _sold = sold;
            _feePercent = feePercent;
        }

        public int GetPrice(ItemObject item, MobileParty tradingParty, bool isSelling, PartyBase merchantParty) =>
            GetPrice(new EquipmentElement(item), tradingParty, isSelling, null!);

        public int GetPrice(EquipmentElement itemRosterElement, MobileParty tradingParty, bool isSelling, PartyBase merchantParty)
        {
            var category = itemRosterElement.Item.GetItemCategory();
            var data = _town.MarketData.GetCategoryData(category);
            var store = data.InStoreValue + SoldValue(category);
            var price = Campaign.Current.Models.TradeItemPriceFactorModel.GetPrice(
                itemRosterElement, tradingParty, merchantParty, isSelling, store, data.Supply, data.Demand);
            return isSelling ? ProvenderSaleRules.FeePrice(price, _feePercent) : price;
        }

        /// <summary>The store value the town would have gained from this session's sales in the category (item value per unit).</summary>
        private long SoldValue(ItemCategory category)
        {
            long value = 0;
            foreach (var element in _sold)
            {
                var item = element.EquipmentElement.Item;
                if (item != null && element.Amount > 0 && item.GetItemCategory() == category)
                    value += (long)element.Amount * item.Value;
            }
            return value;
        }
    }

    /// <summary>The chosen town's purse, as vanilla's merchant listener; it records whether the engine settled through it.</summary>
    private sealed class TownSaleListener : InventoryListener
    {
        private readonly Town _town;

        public TownSaleListener(Town town) => _town = town;

        public bool Settled { get; private set; }

        public override int GetGold() => _town.Gold;

        public override TextObject GetTraderName() => _town.Settlement.Name;

        public override void SetGold(int gold)
        {
            _town.ChangeGold(gold - _town.Gold);
            Settled = true;
        }

        public override PartyBase GetOppositeParty() => _town.Settlement.Party;

        // InventoryLogic never calls it (v1.5.5); vanilla's merchant listener throws here.
        public override void OnTransaction()
        {
        }
    }
}
