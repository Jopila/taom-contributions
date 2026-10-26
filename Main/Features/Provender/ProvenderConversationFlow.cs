using TAOM.Adapters;
using TAOM.Features.Provender.Models;

namespace TAOM.Features.Provender;

/// <summary>
/// The conversation hand-offs of the Provender-master (docs/features/provender.md), one per dialog condition or consequence,
/// bound to one campaign's <see cref="ProvenderOrders"/>. The dialog lines hold the text and the greyed reasons; every
/// decision is here, in <see cref="ProvenderService"/> or on <see cref="MoraleQuote"/> (which drink line lists), so the tree
/// is testable without the game. Built by <c>ProvenderBehavior</c> for every
/// campaign, so a new campaign starts with both orders off.
/// </summary>
public sealed class ProvenderConversationFlow
{
    private readonly ProvenderService _service;
    private readonly ProvenderSaleService _sale;
    private readonly IProvenderWorldAdapter _world;
    private SaleQuote _saleQuote = SaleQuote.Closed(ProvenderBlock.NoBuyer);

    public ProvenderConversationFlow(ProvenderService service, ProvenderSaleService sale, IProvenderWorldAdapter world)
    {
        _service = service;
        _sale = sale;
        _world = world;
    }

    public ProvenderOrders Orders { get; } = new();

    /// <summary>Whether the last morale purchase lifted the host's spirits, for the officer's answer.</summary>
    public bool LastBuyLifted { get; private set; }

    /// <summary>The player is talking to the officer and may give orders.</summary>
    public bool CanTalk() => _service.CanTalk(_world.ConversationPartnerId);

    public bool FoodOrderIsOff() => !Orders.AutoFood;

    public bool FoodOrderIsOn() => Orders.AutoFood;

    public bool SlaughterOrderIsOff() => !Orders.Slaughter;

    public bool SlaughterOrderIsOn() => Orders.Slaughter;

    /// <summary>Sets the food order, re-checking the gate: a line shown before a co-op session went live does nothing.</summary>
    public void SetFoodOrder(bool on)
    {
        if (CanTalk())
            Orders.AutoFood = on;
    }

    /// <summary>Sets the slaughter order, re-checking the gate like <see cref="SetFoodOrder"/>.</summary>
    public void SetSlaughterOrder(bool on)
    {
        if (CanTalk())
            Orders.Slaughter = on;
    }

    /// <summary>The quote shown on a morale line; a closed option still lists, greyed with its reason.</summary>
    public MoraleQuote Quote(MoraleOption option) => _service.Quote(option);

    /// <summary>The item's display name for a morale line, or nothing for a quote with no item.</summary>
    public string ItemName(string itemId) => string.IsNullOrEmpty(itemId) ? string.Empty : _world.ItemName(itemId);

    public void Buy(MoraleOption option) => LastBuyLifted = _service.Buy(option);

    /// <summary>Whether the last chosen town's sale screen will open, for the officer's answer.</summary>
    public bool LastSaleOpened { get; private set; }

    /// <summary>The towns on offer now, or why selling is closed: the hub line's greyed reason.</summary>
    public SaleQuote QuoteSale() => _sale.Quote();

    /// <summary>Takes the quote once, when the player asks to sell, so the town rows that follow read one list.</summary>
    public void PrepareSale() => _saleQuote = _sale.Quote();

    /// <summary>The offer a town row shows, or null when the list is shorter.</summary>
    public SaleOffer? SaleOfferAt(int rank) =>
        rank >= 0 && rank < _saleQuote.Offers.Count ? _saleQuote.Offers[rank] : null;

    /// <summary>Opens the sale screen for the town the row showed; the service re-checks every gate and that town first.</summary>
    public void Sell(int rank) => LastSaleOpened = SaleOfferAt(rank) is { } offer && _sale.Open(offer.TownId);
}
