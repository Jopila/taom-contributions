using System;
using System.Diagnostics;
using System.Linq;
using TAOM.Adapters;

namespace TAOM.Features.FireAndTribute;

/// <summary>The steering half of <see cref="FireAndTributeService"/>: the hourly step of each host and the events that end one.</summary>
public sealed partial class FireAndTributeService
{
    public void OnHourlyTick()
    {
        if (_state.Hosts.Count == 0)
            return;
        if (!IsSinglePlayer)
        {
            ReleaseAll("a co-op session or a dedicated server is live");
            return;
        }
        if (!TryToday(out int day))
            return;

        FireAndTributeConfig config;
        long started = Stopwatch.GetTimestamp();
        try
        {
            config = _configProvider.GetConfig();
            // One record per side; a copy, because a step can remove its own record.
            foreach (var record in _state.Hosts.ToList())
            {
                try
                {
                    if (!SideEnabled(record.Side))
                        Retire(record, "its switch is off");
                    else
                        Steer(record, config, day);
                }
                catch (Exception ex)
                {
                    // One host's failing step must not leave the others unsteered this hour.
                    _logger.LogError($"{Tag} hourly step of {record.HostId} ({record.PartyId}) failed: {ex}");
                }
            }
        }
        finally
        {
            RecordHour(Stopwatch.GetTimestamp() - started);
        }
        // After the timing is kept: the popup's movie load is engine UI time, not the service's.
        RunEnvoys(config);
    }

    /// <summary>
    /// One step of one host. A host that is not on the map or is in a map event is left alone (the engine finalizes the
    /// raid event itself). Returning: dismissed near home, else sent there. Raiding: it turns home on its quota, on the
    /// timeout, or when no village is left; otherwise it engages a weaker party of its target kingdom nearby, or keeps
    /// or picks a village to raid.
    /// </summary>
    private void Steer(RaidHostRecord record, FireAndTributeConfig config, int day)
    {
        var read = _adapter.Read(record.PartyId);
        if (!read.Exists)
        {
            Lost(record);
            return;
        }
        if (!read.IsActive || read.InMapEvent)
            return;

        var t = config.Tuning;
        if (record.Returning)
        {
            GoHomeStep(record, read, t, day);
            return;
        }
        if (record.Raids >= record.Quota)
        {
            BeginReturn(record, read, t, day, "its quota is done");
            return;
        }
        if (day - record.StartDay >= t.MaxHostDays)
        {
            BeginReturn(record, read, t, day, $"{t.MaxHostDays} days are up");
            return;
        }

        var target = FindEngageTarget(record, read, t);
        if (target != null)
        {
            _adapter.OrderEngage(record.PartyId, target.Id);
            return;
        }
        if (!KeepOrPickVillage(record, read, t))
            BeginReturn(record, read, t, day, "no village is left to raid");
    }

    /// <summary>The nearest party of the host's target kingdom it can take: out of battle, weaker and not faster.</summary>
    private NearbyParty? FindEngageTarget(RaidHostRecord record, RaidHostSnapshot host, FireAndTributeTuning t)
    {
        NearbyParty? best = null;
        foreach (var party in _adapter.PartiesNear(record.PartyId, t.EngageRadius))
        {
            if (party.KingdomId != record.TargetKingdomId || party.InMapEvent || party.InSettlement || party.IsAttached)
                continue;
            // A host that was paid never engages the player's own party, nor the leader of an army that carries it (a battle
            // with the leader pulls the player in); the rest of the player's clan is fair game.
            if ((party.IsMainParty || party.LeadsMainParty) && record.SparedClanId.Length > 0)
                continue;
            // Positive requirements: a NaN distance, strength or speed fails them.
            if (!(party.Distance <= t.EngageRadius) || !(party.Strength <= t.EngageStrengthRatio * host.Strength) || !(party.Speed <= host.Speed))
                continue;
            if (best == null || party.Distance < best.Distance
                || (party.Distance == best.Distance && string.CompareOrdinal(party.Id, best.Id) < 0))
                best = party;
        }
        return best;
    }

