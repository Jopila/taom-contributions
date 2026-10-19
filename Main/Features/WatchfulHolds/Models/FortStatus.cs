namespace TAOM.Features.WatchfulHolds.Models;

/// <summary>A fort's live state, read when a party is near it: owner culture, siege and the men in its garrison.</summary>
public readonly struct FortStatus
{
    /// <summary>The owner's culture id, which picks the culture row of the config; empty when unknown.</summary>
    public string OwnerCultureId { get; init; }

    public bool UnderSiege { get; init; }

    public int GarrisonMen { get; init; }
}
