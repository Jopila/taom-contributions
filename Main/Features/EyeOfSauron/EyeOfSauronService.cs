using System;
using System.Collections.Generic;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.AiPartySize;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;

namespace TAOM.Features.EyeOfSauron;

/// <summary>
/// The Eye of Sauron hunt (#765 M8, docs/features/eye-of-sauron.md). Battles the player wins against an enemy kingdom
/// build a decaying deed score; past the threshold, and only while the player's kingdom is at war with the hunter's,
/// a hunter rides: one of the Nine for a Free player, Glorfindel for an Evil one. A hunter who leads a party has that
/// party re-tasked and topped up; a free hunter gets a new party out of the player's sight. Out of sight the hunter
/// closes in steps each hour; in sight the speed model lifts him above the player's speed. Every end releases the AI
/// pin the hunt set. All engine work goes through <see cref="IHunterPartyAdapter"/>; this class holds no engine type.
/// Single-player only: a co-op session or a dedicated server stops and ends the hunt.
/// </summary>
public sealed class EyeOfSauronService : IEyeOfSauronHook
{
    /// <summary>No party at war with the hunter may stand this close to a landing point (vanilla's own scan uses 0.5).</summary>
    internal const float EnemyClearance = 3f;

    /// <summary>The landing point keeps this many of the player's seeing ranges away from him.</summary>
    internal const float SightMargin = 1.5f;

    /// <summary>A further gap, in map units, kept between the step's end and the sight margin.</summary>
    internal const float StepSlack = 3f;

    /// <summary>A change in the player's speed beyond this fraction refreshes the hunter's cached speed.</summary>
    internal const float SpeedRefreshFraction = 0.05f;

    private const float MinFactorScale = 0.01f;
    private const double CeilingSlack = 1e-3;
    private const int FoodDays = 3;
    private const int MenPerFood = 20;

    private readonly IHunterPartyAdapter _adapter;
    private readonly IAlignmentService _alignment;
    private readonly IEyeOfSauronSettings _settings;
    private readonly ICoopSessionProvider _coopSession;
    private readonly IDedicatedServerProvider _dedicatedServer;
    private readonly IModLogger _logger;

    private float _scoreVsEvil;
    private float _scoreVsFree;
    private float _lastDecayDay;
    private float _cooldownUntilDay;
    private string? _hunterHeroId;
    private string? _hunterPartyId;
    private bool _pinnedByUs;
    private float _huntStartDay;

    // Published from the main thread, read by the speed and size models on the parallel party tick.
    private HuntSnapshot? _snapshot;
    private int _sizeRejectionLogged;

    public EyeOfSauronService(IHunterPartyAdapter adapter, IAlignmentService alignment, IEyeOfSauronSettings settings,
        ICoopSessionProvider coopSession, IDedicatedServerProvider dedicatedServer, IModLogger logger)
    {
        _adapter = adapter;
        _alignment = alignment;
        _settings = settings;
        _coopSession = coopSession;
        _dedicatedServer = dedicatedServer;
        _logger = logger;
    }

    public string? HunterHeroId => _hunterHeroId;

    public string? HunterPartyId => _hunterPartyId;

    public bool IsHunting => _hunterPartyId != null;

    public bool PinnedByUs => _pinnedByUs;

    /// <summary>Read live on every use, never cached (ICoopSessionProvider's contract).</summary>
    private bool IsSinglePlayer => !_coopSession.IsSessionActive && !_dedicatedServer.IsDedicatedServer;

    private float Now => (float)_adapter.NowDays;

    // ------------------------------------------------------------------ session lifecycle

    /// <summary>
    /// Clears every per-campaign field. Called from the behavior's constructor, which the feature module runs for every
    /// campaign before any event or SyncData, so campaign B never inherits campaign A's hunt (csharp-architecture.md).
    /// Releases nothing: the previous campaign's parties are gone.
    /// </summary>
    public void ResetForNewSession()
    {
        _scoreVsEvil = 0f;
        _scoreVsFree = 0f;
        _lastDecayDay = 0f;
        _cooldownUntilDay = 0f;
        ClearHunt();
    }

