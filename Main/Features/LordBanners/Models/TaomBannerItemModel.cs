using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;

namespace TAOM.Features.LordBanners.Models;

/// <summary>
/// Banner items a lord may receive (docs/features/lord-banners.md). The base model keeps only the hero's own
/// culture and culture-less items, so a lord of a TAOM culture never gets a banner. The service widens that
/// per culture; the level still comes from the base model. Its only reader is the vanilla give path in
/// <c>BannerCampaignBehavior</c>.
/// </summary>
public class TaomBannerItemModel : DefaultBannerItemModel
{
    private readonly LordBannerService _service;

    public TaomBannerItemModel(LordBannerService service)
    {
        _service = service;
    }

    public override IEnumerable<ItemObject> GetPossibleRewardBannerItemsForHero(Hero hero)
    {
        return _service.Allowed(hero.Culture?.StringId, GetBannerItemLevelForHero(hero), GetPossibleRewardBannerItems(),
            item => (item.Culture?.StringId, item.BannerComponent?.BannerLevel ?? 0));
    }
}
