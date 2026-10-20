using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Roster;

namespace TAOM.Features.LordBanners.Models;

/// <summary>
/// The banner reward for joining a kingdom (docs/features/lord-banners.md, #933). The base model picks a level 2
/// banner whose culture is the kingdom's, which never matches a TAOM culture. The roster the base builds is kept; the
/// helper adds a banner only when it holds none.
/// </summary>
public class TaomVassalRewardsModel : DefaultVassalRewardsModel
{
    private readonly LordBannerService _service;

    public TaomVassalRewardsModel(LordBannerService service)
    {
        _service = service;
    }

    public override ItemRoster GetEquipmentRewardsForJoiningKingdom(Kingdom kingdom)
    {
        return VassalBannerReward.AddIfMissing(base.GetEquipmentRewardsForJoiningKingdom(kingdom), kingdom, _service);
    }
}
