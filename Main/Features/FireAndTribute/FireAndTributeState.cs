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

    /// <summary>
    /// The campaign hour the host's tribute envoy is next due, or -1 for none. An envoy is scheduled when the host rides
    /// against the player's kingdom; it stays due, and is tried every hour, until the map is free and the player can be asked.
    /// </summary>
    public int EnvoyDueHour { get; set; } = -1;

    /// <summary>True once the envoy is settled (paid, refused, seized or no longer needed): one envoy per host.</summary>
    public bool EnvoyDone { get; set; }

    /// <summary>The villages of the player's clan the first quote counted; 0 until the envoy has been quoted.</summary>
    public int EnvoyVillages { get; set; }

    /// <summary>The price quoted in denars (after a successful haggle, the cut price); 0 until the envoy has been quoted.</summary>
    public int EnvoyPrice { get; set; }

    /// <summary>True once the player has tried to haggle: one try per host.</summary>
    public bool EnvoyHaggled { get; set; }

    /// <summary>
    /// The clan that paid this host: it raids none of that clan's villages and never engages the player's party. Empty
    /// for none.
    /// </summary>
    public string SparedClanId { get; set; } = string.Empty;
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

    /// <summary>
    /// Per side, the kingdom whose envoy the player seized: that side's next host rides against it first. Spent when a
    /// host of the side rides.
    /// </summary>
    public Dictionary<FactionSide, string> Grudges { get; } = new Dictionary<FactionSide, string>();

    /// <summary>How many envoys the player has seized: each adds one full price to every later tribute.</summary>
    public int EnvoysSeized { get; set; }

    public List<string> Warnings { get; } = new List<string>();
}
