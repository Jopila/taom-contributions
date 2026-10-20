using System;
using System.Collections.Generic;
using TAOM.Core.Validation;
using TAOM.Features.FellFoes.Domain;

namespace TAOM.Features.FellFoes;

/// <summary>
/// The three Fell Foes rule passes, as decisions over one <see cref="FellFoesScan"/>. Pure: no engine
/// type, no adapter, no clock of its own (the state carries the mission clock). Each pass updates the
/// state optimistically and hands back the handles to act on; the service rolls a failed cornered stop or
/// send-off back so it can be tried again. Every gate on an engine float is a positive requirement, so NaN fails it.
/// Loops index the scan list and keep their scratch sets small: a big battle has hundreds of enemy soldiers.
/// </summary>
public static class FellFoesRules
{
    // ---------------------------------------------------------------- cornered routers

    /// <summary>
    /// A panicked soldier whose health just dropped (a hit), with health to fight on and one to
    /// <see cref="FellFoesTunables.CorneredMaxFoes"/> hostile soldiers beside him, stops running and turns, and
    /// rallies up to <see cref="FellFoesTunables.CorneredRallyCap"/> other panicked soldiers within the cornered
    /// radius, nearest first. A turned soldier (hit or rallied) stays turned while a foe is within the hold radius of him
    /// (twice the cornered radius, since a rallied man stands up to a cornered radius from the hit router), and runs again
    /// when none is. Admission uses the cornered radius and 1 to N foes; retention uses the hold radius and at least one. All of it holds only while
    /// somebody on the enemy side still stands on his own: once the whole side has broken, the turned men run
    /// again and nobody new is stopped, so vanilla's rout victory fires on its own schedule. An order retreat is
    /// never panicked, so it is never touched. The state's cornered set is rebuilt from the scan, so a handle that
    /// left the scan (or lost heart again on its own) drops out by not being carried over.
    /// </summary>
    public static void PlanCornered(
        FellFoesScan scan, FellFoesMissionState state, FellFoesTunables t, List<object> stop, List<object> reflee)
    {
        var enemies = scan.Enemies;
        var standing = scan.OthersStanding > 0;
        List<EnemyAgentView>? turned = null;

        for (var i = 0; i < enemies.Count; i++)
        {
            var v = enemies[i];
            if (state.Cornered.Count > 0 && state.Cornered.Contains(v.Handle))
            {
                // Lost heart again on its own: forget the rally and judge it afresh below.
                if (!v.IsPanicked) (turned ??= new List<EnemyAgentView>()).Add(v);
            }
            else if (!standing && !v.IsFleeing && !v.IsPanicked)
            {
                standing = true;
            }
        }

        state.Cornered = KeepTurned(turned, standing, reflee);

        List<EnemyAgentView>? panicked = null;
        List<EnemyAgentView>? hit = null;
        for (var i = 0; i < enemies.Count; i++)
        {
            var v = enemies[i];
            if (!v.IsPanicked) continue;
            var health = v.HealthFraction;
            var known = state.LastHealth.TryGetValue(v.Handle, out var before);
            if (FiniteFloatValidator.IsFinite(health)) state.NextHealth[v.Handle] = health;
            if (!standing || !FiniteFloatValidator.IsFiniteInRange(health, t.CorneredMinHealthFraction, 1f)) continue;
            (panicked ??= new List<EnemyAgentView>()).Add(v);
            if (known && health < before && v.NearbyFoes >= 1 && v.NearbyFoes <= t.CorneredMaxFoes)
                (hit ??= new List<EnemyAgentView>()).Add(v);
        }

        state.CommitHealth();
        if (hit == null) return;

        foreach (var trigger in hit)
        {
            if (state.Cornered.Contains(trigger.Handle)) continue;
            state.Cornered.Add(trigger.Handle);
            stop.Add(trigger.Handle);
            Rally(trigger, panicked!, state, t, stop);
        }
    }

    // The men turned last pass who are still fighting stay turned while the battle is contested and foes are beside them.
    private static HashSet<object> KeepTurned(List<EnemyAgentView>? turned, bool standing, List<object> reflee)
    {
        var next = new HashSet<object>(FellFoesMissionState.Comparer);
        if (turned == null) return next;
        foreach (var v in turned)
        {
            if (!standing || v.HoldFoes == 0) reflee.Add(v.Handle);
            else next.Add(v.Handle);
        }

        return next;
    }

    private static void Rally(
        EnemyAgentView trigger, List<EnemyAgentView> panicked, FellFoesMissionState state, FellFoesTunables t, List<object> stop)
    {
        if (t.CorneredRallyCap <= 0) return;
        var reach = t.CorneredRadius * t.CorneredRadius;
        List<KeyValuePair<float, object>>? near = null;
        foreach (var other in panicked)
        {
            if (ReferenceEquals(other, trigger) || state.Cornered.Contains(other.Handle)) continue;
            var dx = other.X - trigger.X;
            var dy = other.Y - trigger.Y;
            var d2 = dx * dx + dy * dy;
            if (d2 <= reach) (near ??= new List<KeyValuePair<float, object>>()).Add(new KeyValuePair<float, object>(d2, other.Handle));
        }

        if (near == null) return;
        near.Sort((a, b) => a.Key.CompareTo(b.Key));
        for (var i = 0; i < near.Count && i < t.CorneredRallyCap; i++)
        {
            state.Cornered.Add(near[i].Value);
            stop.Add(near[i].Value);
        }
    }

