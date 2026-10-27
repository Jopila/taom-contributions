using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.NotableServices.Domain;

namespace TAOM.Features.NotableServices;

/// <summary>
/// The rules for pointing out the holds near a settlement (docs/features/notable-services.md), shared by the notable's line and
/// the tavern keeper's. The caller decides who may ask and how the price is paid; this service decides the outcome, in this order:
/// war, a relation under <c>revealMinRelation</c>, nothing new to name, then the payment, the marks and the notice. The price,
/// the count, the diagonal fraction and the minimum relation all come from <c>notable_services.json</c>.
/// </summary>
public sealed class HoldsRevealService
{
    private const string Tag = "[HoldsReveal]";

    private readonly IMapRevealAdapter _reveal;
    private readonly INotableServicesConfigProvider _config;
    private readonly IModLogger _logger;

    public HoldsRevealService(IMapRevealAdapter reveal, INotableServicesConfigProvider config, IModLogger logger)
    {
        _reveal = reveal;
        _config = config;
        _logger = logger;
    }

    /// <summary>The configured price of one pointing-out, rounded to whole denars.</summary>
    public int Price => Whole(_config.GetConfig().RevealPrice);

    /// <summary>
    /// Runs the request. <paramref name="pay"/> takes the price, is called only for a price above 0 and only once everything
    /// else allows the reveal, and answers whether the gold moved. A fault is logged and answers
    /// <see cref="RevealOutcome.Unavailable"/>.
    /// </summary>
    public RevealOutcome Reveal(RevealAsk ask, Func<int, bool> pay)
    {
        if (ask.AtWarWithPlayer)
            return RevealOutcome.AtWar;

        try
        {
            var config = _config.GetConfig();
            // A relation that is not a number compares false, so it passes: deliberate, pinned by a test, and unreachable in play.
            if (ask.Relation < config.RevealMinRelation)
                return RevealOutcome.Cold;

            var chosen = NearestUntracked(_reveal.FortificationsAround(ask.SettlementId), config);
            if (chosen.Count == 0)
                return RevealOutcome.NothingNew;

            var price = Whole(config.RevealPrice);
            if (price > 0 && !pay(price))
                return RevealOutcome.Unavailable;

            var marked = _reveal.Track(chosen);
            if (marked > 0)
                _reveal.ShowHoldsMarkedNotice(marked);
            return RevealOutcome.Revealed;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} Pointing out the holds near '{ask.SettlementId}' failed: {ex.Message}");
            return RevealOutcome.Unavailable;
        }
    }

    /// <summary>
    /// The ids of the nearest <c>revealCount</c> towns and castles that carry no mark and lie within
    /// <c>revealMaxDiagonalFraction</c> of the map's diagonal, nearest first. A distance that is not a number is left out.
    /// </summary>
    private IReadOnlyList<string> NearestUntracked(IReadOnlyList<RevealCandidate> around, NotableServicesConfig config)
    {
        var bound = config.RevealMaxDiagonalFraction * _reveal.MapDiagonal;
        var count = Whole(config.RevealCount);
        return around
            .Where(c => !c.IsTracked && c.Distance <= bound)
            .OrderBy(c => c.Distance)
            .ThenBy(c => c.SettlementId, StringComparer.Ordinal)
            .Take(count)
            .Select(c => c.SettlementId)
            .ToList();
    }

    private static int Whole(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
