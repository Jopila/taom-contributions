using Helpers;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Adapters;

/// <summary>
/// The defender count of a settlement as the vanilla settlement tooltip prints it (#846), shared by the tooltip snapshot and
/// the exact stamp of the settlement the player is in so the two can never disagree. It mirrors the sum in
/// <c>TooltipRefresherCollection.RefreshSettlementTooltip</c>: parties inside that are of the settlement's faction and are
/// lord parties, garrisons, or militia while the town is not rebellious. HiddenHostsTooltipBindingTests pins every member
/// this reads, so an engine change to that filter turns a test red. <see cref="IsDefender"/> is the one predicate: the count
/// and the scout's breakdown (#847) both use it, so the list of parties always sums to the total.
/// </summary>
internal static class SettlementDefenders
{
    internal static int Count(Settlement settlement)
    {
        bool militiaCounts = MilitiaCounts(settlement);
        int sum = 0;
        var parties = settlement.Parties;
        for (int i = 0; i < parties.Count; i++)
        {
            var p = parties[i];
            if (IsDefender(settlement, p, militiaCounts))
                sum += p.Party.NumberOfHealthyMembers;
        }
        return sum;
    }

    /// <summary>True when the party inside the settlement counts as one of its defenders.</summary>
    internal static bool IsDefender(Settlement settlement, MobileParty party) =>
        IsDefender(settlement, party, MilitiaCounts(settlement));

    private static bool IsDefender(Settlement settlement, MobileParty p, bool militiaCounts)
    {
        bool militia = militiaCounts && p.IsMilitia;
        return DiplomacyHelper.IsSameFactionAndNotEliminated(settlement.MapFaction, p.MapFaction)
               && (p.IsLordParty || militia || p.IsGarrison);
    }

    private static bool MilitiaCounts(Settlement settlement)
    {
        var town = settlement.Town;
        return town == null || !town.InRebelliousState;
    }
}
