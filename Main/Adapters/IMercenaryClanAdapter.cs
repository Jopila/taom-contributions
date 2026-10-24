namespace TAOM.Adapters;

/// <summary>
/// One clan as Bond of Hire sees it: the service gets a clan that has just signed on as a mercenary; the Clans panel gets
/// whichever clan is selected, mercenary or not. Three facts, since the sealed <c>Clan</c> never reaches the service
/// (ADR-007). The engine's own "stay until" date is saved with the clan, and nothing else here is.
/// </summary>
public interface IMercenaryClanAdapter
{
    /// <summary>True for the player's clan, which the engine never binds to the date: vanilla reads it for AI clans only.</summary>
    bool IsPlayerClan { get; }

    /// <summary>
    /// Days from now to the clan's "stay until" date, negative once it is past and 0 or negative for the never-set default.
    /// A float, as the engine's own <c>RemainingDaysFromNow</c>.
    /// </summary>
    float RemainingStayDays { get; }

    /// <summary>Sets the "stay until" date to this many days from now.</summary>
    void SetStayUntilDaysFromNow(int days);
}
