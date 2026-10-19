namespace TAOM.Features.HostReview.Domain;

/// <summary>One roster line as the service sees it: where it sits and how many of it can stand today.</summary>
public sealed class HostRosterEntry
{
    public HostRosterEntry(int rosterIndex, bool isHero, bool isMainHero, int healthyCount)
    {
        RosterIndex = rosterIndex;
        IsHero = isHero;
        IsMainHero = isMainHero;
        HealthyCount = healthyCount;
    }

    /// <summary>The line's index in the main party's member roster.</summary>
    public int RosterIndex { get; }

    public bool IsHero { get; }

    public bool IsMainHero { get; }

    /// <summary>Men on this line who are not wounded; a hero is 1 when healthy and 0 when wounded.</summary>
    public int HealthyCount { get; }
}

/// <summary>A roster line and how many men of it stand in the review.</summary>
public sealed class HostReviewSlot
{
    public HostReviewSlot(int rosterIndex, int count)
    {
        RosterIndex = rosterIndex;
        Count = count;
    }

    public int RosterIndex { get; }

    public int Count { get; }
}

/// <summary>Why the review cannot open, checked in this order; None means nothing blocks it.</summary>
public enum HostReviewBlock
{
    None,
    Enlisted,
    InEncounter,
    CoopSession,
    HeroWounded,
    TooFewHealthy,

    /// <summary>Nothing blocked the review but the scene could not be opened (empty lineup or the launcher failed).</summary>
    OpenFailed,
}
