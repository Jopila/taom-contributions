using System.Linq;
using TAOM.Adapters;
using TAOM.Features.Execution;
using TAOM.Features.Provender.Models;

namespace TAOM.Features.Provender;

/// <summary>
/// The Provender-master's remote sale (#909, docs/features/provender.md "Remote selling"): the player sends goods to one of
/// the nearest eligible towns and the officer sells them there for a cut. The talk gate is <see cref="ProvenderService"/>'s;
/// the arithmetic is <see cref="ProvenderSaleRules"/>; the campaign is read and written only through the adapters.
/// </summary>
public sealed class ProvenderSaleService
{
    private readonly IProvenderSaleAdapter _sale;
    private readonly IProvenderWorldAdapter _world;
    private readonly ProvenderService _provender;
    private readonly IProvenderSettingsProvider _settings;
    private readonly IAlignmentService _alignment;
    private readonly IHeroSkillXpAdapter _xp;

    public ProvenderSaleService(
        IProvenderSaleAdapter sale,
        IProvenderWorldAdapter world,
        ProvenderService provender,
        IProvenderSettingsProvider settings,
        IAlignmentService alignment,
        IHeroSkillXpAdapter xp)
    {
        _sale = sale;
        _world = world;
        _provender = provender;
        _settings = settings;
        _alignment = alignment;
        _xp = xp;
    }

    /// <summary>
    /// The towns on offer, or why selling is closed. Only in a settlement or a ready field camp, and in a camp only with no live
    /// encounter: the trade screen settles its gold with an encountered party when it is not in a settlement.
    /// </summary>
    public SaleQuote Quote()
    {
        if (!_provender.CanTalk(_world.ConversationPartnerId))
            return SaleQuote.Closed(ProvenderBlock.NoOfficer);
        var party = _world.ReadParty();
        if (!party.InPlace)
            return SaleQuote.Closed(ProvenderBlock.NotInPlace);
        if (string.IsNullOrEmpty(party.SettlementId) && party.InEncounter)
            return SaleQuote.Closed(ProvenderBlock.InEncounter);

        var settings = _settings.Get();
        var trade = _xp.GetSkillValue(party.OfficerId!, ProvenderService.TradeSkill);
        var playerSide = SideOf(_sale.PlayerSide);
        var ranked = ProvenderSaleRules.Rank(_sale.ReadTowns(), t => IsEligible(t, playerSide), settings.SaleTownCount);
        if (ranked.Count == 0)
            return SaleQuote.Closed(ProvenderBlock.NoBuyer);

        var offers = ranked
            .Select((t, rank) => new SaleOffer(t.Id, t.Name,
                ProvenderSaleRules.FeePercent(trade, settings.SaleMaxFeePercent, settings.SaleMinFeePercent, rank)))
            .ToList();
        return new SaleQuote(ProvenderBlock.None, offers);
    }

    /// <summary>
    /// Opens the sale screen for the town the player picked, re-checking every gate on a fresh quote: the town must still be
    /// on offer, and it opens at its fee in that quote. False when selling is closed or the town is no longer offered.
    /// </summary>
    public bool Open(string? townId)
    {
        if (string.IsNullOrEmpty(townId))
            return false;
        var quote = Quote();
        if (quote.Block != ProvenderBlock.None)
            return false;
        var offer = quote.Offers.FirstOrDefault(o => o.TownId == townId);
        if (offer == null)
            return false;
        var officerId = _world.ReadParty().OfficerId!;
        return _sale.OpenScreen(offer.TownId, offer.FeePercent, outcome => OnClosed(officerId, outcome));
    }

    private void OnClosed(string officerId, SaleOutcome outcome)
    {
        if (outcome.Gold > 0)
            _xp.AddSkillXp(officerId, ProvenderService.TradeSkill, ProvenderRules.TradeXp(outcome.Gold));
        if (outcome.Units > 0 || outcome.Gold != 0)
            _world.Announce(ProvenderAnnouncement.Sold, outcome.Units, outcome.Gold);
    }

    /// <summary>
    /// A town the player may sell at: not the one the party stands in, not at war with the player, not barring the player, not
    /// besieged, holding at least <see cref="ProvenderSaleRules.MinTownGold"/>, and not of the opposing alignment.
    /// </summary>
    private bool IsEligible(SaleTownFacts town, FactionSide playerSide) =>
        !town.IsCurrent && !town.AtWarWithPlayer && !town.RefusesPlayer && !town.IsUnderSiege
        && town.Gold >= ProvenderSaleRules.MinTownGold && !Opposed(playerSide, SideOf(town.Side));

    /// <summary>
    /// Both sides are Free or Evil and they differ. Neutral trades with anyone; never <c>AreEnemyAlignments</c>, which treats
    /// Neutral as everyone's enemy (the caravan-trade rule).
    /// </summary>
    private static bool Opposed(FactionSide a, FactionSide b) =>
        a != FactionSide.Neutral && b != FactionSide.Neutral && a != b;

    private FactionSide SideOf(SideIds side) => _alignment.ResolveSide(side);
}
