using System;
using System.Collections.Generic;
using System.Diagnostics;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.Enlistment;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.WatchfulHolds.Models;

namespace TAOM.Features.WatchfulHolds;

/// <summary>
/// Watchful Holds: an enemy castle or town harasses a lord party that marches close by. Every rule lives here; the
/// engine is reached only through <see cref="IWatchfulHoldsAdapter"/> and parties travel as opaque handles (ADR-007).
/// One call per party per campaign hour, so the cheap filters come first and the common party leaves at the first
/// one. A lord near no fort scans every fort but allocates nothing; the in-range list allocates only when a fort is
/// inside the radius.
/// </summary>
/// <remarks>
/// Singleton state is the fort list, the horn clock and the diagnostic counters, all per campaign: the behavior's constructor calls
/// <see cref="ResetForNewSession"/>. Nothing is saved. Runs on the main thread (the campaign event).
/// </remarks>
public sealed class WatchfulHoldsService : IWatchfulHoldsService
{
    private readonly IWatchfulHoldsAdapter _adapter;
    private readonly IWatchfulHoldsNoticeAdapter _notices;
    private readonly IWatchfulProvinceLookup _provinces;
    private readonly IWatchfulHoldsSettings _settings;
    private readonly IWatchfulHoldsConfigProvider _configProvider;
    private readonly IEnlistmentStateQuery _enlistment;
    private readonly IRandomSource _random;
    private readonly IModLogger _logger;

    private FortSite[]? _forts;
    private double? _lastHornHours;
    private long _checks;
    private long _aiTriggers;
    private long _playerTriggers;
    private long _hourBucket = long.MinValue;
    private long _hourTicks;
    private double _maxHourMs;

    public WatchfulHoldsService(
        IWatchfulHoldsAdapter adapter,
        IWatchfulHoldsNoticeAdapter notices,
        IWatchfulProvinceLookup provinces,
        IWatchfulHoldsSettings settings,
        IWatchfulHoldsConfigProvider configProvider,
        IEnlistmentStateQuery enlistment,
        IRandomSource random,
        IModLogger logger)
    {
        _adapter = adapter;
        _notices = notices;
        _provinces = provinces;
        _settings = settings;
        _configProvider = configProvider;
        _enlistment = enlistment;
        _random = random;
        _logger = logger;
    }

    public void OnHourlyTickParty(object engineParty)
    {
        if (!_settings.Enabled)
            return;

        long started = Stopwatch.GetTimestamp();
        try
        {
            Evaluate(engineParty);
        }
        finally
        {
            Record(Stopwatch.GetTimestamp() - started);
        }
    }