    // ---------------------------------------------------------------- archers

    /// <summary>
    /// Idle archers inside range (the field range, or the siege range outside a field battle), steady and out of
    /// a horse stand, roll to hold the player as their target: at most <see cref="FellFoesTunables.ArcherMaxHolding"/>
    /// at once and <see cref="FellFoesTunables.ArcherNewPerPass"/> new per pass, nearest first. A hold ends on its
    /// timer, when the archer is gone, shaken, fleeing or its team stands, and for every hold when the player is dead.
    /// </summary>
    public static void PlanArchers(
        FellFoesScan scan, FellFoesMissionState state, FellFoesTunables t, Func<float> roll,
        List<object> release, List<object> hold)
    {
        var enemies = scan.Enemies;
        if (state.Holds.Count > 0)
        {
            // Every hold ends unless the scan shows its archer still valid.
            var ended = new HashSet<object>(state.Holds.Keys, FellFoesMissionState.Comparer);
            for (var i = 0; i < enemies.Count && scan.PlayerAlive && ended.Count > 0; i++)
            {
                var v = enemies[i];
                if (ended.Contains(v.Handle) && state.Clock < state.Holds[v.Handle]
                    && !v.IsPanicked && !v.IsFleeing && !v.TeamStandsAgainstHorse)
                    ended.Remove(v.Handle);
            }

            foreach (var handle in ended)
            {
                state.Holds.Remove(handle);
                release.Add(handle);
            }
        }

        if (!scan.PlayerAlive) return;

        var room = Math.Min(t.ArcherMaxHolding - state.Holds.Count, t.ArcherNewPerPass);
        if (room <= 0) return;

        var range = scan.IsFieldBattle ? t.ArcherFieldRange : t.ArcherSiegeRange;
        List<EnemyAgentView>? candidates = null;
        for (var i = 0; i < enemies.Count; i++)
        {
            if (IsArcherCandidate(enemies[i], range, t, state))
                (candidates ??= new List<EnemyAgentView>()).Add(enemies[i]);
        }

        if (candidates == null) return;
        candidates.Sort((a, b) => a.DistanceToPlayer.CompareTo(b.DistanceToPlayer));
        foreach (var v in candidates)
        {
            if (hold.Count >= room) break;
            if (!(roll() < t.ArcherChance)) continue;
            state.Holds[v.Handle] = state.Clock + t.ArcherHoldSeconds;
            hold.Add(v.Handle);
        }
    }

    private static bool IsArcherCandidate(EnemyAgentView v, float range, FellFoesTunables t, FellFoesMissionState state) =>
        v.IsIdleArcher && !v.IsPanicked && !v.IsFleeing && !v.TeamStandsAgainstHorse
        && FiniteFloatValidator.IsFinite(v.Morale) && v.Morale > t.ArcherMinMorale
        && FiniteFloatValidator.IsFiniteInRange(v.DistanceToPlayer, 0f, range)
        && !state.Holds.ContainsKey(v.Handle);

    // ---------------------------------------------------------------- stragglers

    /// <summary>
    /// When a big field battle is down to a few enemy soldiers, none a hero, no creature standing, none still
    /// to spawn, and it has stayed so for <see cref="FellFoesTunables.StragglerHoldSeconds"/>, picks one soldier per
    /// call to send off the field so the vanilla rout victory can fire. Never picks one twice, nor one already
    /// running; a send that then fails is rolled back by the service so the soldier is tried again. A soldier
    /// picked leaves the cornered set.
    /// </summary>
    public static object? PlanStraggler(FellFoesScan scan, FellFoesMissionState state, FellFoesTunables t)
    {
        var enemies = scan.Enemies;
        var count = enemies.Count;
        if (count > state.PeakEnemies) state.PeakEnemies = count;

        var applies = scan.IsFieldBattle
                      && scan.EnemyReserves == 0
                      && scan.OthersStanding == 0
                      && count >= 1 && count <= t.StragglerMaxCount
                      && state.PeakEnemies >= t.StragglerPeakMin;
        for (var i = 0; applies && i < count; i++)
            applies = !enemies[i].IsHero;
        if (!applies)
        {
            state.StragglerSince = float.NaN;
            return null;
        }

        if (float.IsNaN(state.StragglerSince)) state.StragglerSince = state.Clock;
        if (state.Clock - state.StragglerSince < t.StragglerHoldSeconds) return null;

        for (var i = 0; i < count; i++)
        {
            var v = enemies[i];
            if (v.IsFleeing || state.SentOff.Contains(v.Handle)) continue;
            state.SentOff.Add(v.Handle);
            state.Cornered.Remove(v.Handle);
            return v.Handle;
        }

        return null;
    }
}
