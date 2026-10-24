using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.Provender.Models;

namespace TAOM.Features.Provender;

/// <summary>
/// The Provender-master's work (docs/features/provender.md, #908): the standing food orders the behavior runs on settlement
/// visits and on the daily tick, and the morale options the conversation offers. Every entry point first asks the same gate:
/// the MCM switch, a co-op client (the host owns the party economy) or a dedicated server (its main hero is an idle world-gen
/// hero, and a single-player save may arrive with the orders on), an enlisted player (the lord feeds the host) and a real
/// officer, the hero in the Quartermaster ROLE, in the party and not the player. The arithmetic is in
/// <see cref="ProvenderRules"/>; the campaign is read and written only through <see cref="IProvenderWorldAdapter"/>. XP and
/// messages follow what the adapter reports was actually done, never the plan.
/// </summary>
public sealed class ProvenderService
{
    internal const string TradeSkill = "Trade";
    internal const string StewardSkill = "Steward";

    private readonly IProvenderWorldAdapter _world;
    private readonly IProvenderSettingsProvider _settings;
    private readonly IRationCatalogProvider _rations;
    private readonly IHeroSkillXpAdapter _xp;
    private readonly IPartyItemRosterAdapter _items;
    private readonly ICoopSessionProvider _coop;
    private readonly IEnlistmentStateQuery _enlistment;
    private readonly IDedicatedServerProvider _server;
    private readonly IModLogger _logger;

    public ProvenderService(
        IProvenderWorldAdapter world,
        IProvenderSettingsProvider settings,
        IRationCatalogProvider rations,
        IHeroSkillXpAdapter xp,
        IPartyItemRosterAdapter items,
        ICoopSessionProvider coop,
        IEnlistmentStateQuery enlistment,
        IDedicatedServerProvider server,
        IModLogger logger)
    {
        _world = world;
        _settings = settings;
        _rations = rations;
        _xp = xp;
        _items = items;
        _coop = coop;
        _enlistment = enlistment;
        _server = server;
        _logger = logger;
    }

    /// <summary>
    /// The player may talk orders with <paramref name="heroId"/>: every gate is open and that hero is the officer. The role
    /// holder is checked first, one field read, so a talk with any other hero never reads the whole party.
    /// </summary>
    public bool CanTalk(string? heroId)
    {
        if (string.IsNullOrEmpty(heroId) || _world.QuartermasterId != heroId)
            return false;
        var party = _world.ReadParty();
        return Gate(_settings.Get(), party) == ProvenderBlock.None && party.OfficerId == heroId;
    }

    /// <summary>
    /// Buys food at <paramref name="settlementId"/> so each type the market stocks reaches the eaters over the stocked types,
    /// never spending the gold kept for wages. Only an open market (<see cref="IsOpenMarket"/>).
    /// </summary>
    public PurchaseResult TopUpFood(ProvenderOrders orders, string settlementId)
    {
        if (!orders.AutoFood || string.IsNullOrEmpty(settlementId))
            return PurchaseResult.None;
        var settings = _settings.Get();
        var party = _world.ReadParty();
        if (Gate(settings, party) != ProvenderBlock.None || party.Eaters <= 0)
            return PurchaseResult.None;

        var place = _world.ReadSettlement(settlementId);
        if (!IsOpenMarket(place))
            return PurchaseResult.None;

        var stocked = 0;
        foreach (var offer in place.Offers)
        {
            if (offer.Stock > 0)
                stocked++;
        }
        var target = ProvenderRules.FoodTargetPerType(party.Eaters, stocked);
        var budget = ProvenderRules.FoodBudget(party.Gold, party.DailyWage, settings.WageDaysKept);

        int units = 0, gold = 0;
        foreach (var offer in place.Offers)
        {
            // Each type is planned against the gold really left, then cut to the market's exact total: a town's price climbs
            // with each unit, so a plan made up front at first prices would starve the later types.
            var left = budget - gold;
            if (left <= 0)
                break;
            party.HeldFood.TryGetValue(offer.ItemId, out var held);
            var wanted = ProvenderRules.FoodToBuy(offer, held, target, left);
            if (wanted <= 0)
                continue;
            var count = ProvenderRules.AffordableCount(_world.UnitPrices(settlementId, offer.ItemId, wanted), left);
            if (count <= 0)
                continue;
            var bought = _world.Buy(settlementId, offer.ItemId, count);
            units += Math.Max(bought.Units, 0);
            gold += Math.Max(bought.Gold, 0);
        }
        if (units <= 0 && gold <= 0)
            return PurchaseResult.None;

        GrantSpendXp(party.OfficerId!, gold);
        _world.Announce(ProvenderAnnouncement.BoughtFood, units, gold);
        return new PurchaseResult(units, gold);
    }

