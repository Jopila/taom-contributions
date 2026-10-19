using TaleWorlds.CampaignSystem.MapEvents;
using TAOM.Features.BondsOfBattle.Models;

namespace TAOM.Adapters;

/// <summary>Converts a finished player <see cref="MapEvent"/> into engine-free battle data at the boundary (ADR-007).</summary>
public interface IBattleRelationAdapter
{
    /// <summary>
    /// The battle's parties, outcome and enemy casualties. When <paramref name="notableRadius"/> is above zero it also
    /// lists the living notables of every settlement within that many map units of the battle. Null when the battle
    /// cannot be read (no player side, an engine fault, logged).
    /// </summary>
    BattleSnapshot? Capture(MapEvent mapEvent, float notableRadius);
}
