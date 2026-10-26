using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace TAOM.Adapters;

/// <summary>The boundary for Keep Wardens' build priority (#889): finds a town's or castle's Guard House and draws the pick roll.</summary>
public interface IGuardHouseAdapter
{
    /// <summary>
    /// The Guard House as a candidate for the AI's next build: whether it passes vanilla's own filter, its level, and a roll
    /// from 0 up to but not including 1. <c>default</c> (not eligible, no roll drawn) when the settlement has no Guard House
    /// building, it is already level 3, or it is already queued.
    /// </summary>
    GuardHouseCandidate Read(Town town);

    /// <summary>The town's or castle's Guard House building, or null when the settlement has none.</summary>
    Building? GuardHouse(Town town);
}
