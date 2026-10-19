using System;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Relations;

namespace TAOM.Features.BondsOfBattle;

/// <summary>
/// Relation earned by handing troops to a lord on the Donate Troops screen. The screen reports the influence the gift is
/// worth; a tenth of it, from 1 to 10 and a point either way, is the lord's goodwill.
/// </summary>
public sealed class TroopHandoverService
{
    internal const int InfluencePerPoint = 10;
    internal const int MinBase = 1;
    internal const int MaxBase = 10;

    private readonly IRelationGrantService _grants;
    private readonly IBondsOfBattleSettingsProvider _settings;
    private readonly IEnlistmentStateQuery _enlistment;
    private readonly IRandomSource _random;
    private readonly IBondsNoticeAdapter _notice;
    private readonly ICoopSessionProvider _coop;

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

    /// <summary>The relation a gift worth <paramref name="influence"/> earns: 0 for none, else 1 and up.</summary>
    public int ComputeGain(int influence)
    {
        if (influence <= 0)
            return 0;

        var baseGain = Math.Min(MaxBase, Math.Max(MinBase, influence / InfluencePerPoint));
        var spread = Math.Max(-1, Math.Min(1, _random.Next(-1, 2)));
        return Math.Max(1, baseGain + spread);
    }

    /// <summary>
    /// True when the lord's relation was raised. A lord of the player's own clan, or the player hero, earns nothing; the
    /// patch reads those two facts off the screen and this service decides.
    /// </summary>
    public bool OnHandover(string? lordHeroId, string lordName, int influence, bool lordIsPlayerClan, bool lordIsPlayerHero)
    {
        var settings = _settings.Get();
        if (!settings.Enabled || !settings.TroopHandoverEnabled || string.IsNullOrEmpty(lordHeroId) || _enlistment.IsEnlisted
            || _coop.ShouldDeferToHost || lordIsPlayerClan || lordIsPlayerHero)
            return false;

        var gain = ComputeGain(influence);
        if (gain <= 0 || !_grants.Grant(lordHeroId!, gain, "troop hand-over, influence " + influence))
            return false;

        _notice.ShowHandover(lordName);
        return true;
    }
}
