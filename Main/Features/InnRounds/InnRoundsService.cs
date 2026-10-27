using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.InnRounds.Domain;
using TAOM.Features.Messengers;
using TAOM.Features.NotableServices;
using TAOM.Features.NotableServices.Domain;

namespace TAOM.Features.InnRounds;

/// <summary>
/// Every rule of Inn Rounds (docs/features/inn-rounds.md): the three lines a town's tavern keeper offers. Stand a round for the
/// player's own party (morale), stand a round for everyone drinking in the tavern (renown), and pay to have the nearest holds
/// marked on the map (<see cref="HoldsRevealService"/>, the same rules as a notable's). A round costs the town market's price of
/// the culture's drink for each head, capped, and the gold goes to the town. Both rounds and the Charm experience come out of one
/// daily count, kept in the <see cref="RoundLedger"/> the campaign's behavior owns. It works on snapshots and ids and reaches the
/// engine only through <see cref="IInnRoundsAdapter"/> and the shared adapters. The conditions (<see cref="QuoteParty"/>,
/// <see cref="QuoteHouse"/>, <see cref="OffersReveal"/>, <see cref="CanAffordReveal"/>) never write; <see cref="StandParty"/>,
/// <see cref="StandHouse"/> and <see cref="Reveal"/> are the only members that do, and each re-checks every gate. The service
/// keeps no campaign state.
/// </summary>
public sealed class InnRoundsService
{
    private const string Tag = "[InnRounds]";
    private const string CharmSkillId = "Charm";
    private const string DefaultDrink = "beer";

    private readonly IInnRoundsAdapter _adapter;
    private readonly HoldsRevealService _holds;
    private readonly IInnRoundsConfigProvider _config;
    private readonly IInnRoundsSettingsProvider _settings;
    private readonly IHeroSkillXpAdapter _skills;
    private readonly IHeroRenownAdapter _renown;
    private readonly IRandomSource _random;
    private readonly IMessengerConversationState _messenger;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _server;
    private readonly IModLogger _logger;

    public InnRoundsService(IInnRoundsAdapter adapter, HoldsRevealService holds, IInnRoundsConfigProvider config,
        IInnRoundsSettingsProvider settings, IHeroSkillXpAdapter skills, IHeroRenownAdapter renown, IRandomSource random,
        IMessengerConversationState messenger, ICoopSessionProvider coop, IDedicatedServerProvider server, IModLogger logger)
    {
        _adapter = adapter;
        _holds = holds;
        _config = config;
        _settings = settings;
        _skills = skills;
        _renown = renown;
        _random = random;
        _messenger = messenger;
        _coop = coop;
        _server = server;
        _logger = logger;
    }

    /// <summary>Writes the player's gold, a town's coffers, the party's morale and a clan's renown, so it runs in single-player only.</summary>
    private bool MayWriteWorld => CoopSessionPolicy.IsSinglePlayerAuthority(_coop, _server);

    /// <summary>The tavern keeper's town when every gate is open: the switch, single-player, no messenger conversation, a tavern keeper, a town.</summary>
    private TavernSnapshot? Keeper()
    {
        if (!_settings.Enabled || !MayWriteWorld || _messenger.IsOpen)
            return null;
        var keeper = _adapter.Keeper();
        return keeper is { IsTavernkeeper: true, InTown: true } ? keeper : null;
    }

    /// <summary>The party round on offer, or null when a gate is closed or the party counts nobody.</summary>
    public RoundQuote? QuoteParty(RoundLedger ledger) => Quote(RoundKind.Party, ledger, Keeper());

    /// <summary>The house round on offer, or null when a gate is closed or nobody is in the tavern.</summary>
    public RoundQuote? QuoteHouse(RoundLedger ledger) => Quote(RoundKind.House, ledger, Keeper());

    public bool OffersReveal() => Keeper() != null;

    public int RevealPrice => _holds.Price;

    public bool CanAffordReveal() => _adapter.PlayerGold >= RevealPrice;

    private RoundQuote? Quote(RoundKind kind, RoundLedger ledger, TavernSnapshot? keeper)
    {
        if (keeper == null)
            return null;
        var heads = kind == RoundKind.Party ? _adapter.PartyHeadcount() : _adapter.TavernHeadcount(keeper.SettlementId);
        if (heads <= 0)
            return null;

        var perHead = PricePerHead(keeper);
        var price = heads * perHead;
        var capReached = ledger.UsedOn(_adapter.Today) >= Whole(_config.GetConfig().MaxRoundsPerDay);
        return new RoundQuote(heads, perHead, price, _adapter.PlayerGold >= price, capReached);
    }

