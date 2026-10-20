namespace TAOM.Features.FellFoes.Domain;

/// <summary>
/// One enemy soldier as the adapter saw him at the last scan. A snapshot: nothing here is live, and
/// <see cref="Handle"/> is an opaque token (the engine agent object) that only the adapter can use,
/// compared by reference and never by index (an agent index is recycled within a mission).
/// </summary>
public sealed class EnemyAgentView
{
    public object Handle { get; set; } = null!;

    /// <summary>A hero (lord, companion, named captain).</summary>
    public bool IsHero { get; set; }

    /// <summary>Routed on its own morale: panicked and retreating. An order retreat is not panicked.</summary>
    public bool IsPanicked { get; set; }

    /// <summary>The engine counts this agent as running away (what the victory check reads).</summary>
    public bool IsFleeing { get; set; }

    /// <summary>Health over the health limit, 0 to 1; NaN when the engine gave garbage.</summary>
    public float HealthFraction { get; set; }

    public float Morale { get; set; }

    /// <summary>Metres to the player; NaN when there is no player or the position is unreadable.</summary>
    public float DistanceToPlayer { get; set; } = float.NaN;

    /// <summary>Position on the ground, metres; NaN when unreadable. Plain floats keep engine types out of the rules.</summary>
    public float X { get; set; } = float.NaN;

    public float Y { get; set; } = float.NaN;

    /// <summary>Soldiers hostile to this one inside the cornered radius; -1 when not measured.</summary>
    public int NearbyFoes { get; set; } = -1;

    /// <summary>Soldiers hostile to this one inside the hold radius (twice the cornered radius); -1 when not measured. Read for turned soldiers.</summary>
    public int HoldFoes { get; set; } = -1;

    /// <summary>Has a bow or crossbow with ammunition, sees no enemy, is not retreating.</summary>
    public bool IsIdleArcher { get; set; }

    /// <summary>The agent's team has a horse stand running (Culture Doctrine owns the archers then).</summary>
    public bool TeamStandsAgainstHorse { get; set; }

    /// <summary>Its engine target is the player; null when the scan did not read it (only held archers are read, and only with Debug on).</summary>
    public bool? TargetsPlayer { get; set; }
}
