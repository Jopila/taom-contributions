using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Adapters;

namespace TAOM.Features.KeepWardens.Models;

/// <summary>
/// Keep Wardens (#888, docs/features/keep-wardens.md): a castle with a built Guard House fields a patrol party. The base
/// model (<see cref="DefaultSettlementPatrolModel"/>) allows towns only, so the castle branch answers first. The template calls
/// base only when the plan has no strength; <c>CanSettlementHavePatrolParties</c> and the spawn delay call base whenever the plan
/// is not Allowed (base answers false for a castle, and vanilla asks for the delay only after Can says yes). A refused castle
/// still gets its template, because vanilla's hourly refill asks for a live patrol's template and base returns null for a castle.
/// Each body is one expression (gamemodels.md rule 4): the service decides.
/// </summary>
public sealed class TaomSettlementPatrolModel : DefaultSettlementPatrolModel
{
    private readonly CastlePatrolService _service;
    private readonly ICastlePatrolAdapter _adapter;

    public TaomSettlementPatrolModel(CastlePatrolService service, ICastlePatrolAdapter adapter)
    {
        _service = service;
        _adapter = adapter;
    }

    public override bool CanSettlementHavePatrolParties(Settlement settlement, bool naval) =>
        Plan(settlement, naval).Allowed || base.CanSettlementHavePatrolParties(settlement, naval);

    public override PartyTemplateObject GetPartyTemplateForPatrolParty(Settlement settlement, bool naval) =>
        Plan(settlement, naval) is { Strength: > 0 } plan
            ? _adapter.TemplateFor(settlement, plan.Strength)
            : base.GetPartyTemplateForPatrolParty(settlement, naval);

    public override CampaignTime GetPatrolPartySpawnDuration(Settlement settlement, bool naval) =>
        Plan(settlement, naval) is { Allowed: true } plan
            ? CampaignTime.Days(plan.SpawnDays)
            : base.GetPatrolPartySpawnDuration(settlement, naval);

    private CastlePatrolPlan Plan(Settlement settlement, bool naval) => _service.Plan(_adapter.Read(settlement), naval);
}