    public EyeOfSauronState CaptureState() => new()
    {
        ScoreVsEvil = _scoreVsEvil,
        ScoreVsFree = _scoreVsFree,
        LastDecayDay = _lastDecayDay,
        CooldownUntilDay = _cooldownUntilDay,
        HunterHeroId = _hunterHeroId,
        HunterPartyId = _hunterPartyId,
        PinnedByUs = _pinnedByUs,
        HuntStartDay = _huntStartDay,
    };

    /// <summary>Restores a saved record. The in-sight state is dropped; the hunter's party stays registered for its size.</summary>
    public void LoadFrom(EyeOfSauronState state)
    {
        ResetForNewSession();
        if (state == null) return;

        _scoreVsEvil = FiniteOrZero(state.ScoreVsEvil);
        _scoreVsFree = FiniteOrZero(state.ScoreVsFree);
        _lastDecayDay = FiniteOrZero(state.LastDecayDay);
        _cooldownUntilDay = FiniteOrZero(state.CooldownUntilDay);
        if (string.IsNullOrEmpty(state.HunterHeroId) || string.IsNullOrEmpty(state.HunterPartyId))
            return;

        _hunterHeroId = state.HunterHeroId;
        _hunterPartyId = state.HunterPartyId;
        _pinnedByUs = state.PinnedByUs;
        _huntStartDay = FiniteOrZero(state.HuntStartDay);
        Publish(new HuntSnapshot(state.HunterPartyId!, inSight: false, playerSpeed: 0f, multiplier: 0f));
    }

    /// <summary>After a load: a hunt whose party is gone ends; a stale pin flag without a hunt is dropped.</summary>
    public void OnSessionLaunched()
    {
        if (!IsHunting)
        {
            _pinnedByUs = false;
            return;
        }

        if (!_adapter.ReadParty(_hunterPartyId!).Exists)
            EndHunt(HuntEndReason.PartyLost);
    }

    // ------------------------------------------------------------------ deeds and scores

    public float GetScore(FactionSide against) =>
        Decay(against == FactionSide.Evil ? _scoreVsEvil : against == FactionSide.Free ? _scoreVsFree : 0f,
            Now - _lastDecayDay, _settings.HalfLifeDays);

    /// <summary>Console: adds to the score earned against one side. Never below zero; a non-finite amount is ignored.</summary>
    public void AddScore(FactionSide against, float amount)
    {
        if (!FiniteFloatValidator.IsFinite(amount) || against == FactionSide.Neutral) return;
        ApplyDecay();
        if (against == FactionSide.Evil)
            _scoreVsEvil = Math.Max(0f, _scoreVsEvil + amount);
        else
            _scoreVsFree = Math.Max(0f, _scoreVsFree + amount);
    }

    public void OnMapEventEnded(HuntBattleSummary summary)
    {
        if (summary == null) return;

        if (IsHunting && summary.InvolvesHunter)
            EndHunt(summary.IsPlayerEvent && summary.PlayerWon ? HuntEndReason.HunterBeaten : HuntEndReason.BattleEnded);

        if (!summary.IsPlayerEvent || !summary.PlayerWon || string.IsNullOrEmpty(summary.LoserKingdomId)) return;

        // GetKingdomSide, never ResolveSide (a culture fallback) or AreEnemyAlignments (Neutral is everyone's enemy).
        var side = _alignment.GetKingdomSide(summary.LoserKingdomId!);
        if (side == FactionSide.Neutral) return;
        AddScore(side, DeedWeight(summary.Kind));
    }

    private void ApplyDecay()
    {
        float now = Now;
        float halfLife = _settings.HalfLifeDays;
        _scoreVsEvil = Decay(_scoreVsEvil, now - _lastDecayDay, halfLife);
        _scoreVsFree = Decay(_scoreVsFree, now - _lastDecayDay, halfLife);
        if (FiniteFloatValidator.IsFinite(now))
            _lastDecayDay = now;
    }