    /// <summary>
    /// Keeps the host's raid order while its village is still raidable, else orders a random one of the nearest raidable
    /// villages. False when the target kingdom has none left.
    /// </summary>
    private bool KeepOrPickVillage(RaidHostRecord record, RaidHostSnapshot read, FireAndTributeTuning t)
    {
        // A clan that paid is spared: its villages are never kept as a target nor picked, even the nearest.
        string? spared = record.SparedClanId.Length > 0 ? record.SparedClanId : null;
        if (read.RaidTargetId != null
            && FireAndTributeMath.IsRaidable(_adapter.ReadVillage(read.RaidTargetId), record.TargetKingdomId, t.MinHearth, spared))
            return true;

        var near = FireAndTributeMath.NearestRaidable(
            _adapter.ReadVillages(), record.TargetKingdomId, read.X, read.Y, t.MinHearth, t.NearestVillages, spared);
        if (near.Count == 0)
            return false;
        _adapter.OrderRaid(record.PartyId, near[near.Count == 1 ? 0 : _random.Next(0, near.Count)]);
        return true;
    }

    private void BeginReturn(RaidHostRecord record, RaidHostSnapshot read, FireAndTributeTuning t, int day, string why)
    {
        record.Returning = true;
        _logger.LogInfo($"{Tag} {record.HostId} ({record.PartyId}) turns for home: {why} ({record.Raids}/{record.Quota} raids)");
        _notices.ShowGoingHome(NameOf(record));
        GoHomeStep(record, read, t, day);
    }

    /// <summary>
    /// A returning host is dismissed within the home radius of its hideout, else sent there. One that has been out twice
    /// the timeout (blocked, or its path lost) is dismissed where it stands, so no pinned host roams forever.
    /// </summary>
    private void GoHomeStep(RaidHostRecord record, RaidHostSnapshot read, FireAndTributeTuning t, int day)
    {
        // A NaN distance is "not home", and a party with no home left has nowhere to walk to.
        long daysOut = (long)day - record.StartDay;
        bool home = !read.HasHome || read.DistanceToHome <= t.HomeRadius || daysOut >= 2L * t.MaxHostDays;
        if (!home)
        {
            _adapter.OrderGoHome(record.PartyId);
            return;
        }
        if (!RemovePlanned(record.PartyId))
            return; // the engine refused (a battle just began); the next hour tries again

        RemoveHost(record);
        StartCooldown(record.Side);
        _notices.ShowWentHome(NameOf(record));
        _logger.LogInfo($"{Tag} {record.HostId} ({record.PartyId}) went home after {record.Raids}/{record.Quota} raids");
    }

    public void OnVillageStateChanged(bool looted, string? raiderPartyId)
    {
        if (!looted || raiderPartyId == null)
            return;
        // Fires inside the finalizing raid map event: record only, and act at the next hourly step.
        var record = Find(raiderPartyId);
        if (record == null)
            return;
        record.Raids++;
        _raidsToday++;
    }

    public void OnPartyDestroyed(string? partyId, string? destroyerFactionId = null, string? destroyerCultureId = null,
        bool destroyerIsKingdom = false, bool destroyerIsMainParty = false)
    {
        if (partyId == null || _planned.Contains(partyId))
            return;
        var record = Find(partyId);
        if (record == null)
            return;

        RemoveHost(record);
        StartCooldown(record.Side);
        _notices.ShowBroken(NameOf(record));
        _logger.LogInfo($"{Tag} {record.HostId} ({partyId}) was broken after {record.Raids}/{record.Quota} raids by "
            + DestroyerText(record, destroyerFactionId, destroyerCultureId, destroyerIsKingdom, destroyerIsMainParty));
    }

    // "its own side" only for an AI party of a kingdom on the host's side: the parties the own-side rule holds back. The
    // engine names the leader party of the side the host fought (usually the winner), not every party in the battle.
    private string DestroyerText(RaidHostRecord record, string? factionId, string? cultureId, bool isKingdom, bool isMainParty)
    {
        if (isMainParty)
            return "the player's party";
        if (string.IsNullOrEmpty(factionId))
            return "an unknown party";
        if (!isKingdom)
            return $"{factionId}, no kingdom";
        var side = _alignment.ResolveSide(factionId!, cultureId ?? string.Empty);
        return side == record.Side ? $"{factionId}, its own side" : $"{factionId}, side {side}";
    }
}
