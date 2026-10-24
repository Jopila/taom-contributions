namespace TAOM.Adapters;

/// <summary>Boundary over the main hero and party (ADR-007 — keeps <c>Hero</c>, <c>MobileParty</c> and
/// <c>CharacterObject</c> out of services): the hero id, the party's daily wage and purchased troops.</summary>
public interface IPlayerPartyAdapter
{
    /// <summary>The main hero's StringId, or null when there is no main hero.</summary>
    string GetMainHeroId();

    /// <summary>
    /// The main party's daily wage, the number the clan finance model charges the player (#839), or 0
    /// when there is no party or the read fails (logged once until a read succeeds).
    /// </summary>
    int GetMainPartyDailyWage();

    /// <summary>Adds <paramref name="count"/> of <paramref name="troopId"/> to the main party roster.
    /// Returns false (no charge should follow) when there is no main party, the troop id doesn't
    /// resolve to a <c>CharacterObject</c>, or the add throws.</summary>
    bool GrantTroop(string troopId, int count);
}