    public void LogDailySummary()
    {
        FlushHour();
        double hours = _adapter.CampaignHours;
        long day = FiniteFloatValidator.IsFinite(hours) ? (long)Math.Floor(hours / 24.0) : -1;
        _logger.LogDebug(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "[WatchfulHolds] day {0}: checks={1}, aiTriggers={2}, playerTriggers={3}, maxHourMs={4:F3}",
            day, _checks, _aiTriggers, _playerTriggers, _maxHourMs));
        _checks = _aiTriggers = _playerTriggers = 0;
        _maxHourMs = 0;
    }

    public void ResetForNewSession()
    {
        _forts = null;
        _lastHornHours = null;
        _checks = _aiTriggers = _playerTriggers = 0;
        _hourBucket = long.MinValue;
        _hourTicks = 0;
        _maxHourMs = 0;
    }

    private void Evaluate(object engineParty)
    {
        if (!_adapter.TryReadParty(engineParty, out var party))
            return;
        if (!(party.IsLordParty || party.IsMainParty) || !party.IsMoving)
            return;
        if (party.InSettlement || party.InMapEvent || party.InSiegeEvent || party.IsArmyFollower)
            return;
        if (party.IsDisorganized || !party.CanGetDisorganized)
            return;

        var config = _configProvider.GetConfig();
        if (party.MenCount < config.MinPartySize)
            return;

        bool isPlayer = party.IsMainParty;
        if (isPlayer ? _enlistment.IsEnlisted : !_settings.HarassAiLords)
            return;

        // A positive requirement: a NaN or infinite position fails it and the party is skipped.
        if (!FiniteFloatValidator.IsFinite(party.X) || !FiniteFloatValidator.IsFinite(party.Y))
            return;
        if (!TryFindHarassingFort(party, config, out var fort, out var status))
            return;

        var row = config.RowFor(status.OwnerCultureId);
        float chance = (isPlayer ? config.PlayerChancePerHour : config.AiChancePerHour) * row.ChanceMultiplier;
        if (!(_random.NextDouble() < Clamp01(chance)))
            return;

        if (isPlayer)
        {
            _playerTriggers++;
            HarassPlayer(party, config, row, fort.Id);
        }
        else
        {
            _aiTriggers++;
            _adapter.Disorganize(party.Handle!);
        }
    }

    /// <summary>The nearest fort within the radius that is awake, manned, at war with the party and holds its province.</summary>
    private bool TryFindHarassingFort(WatchfulPartySnapshot party, WatchfulHoldsConfig config, out FortSite fort, out FortStatus status)
    {
        fort = default;
        status = default;
        float radiusSquared = config.Radius * config.Radius;
        float x = party.X;
        float y = party.Y;
        var sites = _forts ??= _adapter.GetFortSites();
        List<(float DistanceSquared, FortSite Site)>? near = null;
        for (int i = 0; i < sites.Length; i++)
        {
            float dx = sites[i].X - x;
            float dy = sites[i].Y - y;
            float distanceSquared = dx * dx + dy * dy;
            if (!(distanceSquared <= radiusSquared))
                continue;
            (near ??= new List<(float, FortSite)>()).Add((distanceSquared, sites[i]));
        }
        if (near == null)
            return false;

        // With provinces, the party must stand in the fort's own province (asked once, not per fort); without them,
        // distance alone decides.
        string? provinceFort = _provinces.TryGetProvinceFief(x, y, out var fief) ? fief : null;
        near.Sort(CompareByDistance);
        foreach (var (_, site) in near)
        {
            if (provinceFort != null && site.Id != provinceFort)
                continue;
            if (!_adapter.TryReadFort(site.Id, out var live) || live.UnderSiege || live.GarrisonMen < config.MinGarrison)
                continue;
            if (!_adapter.AreAtWar(party.Handle!, site.Id))
                continue;
            fort = site;
            status = live;
            return true;
        }
        return false;
    }

    private static int CompareByDistance((float DistanceSquared, FortSite Site) a, (float DistanceSquared, FortSite Site) b)
    {
        int byDistance = a.DistanceSquared.CompareTo(b.DistanceSquared);
        return byDistance != 0 ? byDistance : string.CompareOrdinal(a.Site.Id, b.Site.Id);
    }

    private void HarassPlayer(WatchfulPartySnapshot party, WatchfulHoldsConfig config, WatchfulCultureRow row, string fortId)
    {
        IReadOnlyList<WatchfulPartySnapshot> members = party.IsArmyLeader
            ? _adapter.GetArmyMemberParties(party.Handle!)
            : Array.Empty<WatchfulPartySnapshot>();
        if (members.Count == 0)
            members = new[] { party };

        int killed = 0, wounded = 0, food = 0;
        foreach (var member in members)
        {
            int kill = WatchfulLossMath.CountFromFraction(member.MenCount, config.KillFraction, row.LossMultiplier)
                + _random.Next(0, config.KillExtraMax + 1);
            int wound = WatchfulLossMath.CountFromFraction(member.MenCount, config.WoundFraction, row.LossMultiplier)
                + _random.Next(0, config.WoundExtraMax + 1);
            var (appliedKill, appliedWound) = _adapter.ApplyLosses(member.Handle!, kill, wound);
            killed += appliedKill;
            wounded += appliedWound;
            food += _adapter.RemoveFood(member.Handle!, config.FoodLossFraction);
            _adapter.LowerMorale(member.Handle!, config.MoraleLoss);
        }

        _adapter.Disorganize(party.Handle!);
        if (party.IsArmyLeader)
            _adapter.LowerArmyCohesion(party.Handle!, config.CohesionLoss, config.CohesionFloor);

        _notices.ShowHarried(fortId, killed, wounded, food);
        SoundTheHornIfDue(config);
    }

    private void SoundTheHornIfDue(WatchfulHoldsConfig config)
    {
        double now = _adapter.CampaignHours;
        // A positive requirement: a backwards or NaN clock stays silent instead of sounding every hour.
        if (_lastHornHours.HasValue && !(now - _lastHornHours.Value >= config.HornCooldownHours))
            return;
        _lastHornHours = FiniteFloatValidator.IsFinite(now) ? now : (double?)null;
        _notices.PlayHorn();
    }

    private void Record(long elapsedTicks)
    {
        _checks++;
        double hours = _adapter.CampaignHours;
        if (FiniteFloatValidator.IsFinite(hours))
        {
            long bucket = (long)Math.Floor(hours);
            if (bucket != _hourBucket)
            {
                FlushHour();
                _hourBucket = bucket;
            }
        }
        _hourTicks += elapsedTicks;
    }

    private void FlushHour()
    {
        double ms = _hourTicks * 1000.0 / Stopwatch.Frequency;
        if (ms > _maxHourMs)
            _maxHourMs = ms;
        _hourTicks = 0;
        _hourBucket = long.MinValue;
    }

    private static float Clamp01(float value) => !(value > 0f) ? 0f : value > 1f ? 1f : value;
}
