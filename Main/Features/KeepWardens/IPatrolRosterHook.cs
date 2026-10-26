using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace TAOM.Features.KeepWardens;

/// <summary>
/// The narrow seam <c>TaomPartySizeModel.FindAppropriateInitialRosterForMobileParty</c> reads for patrol rosters (#991). Vanilla
/// fills each template stack to its maximum, so a patrol spawns, and refills after a fight, at the template's head count
/// instead of its size limit; a patrol under 90 percent of its limit never leaves home.
/// </summary>
public interface IPatrolRosterHook
{
    /// <summary>
    /// For a land patrol party, the roster scaled to the party's current size limit (same troops, same order). Any other
    /// party, a naval patrol, or a limit of 0 or less gets <paramref name="baseRoster"/> back untouched.
    /// </summary>
    TroopRoster FitToLimit(MobileParty party, TroopRoster baseRoster);
}
