using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TAOM.Adapters;

namespace TAOM.Features.SeasonedHosts.Models;

/// <summary>
/// Seasoned Hosts (#890, docs/features/seasoned-hosts.md): an AI lord's skill XP is multiplied. Vanilla asks this model for the
/// multiplier only after it has banked the raw XP toward the hero's level, so the factor never speeds the hero level. Base
/// first, then the service's factor. Registry: docs/reference/gamemodel-registry.md.
/// </summary>
public sealed class TaomGenericXpModel : DefaultGenericXpModel
{
    private readonly SeasonedHostsService _service;
    private readonly ISeasonedHostsAdapter _adapter;

    public TaomGenericXpModel(SeasonedHostsService service, ISeasonedHostsAdapter adapter)
    {
        _service = service;
        _adapter = adapter;
    }

    public override float GetXpMultiplier(Hero hero) =>
        base.GetXpMultiplier(hero) * _service.SkillXpMultiplier(_adapter.Read(hero));
}
