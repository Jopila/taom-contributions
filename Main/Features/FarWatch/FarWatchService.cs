using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.CoopInterop;
using TAOM.Features.FieldCamp;
using TAOM.Features.HiddenHosts;

namespace TAOM.Features.FarWatch;

/// <summary>
/// The rules of Far Watch (#847, docs/features/far-watch.md): who may ride out from the camp and what blocks it, how a ride
/// starts, runs, returns and ends early, what the hourly look decides, and the sight a ride adds to the party. Plain data in
/// and out; every engine read goes through <see cref="IFarWatchWorldAdapter"/> and every number through
/// <see cref="FarWatchMath"/>. Time is always passed in, as campaign hours. Single-player only (the Eye of Sauron's and From the
/// Ashes' rule), and the Scouting XP also needs the authority gate. The sweep and the settlement report are in
/// <c>FarWatchService.Sweep.cs</c>. Main thread; a process singleton whose only state is <see cref="FarWatchStore"/>.
/// </summary>
internal sealed partial class FarWatchService
{
    private readonly FarWatchStore _store;
    private readonly IFarWatchWorldAdapter _world;
    private readonly HiddenHostsService _hidden;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _dedicated;
    private readonly ICampSettingsProvider _camps;
    private readonly IModLogger _logger;

    public FarWatchService(FarWatchStore store, IFarWatchWorldAdapter world, HiddenHostsService hidden, ICoopSessionProvider coop,
        IDedicatedServerProvider dedicated, ICampSettingsProvider camps, IModLogger logger)
    {
        _store = store;
        _world = world;
        _hidden = hidden;
        _coop = coop;
        _dedicated = dedicated;
        _camps = camps;
        _logger = logger;
    }

    internal bool HasRide => _store.HasRide;

    /// <summary>
    /// Read live on every use (ICoopSessionProvider's contract). Stands down while a co-op session is live, while a co-op mod
    /// TAOM cannot probe is loaded, on a dedicated server and on a peer that may not write the world.
    /// </summary>
    private bool IsSinglePlayer =>
        !_coop.IsSessionActive && !_coop.ShouldDeferToHost && !_dedicated.IsDedicatedServer && _coop.IsAuthority;

    // --- The offer ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// What the camp option shows and who would ride. A ride that runs always answers Running (so a player forced out of the
    /// wait can return to it); the camp feature being off hides the option otherwise. Then the blocks, in this order: a shared
    /// campaign, an encounter or battle, a camp not yet raised, no scout, a wounded scout, a resting scout. Writes nothing.
    /// </summary>
    internal ScoutOffer GetOffer(in ScoutCampFacts f, double now)
    {
        if (_store.HasRide) return new ScoutOffer(ScoutOfferStatus.Running, selfScout: _store.SelfScout);
        if (!_camps.Enabled || !FiniteFloatValidator.IsFiniteAtLeast(now, 0d)) return new ScoutOffer(ScoutOfferStatus.Hidden);
        if (!IsSinglePlayer) return Blocked(ScoutBlock.SharedCampaign);
        if (f.InEncounter || f.InMapEvent) return Blocked(ScoutBlock.Busy);
        if (!f.CampExists || !f.CampReady) return Blocked(ScoutBlock.CampNotReady);

        string? id;
        string? name;
        int skill;
        bool wounded;
        bool self = false;
        if (f.AssignedScoutInParty && !string.IsNullOrEmpty(f.AssignedScoutId) && f.AssignedScoutId != f.MainHeroId)
        {
            id = f.AssignedScoutId;
            name = f.AssignedScoutName;
            skill = f.AssignedScoutSkill;
            wounded = f.AssignedScoutWounded;
        }
        else if (f.PartyAlone && !string.IsNullOrEmpty(f.MainHeroId))
        {
            id = f.MainHeroId;
            name = f.MainHeroName;
            skill = f.MainHeroSkill;
            wounded = f.MainHeroWounded;
            self = true;
        }
        else
        {
            return Blocked(ScoutBlock.NoScout);
        }

        if (wounded) return Blocked(ScoutBlock.Wounded, id, name, skill, self);

        double restLeft = _store.RestUntilHour - now;
        if (restLeft > 0d)
            return Blocked(ScoutBlock.Resting, id, string.IsNullOrEmpty(_store.RestRiderName) ? name : _store.RestRiderName, skill, self,
                (int)Math.Ceiling(restLeft));

        return new ScoutOffer(ScoutOfferStatus.Ready, ScoutBlock.None, id, name, skill, self);
    }

    private static ScoutOffer Blocked(ScoutBlock block, string? id = null, string? name = null, int skill = 0, bool self = false, int restHoursLeft = 0) =>
        new(ScoutOfferStatus.Blocked, block, id, name, skill, self, restHoursLeft);

    // --- The ride ----------------------------------------------------------------------------------------------------