    /// <summary>
    /// <c>score * 0.5^(elapsed / halfLife)</c>. A non-finite score is zero; a negative or non-finite elapsed time, or a
    /// half-life that is not a positive finite number, leaves the score as it is.
    /// </summary>
    internal static float Decay(float score, double elapsedDays, float halfLifeDays)
    {
        if (!FiniteFloatValidator.IsFinite(score)) return 0f;
        if (!(elapsedDays > 0d) || !FiniteFloatValidator.IsFinite(elapsedDays)) return score;
        if (!(halfLifeDays > 0f) || !FiniteFloatValidator.IsFinite(halfLifeDays)) return score;
        return (float)(score * Math.Pow(0.5d, elapsedDays / halfLifeDays));
    }

    internal static float DeedWeight(DeedKind kind) => kind switch
    {
        DeedKind.Assault => 3f,
        DeedKind.SallyOrOutside => 2f,
        _ => 1f,
    };

    // ------------------------------------------------------------------ starting a hunt

    public void OnDailyTick()
    {
        if (IsHunting)
        {
            CheckActiveHunt();
            return;
        }

        if (!_settings.Enabled || !IsSinglePlayer) return;
        ApplyDecay();
        string verdict = TryStartHunt(ignoreScoreAndCooldown: false);
        if (verdict.StartsWith("Waiting", StringComparison.Ordinal))
            _logger.LogInfo("[EyeOfSauron] " + verdict);
    }

    /// <summary>
    /// Starts a hunt when every gate holds and returns a one-line verdict. The console passes
    /// <paramref name="ignoreScoreAndCooldown"/>; the switch, the single-player gate, the kingdom and the war gate
    /// hold either way.
    /// </summary>
    public string TryStartHunt(bool ignoreScoreAndCooldown)
    {
        if (IsHunting) return $"A hunt is already running ({_hunterHeroId}, party {_hunterPartyId}).";
        if (!_settings.Enabled) return "Refused: the hunt is switched off in MCM.";
        if (!IsSinglePlayer) return "Refused: single-player only, and a co-op session or dedicated server is live.";

        string? kingdomId = _adapter.PlayerKingdomId;
        if (string.IsNullOrEmpty(kingdomId)) return "No hunt: you belong to no kingdom.";
        var side = _alignment.GetKingdomSide(kingdomId!);
        if (side == FactionSide.Neutral) return $"No hunt: your kingdom '{kingdomId}' is on neither side.";

        if (!ignoreScoreAndCooldown)
        {
            float now = Now;
            if (now < _cooldownUntilDay) return $"No hunt: cooldown for {_cooldownUntilDay - now:0.0} more days.";
            float score = GetScore(side == FactionSide.Free ? FactionSide.Evil : FactionSide.Free);
            float threshold = _settings.Threshold;
            if (!(score >= threshold)) return $"No hunt: score {score:0.0} is under the threshold {threshold:0.0}.";
        }

        var candidates = side == FactionSide.Free ? HuntTroopLists.WraithOrder : HuntTroopLists.ElvenOrder;
        foreach (string heroId in candidates)
        {
            var hero = _adapter.ReadHero(heroId);
            if (!CanHunt(hero) || !hero.AtWarWithPlayerKingdom) continue;
            return StartWith(heroId, hero, side);
        }

        return "No hunt: no hunter is free and at war with your kingdom.";
    }

    private static bool CanHunt(HunterHeroSnapshot hero) =>
        hero.Exists && hero.IsAlive && !hero.IsFugitive && !hero.IsPrisoner && !hero.IsHeldByAParty && !hero.IsMarkedForDeath;

