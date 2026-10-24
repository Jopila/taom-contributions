namespace TAOM.Features.BondOfHire;

/// <summary>The "Bond of Hire" MCM group, clamped. Read when a clan signs on as a mercenary, never per frame.</summary>
public interface IBondOfHireSettingsProvider
{
    /// <summary>The fewest whole days a mercenary clan stays, 0 to 120. 0 turns the minimum off.</summary>
    int MinimumDays { get; }
}
