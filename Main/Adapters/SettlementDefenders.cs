using Helpers;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Adapters;

/// <summary>
/// The defender count of a settlement as the vanilla settlement tooltip prints it (#846), shared by the tooltip snapshot and
/// the exact stamp of the settlement the player is in so the two can never disagree. It mirrors the sum in
/// <c>TooltipRefresherCollection.RefreshSettlementTooltip</c>: parties inside that are of the settlement's faction and are
/// lord parties, garrisons, or militia while the town is not rebellious. HiddenHostsTooltipBindingTests pins every member
/// this reads, so an engine change to that filter turns a test red.
/// </summary>
internal static class SettlementDefenders
{
    internal static int Count(Settlement settlement)
    {
        var town = settlement.Town;
        bool militiaCounts = town == null || !town.InRebelliousState;
        int sum = 0;
        var parties = settlement.Parties;
        for (int i = 0; i < parties.Count; i++)
        {
            var p = parties[i];
            bool militia = militiaCounts && p.IsMilitia;
            if (DiplomacyHelper.IsSameFactionAndNotEliminated(settlement.MapFaction, p.MapFaction)
                && (p.IsLordParty || militia || p.IsGarrison))
                sum += p.Party.NumberOfHealthyMembers;
        }
        return sum;
    }
}
