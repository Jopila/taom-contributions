namespace TAOM.Adapters;

/// <summary>Why a raiding host could not be created. <see cref="None"/> means it can.</summary>
public enum RaidSpawnFailure
{
    None,

    /// <summary>No clan with that id in this campaign (a save from before the clans existed).</summary>
    ClanMissing,

    /// <summary>The anchor settlement is not on this map.</summary>
    AnchorMissing,

    /// <summary>No hideout of the clan's culture exists; the party would have no home.</summary>
    NoHideout,

    /// <summary>
    /// The engine found no valid map point for the host. It looks for one within the spawn radius of the anchor's gate and
    /// falls back to the gate itself, so this means even that is not a valid position.
    /// </summary>
    NoPoint,

    /// <summary>The engine returned no party.</summary>
    CreateFailed,
}

/// <summary>The result of creating a host: its party id and where it stands, or why not.</summary>
public readonly struct RaidHostSpawn
{
    public RaidHostSpawn(RaidSpawnFailure failure, string? partyId, string? homeId, string? nearestTownId)
    {
        Failure = failure;
        PartyId = partyId;
        HomeId = homeId;
        NearestTownId = nearestTownId;
    }

    public RaidSpawnFailure Failure { get; }

    public string? PartyId { get; }

    /// <summary>The hideout settlement id the host calls home.</summary>
    public string? HomeId { get; }

    /// <summary>The town nearest to where the host appeared, for the rumour; null when the map has no town.</summary>
    public string? NearestTownId { get; }

    public bool Succeeded => Failure == RaidSpawnFailure.None && !string.IsNullOrEmpty(PartyId);
}

/// <summary>A host's raw facts for one hourly step. <see cref="Missing"/> when the party is gone.</summary>
public sealed class RaidHostSnapshot
{
    public static readonly RaidHostSnapshot Missing = new RaidHostSnapshot();

    public bool Exists { get; set; }

    public bool IsActive { get; set; }

    public bool InMapEvent { get; set; }

    public float Strength { get; set; }

    public float Speed { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    /// <summary>False when the party has no home settlement left.</summary>
    public bool HasHome { get; set; }

    /// <summary>Map units to the home settlement; NaN when there is none.</summary>
    public float DistanceToHome { get; set; } = float.NaN;

    /// <summary>The village id the party is ordered to raid, or null when its order is anything else.</summary>
    public string? RaidTargetId { get; set; }
}

/// <summary>A village read live: who owns it, where it lies, whether it can still be raided, and its hearth.</summary>
public readonly struct VillageReading
{
    private readonly string? _id;

    public VillageReading(bool exists, string? kingdomId, bool isNormal, float hearth, string? id = null, float x = 0f, float y = 0f)
    {
        Exists = exists;
        KingdomId = kingdomId;
        IsNormal = isNormal;
        Hearth = hearth;
        _id = id;
        X = x;
        Y = y;
    }

    public static VillageReading Missing => default;

    public bool Exists { get; }

    /// <summary>The village settlement's StringId; empty for <see cref="Missing"/>.</summary>
    public string Id => _id ?? string.Empty;

    /// <summary>Map position of the village (positions never change).</summary>
    public float X { get; }

    public float Y { get; }

    /// <summary>The owner's kingdom id, or null when the owner has none.</summary>
    public string? KingdomId { get; }

    /// <summary>The village is in its normal state: not being raided, looted or forced for supplies.</summary>
    public bool IsNormal { get; }

    public float Hearth { get; }
}

/// <summary>
/// A party near a host that belongs to some kingdom, as a candidate to engage. The adapter leaves out parties the engine
/// itself would not interact with (ignored ones, a besieger that is not its camp's leader).
/// </summary>
public sealed class NearbyParty
{
    public string Id { get; set; } = string.Empty;

    public string KingdomId { get; set; } = string.Empty;

    public bool InMapEvent { get; set; }

    public bool InSettlement { get; set; }

    /// <summary>Attached to an army another party leads.</summary>
    public bool IsAttached { get; set; }

    /// <summary>
    /// What a battle with this party would put at risk: its whole army's strength when it leads an army (vanilla weighs a
    /// leader that way, because the battle pulls the army in), else its own roster's. A party in a siege camp is never
    /// reported: vanilla weighs it by its whole camp, which the host must not engage.
    /// </summary>
    public float Strength { get; set; }

    public float Speed { get; set; }

    /// <summary>Map units from the host.</summary>
    public float Distance { get; set; }
}
