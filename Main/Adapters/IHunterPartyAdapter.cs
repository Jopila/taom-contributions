namespace TAOM.Adapters;

/// <summary>
/// Every engine call the Eye of Sauron hunt (#765 M8) makes: heroes, the hunter's party, the player's party, the
/// navigation mesh and the on-screen announcement (ADR-007). Ids in, plain values out; each id is resolved against the
/// live campaign on every call, so nothing here outlives a save load. It reads and acts; every decision is the
/// service's. Main thread only.
/// </summary>
public interface IHunterPartyAdapter
{
    /// <summary><c>CampaignTime.Now.ToDays</c>.</summary>
    double NowDays { get; }

    /// <summary>The player clan's kingdom StringId (ruler, vassal or mercenary), or null when kingdomless.</summary>
    string? PlayerKingdomId { get; }

    /// <summary>
    /// The player kingdom's own <c>Kingdom.Culture</c> StringId, or null when kingdomless. It sides a kingdom that
    /// alignment.json does not list, such as a player-founded <c>new_kingdom</c> (#744: the kingdom's culture, never the
    /// player hero's).
    /// </summary>
    string? PlayerKingdomCultureId { get; }

    /// <summary>The player's strength: his army's when he leads one, else his party's (<c>EstimatedStrength</c>).</summary>
    float PlayerStrength { get; }

    HunterHeroSnapshot ReadHero(string heroId);

    HuntPartySnapshot ReadParty(string partyId);

    /// <summary>
    /// Orders the party to engage the player, refreshes its AI and pins it (<c>SetDoNotMakeNewDecisions(true)</c>).
    /// False when the party is gone or inactive, or the player's party is not on the map.
    /// </summary>
    bool PinAndEngagePlayer(string partyId);

    /// <summary>
    /// Unpins the party, drops an engage order on the player (vanilla's own peace repair), and has it rethink at its
    /// next hourly tick: a pinned party never advanced its think counter. A missing party is a no-op.
    /// </summary>
    void ReleasePin(string partyId);

    /// <summary>Bumps the party's version so its cached speed is recalculated. A missing party is a no-op.</summary>
    void InvalidateSpeed(string partyId);

    /// <summary>
    /// Spawns a lord party for a free hero at a reachable point between <paramref name="ringInner"/> and
    /// <paramref name="ringOuter"/> times the player's seeing range from him, and at least <paramref name="minSights"/>
    /// seeing ranges away in a straight line; removes the hero as governor and re-checks that he still belongs to no
    /// party first. Returns the new party's StringId, or null when refused.
    /// </summary>
    string? SpawnHunterParty(string heroId, float ringInner, float ringOuter, float minSights);

    /// <summary>One troop's estimated power under the hero's leadership, or NaN when the troop or hero is unknown.</summary>
    float GetTroopPower(string troopId, string leaderHeroId);

    /// <summary>Adds healthy troops to the party's roster; returns how many were added (0 when anything is missing).</summary>
    int AddTroops(string partyId, string troopId, int count);

    /// <summary>Adds grain until the party carries at least <paramref name="targetFood"/> food.</summary>
    void TopUpFood(string partyId, int targetFood);

    /// <summary>
    /// The in-sight inputs, every frame. The hunter is searched for only within the player's seeing range (the locator
    /// grid around him), so a hunter beyond it, or gone, reads as <see cref="HuntSightReading.Missing"/>.
    /// </summary>
    HuntSightReading ReadSight(string partyId);

    /// <summary>The raw facts for an hourly step, read without side effects.</summary>
    HuntStepState ReadStepState(string partyId);

    /// <summary>
    /// Rebuilds the hunter's long-term path to the player and returns its waypoints; <see cref="HuntPath.Empty"/> when the
    /// rebuild fails or the path is empty (a failed rebuild also clears the party's path mode).
    /// </summary>
    HuntPath TryRebuildPath(string partyId);

    /// <summary>
    /// Validates a candidate landing point: on the navigation mesh, on the player's island (nudged once when not), and
    /// no party at war with the hunter within <paramref name="enemyClearance"/>.
    /// </summary>
    HuntLanding ResolveLanding(string partyId, float x, float y, float enemyClearance);

    /// <summary>Moves the party to the point, then rebuilds its path and refreshes its AI.</summary>
    void Relocate(string partyId, float x, float y);

    /// <summary>Shows one of the hunt's lines with the hero's name.</summary>
    void Announce(HuntAnnouncement announcement, string heroId);

    /// <summary>The hero's display name for the console, or the id when unknown.</summary>
    string HeroName(string heroId);
}