    /// <summary>
    /// Sends the scout. It asks <see cref="GetOffer"/> (one method, never a copy of its checks) and starts the ride only when
    /// the answer is Ready; whatever it answers is returned, so a caller reads the block that stopped it. A settlement ride
    /// needs a settlement; no ride needs a kind.
    /// </summary>
    internal ScoutOffer TryStart(ScoutKind kind, string? siteId, string? siteName, in ScoutCampFacts f, double now)
    {
        var offer = GetOffer(in f, now);
        if (offer.Status != ScoutOfferStatus.Ready) return offer;

        bool kindOk = kind == ScoutKind.Surroundings || (kind == ScoutKind.Settlement && !string.IsNullOrEmpty(siteId));
        if (!kindOk) return new ScoutOffer(ScoutOfferStatus.Hidden);

        _store.StartRide(kind, offer.ScoutId!, offer.ScoutName ?? "", offer.SelfScout, kind == ScoutKind.Settlement ? siteId : null,
            kind == ScoutKind.Settlement ? siteName : null, now, FarWatchMath.RideHours(kind, offer.ScoutSkill),
            Math.Min(Math.Max(offer.ScoutSkill, 0), FarWatchMath.StoredSkillCap));
        return offer;
    }

    /// <summary>How far along the ride is, from its stored start. Cheap: the wait menu asks it every frame.</summary>
    internal ScoutProgress Progress(double now)
    {
        if (!_store.HasRide) return default;
        return new ScoutProgress(true, FarWatchMath.Fraction(_store.StartHour, _store.DurationHours, now), _store.DurationHours, _store.Kind,
            _store.HeroName, _store.TargetName, _store.SelfScout);
    }

    /// <summary>
    /// The scout returns on time. Idempotent: with no ride it does nothing, so the wait's own completion and the hourly look
    /// cannot both pay. The order matters: the ride is copied and CLEARED and the rest started before anything is written to the
    /// world, so a throw in the XP write cannot leave a ride that pays again; then the hero is looked up (gone gives a Lost
    /// report with nothing else), the sight is set (a surroundings ride), the XP is paid (authority only), and the report is
    /// gathered.
    /// </summary>
    internal ScoutReport? Complete(double now)
    {
        if (!_store.HasRide) return null;

        // The same predicate as the hourly look: with Field Camp off the contributor returns 0, so a report would promise a
        // sight that is not there. The ride ends as a struck camp instead, and the player is told in one line.
        if (FieldCampOff)
        {
            var endedKind = _store.Kind;
            string endedName = _store.HeroName;
            return new ScoutReport(endedKind, endedName, lost: false) { Ended = End(now, ScoutEndReason.CampStruck) };
        }

        var kind = _store.Kind;
        string heroId = _store.HeroId;
        string heroName = _store.HeroName;
        string targetId = _store.TargetId;
        string targetName = _store.TargetName;
        int skill = _store.Skill;
        _store.ClearRide();
        StartRest(now, heroName);

        if (!_world.IsHeroWithParty(heroId)) return new ScoutReport(kind, heroName, lost: true);

        var report = new ScoutReport(kind, heroName, lost: false);
        double namedUntil = kind == ScoutKind.Surroundings ? RaiseSight(report, now, skill) : 0d;
        if (_coop.IsAuthority && CoopSessionPolicy.MayWriteSaveBackedState(_coop.IsCoopClient)
            && !_world.GrantScoutingXp(heroId, FarWatchMath.Xp(skill)))
            _logger.LogWarning($"[FarWatch] could not credit the Scouting XP to '{heroId}'");

        if (kind == ScoutKind.Surroundings) SweepSurroundings(report, now, skill, namedUntil);
        else ReadSettlement(report, targetId, targetName);
        return report;
    }

    /// <summary>
    /// The ride ends early (called back, a battle, the camp struck, the scout lost, a shared campaign): no report and no XP. A
    /// surroundings ride keeps the sight it had reached, which then holds and fades on the normal schedule; a settlement ride
    /// adds nothing. The rest starts. Null with no ride.
    /// </summary>
    internal ScoutEndNotice? End(double now, ScoutEndReason reason)
    {
        if (!_store.HasRide) return null;

        var kind = _store.Kind;
        string name = _store.HeroName;
        float reached = kind == ScoutKind.Surroundings
            ? FarWatchMath.Ramp(_store.StartHour, _store.DurationHours, now)
            : 0f;
        float held = FarWatchMath.BuffAt(_store.BuffLevel, _store.BuffStartHour, _store.BuffHoldHours, now);
        int skill = _store.Skill;
        _store.ClearRide();
        StartRest(now, name);

        if (reached > held)
            _store.SetBuff(reached, now, FarWatchMath.HoldHours(skill));
        return new ScoutEndNotice(reason, name);
    }

