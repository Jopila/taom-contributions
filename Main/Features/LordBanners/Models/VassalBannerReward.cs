using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TAOM.Features.LordBanners.Models;

/// <summary>
/// Boundary helper for <see cref="TaomVassalRewardsModel"/>: reads the engine's banner pool and roster, hands the
/// service the facts, and adds the picked banner. The base roster is returned untouched when it already holds a banner,
/// so a vanilla culture's kingdom gets exactly vanilla's result.
/// </summary>
internal static class VassalBannerReward
{
    private const int VassalRewardBannerLevel = 2;

    internal static ItemRoster AddIfMissing(ItemRoster roster, Kingdom kingdom, LordBannerService service)
    {
        if (roster.Any(element => element.EquipmentElement.Item?.IsBannerItem == true))
            return roster;

        var picked = service.Allowed(kingdom.Culture?.StringId, VassalRewardBannerLevel,
            Campaign.Current.Models.BannerItemModel.GetPossibleRewardBannerItems(),
            item => (item.Culture?.StringId, item.BannerComponent?.BannerLevel ?? 0)).GetRandomElement();
        if (picked != null)
            roster.AddToCounts(picked, 1);
        return roster;
    }
}
