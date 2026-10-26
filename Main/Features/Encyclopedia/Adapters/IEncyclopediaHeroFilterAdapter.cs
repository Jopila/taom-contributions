using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Encyclopedia;

namespace TAOM.Features.Encyclopedia.Adapters;

public interface IEncyclopediaHeroFilterAdapter
{
    /// <summary>The Race group, then the Age group; empty when the config has no valid group. Fresh objects on every call.</summary>
    IReadOnlyList<EncyclopediaFilterGroup> BuildGroups();
}
