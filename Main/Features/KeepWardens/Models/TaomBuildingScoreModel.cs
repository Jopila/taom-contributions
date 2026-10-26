using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TAOM.Adapters;

namespace TAOM.Features.KeepWardens.Models;

/// <summary>
/// Keep Wardens (#889, docs/features/keep-wardens.md): the AI queues the Guard House first. Vanilla's next build is a random
/// pick among the buildings that are not daily projects, below level 3 and not already queued; the service picks the Guard
/// House when it is not built yet (level 0), or about 60 percent of the time while it is still upgradable, and the base pick decides
/// every other time. Only <c>GetNextBuilding</c> is overridden: the daily project pick stays vanilla's, and the engine asks
/// for the next build daily only for fiefs the player does not own, and once for every fortification at a new game. The body is one expression (gamemodels.md rule 4).
/// </summary>
public sealed class TaomBuildingScoreModel : DefaultBuildingScoreCalculationModel
{
    private readonly CastlePatrolService _service;
    private readonly IGuardHouseAdapter _adapter;

    public TaomBuildingScoreModel(CastlePatrolService service, IGuardHouseAdapter adapter)
    {
        _service = service;
        _adapter = adapter;
    }

    public override Building? GetNextBuilding(Town town) =>
        _service.PicksGuardHouse(_adapter.Read(town)) ? _adapter.GuardHouse(town) : base.GetNextBuilding(town);
}
