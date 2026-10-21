using System.Collections.Generic;

namespace TAOM.Adapters;

public enum ScoutDefenderKind
{
    Garrison,
    Militia,
    Lord,
}

/// <summary>One party inside a settlement that counts as its defenders (the vanilla tooltip's sum), by kind.</summary>
public readonly struct ScoutDefender
{
    public ScoutDefenderKind Kind { get; init; }

    public string Name { get; init; }

    public int Count { get; init; }

    public int Foot { get; init; }

    public int Archers { get; init; }

    public int Horse { get; init; }

    public int HorseArchers { get; init; }
}

/// <summary>
/// What a scout sees in a settlement: the snapshot Hidden Hosts remembers (its <c>DefenderCount</c> is the exact total, the
/// vanilla tooltip's sum) and each defender party.
/// </summary>
public sealed class ScoutSiteReport
{
    public bool Found { get; init; }

    public string Name { get; init; } = string.Empty;

    public SettlementIntelSnapshot Intel { get; init; }

    public List<ScoutDefender> Defenders { get; init; } = new();
}
