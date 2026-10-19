using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.HostReview.Domain;

namespace TAOM.Features.HostReview;

/// <summary>
/// Host Review (#891): decides whether the review can open, who stands in it, and starts it. Pure logic;
/// the party is read through <see cref="IHostReviewPartyAdapter"/> and the scene is opened through
/// <see cref="IHostReviewMissionLauncher"/>.
/// </summary>
public sealed class HostReviewService
{
    private const int MinimumHealthyMen = 2;

    private readonly IHostReviewPartyAdapter _party;
    private readonly IEnlistmentStateQuery _enlistment;
    private readonly IEncounterAdapter _encounter;
    private readonly ICoopSessionProvider _coop;
    private readonly IHostReviewMissionLauncher _launcher;
    private readonly IModLogger _logger;

    public HostReviewService(
        IHostReviewPartyAdapter party,
        IEnlistmentStateQuery enlistment,
        IEncounterAdapter encounter,
        ICoopSessionProvider coop,
        IHostReviewMissionLauncher launcher,
        IModLogger logger)
    {
        _party = party;
        _enlistment = enlistment;
        _encounter = encounter;
        _coop = coop;
        _launcher = launcher;
        _logger = logger;
    }

    public HostReviewBlock GetBlock()
    {
        if (_enlistment.IsEnlisted) return HostReviewBlock.Enlisted;
        if (_encounter.HasCurrent || _encounter.IsMainPartyInMapEvent) return HostReviewBlock.InEncounter;
        if (_coop.IsSessionActive) return HostReviewBlock.CoopSession;

        // The general must stand: wounded or absent (an empty roster is a failed read), he cannot lead the review.
        var roster = _party.GetRoster();
        if (!roster.Any(e => e.IsMainHero && e.HealthyCount > 0)) return HostReviewBlock.HeroWounded;
        if (roster.Sum(e => e.HealthyCount) < MinimumHealthyMen) return HostReviewBlock.TooFewHealthy;
        return HostReviewBlock.None;
    }

    /// <summary>The main hero first, then healthy heroes, then healthy troops in roster order, cut at <paramref name="battleSize"/>.</summary>
    public IReadOnlyList<HostReviewSlot> BuildLineup(int battleSize)
    {
        var lineup = new List<HostReviewSlot>();
        if (battleSize <= 0) return lineup;

        var roster = _party.GetRoster().Where(e => e.HealthyCount > 0).ToList();
        var ordered = roster.Where(e => e.IsMainHero)
            .Concat(roster.Where(e => e.IsHero && !e.IsMainHero))
            .Concat(roster.Where(e => !e.IsHero && !e.IsMainHero));

        var room = battleSize;
        foreach (var entry in ordered)
        {
            if (room <= 0) break;
            var count = Math.Min(entry.HealthyCount, room);
            lineup.Add(new HostReviewSlot(entry.RosterIndex, count));
            room -= count;
        }

        return lineup;
    }

    /// <summary>Tries to open the review. None means it opened; any other value is the reason it did not.</summary>
    public HostReviewBlock Launch()
    {
        var block = GetBlock();
        if (block != HostReviewBlock.None)
        {
            _logger.LogInfo($"[HostReview] Not opened: {block}");
            return block;
        }

        var lineup = BuildLineup(_party.RealBattleSize);
        if (lineup.Count == 0)
        {
            _logger.LogWarning("[HostReview] Not opened: the lineup is empty.");
            return HostReviewBlock.OpenFailed;
        }

        return _launcher.TryOpen(lineup) ? HostReviewBlock.None : HostReviewBlock.OpenFailed;
    }
}
