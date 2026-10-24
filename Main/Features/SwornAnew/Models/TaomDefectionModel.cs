using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TAOM.Adapters;

namespace TAOM.Features.SwornAnew.Models;

/// <summary>
/// Whether a hero can be asked to join the player's kingdom (docs/features/sworn-anew.md, #869). Vanilla's
/// <c>DefaultDefectionModel</c> ends with "not a prisoner", so a captured lord never defects. The facts about the hero are read
/// at the boundary and handed to <see cref="SwornAnewService"/>, which answers null for a free hero (and whenever the feature
/// stands down), so vanilla's own check decides; for a prisoner its verdict is the answer. The only consumers are the two
/// <c>LordDefectionCampaignBehavior</c> conversation conditions on the diplomacy menu (<c>conversation_player_is_asking_to_recruit_enemy_on_condition</c>
/// and its neutral twin); the model has no AI-side use. Vanilla's prisoner line never asks this model, and has its own
/// postfix (<c>Patch116_PrisonerDefectionLine_Postfix</c>).
/// </summary>
public class TaomDefectionModel : DefaultDefectionModel
{
    private readonly SwornAnewService _service;
    private readonly ISwornAnewAdapter _adapter;

    public TaomDefectionModel(SwornAnewService service, ISwornAnewAdapter adapter)
    {
        _service = service;
        _adapter = adapter;
    }

    public override bool CanHeroDefectToFaction(Hero hero, Kingdom kingdom) =>
        _service.CanPrisonerDefect(_adapter.Read(hero, kingdom)) ?? base.CanHeroDefectToFaction(hero, kingdom);
}
