using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TAOM.Core.Validation;

namespace TAOM.Features.FellFoes.Domain;

/// <summary>
/// Everything Fell Foes remembers inside one mission, keyed on the opaque agent handle by reference.
/// A fresh instance is built for every mission and dropped with it, so nothing carries into the next
/// battle. Main thread only (OnMissionTick); no locking.
/// </summary>
public sealed class FellFoesMissionState
{
    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>Agent handles are equal only when they are the same object.</summary>
    internal static readonly IEqualityComparer<object> Comparer = new ReferenceComparer();

    /// <summary>Routers the rules stopped and that are fighting now.</summary>
    public HashSet<object> Cornered { get; internal set; } = new HashSet<object>(Comparer);

    /// <summary>Archers holding the player as a target, with the clock time the hold ends.</summary>
    public Dictionary<object, float> Holds { get; } = new Dictionary<object, float>(Comparer);

    /// <summary>Health fraction of each panicked soldier at the last pass; a drop since then is a hit.</summary>
    public Dictionary<object, float> LastHealth { get; private set; } = new Dictionary<object, float>(Comparer);

    /// <summary>The map being built this pass; <see cref="CommitHealth"/> makes it the last one.</summary>
    public Dictionary<object, float> NextHealth { get; private set; } = new Dictionary<object, float>(Comparer);

    /// <summary>How many times each soldier was stopped this mission; filled only while Debug is on, to show a flicker.</summary>
    public Dictionary<object, int> StopCounts { get; } = new Dictionary<object, int>(Comparer);

    /// <summary>Soldiers already sent off the field; never sent twice.</summary>
    public HashSet<object> SentOff { get; } = new HashSet<object>(Comparer);

    /// <summary>The largest enemy headcount seen this mission.</summary>
    public int PeakEnemies { get; set; }

    /// <summary>Clock time the straggler conditions first held, or NaN while they do not.</summary>
    public float StragglerSince { get; set; } = float.NaN;

    /// <summary>Seconds of mission time accumulated from the tick deltas.</summary>
    public float Clock { get; private set; }

    /// <summary>Adds a tick delta; a negative or non-finite one is ignored so the clock never goes bad.</summary>
    public void Advance(float dt)
    {
        if (FiniteFloatValidator.IsFinite(dt) && dt > 0f)
            Clock += dt;
    }

    /// <summary>Forgets every recorded health, so the next pass that runs is a first sighting for everyone.</summary>
    public void ClearHealth()
    {
        LastHealth.Clear();
        NextHealth.Clear();
    }

    /// <summary>Makes this pass's health map the last one and empties the other for the next pass.</summary>
    public void CommitHealth()
    {
        var done = NextHealth;
        NextHealth = LastHealth;
        LastHealth = done;
        NextHealth.Clear();
    }

    /// <summary>Removes and returns every held handle, for the release at mission end or when archers are switched off.</summary>
    public List<object> TakeHolds()
    {
        var taken = new List<object>(Holds.Keys);
        Holds.Clear();
        return taken;
    }
}