    private string StartWith(string heroId, HunterHeroSnapshot hero, FactionSide playerSide)
    {
        string? partyId;
        if (hero.LedPartyId != null)
        {
            var party = _adapter.ReadParty(hero.LedPartyId);
            string? busy = BusyReason(party);
            if (busy != null) return $"Waiting: {heroId}'s party {hero.LedPartyId} is {busy}; never a second party.";
            partyId = hero.LedPartyId;
        }
        else if (hero.IsInAParty)
        {
            return $"Waiting: {heroId} rides in another lord's party; never a second party.";
        }
        else
        {
            if (!hero.IsActive || !hero.CanLeadParty || !hero.IsLord || hero.SettlementBlocksSpawn)
                return $"Waiting: {heroId} cannot lead a party today.";
            partyId = _adapter.SpawnHunterParty(heroId);
            if (string.IsNullOrEmpty(partyId)) return $"Refused: no party could be spawned for {heroId}.";
        }

        // Register the host BEFORE its roster grows: PartySizeLimit is cached per roster version.
        _hunterHeroId = heroId;
        _hunterPartyId = partyId;
        _huntStartDay = Now;
        _pinnedByUs = false;
        Interlocked.Exchange(ref _sizeRejectionLogged, 0);
        Publish(new HuntSnapshot(partyId!, inSight: false, playerSpeed: 0f, multiplier: 0f));

        if (!_adapter.PinAndEngagePlayer(partyId!))
        {
            _logger.LogWarning($"[EyeOfSauron] {heroId}'s party {partyId} could not be ordered after the player; no hunt.");
            ClearHunt();
            return $"Refused: {heroId}'s party could not be ordered after you.";
        }

        _pinnedByUs = true;
        TopUpHost(partyId!, heroId, hero.FactionId);
        TopUpFood(partyId!);
        _logger.LogInfo($"[EyeOfSauron] Hunt started: {heroId} ({hero.FactionId}) with party {partyId}.");
        _adapter.Announce(playerSide == FactionSide.Free ? HuntAnnouncement.StartFree : HuntAnnouncement.StartEvil, heroId);
        return $"Hunt started: {heroId} rides with party {partyId}.";
    }

    /// <summary>Why a party may not be re-tasked today, or null when it may (design 1.3).</summary>
    private static string? BusyReason(HuntPartySnapshot party)
    {
        if (!party.Exists || !party.IsActive) return "not on the map";
        if (party.InMapEvent) return "in a battle";
        if (party.InArmy) return "in an army";
        if (party.Besieging) return "besieging";
        if (party.InSettlement) return "in a settlement";
        if (party.IsDisbanding) return "disbanding";
        if (party.AiPinned) return "held by another order";
        return null;
    }

    // ------------------------------------------------------------------ the host

    private void TopUpHost(string partyId, string heroId, string? factionId)
    {
        var party = _adapter.ReadParty(partyId);
        float playerStrength = _adapter.PlayerStrength;
        if (!(playerStrength > 0f) || !FiniteFloatValidator.IsFinite(playerStrength)) return;
        float hostStrength = FiniteFloatValidator.IsFinite(party.Strength) ? party.Strength : float.NaN;
        float deficit = _settings.HunterStrengthRatio * playerStrength - hostStrength;
        int room = _settings.HunterMaxTroops - party.HeadCount;

        var plan = PlanTopUp(HuntTroopLists.HostFor(heroId, factionId), deficit, id => _adapter.GetTroopPower(id, heroId), room);
        int added = 0;
        foreach (var (troopId, count) in plan)
            added += _adapter.AddTroops(partyId, troopId, count);
        if (added > 0)
            _logger.LogInfo($"[EyeOfSauron] {heroId}'s host topped up by {added} men (deficit {deficit:0.0}, room {room}).");
    }

