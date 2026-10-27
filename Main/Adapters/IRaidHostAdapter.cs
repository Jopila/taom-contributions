using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// Every engine call the raiding hosts make (#870, ADR-007): creating a bandit-clan party, filling its roster, pinning
/// its AI, giving it raid, engage and go-home orders, reading villages and nearby parties, and removing it. Ids in,
/// plain values out; each id is resolved against the live campaign on every call, so nothing here outlives a save load.
/// It reads and acts; every decision is the service's. Main thread only (campaign events).
/// </summary>
public interface IRaidHostAdapter
{
    /// <summary>
    /// Whether a host of <paramref name="clanId"/> can be created near <paramref name="anchorId"/>, changing nothing:
    /// the clan, the anchor and a hideout of the clan's culture must all exist.
    /// </summary>
    RaidSpawnFailure Probe(string clanId, string anchorId);

    /// <summary>
    /// The anchor's current owner: its map faction's id and culture id, and whether that faction is a kingdom (#1032).
    /// False, with both ids empty, when the anchor or its owner cannot be read.
    /// </summary>
    bool TryGetAnchorOwner(string anchorId, out string factionId, out string cultureId, out bool isKingdom);

    /// <summary>
    /// Creates an empty host: a bandit party of the clan whose home is the nearest hideout of the clan's culture to the
    /// anchor, named <paramref name="keyedName"/> (a <c>{=key}English</c> string). It appears at a point the engine finds
    /// near the anchor's gate (the gate itself when the engine finds none), which is then retried up to 15 times around
    /// that point for one out of the player's sight. The result can land up to about twice the spawn radius from the
    /// gate, and a point in sight is kept when every retry fails, so a host can appear in sight. A null home would crash vanilla's bandit bookkeeping, so none is ever created.
    /// </summary>
    RaidHostSpawn SpawnHost(string clanId, string anchorId, string keyedName, float spawnRadiusDays);

    /// <summary>One troop's estimated power (no leader modifier), or NaN when the troop is unknown.</summary>
    float TroopPower(string troopId);

    /// <summary>Adds healthy troops to the host's roster; returns how many were added (0 when anything is missing).</summary>
    int AddTroops(string partyId, string troopId, int count);

    /// <summary>
    /// Finishes a freshly filled host: marks its visual dirty, starts its trade purse at zero and pins its AI
    /// (<c>SetDoNotMakeNewDecisions(true)</c>). Vanilla's hourly think then never re-plans it, so its orders are the
    /// service's; the engine's initiative still applies around the settlement it targets.
    /// </summary>
    void CompleteSpawn(string partyId);

    /// <summary>Unpins the host and has it rethink at its next hourly tick. A missing party is a no-op.</summary>
    void ReleasePin(string partyId);

    RaidHostSnapshot Read(string partyId);

    /// <summary>
    /// Every village on the map, read live (owner kingdom, state, hearth, position). Nothing is cached: a village holds the
    /// campaign graph. Which villages are raidable, and which are nearest, is the service's rule (FireAndTributeMath).
    /// </summary>
    IReadOnlyList<VillageReading> ReadVillages();

    /// <summary>One village read live, for the hourly check that a raid order still stands; <see cref="VillageReading.Missing"/> when it is gone.</summary>
    VillageReading ReadVillage(string villageId);

    /// <summary>
    /// Active parties of some kingdom within <paramref name="radius"/> map units of the host, the host excluded. Parties
    /// the engine itself would not interact with (ignored ones, a besieger that is not its camp's leader) are left out,
    /// and an army's leader reports its army's strength.
    /// </summary>
    IReadOnlyList<NearbyParty> PartiesNear(string hostPartyId, float radius);

    /// <summary>
    /// Has the engine's own party AI leave the player's party alone for <paramref name="hours"/> (the call vanilla's
    /// safe-passage bribe makes, <c>MobilePartyAi.SetDoNotAttackMainParty</c>). It only ever lengthens the span. A
    /// missing or inactive party is a no-op.
    /// </summary>
    void SpareMainParty(string hostPartyId, int hours);

    void OrderEngage(string hostPartyId, string targetPartyId);

    void OrderRaid(string hostPartyId, string villageId);

    /// <summary>Orders the host to its home hideout (a no-op when it has none).</summary>
    void OrderGoHome(string hostPartyId);

    /// <summary>
    /// Removes the host from the map. False, and nothing changes, unless it is active and in no map event.
    /// </summary>
    bool Despawn(string partyId);
}