    /// <summary>Slaughters livestock when the stores run below the low mark, keeping the configured heads of each type.</summary>
    public int DailySlaughter(ProvenderOrders orders)
    {
        if (!orders.Slaughter)
            return 0;
        var settings = _settings.Get();
        var party = _world.ReadParty();
        if (Gate(settings, party) != ProvenderBlock.None)
            return 0;

        var plan = ProvenderRules.PlanSlaughter(party.Livestock, party.EdibleFood, party.FoodPerDay, settings.LowFoodDays, settings.KeepPerType);
        var heads = 0;
        foreach (var line in plan)
            heads += Math.Max(_world.Slaughter(line.ItemId, line.Heads), 0);
        if (heads <= 0)
            return 0;

        _xp.AddSkillXp(party.OfficerId!, StewardSkill, ProvenderRules.SlaughterXp(heads));
        _world.Announce(ProvenderAnnouncement.Slaughtered, heads, 0);
        return heads;
    }

    /// <summary>
    /// What <paramref name="option"/> costs and gives now, or why it is closed. No troops outranks the place, and the place
    /// outranks the option's own reason; both keep the option's figures for the greyed line.
    /// </summary>
    public MoraleQuote Quote(MoraleOption option)
    {
        var settings = _settings.Get();
        var party = _world.ReadParty();
        var gate = Gate(settings, party);
        if (gate != ProvenderBlock.None)
            return MoraleQuote.Closed(gate);

        var quote = option == MoraleOption.Drink ? QuoteDrink(party) : QuotePay(option, settings, party);
        if (party.Troops <= 0)
            return quote with { Block = ProvenderBlock.NoTroops };
        if (!party.InPlace)
            return quote with { Block = ProvenderBlock.NotInPlace };
        return quote;
    }

    /// <summary>Carries out <paramref name="option"/> when its quote is open; returns whether the host's spirits were lifted.</summary>
    public bool Buy(MoraleOption option)
    {
        var quote = Quote(option);
        if (quote.Block != ProvenderBlock.None)
            return false;
        var lifted = option == MoraleOption.Drink ? HandOut(quote) : Pay(quote);
        // A conversation opened from the party screen is reverted by Escape: commit after any gold or item write.
        _world.CommitPartyScreen();
        return lifted;
    }

    private static MoraleQuote QuotePay(MoraleOption option, ProvenderSettings settings, ProvenderPartyFacts party)
    {
        var large = option == MoraleOption.PayLarge;
        var perTroop = large ? settings.LargePayGold : settings.SmallPayGold;
        var morale = large ? settings.LargePayMorale : settings.SmallPayMorale;
        var total = (long)perTroop * party.Troops;
        if (total > int.MaxValue || total > party.Gold)
            return new MoraleQuote(ProvenderBlock.NoGold, (int)Math.Min(total, int.MaxValue), morale, perTroop, string.Empty, 0, 0);
        return new MoraleQuote(ProvenderBlock.None, (int)total, morale, perTroop, string.Empty, 0, 0);
    }

