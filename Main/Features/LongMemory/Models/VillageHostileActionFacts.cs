using System.Collections.Generic;
using TAOM.Features.Execution;
using TAOM.Features.Relations;

namespace TAOM.Features.LongMemory.Models;

/// <summary>
/// A raid on a village, or a forced taking from one, as raw facts read by the adapter. Whether it counts, and what it costs, is
/// the service's decision. <see cref="PlayerLeads"/> is true when the player's own party is the party leading the act (an AI
/// lord's raid, a raid inside another lord's army and a raid an enlisted player joins under a commander are not; an act the enlisted player starts personally counts). <see cref="RaidDamage"/> is the hit points
/// this raid event removed from the village, on the village's 0 to 1 health scale and capped at 1 by the engine, 0 for anything
/// that is not a raid. <see cref="Player"/> and <see cref="OwnerSide"/> are the sides of the player's map
/// faction and the village owner's, each built through <c>FactionSides</c>. <see cref="OwnerLeader"/> is the leader of the owning
/// clan (null when there is none) and <see cref="Notables"/> the notables living in the village; each carries the hero a change
/// to it really moves.
/// </summary>
public sealed record VillageHostileActionFacts(
    string VillageName,
    SideIds Player,
    SideIds OwnerSide,
    bool IsVillage,
    bool PlayerClanOwns,
    bool PlayerLeads,
    float RaidDamage,
    RelationTarget? OwnerLeader,
    IReadOnlyList<RelationTarget> Notables);
