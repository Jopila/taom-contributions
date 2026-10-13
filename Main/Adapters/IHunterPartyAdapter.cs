namespace TAOM.Adapters;

/// <summary>
/// Every engine call the Eye of Sauron hunt (#765 M8) makes: heroes, the hunter's party, the player's party, the
/// navigation mesh and the on-screen announcement (ADR-007). Ids in, plain values out; each id is resolved against the
/// live campaign on every call, so nothing here outlives a save load. Main thread only.
/// </summary>
public interface IHunterPartyAdapter
{
    /// <summary><c>CampaignTime.Now.ToDays</c>.</summary>
    double NowDays { get; }

    /// <summary>The player clan's kingdom StringId (ruler, vassal or mercenary), or null when kingdomless.</summary>
    string? PlayerKingdomId { get; }

    /// <summary>The player's strength: his army's when he leads one, else his party's (<c>EstimatedStrength</c>).</summary>
    float PlayerStrength { get; }

    HunterHeroSnapshot ReadHero(string heroId);

    HuntPartySnapshot ReadParty(string partyId);

    /// <summary>
    /// Orders the party to engage the player, refreshes its AI and pins it (<c>SetDoNotMakeNewDecisions(true)</c>).
    /// False when the party is gone or inactive, or the player's party is not on the map.
    /// </summary>
    bool PinAndEngagePlayer(string partyId);

    /// <summary>Unpins the party and refreshes its AI when it is free to decide. A missing party is a no-op.</summary>
    void ReleasePin(string partyId);

    /// <summary>Bumps the party's version so its cached speed is recalculated. A missing party is a no-op.</summary>
    void InvalidateSpeed(string partyId);

    /// <summary>
    /// Spawns a lord party for a free hero out of the player's sight near him, after removing the hero as governor and
    /// re-checking that he still belongs to no party. Returns the new party's StringId, or null when refused.
    /// </summary>
    string? SpawnHunterParty(string heroId);

    /// <summary>One troop's estimated power under the hero's leadership, or NaN when the troop or hero is unknown.</summary>
    float GetTroopPower(string troopId, string leaderHeroId);

    /// <summary>Adds healthy troops to the party's roster; returns how many were added (0 when anything is missing).</summary>
    int AddTroops(string partyId, string troopId, int count);

    /// <summary>Adds grain until the party carries at least <paramref name="targetFood"/> food.</summary>
    void TopUpFood(string partyId, int targetFood);

    HuntSightReading ReadSight(string partyId);

    /// <summary>Checks the step guards and rebuilds the hunter's path to the player.</summary>
    HuntStepContext ReadStepContext(string partyId);

    /// <summary>
    /// Validates a candidate landing point: on the navigation mesh, on the player's island (nudged once when not), and
    /// no party at war with the hunter within <paramref name="enemyClearance"/>.
    /// </summary>
    HuntLanding ResolveLanding(string partyId, float x, float y, float enemyClearance);

    /// <summary>Moves the party (and anything attached to it) to the point, then rebuilds its path and refreshes its AI.</summary>
    void Relocate(string partyId, float x, float y);

    /// <summary>Shows one of the hunt's lines with the hero's name.</summary>
    void Announce(HuntAnnouncement announcement, string heroId);

    /// <summary>The hero's display name for the console, or the id when unknown.</summary>
    string HeroName(string heroId);
}