    /// <summary>
    /// Splits a strength deficit over the host's troops: <c>ceil(deficit * share / power)</c> each, skipping a troop
    /// whose power is not a positive finite number, then scaled down to fit <paramref name="room"/> men in list order.
    /// </summary>
    internal static IReadOnlyList<(string TroopId, int Count)> PlanTopUp(
        IReadOnlyList<HostTroop> host, float deficit, Func<string, float> troopPower, int room)
    {
        var plan = new List<(string TroopId, int Count)>();
        if (host == null || room <= 0 || !(deficit > 0f) || !FiniteFloatValidator.IsFinite(deficit)) return plan;

        long total = 0;
        foreach (var troop in host)
        {
            float power = troopPower(troop.TroopId);
            if (!(power > 0f) || !FiniteFloatValidator.IsFinite(power)) continue;
            // The small slack keeps float noise (1.3f * 100 is 130.000005) from adding a whole extra man.
            double wanted = Math.Ceiling((double)deficit * troop.Share / power - CeilingSlack);
            if (!(wanted >= 1d)) continue;
            int count = wanted > room ? room : (int)wanted;
            plan.Add((troop.TroopId, count));
            total += count;
        }

        if (total <= room) return plan;

        var scaled = new List<(string TroopId, int Count)>(plan.Count);
        int left = room;
        foreach (var (troopId, count) in plan)
        {
            int share = (int)((long)count * room / total);
            scaled.Add((troopId, share));
            left -= share;
        }
        for (int i = 0; left > 0 && i < scaled.Count; i++, left--)
            scaled[i] = (scaled[i].TroopId, scaled[i].Count + 1);
        scaled.RemoveAll(p => p.Count <= 0);
        return scaled;
    }

    /// <summary>Three days of food for the host: <c>ceil(men / 20 * 3)</c>. Never the whole hunt's food: grain is heavy.</summary>
    internal static int FoodTarget(int headCount) =>
        headCount <= 0 ? 0 : (int)Math.Ceiling(headCount * (double)FoodDays / MenPerFood);

    private void TopUpFood(string partyId)
    {
        var party = _adapter.ReadParty(partyId);
        if (party.Exists)
            _adapter.TopUpFood(partyId, FoodTarget(party.HeadCount));
    }

    // ------------------------------------------------------------------ the running hunt

    private void CheckActiveHunt()
    {
        string partyId = _hunterPartyId!;
        string heroId = _hunterHeroId!;
        if (!_settings.Enabled) { EndHunt(HuntEndReason.Disabled); return; }
        if (!IsSinglePlayer) { EndHunt(HuntEndReason.SessionLive); return; }

        var hero = _adapter.ReadHero(heroId);
        if (!CanHunt(hero)) { EndHunt(HuntEndReason.HunterDown); return; }

        var party = _adapter.ReadParty(partyId);
        if (!party.Exists || !string.Equals(party.LeaderHeroId, heroId, StringComparison.Ordinal))
        {
            EndHunt(HuntEndReason.PartyLost);
            return;
        }

        if (string.IsNullOrEmpty(_adapter.PlayerKingdomId)) { EndHunt(HuntEndReason.Kingdomless); return; }
        if (!hero.AtWarWithPlayerKingdom) { EndHunt(HuntEndReason.WarEnded); return; }
        if (!(Now - _huntStartDay < _settings.HuntTimeoutDays)) { EndHunt(HuntEndReason.TimedOut); return; }

        TopUpFood(partyId);
    }

    /// <summary>The hourly tick of one party; only the hunter's party is acted on (design 1.4 and 1.7).</summary>
    public void OnHourlyTickParty(string? partyId)
    {
        if (!IsHunting || !string.Equals(partyId, _hunterPartyId, StringComparison.Ordinal)) return;
        if (!IsSinglePlayer) { EndHunt(HuntEndReason.SessionLive); return; }

        var party = _adapter.ReadParty(partyId!);
        if (!party.Exists || !string.Equals(party.LeaderHeroId, _hunterHeroId, StringComparison.Ordinal))
        {
            EndHunt(HuntEndReason.PartyLost);
            return;
        }

        // Re-issue only when lost: every engage call resets all of the party's movement state.
        if (!party.InMapEvent && !party.IsEngagingPlayer && _adapter.PinAndEngagePlayer(partyId!))
            _pinnedByUs = true;

        TryStep(partyId!);
    }

