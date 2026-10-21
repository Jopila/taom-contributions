using TaleWorlds.CampaignSystem.MapEvents;
using TAOM.Features.LongMemory.Models;

namespace TAOM.Adapters;

/// <summary>
/// The villages, map events and heroes of Long Memory, as plain facts (ADR-007). Every value is raw: whether the village belongs
/// to the player's clan, whether the player's party leads, how much damage a raid did are read here, and
/// <c>LongMemoryService</c> decides what each one means. Each read answers null when the campaign, the event or the village
/// cannot be read, or the engine faults (logged by the adapter).
/// </summary>
public interface ILongMemoryVillageAdapter
{
    /// <summary>
    /// The village of a raid, force-supplies or force-volunteers map event, from the event component the completed event carries.
    /// A raid reports its damage; the other two report 0. Null, before any name, side or notable is read, when the attacker leader
    /// party is not the player's own party (an AI lord's raid, a raid inside another lord's army, a raid an enlisted player joins under a commander; an act the enlisted player starts personally counts).
    /// </summary>
    VillageHostileActionFacts? ReadMapEvent(MapEventComponent? component);

    /// <summary>
    /// The village the player is at, for the coercion that fires no map event (the no-resist menus): the current settlement,
    /// read with the leads fact answering whether the player's party leads (not attached to another party, not inside another
    /// lord's army). Null when there is no campaign or the player is not at a settlement.
    /// </summary>
    VillageHostileActionFacts? ReadCurrentVillage();
}
