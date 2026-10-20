using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TAOM.Features.HiddenHosts;

namespace TAOM.Adapters;

/// <summary>
/// The campaign facts Hidden Hosts' memory reads (#846 phase 3). Holds nothing between calls: no engine object is kept on
/// this process singleton, so no campaign outlives its own (PartyIntelAdapterTests pins the fields). Main thread only.
/// </summary>
public sealed class HiddenHostsCampaignAdapter : IHiddenHostsCampaignAdapter
{
    public int NowHour => Campaign.Current == null ? -1 : HiddenHostsMath.ToWholeHours(CampaignTime.Now.ToHours);

    public int MainScoutingSkill => MobileParty.MainParty?.EffectiveScout?.GetSkillValue(DefaultSkills.Scouting) ?? 0;

    public bool? IsSettlementInSight(string id)
    {
        var settlement = Settlement.Find(id);
        return settlement == null ? null : settlement.IsInspected;
    }

    public bool TryReadVisibilityChange(PartyBase? party, out string id, out bool visible)
    {
        var mobile = party?.MobileParty;
        id = mobile?.StringId ?? string.Empty;
        visible = mobile != null && mobile.IsVisible;
        return mobile != null && !string.IsNullOrEmpty(mobile.StringId);
    }

    public void GrantScoutingXp(int xp)
    {
        var scout = MobileParty.MainParty?.EffectiveScout;
        if (scout == null || xp <= 0) return;
        scout.AddSkillXp(DefaultSkills.Scouting, xp);
    }
}