    private void TryStep(string partyId)
    {
        var ctx = _adapter.ReadStepContext(partyId);
        if (ctx.BlockedBy != null) return;

        float sight = ctx.SeeingRange;
        if (!(sight > 0f) || !FiniteFloatValidator.IsFinite(sight)) return;
        float keepAway = SightMargin * sight;
        if (!(Distance(ctx.HunterX, ctx.HunterY, ctx.PlayerX, ctx.PlayerY) >= keepAway)) return;

        float step = ComputeStep(PathLength(ctx.HunterX, ctx.HunterY, ctx.PathX, ctx.PathY), sight, _settings.HunterStepCap);
        if (!(step > 0f)) return;

        var (x, y) = WalkPath(ctx.HunterX, ctx.HunterY, ctx.PathX, ctx.PathY, step);
        if (!(Distance(x, y, ctx.PlayerX, ctx.PlayerY) >= keepAway)) return;

        var landing = _adapter.ResolveLanding(partyId, x, y, EnemyClearance);
        if (!landing.Ok || !(Distance(landing.X, landing.Y, ctx.PlayerX, ctx.PlayerY) >= keepAway)) return;

        _adapter.Relocate(partyId, landing.X, landing.Y);
    }

    /// <summary><c>min(pathGap - 1.5 S - 3, cap)</c>; zero unless that is a positive finite number.</summary>
    internal static float ComputeStep(float pathGap, float seeingRange, float stepCap)
    {
        if (!FiniteFloatValidator.IsFinite(pathGap) || !FiniteFloatValidator.IsFinite(seeingRange)
            || !FiniteFloatValidator.IsFinite(stepCap))
            return 0f;
        float step = Math.Min(pathGap - SightMargin * seeingRange - StepSlack, stepCap);
        return step > 0f ? step : 0f;
    }

    internal static float PathLength(float startX, float startY, float[] xs, float[] ys)
    {
        int n = Math.Min(xs?.Length ?? 0, ys?.Length ?? 0);
        float total = 0f, px = startX, py = startY;
        for (int i = 0; i < n; i++)
        {
            total += Distance(px, py, xs![i], ys![i]);
            px = xs[i];
            py = ys[i];
        }
        return total;
    }

    /// <summary>The point <paramref name="distance"/> along the path; the last point when the path is shorter.</summary>
    internal static (float X, float Y) WalkPath(float startX, float startY, float[] xs, float[] ys, float distance)
    {
        int n = Math.Min(xs?.Length ?? 0, ys?.Length ?? 0);
        float px = startX, py = startY, left = distance;
        for (int i = 0; i < n; i++)
        {
            float leg = Distance(px, py, xs![i], ys![i]);
            if (leg >= left && leg > 0f)
            {
                float t = left / leg;
                return (px + (xs[i] - px) * t, py + (ys[i] - py) * t);
            }
            left -= leg;
            px = xs[i];
            py = ys[i];
        }
        return (px, py);
    }

