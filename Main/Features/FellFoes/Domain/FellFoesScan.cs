using System;
using System.Collections.Generic;

namespace TAOM.Features.FellFoes.Domain;

/// <summary>What one scan of the battlefield found: the enemy soldiers of the player's team and four facts about the battle (player alive, field battle, enemy reserves, creatures standing).</summary>
public sealed class FellFoesScan
{
    /// <summary>Active enemy soldiers on foot or horseback, never mounts. Mounts are not soldiers.</summary>
    public IReadOnlyList<EnemyAgentView> Enemies { get; set; } = Array.Empty<EnemyAgentView>();

    /// <summary>The player controls a live agent.</summary>
    public bool PlayerAlive { get; set; }

    /// <summary>A field battle: the only kind the straggler cleanup runs in.</summary>
    public bool IsFieldBattle { get; set; }

    /// <summary>Troops the enemy side has yet to spawn; -1 when the engine does not say.</summary>
    public int EnemyReserves { get; set; } = -1;

    /// <summary>Enemy team members that are not human (creatures) and are not running away. They stand on their own.</summary>
    public int OthersStanding { get; set; }
}

/// <summary>What the rules want measured by the next scan.</summary>
public sealed class FellFoesScanRequest
{
    /// <summary>Radius for the foes-nearby count of panicked or cornered soldiers; 0 skips the count.</summary>
    public float CorneredRadius { get; set; }

    /// <summary>Radius for the foe count of turned soldiers (their retention test); 0 skips it.</summary>
    public float HoldRadius { get; set; }

    /// <summary>Archers are read inside this range (the field range when archers are on); 0 skips the archer reads.</summary>
    public float ArcherReadRange { get; set; }

    /// <summary>The rallied set: their foe count is read even though they are no longer panicked. The state's own set, not a copy.</summary>
    public ICollection<object> Cornered { get; set; } = Array.Empty<object>();

    /// <summary>Held archers whose target is read back; empty unless Debug is on. The state's own keys, not a copy.</summary>
    public ICollection<object> Held { get; set; } = Array.Empty<object>();
}
