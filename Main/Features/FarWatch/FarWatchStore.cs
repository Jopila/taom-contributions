using System;
using System.Collections.Generic;
using System.Text;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.FarWatch;

/// <summary>The saved record of the store as plain values: what the behavior's SyncData keys carry.</summary>
internal sealed class FarWatchRecord
{
    internal int Kind;
    internal string? HeroId;
    internal string? HeroName;
    internal bool SelfScout;
    internal string? TargetId;
    internal string? TargetName;
    internal double StartHour;
    internal double DurationHours;
    internal int Skill;
    internal float BuffLevel;
    internal double BuffStartHour;
    internal double BuffHoldHours;
    internal double RestUntilHour;
    internal string? RestRiderName;
    internal List<string>? NamedIds;
    internal double NamedUntilHour;
}

/// <summary>
/// What Far Watch remembers (docs/features/far-watch.md "Data and save"): the one ride, the sight a ride left behind, the rest
/// that follows every ride and the hosts the last sweep named. Plain ids, strings, ints and campaign hours as doubles: no engine
/// type and no <c>CampaignTime</c>, so a process singleton can hold it and a test can build it. Main thread. The record that
/// travels through a save is validated part by part on the way in: a bad part is dropped, never applied, and the load carries on.
/// It also answers Hidden Hosts through <see cref="IScoutIntelQuery"/> from its own fields, with no allocation.
/// </summary>
internal sealed class FarWatchStore : IScoutIntelQuery
{
    /// <summary>The hosts a sweep names are the hosts its report lists, so the store holds as many as the report has rows.</summary>
    internal const int MaxNamed = FarWatchMath.MaxRows;

    private readonly IModLogger _logger;
    private readonly string[] _named = new string[MaxNamed];

    public FarWatchStore(IModLogger logger) => _logger = logger;

    internal ScoutKind Kind { get; private set; }

    internal bool HasRide => Kind != ScoutKind.None;

    internal string HeroId { get; private set; } = "";

    internal string HeroName { get; private set; } = "";

    internal bool SelfScout { get; private set; }

    internal string TargetId { get; private set; } = "";

    internal string TargetName { get; private set; } = "";

    internal double StartHour { get; private set; }

    internal double DurationHours { get; private set; }

    internal int Skill { get; private set; }

    internal float BuffLevel { get; private set; }

    internal double BuffStartHour { get; private set; }

    internal double BuffHoldHours { get; private set; }

    internal double RestUntilHour { get; private set; }

    /// <summary>Who rode last, for the rest text: the scout named now may be another man than the one who is resting.</summary>
    internal string RestRiderName { get; private set; } = "";

    internal double NamedUntilHour { get; private set; }

    internal int NamedCount { get; private set; }

    public bool IsScoutAway => Kind == ScoutKind.Settlement;

    public bool IsNamedByScout(string partyId, int nowHour)
    {
        if (NamedCount == 0 || nowHour < 0 || string.IsNullOrEmpty(partyId)) return false;
        if (!(nowHour < NamedUntilHour)) return false;
        for (int i = 0; i < NamedCount; i++)
            if (string.Equals(_named[i], partyId, StringComparison.Ordinal)) return true;
        return false;
    }

    internal void StartRide(ScoutKind kind, string heroId, string heroName, bool selfScout, string? targetId, string? targetName,
        double startHour, double durationHours, int skill)
    {
        Kind = kind;
        HeroId = heroId;
        HeroName = heroName;
        SelfScout = selfScout;
        TargetId = targetId ?? "";
        TargetName = targetName ?? "";
        StartHour = startHour;
        DurationHours = durationHours;
        Skill = skill;
    }

    /// <summary>Ends the ride: the buff, the rest and the named hosts are not part of it.</summary>
    internal void ClearRide()
    {
        Kind = ScoutKind.None;
        HeroId = "";
        HeroName = "";
        SelfScout = false;
        TargetId = "";
        TargetName = "";
        StartHour = 0d;
        DurationHours = 0d;
        Skill = 0;
    }

    internal void SetBuff(float level, double startHour, double holdHours)
    {
        BuffLevel = level;
        BuffStartHour = startHour;
        BuffHoldHours = holdHours;
    }

    internal void ClearBuff() => SetBuff(0f, 0d, 0d);

    internal void SetRest(double untilHour, string riderName = "")
    {
        RestUntilHour = untilHour;
        RestRiderName = riderName ?? "";
    }

    /// <summary>Replaces the named hosts: blank ids are skipped and at most <see cref="MaxNamed"/> are kept.</summary>
    internal void SetNamed(IReadOnlyList<string> ids, double untilHour)
    {
        NamedCount = 0;
        for (int i = 0; i < ids.Count && NamedCount < MaxNamed; i++)
            if (!string.IsNullOrEmpty(ids[i])) _named[NamedCount++] = ids[i];
        NamedUntilHour = NamedCount == 0 ? 0d : untilHour;
    }

