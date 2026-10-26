using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;

namespace TAOM.Features.SeasonedHosts.Models;

/// <summary>
/// Seasoned Hosts (#890, docs/features/seasoned-hosts.md): learning slows past a skill's limit but never quite stops. Base
/// first, then the rate's minimum is raised to the service's answer. The signature carries no hero, so the floor reaches every
/// hero, the player included. Registry: docs/reference/gamemodel-registry.md.
/// </summary>
public sealed class TaomCharacterDevelopmentModel : DefaultCharacterDevelopmentModel
{
    private readonly SeasonedHostsService _service;

    public TaomCharacterDevelopmentModel(SeasonedHostsService service) => _service = service;

    public override ExplainedNumber CalculateLearningRate(IReadOnlyPropertyOwner<CharacterAttribute> characterAttributes, int focusValue, int skillValue, SkillObject skill, bool includeDescriptions = false) =>
        WithFloor(base.CalculateLearningRate(characterAttributes, focusValue, skillValue, skill, includeDescriptions));

    private ExplainedNumber WithFloor(ExplainedNumber rate)
    {
        rate.LimitMin(_service.LearningRateFloor(rate.LimitMinValue));
        return rate;
    }
}