    private MoraleQuote QuoteDrink(ProvenderPartyFacts party)
    {
        var ration = ProvenderRules.SelectRation(_rations.Get(), party.RaceCounts, party.LeaderCulture);
        if (ration == null)
            return MoraleQuote.Closed(ProvenderBlock.NoRation);

        var need = ProvenderRules.RationItems(party.Troops, ration.TroopsPerItem);
        var have = Math.Max(_items.GetItemCount(ration.ItemId), 0);
        var toBuy = Math.Max(need - have, 0);
        if (toBuy == 0)
            return new MoraleQuote(ProvenderBlock.None, 0, ration.Morale, 0, ration.ItemId, need, 0);

        // The shortfall is bought only at an open market the party stands in; a field camp has none. The quote is the market's
        // exact total, each unit's price rise included, so the gold the line shows is the gold the purchase takes.
        if (string.IsNullOrEmpty(party.SettlementId) || !IsOpenMarket(_world.ReadSettlement(party.SettlementId!)))
            return new MoraleQuote(ProvenderBlock.NoMarket, 0, ration.Morale, 0, ration.ItemId, need, toBuy);
        var prices = _world.UnitPrices(party.SettlementId!, ration.ItemId, toBuy);
        var cost = prices.Count < toBuy ? -1 : ProvenderRules.TotalCost(prices);
        if (cost <= 0)
            return new MoraleQuote(ProvenderBlock.NoStock, 0, ration.Morale, 0, ration.ItemId, need, toBuy);
        var block = cost > party.Gold ? ProvenderBlock.NoGold : ProvenderBlock.None;
        return new MoraleQuote(block, cost, ration.Morale, 0, ration.ItemId, need, toBuy);
    }

    private bool Pay(MoraleQuote quote)
    {
        var paid = _world.PayTroops(quote.Gold);
        if (paid < quote.Gold)
        {
            _logger.LogWarning($"[Provender] The pay took {paid} of {quote.Gold} gold; no morale was given.");
            return false;
        }
        _world.AddMorale(quote.Morale, disorganize: false);
        _world.Announce(ProvenderAnnouncement.PaidTroops, quote.Morale, paid);
        return true;
    }

    private bool HandOut(MoraleQuote quote)
    {
        var party = _world.ReadParty();
        var spent = 0;
        if (quote.ToBuy > 0)
        {
            var bought = _world.Buy(party.SettlementId!, quote.ItemId, quote.ToBuy);
            spent = Math.Max(bought.Gold, 0);
            if (spent > 0)
                GrantSpendXp(party.OfficerId!, spent);
            if (bought.Units < quote.ToBuy)
            {
                _logger.LogWarning($"[Provender] The market sold {bought.Units} of {quote.ToBuy} {quote.ItemId}; nothing was handed out.");
                return false;
            }
        }

        var removed = _items.RemoveItem(quote.ItemId, quote.ItemCount);
        if (removed < quote.ItemCount)
        {
            if (removed > 0)
                _items.AddItem(quote.ItemId, removed);
            _logger.LogWarning($"[Provender] Only {removed} of {quote.ItemCount} {quote.ItemId} could be taken; nothing was handed out.");
            return false;
        }
        _world.AddMorale(quote.Morale, disorganize: true);
        _world.Announce(ProvenderAnnouncement.Drank, quote.ItemCount, spent);
        return true;
    }

    private void GrantSpendXp(string officerId, int gold)
    {
        if (gold <= 0)
            return;
        _xp.AddSkillXp(officerId, TradeSkill, ProvenderRules.TradeXp(gold));
        _xp.AddSkillXp(officerId, StewardSkill, ProvenderRules.StewardXp(gold));
    }

    private ProvenderBlock Gate(ProvenderSettings settings, ProvenderPartyFacts party)
    {
        if (!settings.Enabled)
            return ProvenderBlock.MasterOff;
        if (_coop.ShouldDeferToHost || _server.IsDedicatedServer)
            return ProvenderBlock.Coop;
        if (_enlistment.IsEnlisted)
            return ProvenderBlock.Enlisted;
        return party.OfficerIsValid ? ProvenderBlock.None : ProvenderBlock.NoOfficer;
    }

    /// <summary>
    /// A market the player may buy from: a town, or a village with a trade town, never a castle (a castle also has a town
    /// component); not a village out of its normal state (raided, being raided, forced), not at war with the player, and not
    /// a settlement that denies the player full access (a crime rating at a neutral town). These follow vanilla's trade
    /// screen, which also refuses a village with no gold of its own (a sale to the player pays the village, so that rule does
    /// not apply here) and lets a disguised hero with Smuggler Connections trade (refused here: conservative).
    /// </summary>
    private static bool IsOpenMarket(ProvenderSettlementFacts place) =>
        place.Found && !place.IsCastle && (place.IsTown || (place.IsVillage && place.HasMarketTown))
        && !place.IsDisturbed && !place.AtWarWithPlayer && !place.RefusesPlayer;
}
