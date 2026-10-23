namespace TAOM.Features.ArmyTargeting.HoldTheWalls;

/// <summary>
/// How a hold ended, for the early-release decision and the daily debug line. Every reason but <see cref="Expired"/> writes
/// to the engine (it restores what is left of the engine's own 10 hour pause, or resumes the AI once those hours are over);
/// <see cref="Expired"/> ends the record with no write, because the engine's own timer ends the pause.
/// </summary>
public enum HoldEnd
{
    /// <summary>Nothing ends the hold.</summary>
    None,

    /// <summary>The fortress came under siege.</summary>
    Siege,

    /// <summary>Cohesion fell below the release floor.</summary>
    Cohesion,

    /// <summary>The army's leanest party has less than the release food.</summary>
    Food,

    /// <summary>The MCM switch went off.</summary>
    Off,

    /// <summary>A co-op session went live, a co-op mod TAOM cannot probe is loaded, or this is a dedicated server.</summary>
    Coop,

    /// <summary>The player joined the army.</summary>
    Player,

    /// <summary>The hold's time ran out; the engine's own timer ends the pause.</summary>
    Expired,

    /// <summary>The army left the fortress, or its realm no longer owns it (found at the next pass, whoever took it).</summary>
    Moved,

    /// <summary>The army is gone: disbanded, destroyed or no longer led by the recorded party. The leader may live on.</summary>
    Gone,
}
