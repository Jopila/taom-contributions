using System.Collections.Generic;
using TAOM.Features.Execution;

namespace TAOM.Features.FireAndTribute;

/// <summary>
/// One live raiding host, by StringIds and primitives only: the party id is resolved against the live campaign on every
/// use, so a loaded save never meets an object from the last one.
/// </summary>
public sealed class RaidHostRecord
{
    public string PartyId { get; set; } = string.Empty;

    /// <summary>The host's id in fire_and_tribute.json.</summary>
    public string HostId { get; set; } = string.Empty;

    public FactionSide Side { get; set; }

    public string TargetKingdomId { get; set; } = string.Empty;

    public int StartDay { get; set; }

    /// <summary>Completed loots by this host, counted from <c>VillageStateChanged</c>.</summary>
    public int Raids { get; set; }

    public int Quota { get; set; }

    /// <summary>True once the host has turned for home: it raids no more.</summary>
    public bool Returning { get; set; }
}

/// <summary>
/// The feature's saved record. <see cref="Warnings"/> is filled by the codec on a load and never saved.
/// </summary>
public sealed class FireAndTributeState
{
    /// <summary>The first day this save saw Full War or its end; negative for never.</summary>
    public int WarSeenDay { get; set; } = -1;

    /// <summary>The first day a side may send a host again.</summary>
    public Dictionary<FactionSide, int> CooldownUntil { get; } = new Dictionary<FactionSide, int>();

    public List<RaidHostRecord> Hosts { get; } = new List<RaidHostRecord>();

    public List<string> Warnings { get; } = new List<string>();
}
