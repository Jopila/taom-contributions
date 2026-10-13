using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Validation;

namespace TAOM.Adapters;

/// <summary>
/// Production <see cref="IChronicleWorldAdapter"/>. Members read here, checked against the installed v1.5.4
/// decompile: <c>CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow</c> (float), <c>CampaignTime.Now.ToHours</c>,
/// <c>Settlement.Find</c> (<c>MBObjectManager.GetObject</c>, null for an unknown id),
/// <c>Settlement.MapFaction</c> (computed: <c>SettlementComponent?.MapFaction</c>), <c>Settlement.SiegeEvent</c>,
/// <c>SiegeEvent.BesiegerCamp</c> (a field), <c>BesiegerCamp.LeaderParty</c>, <c>MobileParty.MapFaction</c>
/// (computed) and <c>Kingdom.All</c> (<c>Campaign.Current.Kingdoms</c>, so guarded on the campaign). Every
/// computed getter is reached through <c>?.</c> (adapters.md); the chronicle service catches anything that
/// still throws.
/// </summary>
public sealed class ChronicleWorldAdapter : IChronicleWorldAdapter
{
    public double GetElapsedDays()
    {
        var days = Campaign.Current?.Models?.CampaignTimeModel?.CampaignStartTime.ElapsedDaysUntilNow;
        return days.HasValue && FiniteFloatValidator.IsFinite(days.Value) ? days.Value : 0d;
    }

    public double GetNowHours() => Campaign.Current == null ? 0d : CampaignTime.Now.ToHours;

    public bool SettlementExists(string settlementId) => Find(settlementId) != null;

    public bool KingdomExists(string kingdomId)
    {
        if (Campaign.Current == null || string.IsNullOrEmpty(kingdomId))
            return false;
        foreach (var kingdom in Kingdom.All)
        {
            if (kingdom != null && kingdom.StringId == kingdomId)
                return true;
        }

        return false;
    }

    public string? GetOwnerKingdomId(string settlementId) => (Find(settlementId)?.MapFaction as Kingdom)?.StringId;

    public string? GetBesiegerKingdomId(string settlementId) =>
        (Find(settlementId)?.SiegeEvent?.BesiegerCamp?.LeaderParty?.MapFaction as Kingdom)?.StringId;

    private static Settlement? Find(string settlementId) =>
        Campaign.Current == null || string.IsNullOrEmpty(settlementId) ? null : Settlement.Find(settlementId);
}
