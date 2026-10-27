using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;
using Inv = System.Globalization.CultureInfo;

namespace TAOM.Features.FireAndTribute;

/// <summary>The spawn half of <see cref="FireAndTributeService"/>: the daily roll and the creation of a host.</summary>
public sealed partial class FireAndTributeService
{
    /// <summary>What one attempt to send a host came to: <see cref="Sent"/> when a host exists (or would, for a dry run).</summary>
    private readonly struct SpawnOutcome
    {
        public SpawnOutcome(bool sent, string message)
        {
            Sent = sent;
            Message = message;
        }

        public bool Sent { get; }

        public string Message { get; }
    }

    public void OnDailyTick()
    {
        if (!IsSinglePlayer)
        {
            ReleaseAll("a co-op session or a dedicated server is live");
            return;
        }
        if (!TryToday(out int day))
            return;

        NoteWarSeen(day);
        if (_settings.Enabled)
            RollSpawns(day);
        // Once a day, so a kingdom that changes side is followed; a spawn and a removal publish at once.
        PublishOwnSide();
        LogDaily(day);
    }

    /// <summary>Records the first day this save saw Full War, so a late install counts its delay from that day.</summary>
    private void NoteWarSeen(int day) => _state.WarSeenDay = ProjectedWarSeenDay(day);

    /// <summary>
    /// The first-seen day <see cref="NoteWarSeen"/> would leave today, computed without recording it. The window is always
    /// read through this, so the console's status shows the window the next roll will use.
    /// </summary>
    private int ProjectedWarSeenDay(int day) => _state.WarSeenDay < 0 && FullWarOrEnded() ? day : _state.WarSeenDay;

    private bool FullWarOrEnded() =>
        _warOfTheRing.CurrentPhase == WarPhase.FullWar || _warOfTheRing.CurrentPhase == WarPhase.WarEnded;

    private bool WindowOpen(int day, FireAndTributeConfig config)
    {
        var t = config.Tuning;
        return FireAndTributeMath.SpawnWindowOpen(
            _warOfTheRing.IsEnabled, FullWarOrEnded(), _warOfTheRing.GetEffectivePhaseDay(WarPhase.FullWar),
            ProjectedWarSeenDay(day), day, t.FirstHostDelayDays, t.WarlessStartDay);
    }

    private void RollSpawns(int day)
    {
        var config = _configProvider.GetConfig();
        if (config.Hosts.Count == 0 || !WindowOpen(day, config))
            return;

        foreach (var side in Sides)
        {
            if (!SideEnabled(side) || HostOf(side) != null)
                continue;
            if (_state.CooldownUntil.TryGetValue(side, out int until) && day < until)
                continue;
            if (!(_random.NextDouble() < config.Tuning.SpawnChance))
                continue;

            var outcome = SendHost(side, config, day, dryRun: false);
            if (!outcome.Sent)
                _logger.LogInfo($"{Tag} {side}: no host rode today: {outcome.Message}");
        }
    }

    public string TrySpawn(FactionSide side, bool dryRun)
    {
        if (!IsSinglePlayer)
            return "Refused: single-player only, and a co-op session, a co-op mod TAOM cannot probe, or a dedicated server is live.";
        if (!_settings.Enabled)
            return "Refused: Fire and Tribute is switched off in MCM.";
        if (!SideEnabled(side))
            return $"Refused: {side} hosts are switched off in MCM.";
        if (!TryToday(out int day))
            return "Refused: the campaign clock is unreadable.";
        var live = HostOf(side);
        if (live != null)
            return $"Refused: {side} already has a host ({live.HostId}, party {live.PartyId}).";

        var outcome = SendHost(side, _configProvider.GetConfig(), day, dryRun);
        return outcome.Message;
    }

    /// <summary>
    /// Tries the side's hosts in a random rotation and sends the first that has a target, a home and a roster. A host
    /// that cannot go (no clan, no hideout, no anchor, no target) is skipped with one log line.
    /// </summary>
    private SpawnOutcome SendHost(FactionSide side, FireAndTributeConfig config, int day, bool dryRun)
    {
        var defs = config.Hosts.Where(h => h.Side == side).ToList();
        if (defs.Count == 0)
            return new SpawnOutcome(false, $"no {side} host is configured.");

        var candidates = TargetCandidates(config.Tuning.MinHearth);
        int start = defs.Count == 1 ? 0 : _random.Next(0, defs.Count);
        var notes = new List<string>();
        for (int i = 0; i < defs.Count; i++)
        {
            var outcome = SendOne(defs[(start + i) % defs.Count], config, candidates, day, dryRun);
            if (outcome.Sent)
                return outcome;
            notes.Add(outcome.Message);
        }
        return new SpawnOutcome(false, string.Join(" ", notes));
    }

    /// <summary>The living kingdoms that own a raidable village, with their side and strength.</summary>
    private List<TargetCandidate> TargetCandidates(float minHearth)
    {
        var raidable = FireAndTributeMath.KingdomsWithRaidable(_adapter.ReadVillages(), minHearth);
        var candidates = new List<TargetCandidate>();
        foreach (var kingdom in _snapshots.GetKingdoms())
        {
            if (kingdom != null && raidable.Contains(kingdom.Id))
                candidates.Add(new TargetCandidate(kingdom.Id, _alignment.ResolveSide(kingdom.Id, kingdom.CultureId), kingdom.Strength));
        }
        return candidates;
    }