    /// <summary>
    /// The hourly look at the running ride, from state alone (never the menu id): a shared campaign, the camp gone or Field Camp off, a battle or
    /// encounter and a scout who is gone each end it; otherwise it is due or not. A due ride is not completed here: the caller
    /// does, so the report has one place to be shown. Precedence follows the order listed.
    /// </summary>
    internal ScoutTurn Reconcile(in ScoutCampFacts f, double now)
    {
        if (!_store.HasRide) return default;

        if (!IsSinglePlayer) return Ended(now, ScoutEndReason.SharedCampaign);
        // Field Camp switched off mid-ride: the contributor already returns 0, so the sight line the ride promised is gone while
        // the scout-away rule would still stand. End the ride so both stop together.
        if (!f.CampExists || FieldCampOff) return Ended(now, ScoutEndReason.CampStruck);
        if (f.InMapEvent || f.InEncounter) return Ended(now, ScoutEndReason.Battle);
        if (!_world.IsHeroWithParty(_store.HeroId)) return Ended(now, ScoutEndReason.Lost);

        double dueAt = _store.StartHour + _store.DurationHours;
        return FiniteFloatValidator.IsFiniteAtLeast(now, dueAt) ? new ScoutTurn(ScoutTurnKind.Due) : default;
    }

    /// <summary>Field Camp's master toggle is off: the camp effects, the sight among them, are not applied.</summary>
    private bool FieldCampOff => !_camps.Enabled;

    private ScoutTurn Ended(double now, ScoutEndReason reason)
    {
        return new ScoutTurn(ScoutTurnKind.Ended, End(now, reason));
    }

    private void StartRest(double now, string riderName)
    {
        if (FiniteFloatValidator.IsFinite(now)) _store.SetRest(now + FarWatchMath.RestHours, riderName);
    }

    // --- Sight -------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The share of base sight the scout adds to the main party now: the larger of the kept sight and the ramp of a running
    /// surroundings ride, less 35 % while a settlement ride runs. Asked per spotting query, so it is plain arithmetic on the store's fields (pinned by an allocated-bytes test). 0 without a usable
    /// clock, and never a number the engine could choke on.
    /// </summary>
    internal float SightFactor(double now)
    {
        if (!UsableClock(now)) return 0f;
        float total = Lift(now) + (_store.Kind == ScoutKind.Settlement ? FarWatchMath.SettlementPenalty : 0f);
        return FiniteFloatValidator.IsFinite(total) ? total : 0f;
    }

    /// <summary>How much the scout lifts vanilla's cap on sight: the same lift, with no penalty (a settlement ride lowers sight under the cap).</summary>
    internal float SightCapBonus(double now)
    {
        if (!UsableClock(now)) return 0f;
        float lift = Lift(now);
        return FiniteFloatValidator.IsFinite(lift) ? lift : 0f;
    }

    private float Lift(double now)
    {
        float kept = FarWatchMath.BuffAt(_store.BuffLevel, _store.BuffStartHour, _store.BuffHoldHours, now);
        float ramp = _store.Kind == ScoutKind.Surroundings ? FarWatchMath.Ramp(_store.StartHour, _store.DurationHours, now) : 0f;
        return Math.Max(kept, ramp);
    }

    private static bool UsableClock(double now) => FiniteFloatValidator.IsFiniteAtLeast(now, 0d);

    /// <summary>
    /// The sight a returned surroundings ride gives: the full +50 %, held for 24 to 48 hours by Scouting and fading over half as
    /// long again. A buff already held at full strength whose hold ends later than the new one would is kept as it is, so a
    /// weaker scout's return never shortens a longer sight; otherwise the new one is set. Returns the hour the sight is over,
    /// which is also how long the hosts the report names keep their names, and writes the full-strength hours left to the report.
    /// </summary>
    private double RaiseSight(ScoutReport report, double now, int skill)
    {
        double hold = FarWatchMath.HoldHours(skill);
        double heldEnd = _store.BuffStartHour + _store.BuffHoldHours;
        bool keepHeld = FarWatchMath.BuffAt(_store.BuffLevel, _store.BuffStartHour, _store.BuffHoldHours, now) >= FarWatchMath.RampMax
                        && heldEnd > now + hold;
        if (keepHeld)
        {
            report.SightHours = (int)Math.Round(heldEnd - now);
            return heldEnd + _store.BuffHoldHours / 2d;
        }

        _store.SetBuff(FarWatchMath.RampMax, now, hold);
        report.SightHours = (int)Math.Round(hold);
        return now + hold + hold / 2d;
    }

    // --- The pick ----------------------------------------------------------------------------------------------------

    /// <summary>Fills <paramref name="into"/> (cleared first) with the towns and castles the pick offers, nearest first.</summary>
    internal void PickSites(List<ScoutSite> into)
    {
        var fiefs = new List<ScoutSite>();
        _world.CollectFiefs(fiefs);
        FarWatchMath.PickSites(fiefs, into);
    }
}
