using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Adapters;

/// <summary>The boundary for Keep Wardens (#888): reads a settlement into a snapshot and hands back the engine's patrol templates.</summary>
public interface ICastlePatrolAdapter
{
    /// <summary>The settlement as a snapshot. A town, a village or null gives <c>default</c> without reading any building.</summary>
    CastlePatrolSnapshot Read(Settlement? settlement);

    /// <summary>The owner culture's patrol template for a strength of 1, 2 or 3; null when there is none.</summary>
    PartyTemplateObject? TemplateFor(Settlement settlement, int strength);

    /// <summary>
    /// For a patrol party whose home is a castle, the level of that castle's Guard House (0 when none is built);
    /// <see cref="CastlePatrolSnapshot.NotACastlePatrol"/> (-1) for any other party.
    /// Reads the party's flags first, so the common party costs two property reads.
    /// </summary>
    int CastlePatrolGuardHouseLevel(MobileParty? party);
}