    private static float Distance(float ax, float ay, float bx, float by)
    {
        float dx = bx - ax, dy = by - ay;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    public void OnPartyDestroyed(string? partyId)
    {
        if (IsHunting && string.Equals(partyId, _hunterPartyId, StringComparison.Ordinal))
            EndHunt(HuntEndReason.PartyLost);
    }

    /// <summary>
    /// Ends the hunt: releases the AI pin when the hunt set it, clears the published snapshot, then refreshes the
    /// party's speed so the old floor cannot linger, and starts the cooldown.
    /// </summary>
    public void EndHunt(HuntEndReason reason)
    {
        if (!IsHunting) return;
        string partyId = _hunterPartyId!;
        string heroId = _hunterHeroId!;

        if (_pinnedByUs)
            _adapter.ReleasePin(partyId);
        ClearHunt();
        _adapter.InvalidateSpeed(partyId);

        float now = Now;
        if (FiniteFloatValidator.IsFinite(now))
            _cooldownUntilDay = now + Math.Max(0, _settings.CooldownDays);
        _logger.LogInfo($"[EyeOfSauron] Hunt ended ({reason}): {heroId}, party {partyId}.");
        if (reason != HuntEndReason.SessionLive)
            _adapter.Announce(reason == HuntEndReason.HunterBeaten ? HuntAnnouncement.Won : HuntAnnouncement.Over, heroId);
    }

    private void ClearHunt()
    {
        _hunterHeroId = null;
        _hunterPartyId = null;
        _pinnedByUs = false;
        _huntStartDay = 0f;
        Publish(null);
    }

    // ------------------------------------------------------------------ the sight snapshot (main thread)

    /// <summary>
    /// Every campaign frame: works out whether the hunter is within the player's sight and publishes it, with the
    /// player's speed and the multiplier, for the speed model on the worker threads. When the in-sight state flips or
    /// the player's speed moves past 5%, the hunter's cached speed is refreshed (the cache never notices distance).
    /// </summary>
    public void OnTick()
    {
        string? partyId = _hunterPartyId;
        if (partyId == null) return;

        var reading = _adapter.ReadSight(partyId);
        bool inSight = IsInSight(reading);
        float speed = FiniteFloatValidator.IsFinite(reading.PlayerSpeed) ? reading.PlayerSpeed : float.NaN;
        float multiplier = _settings.HunterSpeedMultiplier;

        var old = Volatile.Read(ref _snapshot);
        bool flipped = old == null || old.InSight != inSight;
        bool speedMoved = old == null || SpeedMoved(old.PlayerSpeed, speed);
        bool multiplierMoved = old == null || !old.Multiplier.Equals(multiplier);
        if (!flipped && !speedMoved && !multiplierMoved) return;

        Publish(new HuntSnapshot(partyId, inSight, speedMoved ? speed : old!.PlayerSpeed, multiplier));
        // Out of sight no floor applies, so only a flip, or a speed or multiplier change in sight, needs a refresh.
        if (flipped || (inSight && (speedMoved || multiplierMoved)))
            _adapter.InvalidateSpeed(partyId);
    }

    private static bool IsInSight(HuntSightReading r)
    {
        if (!r.Valid || !r.PlayerActive || !r.HunterActive || r.HunterInMapEvent || r.PlayerInMapEvent || r.PlayerAtSea)
            return false;
        float sight = r.SeeingRange, d = r.Distance;
        return sight > 0f && FiniteFloatValidator.IsFinite(sight) && FiniteFloatValidator.IsFinite(d) && d <= sight;
    }

    private static bool SpeedMoved(float old, float now)
    {
        bool oldOk = FiniteFloatValidator.IsFinite(old), nowOk = FiniteFloatValidator.IsFinite(now);
        if (oldOk != nowOk) return true;
        if (!nowOk) return false;
        return Math.Abs(now - old) > SpeedRefreshFraction * Math.Max(Math.Abs(old), 1e-3f);
    }

    private void Publish(HuntSnapshot? snapshot) => Volatile.Write(ref _snapshot, snapshot);

    // ------------------------------------------------------------------ IEyeOfSauronHook (any thread)

    public void ApplyHunterSpeedFloor(string? partyId, ref ExplainedNumber result)
    {
        if (TryGetSpeedFloor(partyId, result.ResultNumber, out float floor))
            result.LimitMin(floor);
    }

    /// <summary>
    /// The hunter's floor: the player's speed times the multiplier, applied only to the hunter's party while in sight,
    /// only when it is a finite number above the current speed, and never under 1 (LimitMin replaces vanilla's floor).
    /// </summary>
    internal bool TryGetSpeedFloor(string? partyId, float current, out float floor)
    {
        floor = 0f;
        var s = Volatile.Read(ref _snapshot);
        if (s == null || partyId == null || !s.InSight || !string.Equals(s.PartyId, partyId, StringComparison.Ordinal))
            return false;
        if (!FiniteFloatValidator.IsFiniteInRange(s.Multiplier, EyeOfSauronSettingsProvider.MinSpeedMultiplier,
                EyeOfSauronSettingsProvider.MaxSpeedMultiplier))
            return false;

        float candidate = s.PlayerSpeed * s.Multiplier;
        if (!FiniteFloatValidator.IsFinite(candidate) || !(candidate > current)) return false;
        floor = Math.Max(candidate, 1f);
        return true;
    }

    public float GetHostSizeBonus(string? partyId, float currentLimit, float sumOfFactors, int rawCount, Func<float> weightedCount)
    {
        var s = Volatile.Read(ref _snapshot);
        if (s == null || partyId == null || !string.Equals(s.PartyId, partyId, StringComparison.Ordinal)) return 0f;
        if (!FiniteFloatValidator.IsFinite(currentLimit)) return Rejected($"limit {currentLimit} is not finite");
        if (!(1f + sumOfFactors > MinFactorScale)) return Rejected($"factor scale {1f + sumOfFactors} has collapsed");

        float weighted = weightedCount == null ? float.NaN : weightedCount();
        float target = Math.Max(rawCount, FiniteFloatValidator.IsFinite(weighted) ? (float)Math.Ceiling(weighted) : 0f);
        if (!(target > currentLimit)) return 0f;

        // Half a slot of headroom: the engine truncates ResultNumber, and a float sum can land a hair under the target.
        float bonus = target - currentLimit + 0.5f;
        if (bonus > AiPartySizeService.MaxFlatBonus)
        {
            Rejected($"bonus {bonus:0} clamped to {AiPartySizeService.MaxFlatBonus:0}");
            return AiPartySizeService.MaxFlatBonus;
        }
        return bonus;
    }

    /// <summary>Logs a refused or clamped size bonus once per hunt (this can run on a worker thread).</summary>
    private float Rejected(string why)
    {
        if (Interlocked.Exchange(ref _sizeRejectionLogged, 1) == 0)
            _logger.LogWarning($"[EyeOfSauron] Host size bonus for {_hunterPartyId}: {why}; the daily shed may trim the host.");
        return 0f;
    }

    // ------------------------------------------------------------------ console

    public IReadOnlyList<string> Describe()
    {
        var lines = new List<string>
        {
            $"Eye of Sauron: {(_settings.Enabled ? "on" : "off")}, {(IsSinglePlayer ? "single-player" : "co-op or server: stood down")}.",
            string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Score against Evil {0:0.0}, against Free {1:0.0} (threshold {2:0.0}, half-life {3:0} days).",
                GetScore(FactionSide.Evil), GetScore(FactionSide.Free), _settings.Threshold, _settings.HalfLifeDays),
        };
        string? kingdom = _adapter.PlayerKingdomId;
        lines.Add(kingdom == null
            ? "Your kingdom: none (deeds still score; no hunt)."
            : $"Your kingdom: {kingdom} ({_alignment.GetKingdomSide(kingdom)}).");

        float now = Now;
        lines.Add(now < _cooldownUntilDay
            ? string.Format(System.Globalization.CultureInfo.InvariantCulture, "Cooldown: {0:0.0} days left.", _cooldownUntilDay - now)
            : "Cooldown: none.");

        if (!IsHunting)
        {
            lines.Add("No hunt is running.");
            return lines;
        }

        var snapshot = Volatile.Read(ref _snapshot);
        lines.Add($"Hunter: {_adapter.HeroName(_hunterHeroId!)} ({_hunterHeroId}), party {_hunterPartyId}, "
                  + $"pinned by the hunt: {(_pinnedByUs ? "yes" : "no")}.");
        lines.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "Day {0:0.0} of {1}; in sight: {2}; your speed {3:0.00}, multiplier {4:0.00}.",
            now - _huntStartDay, _settings.HuntTimeoutDays, snapshot?.InSight == true ? "yes" : "no",
            snapshot?.PlayerSpeed ?? 0f, snapshot?.Multiplier ?? 0f));
        return lines;
    }

    private static float FiniteOrZero(float value) => FiniteFloatValidator.IsFinite(value) ? value : 0f;

    /// <summary>Immutable, so a worker thread reads one consistent set of values.</summary>
    private sealed class HuntSnapshot
    {
        public HuntSnapshot(string partyId, bool inSight, float playerSpeed, float multiplier)
        {
            PartyId = partyId;
            InSight = inSight;
            PlayerSpeed = playerSpeed;
            Multiplier = multiplier;
        }

        public string PartyId { get; }

        public bool InSight { get; }

        public float PlayerSpeed { get; }

        public float Multiplier { get; }
    }
}