    /// <summary>
    /// Forgets everything: the ride, the kept sight, the rest and the named hosts. Runs for every campaign start (the behavior's
    /// constructor) and at the start of a load. <see cref="ClearRide"/> and <see cref="ClearBuff"/> each clear one part.
    /// </summary>
    internal void ResetForNewSession()
    {
        ClearRide();
        ClearBuff();
        RestUntilHour = 0d;
        RestRiderName = "";
        NamedCount = 0;
        NamedUntilHour = 0d;
        Array.Clear(_named, 0, _named.Length);
    }

    /// <summary>The store as a record. Never changes the store.</summary>
    internal FarWatchRecord SaveInto()
    {
        var record = new FarWatchRecord
        {
            Kind = (int)Kind, HeroId = HeroId, HeroName = HeroName, SelfScout = SelfScout, TargetId = TargetId, TargetName = TargetName,
            StartHour = StartHour, DurationHours = DurationHours, Skill = Skill, BuffLevel = BuffLevel, BuffStartHour = BuffStartHour,
            BuffHoldHours = BuffHoldHours, RestUntilHour = RestUntilHour, RestRiderName = RestRiderName, NamedUntilHour = NamedUntilHour,
        };
        if (NamedCount > 0)
        {
            record.NamedIds = new List<string>(NamedCount);
            for (int i = 0; i < NamedCount; i++) record.NamedIds.Add(_named[i]);
        }
        return record;
    }

    /// <summary>
    /// Replaces everything with the saved record (a load always replaces: the store is process-lifetime, and the last
    /// campaign's ride must not leak into this one). The ride, the buff, the rest and the named hosts are judged apart; a
    /// part that cannot be what it claims is dropped, and the load writes one warning naming what went.
    /// </summary>
    internal void LoadFrom(FarWatchRecord record)
    {
        ResetForNewSession();
        var dropped = new StringBuilder();

        LoadRide(record, dropped);
        LoadBuff(record, dropped);

        RestUntilHour = FiniteFloatValidator.IsFiniteAtLeast(record.RestUntilHour, 0d) ? record.RestUntilHour : 0d;
        if (RestUntilHour == 0d && record.RestUntilHour != 0d) Note(dropped, "the rest hour");
        else RestRiderName = record.RestRiderName ?? "";

        LoadNamed(record, dropped);

        if (dropped.Length > 0)
            _logger.LogWarning($"[FarWatch] dropped from the saved record: {dropped}");
    }

    private void LoadRide(FarWatchRecord record, StringBuilder dropped)
    {
        if (record.Kind == 0) return;

        string? why = null;
        if (record.Kind != (int)ScoutKind.Surroundings && record.Kind != (int)ScoutKind.Settlement) why = $"unknown kind {record.Kind}";
        else if (string.IsNullOrEmpty(record.HeroId)) why = "no scout";
        else if (record.Kind == (int)ScoutKind.Settlement && string.IsNullOrEmpty(record.TargetId)) why = "no settlement";
        else if (!FiniteFloatValidator.IsFiniteAtLeast(record.StartHour, 0d)) why = "a start hour that is not usable";
        else if (!(FiniteFloatValidator.IsFinite(record.DurationHours) && record.DurationHours > 0d && record.DurationHours <= FarWatchMath.MaxRideHours))
            why = $"a length of {record.DurationHours} hours";

        if (why != null)
        {
            Note(dropped, "the ride (" + why + ")");
            return;
        }

        StartRide((ScoutKind)record.Kind, record.HeroId!, record.HeroName ?? "", record.SelfScout, record.TargetId, record.TargetName,
            record.StartHour, record.DurationHours, Math.Min(Math.Max(record.Skill, 0), FarWatchMath.StoredSkillCap));
    }

    private void LoadBuff(FarWatchRecord record, StringBuilder dropped)
    {
        bool none = record.BuffLevel == 0f && record.BuffStartHour == 0d && record.BuffHoldHours == 0d;
        if (none) return;

        bool usable = FiniteFloatValidator.IsFiniteInRange(record.BuffLevel, 0f, FarWatchMath.RampMax)
                      && FiniteFloatValidator.IsFiniteAtLeast(record.BuffStartHour, 0d)
                      && FiniteFloatValidator.IsFiniteInRange(record.BuffHoldHours, 0d, FarWatchMath.MaxHoldHours);
        if (usable) SetBuff(record.BuffLevel, record.BuffStartHour, record.BuffHoldHours);
        else Note(dropped, "the kept sight");
    }

    private void LoadNamed(FarWatchRecord record, StringBuilder dropped)
    {
        var ids = record.NamedIds;
        if (ids == null || ids.Count == 0) return;

        if (!FiniteFloatValidator.IsFiniteAtLeast(record.NamedUntilHour, 0d))
        {
            Note(dropped, "the named hosts");
            return;
        }

        SetNamed(ids, record.NamedUntilHour);
    }

    private static void Note(StringBuilder dropped, string part)
    {
        if (dropped.Length > 0) dropped.Append(", ");
        dropped.Append(part);
    }
}
