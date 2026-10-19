using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Relations;

namespace TAOM.Features.BondsOfBattle;

/// <summary>
/// Relation earned by handing troops to a lord on the Donate Troops screen. The screen reports the influence the gift is
/// worth; half of it, held to 10 and a point either way, is the lord's goodwill; a gift worth less than 2 earns nothing.
/// Hand-overs stop paying a lord once 10 relation has landed with him that day; the Done that reaches 10 asks only for what is
/// left, and Charm can raise it. A lord gains at most 10 a day from hand-overs, however the gift is split (Charm can raise the last Done).
/// </summary>
public sealed class TroopHandoverService
{
    internal const int InfluencePerPoint = 2;
    internal const int MaxBase = 10;

    /// <summary>The most relation one lord gains from hand-overs in one in-game day.</summary>
    internal const int DailyCap = 10;

    private readonly IRelationGrantService _grants;
    private readonly IBondsOfBattleSettingsProvider _settings;
    private readonly IEnlistmentStateQuery _enlistment;
    private readonly IRandomSource _random;
    private readonly IBondsNoticeAdapter _notice;
    private readonly ICoopSessionProvider _coop;

    // Relation owner id to the relation that landed with him today. In memory only, and a process singleton's: the behavior
    // clears it at each campaign's start (ResetForNewSession, so a load mid-day starts the day over) and on every daily tick
    // (StartNewDay), so it holds at most the owners handed troops today.
    private readonly Dictionary<string, int> _landedToday = new(StringComparer.Ordinal);

    public TroopHandoverService(IRelationGrantService grants, IBondsOfBattleSettingsProvider settings,
        IEnlistmentStateQuery enlistment, IRandomSource random, IBondsNoticeAdapter notice, ICoopSessionProvider coop)
    {
        _grants = grants;
        _settings = settings;
        _enlistment = enlistment;
        _random = random;
        _notice = notice;
        _coop = coop;
    }

    /// <summary>Empties the per-campaign state: the daily ledger. Called from the behavior's constructor, which the module builds in every campaign.</summary>
    public void ResetForNewSession() => _landedToday.Clear();

    /// <summary>A new in-game day: every owner may be paid up to the cap again.</summary>
    public void StartNewDay() => _landedToday.Clear();

    /// <summary>The relation a gift worth <paramref name="influence"/> earns: 0 for a gift worth under 2, else half of it held to 10, a point either way.</summary>
    public int ComputeGain(int influence)
    {
        if (influence <= 0)
            return 0;

        // No floor of 1: a gift worth 2 or 3 can roll 0, which OnHandover's gate drops, so across the three rolls the average
        // is half the influence.
        var baseGain = Math.Min(MaxBase, influence / InfluencePerPoint);
        if (baseGain <= 0)
            return 0;

        var spread = Math.Max(-1, Math.Min(1, _random.Next(-1, 2)));
        return baseGain + spread;
    }

    /// <summary>
    /// True when the lord's relation was raised. A lord of the player's own clan, or the player hero, earns nothing; the
    /// patch reads those two facts off the screen and this service decides.
    /// </summary>
    public bool OnHandover(string? lordHeroId, string lordName, int influence, bool lordIsPlayerClan, bool lordIsPlayerHero,
        string? relationHeroId)
    {
        var settings = _settings.Get();
        if (!settings.Enabled || !settings.TroopHandoverEnabled || string.IsNullOrEmpty(lordHeroId) || _enlistment.IsEnlisted
            || _coop.ShouldDeferToHost || lordIsPlayerClan || lordIsPlayerHero)
            return false;

        var gain = ComputeGain(influence);
        if (gain <= 0)
            return false;

        // The cap is on the hero the relation lands on (his clan leader without personal relations), so kinsmen share one day.
        var owner = string.IsNullOrEmpty(relationHeroId) ? lordHeroId! : relationHeroId!;
        var already = _landedToday.TryGetValue(owner, out var sofar) ? sofar : 0;
        var allowed = Math.Min(gain, DailyCap - already);
        if (allowed <= 0)
            return false;

        var landed = _grants.Grant(lordHeroId!, allowed, "troop hand-over, influence " + influence);
        if (landed == 0)
            return false;

        // Only a change that landed counts towards the day.
        _landedToday[owner] = already + Math.Max(0, landed);
        _notice.ShowHandover(lordName);
        return true;
    }
}