    private SpawnOutcome SendOne(RaidHostDef def, FireAndTributeConfig config, List<TargetCandidate> candidates, int day, bool dryRun)
    {
        var t = config.Tuning;
        _state.Grudges.TryGetValue(def.Side, out string? grudge);
        string? target = FireAndTributeMath.PickTarget(candidates, def.TargetSides, def.PreferredTargets, grudge);
        if (target == null)
            return new SpawnOutcome(false, $"{def.Id}: no kingdom of {string.Join("/", def.TargetSides)} has a raidable village.");
        float targetStrength = candidates.First(c => c.Id == target).Strength;

        var plan = FireAndTributeMath.PlanHost(def.Troops, targetStrength, t.StrengthShare, t.MinTroops, t.MaxTroops, _adapter.TroopPower);
        if (plan.Count == 0)
        {
            _logger.LogWarning($"{Tag} host {def.Id} skipped: none of its troops has a usable power (unknown troop ids?)");
            return new SpawnOutcome(false, $"{def.Id}: none of its troops is known to the game.");
        }

        if (!TryAnchor(def, out string anchor, out var failure))
        {
            _logger.LogWarning($"{Tag} host {def.Id} skipped: {failure}");
            return new SpawnOutcome(false, $"{def.Id}: skipped ({failure}).");
        }

        if (dryRun)
            return new SpawnOutcome(true, DryRunText(def, target, targetStrength, plan, t, anchor));

        int extra = _random.Next(t.QuotaExtraMin, t.QuotaExtraMax + 1);
        int quota = FireAndTributeMath.Quota(targetStrength, t.StrengthPerRaid, extra, t.QuotaMin, t.QuotaMax);
        var spawn = _adapter.SpawnHost(def.ClanId, anchor, def.Name, t.SpawnRadiusDays);
        if (!spawn.Succeeded)
        {
            _logger.LogWarning($"{Tag} host {def.Id} skipped: {spawn.Failure} near {anchor}");
            return new SpawnOutcome(false, $"{def.Id}: skipped ({spawn.Failure} near {anchor}).");
        }

        string partyId = spawn.PartyId!;
        bool filled;
        try
        {
            filled = Fill(partyId, plan);
        }
        catch
        {
            // A half-built party would roam unpinned and unowned.
            RemovePlanned(partyId);
            throw;
        }
        if (!filled)
        {
            RemovePlanned(partyId);
            _logger.LogWarning($"{Tag} host {def.Id} skipped: none of its troops could be added");
            return new SpawnOutcome(false, $"{def.Id}: skipped (no troop could be added).");
        }

        var record = new RaidHostRecord
        {
            PartyId = partyId, HostId = def.Id, Side = def.Side, TargetKingdomId = target,
            StartDay = day, Raids = 0, Quota = quota, Returning = false,
        };
        _state.Hosts.Add(record);
        _spawnedToday++;
        // The side's grudge is spent by the host that rides, whether or not it could use it.
        _state.Grudges.Remove(def.Side);
        ScheduleEnvoy(record, t);
        _notices.ShowSpawned(def.Name, spawn.NearestTownId);
        _logger.LogInfo($"{Tag} {def.Id} rides ({partyId}) from {spawn.HomeId} against {target}: {plan.Sum(p => p.Count)} men, quota {quota}");
        PublishOwnSide();
        Steer(record, config, day);
        return new SpawnOutcome(true, $"{def.Id} rides against {target} with {plan.Sum(p => p.Count)} men, quota {quota} (party {partyId}).");
    }

    /// <summary>Fills the roster and finishes the party; false, with the party untouched, when no troop could be added.</summary>
    private bool Fill(string partyId, IReadOnlyList<(string TroopId, int Count)> plan)
    {
        int added = 0;
        foreach (var (troopId, count) in plan)
            added += _adapter.AddTroops(partyId, troopId, count);
        if (added <= 0)
            return false;
        _adapter.CompleteSpawn(partyId);
        return true;
    }

    /// <summary>The first anchor, in a random rotation, a host can be created near; else why the last one failed.</summary>
    private bool TryAnchor(RaidHostDef def, out string anchor, out RaidSpawnFailure failure)
    {
        anchor = string.Empty;
        failure = RaidSpawnFailure.AnchorMissing;
        int start = def.Anchors.Count <= 1 ? 0 : _random.Next(0, def.Anchors.Count);
        for (int i = 0; i < def.Anchors.Count; i++)
        {
            string candidate = def.Anchors[(start + i) % def.Anchors.Count];
            failure = _adapter.Probe(def.ClanId, candidate);
            if (failure == RaidSpawnFailure.None)
            {
                anchor = candidate;
                return true;
            }
            // The clan and its hideouts are the host's, not the anchor's: another anchor would fail the same way.
            if (failure == RaidSpawnFailure.ClanMissing || failure == RaidSpawnFailure.NoHideout)
                return false;
        }
        return false;
    }

    private static string DryRunText(RaidHostDef def, string target, float targetStrength,
        IReadOnlyList<(string TroopId, int Count)> plan, FireAndTributeTuning t, string anchor)
    {
        int low = FireAndTributeMath.Quota(targetStrength, t.StrengthPerRaid, t.QuotaExtraMin, t.QuotaMin, t.QuotaMax);
        int high = FireAndTributeMath.Quota(targetStrength, t.StrengthPerRaid, t.QuotaExtraMax, t.QuotaMin, t.QuotaMax);
        string troops = string.Join(", ", plan.Select(p => $"{p.Count} {p.TroopId}"));
        return string.Format(Inv.InvariantCulture,
            "Would send {0} ({1}) from near {2} against {3} (strength {4:0}): {5} men ({6}), quota {7} to {8}.",
            def.Id, def.ClanId, anchor, target, targetStrength, plan.Sum(p => p.Count), troops, low, high);
    }
}
