using System;
using System.Collections.Generic;

namespace TAOM.Features.Provender.Models;

/// <summary>One food type a settlement's market has in stock: the item, the price of one unit and how many it holds.</summary>
public readonly record struct FoodOffer(string ItemId, int Price, int Stock);

/// <summary>A stack of livestock in the main party's inventory and the meat one head yields.</summary>
public readonly record struct LivestockStack(string ItemId, int Count, int MeatPerHead);

/// <summary>One slaughter the plan asks for: <paramref name="Heads"/> heads of the livestock item.</summary>
public readonly record struct SlaughterLine(string ItemId, int Heads);

/// <summary>What a purchase actually did: units landed and gold spent. The market may stop a buy short, so the plan never stands in for it.</summary>
public readonly record struct PurchaseResult(int Units, int Gold)
{
    public static readonly PurchaseResult None = new(0, 0);
}

/// <summary>
/// The facts about the main party the provender rules read (docs/features/provender.md). The adapter reports what the campaign
/// says and decides nothing. <see cref="None"/> (<see cref="HasParty"/> false) means there was no campaign, or the player is
/// captive; the adapter never returns null.
/// </summary>
public sealed class ProvenderPartyFacts
{
    public static readonly ProvenderPartyFacts None = new();

    public bool HasParty { get; init; }

    /// <summary>The hero in the Quartermaster ROLE (never the effective one, which falls back to the leader), or null.</summary>
    public string? OfficerId { get; init; }

    public bool OfficerIsPlayer { get; init; }

    /// <summary>The officer is in the main party, alive and not a prisoner.</summary>
    public bool OfficerInParty { get; init; }

    /// <summary>Mouths the engine feeds: members plus half the prisoners.</summary>
    public int Eaters { get; init; }

    public int Gold { get; init; }

    /// <summary>Soldiers who can be paid or given drink: every man in the party but the player.</summary>
    public int Troops { get; init; }

    /// <summary>The settlement the party is inside, or null on the map.</summary>
    public string? SettlementId { get; init; }

    /// <summary>
    /// Food units in the stores, the herd not counted: every food item plus the part-eaten unit the engine carries
    /// (<c>RemainingFoodPercentage</c> / 100, below zero when the party went hungry). The engine's own days of food count each
    /// live head's meat, so a slaughter would never move them.
    /// </summary>
    public float EdibleFood { get; init; }

    /// <summary>The party's daily wage, the money the food order keeps back.</summary>
    public int DailyWage { get; init; }

    /// <summary>Food units the party eats per day (the engine's daily change, negated); not positive when the party gains food.</summary>
    public float FoodPerDay { get; init; }

    /// <summary>Food item id to the count held, every stack summed.</summary>
    public IReadOnlyDictionary<string, int> HeldFood { get; init; } = new Dictionary<string, int>();

    public IReadOnlyList<LivestockStack> Livestock { get; init; } = Array.Empty<LivestockStack>();

    /// <summary>Troops per race name, the player excluded. A race the game cannot name is left out.</summary>
    public IReadOnlyDictionary<string, int> RaceCounts { get; init; } = new Dictionary<string, int>();

    public string? LeaderCulture { get; init; }

    /// <summary>The player's field camp stands and its raise is finished.</summary>
    public bool CampReady { get; init; }

    public bool OfficerIsValid => HasParty && !string.IsNullOrEmpty(OfficerId) && !OfficerIsPlayer && OfficerInParty;

    public bool InPlace => !string.IsNullOrEmpty(SettlementId) || CampReady;
}

/// <summary>The facts about one settlement the buying rules read.</summary>
public sealed class ProvenderSettlementFacts
{
    public static readonly ProvenderSettlementFacts None = new();

    public bool Found { get; init; }

    public bool IsTown { get; init; }

    public bool IsVillage { get; init; }

    public bool IsCastle { get; init; }

    /// <summary>A village that is raided, being raided, or forced for supplies or volunteers: not in its normal state.</summary>
    public bool IsDisturbed { get; init; }

    /// <summary>
    /// The settlement does not give the player full access (vanilla's access model: a crime rating at a neutral town, which
    /// leaves only a disguised visit). <c>SettlementEntered</c> fires before the gate menu shows, so this must close the market.
    /// </summary>
    public bool RefusesPlayer { get; init; }

    /// <summary>The settlement's faction is at war with the player's map faction (the vanilla war test).</summary>
    public bool AtWarWithPlayer { get; init; }

    /// <summary>A town, or a village with a trade town to set its prices (a castle's village with none has no market).</summary>
    public bool HasMarketTown { get; init; }

    /// <summary>Every food type the market holds at least one unit of.</summary>
    public IReadOnlyList<FoodOffer> Offers { get; init; } = Array.Empty<FoodOffer>();
}

/// <summary>Why an order or a morale option is not open. <see cref="None"/> means it is.</summary>
public enum ProvenderBlock
{
    None,
    MasterOff,
    NoOfficer,
    Coop,
    Enlisted,
    NotInPlace,
    NoTroops,
    NoGold,
    NoRation,
    NoStock,
    NoMarket,
}

/// <summary>The three ways the player can lift the host's spirits.</summary>
public enum MoraleOption
{
    PaySmall,
    PayLarge,
    Drink,
}

/// <summary>What a morale option would cost and give right now, for the line that shows it.</summary>
/// <remarks>
/// A quote closed for the place or the troops keeps its figures, so the greyed line still says what it would cost. Only a
/// closed gate, or a ration that cannot be chosen, gives the empty <see cref="Closed"/> quote.
/// </remarks>
public sealed record MoraleQuote(ProvenderBlock Block, int Gold, int Morale, int PerTroopGold, string ItemId, int ItemCount, int ToBuy)
{
    public static MoraleQuote Closed(ProvenderBlock block) => new(block, 0, 0, 0, string.Empty, 0, 0);

    /// <summary>A ration was chosen, so a drink line can name it.</summary>
    public bool HasItem => !string.IsNullOrEmpty(ItemId);

    /// <summary>
    /// Part of the ration must be bought and a market priced it, so the line states the purchase and its gold. Without a price
    /// (no market, or too little stock) the cost is unknown and the line must not quote one.
    /// </summary>
    public bool QuotesPurchase => ToBuy > 0 && Gold > 0;

    /// <summary>The ration is chosen and no priced purchase is quoted: the plain drink line lists, not the purchase line.</summary>
    public bool ListsFromStores => HasItem && !QuotesPurchase;
}

/// <summary>What the service reports back for a message to the player.</summary>
public enum ProvenderAnnouncement
{
    BoughtFood,
    Slaughtered,
    PaidTroops,
    Drank,
}
