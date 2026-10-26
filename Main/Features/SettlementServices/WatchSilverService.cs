using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.SettlementServices;

/// <summary>
/// The rules of Watch-Silver (#859, docs/features/settlement-services.md): the governor of a town or castle the player's clan
/// owns takes silver for the watch and the fief's loyalty rises at once. One check serves the offer condition, the clickable
/// conditions and the payment, so a button that shows is a button that pays. It reads and writes the engine only through
/// <see cref="ISettlementServicesAdapter"/>, keeps no campaign state and saves nothing. The callers (the dialog behavior) catch what escapes.
/// </summary>
public sealed class WatchSilverService
{
    /// <summary>Silver steadies a fief up to the point where vanilla's drift settles; it never buys the tax boost above it.</summary>
    public const int Ceiling = 50;

    /// <summary>Denars per point of loyalty, per head of militia.</summary>
    public const int GoldPerMilitiaPerPoint = 4;

    /// <summary>A militia below this prices as if it were this large, so silver is never free after a siege.</summary>
    public const int MilitiaFloor = 50;

    private static readonly int[] TierPoints = { 5, 10, 20 };

    private readonly ISettlementServicesAdapter _adapter;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _dedicated;
    private readonly SettlementServicesSettingsProvider _settings;
    private readonly IModLogger _logger;

    // A log latch only, on a singleton: once per fief per game session, so it holds settlement ids and nothing a campaign owns.
    private readonly HashSet<string> _loggedFiefs = new(StringComparer.Ordinal);

    public WatchSilverService(ISettlementServicesAdapter adapter, ICoopSessionProvider coop, IDedicatedServerProvider dedicated,
        SettlementServicesSettingsProvider settings, IModLogger logger)
    {
        _adapter = adapter;
        _coop = coop;
        _dedicated = dedicated;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Writes the player's gold and a fief's loyalty, so it runs in single-player only: no live co-op session, no co-op mod TAOM
    /// cannot probe, no dedicated server, and a peer that may write the world. Read live on every use (ICoopSessionProvider's contract).
    /// </summary>
    private bool MayWriteWorld =>
        CoopSessionPolicy.IsSinglePlayerAuthority(_coop, _dedicated);

    /// <summary>
    /// The gate, and when it is None the offers. Reads the fief now. The block is the first that applies: switch off, not
    /// single-player, not eligible (hidden), a non-finite number (hidden, logged once), at the ceiling (shown, greyed).
    /// </summary>
    public WatchSilverQuote Quote() => QuoteCore(out _);

    private WatchSilverQuote QuoteCore(out GovernedFiefSnapshot? fief)
    {
        fief = null;
        if (!_settings.WatchSilverEnabled)
            return Closed(WatchSilverBlock.Disabled, 0f);
        if (!MayWriteWorld)
            return Closed(WatchSilverBlock.NotSinglePlayer, 0f);
        fief = _adapter.ReadGovernedFief();
        if (fief == null || !fief.OwnedByPlayerClan || !fief.GovernorInFief || !fief.PlayerInFief)
            return Closed(WatchSilverBlock.NotEligible, 0f);
        if (!FiniteFloatValidator.IsFinite(fief.Loyalty) || !FiniteFloatValidator.IsFinite(fief.Militia))
        {
            if (_loggedFiefs.Add(fief.Id))
                _logger.LogError($"[SettlementServices] Watch-Silver is hidden for {fief.Id}: loyalty {fief.Loyalty} or militia {fief.Militia} is not a finite number.");
            return Closed(WatchSilverBlock.Problem, 0f);
        }

        var gap = Ceiling - fief.Loyalty;
        if (gap < 1f)
            return Closed(WatchSilverBlock.AtCeiling, fief.Loyalty);

        var whole = (int)Math.Floor(gap);
        var tiers = new List<WatchSilverTier>(TierPoints.Length);
        var previous = 0;
        foreach (var tier in TierPoints)
        {
            var points = Math.Min(tier, whole);
            if (points <= previous)
                continue;
            tiers.Add(new WatchSilverTier(points, PriceFor(points, fief.Militia)));
            previous = points;
        }

        return new WatchSilverQuote(WatchSilverBlock.None, fief.Loyalty, tiers);
    }

    private static WatchSilverQuote Closed(WatchSilverBlock block, float loyalty) =>
        new(block, loyalty, Array.Empty<WatchSilverTier>());

    // ceil(points * max(militia, floor) * 4) in double, clamped to [1, int.MaxValue]. A float militia is finite here, so the
    // product cannot overflow a double; only the cast needs the clamp.
    private static int PriceFor(int points, float militia)
    {
        var price = Math.Ceiling(points * Math.Max((double)militia, MilitiaFloor) * GoldPerMilitiaPerPoint);
        if (price >= int.MaxValue)
            return int.MaxValue;
        return price < 1d ? 1 : (int)price;
    }

    /// <summary>Whether the purse covers <paramref name="price"/> now. Presentation only: <see cref="Apply"/> re-checks it.</summary>
    public bool CanAfford(int price) => _adapter.PlayerGold >= price;

    /// <summary>
    /// Pays for offer <paramref name="tierIndex"/> (an index into the live quote's rows) after re-running every gate and re-pricing
    /// against the engine's state now. Gold goes first, because the debit is the step that can come up short; the loyalty write
    /// cannot. A charge that does not take exactly the price, or a throwing write, gives the gold back and leaves the verdict a Problem.
    /// </summary>
    public WatchSilverResult Apply(int tierIndex)
    {
        var quote = QuoteCore(out var fief);
        if (quote.Block != WatchSilverBlock.None)
            return new WatchSilverResult(quote.Block, quote.Loyalty);
        if (tierIndex < 0 || tierIndex >= quote.Tiers.Count)
            return new WatchSilverResult(WatchSilverBlock.NotEligible, quote.Loyalty);

        var tier = quote.Tiers[tierIndex];
        if (!CanAfford(tier.Price))
            return new WatchSilverResult(WatchSilverBlock.NotEnoughGold, quote.Loyalty);

        if (!PlayerPurse.TryCharge(_adapter, _logger, tier.Price, "Watch-Silver for " + fief!.Id))
            return new WatchSilverResult(WatchSilverBlock.Problem, quote.Loyalty);

        float readBack;
        try
        {
            readBack = _adapter.SetLoyalty(fief.Id, fief.Loyalty + tier.Points);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SettlementServices] Watch-Silver could not set the loyalty of {fief.Id}; {tier.Price} gold is given back: {ex}");
            _adapter.Refund(tier.Price);
            return new WatchSilverResult(WatchSilverBlock.Problem, quote.Loyalty);
        }

        return new WatchSilverResult(WatchSilverBlock.None, readBack);
    }
}