    /// <summary>The town market's price of the culture's drink capped at <c>maxPricePerHead</c>, or <c>fallbackPricePerHead</c> when the market cannot say.</summary>
    private int PricePerHead(TavernSnapshot keeper)
    {
        var config = _config.GetConfig();
        var drink = config.CultureDrinks.TryGetValue(keeper.CultureId.ToLowerInvariant(), out var named) ? named : DefaultDrink;
        var market = _adapter.DrinkPrice(keeper.SettlementId, drink);
        return market is > 0 ? Math.Min(market.Value, Whole(config.MaxPricePerHead)) : Whole(config.FallbackPricePerHead);
    }

    /// <summary>Stands a round for the party: pays the town, then raises morale and gives Charm experience. False, with nothing changed, when it cannot be done.</summary>
    public bool StandParty(RoundLedger ledger) => Stand(RoundKind.Party, ledger);

    /// <summary>Stands a round for the tavern: pays the town, then gives renown and Charm experience. False, with nothing changed, when it cannot be done.</summary>
    public bool StandHouse(RoundLedger ledger) => Stand(RoundKind.House, ledger);

    private bool Stand(RoundKind kind, RoundLedger ledger)
    {
        var keeper = Keeper();
        if (keeper == null)
            return false;

        try
        {
            var quote = Quote(kind, ledger, keeper);
            if (quote == null || quote.CapReached || !quote.CanAfford || !_adapter.PayTown(keeper.SettlementId, quote.Price))
                return false;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} Paying for a {kind} round in '{keeper.SettlementId}' failed: {ex.Message}");
            return false;
        }

        try
        {
            ledger.Record(_adapter.Today);
            Reward(kind, keeper);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} Rewarding a {kind} round in '{keeper.SettlementId}' failed: {ex.Message}");
        }
        return true;
    }

    private void Reward(RoundKind kind, TavernSnapshot keeper)
    {
        var config = _config.GetConfig();
        var playerId = _adapter.PlayerHeroId;

        if (kind == RoundKind.Party)
        {
            var landed = _adapter.AddMorale(Draw(config.MoraleMin, config.MoraleMax) * _settings.MoraleScale);
            _adapter.ShowPartyRoundNotice(keeper.CultureId, Whole(landed));
        }
        else
        {
            var renown = Whole(Draw(config.RenownMin, config.RenownMax) * _settings.RenownScale);
            if (renown > 0 && !string.IsNullOrEmpty(playerId))
                _renown.AddClanRenown(playerId!, renown);
            _adapter.ShowHouseRoundNotice(keeper.CultureId);
        }

        GiveCharm(playerId, config);
    }

    /// <summary>Gives Charm experience and shows the amount that landed (the engine scales the ask), nothing when none did.</summary>
    private void GiveCharm(string? playerId, InnRoundsConfig config)
    {
        var xp = Whole(Draw(config.CharmXpMin, config.CharmXpMax));
        if (xp <= 0 || string.IsNullOrEmpty(playerId))
            return;
        var landed = Whole(_skills.AddSkillXpLanded(playerId!, CharmSkillId, xp));
        if (landed > 0)
            _adapter.ShowCharmXpNotice(landed);
    }

    /// <summary>One uniform draw between the two ends of a configured range.</summary>
    private float Draw(float min, float max) => min + (float)_random.NextDouble() * (max - min);

    /// <summary>
    /// Points out the holds near the town, through the rules a notable's line uses, for <c>revealPrice</c> paid to the town. The
    /// town's owner stands in for the notable: the relation is the player's with the leader of the clan that holds it.
    /// </summary>
    public RevealOutcome Reveal()
    {
        var keeper = Keeper();
        if (keeper == null)
            return RevealOutcome.Unavailable;

        var ask = new RevealAsk(keeper.SettlementId, keeper.AtWarWithPlayer, keeper.Relation);
        return _holds.Reveal(ask, price => _adapter.PlayerGold >= price && _adapter.PayTown(keeper.SettlementId, price));
    }

    private static int Whole(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
