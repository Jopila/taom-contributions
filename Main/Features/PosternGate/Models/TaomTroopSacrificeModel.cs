using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;

namespace TAOM.Features.PosternGate.Models;

/// <summary>
/// Postern Gate's loss cut (docs/features/postern-gate.md). Breaking into and out of a besieged settlement each get the
/// base model's number, then the service adds the building's factor and the result is held at zero. The settlement is
/// <c>siegeEvent.BesiegedSettlement</c>: the engine passes only the main party to its break-in/out action, so the party
/// argument is never the key. With no Postern Gate, or the switch off, the factor is zero and the number is the base
/// model's, unchanged. The override body holds no branch (gamemodels.md rule 4): the service decides.
/// </summary>
public sealed class TaomTroopSacrificeModel : DefaultTroopSacrificeModel
{
    private readonly PosternGateService _service;

    public TaomTroopSacrificeModel(PosternGateService service)
    {
        _service = service;
    }

    public override ExplainedNumber GetLostTroopCountForBreakingInBesiegedSettlement(MobileParty party, SiegeEvent siegeEvent) =>
        Cut(base.GetLostTroopCountForBreakingInBesiegedSettlement(party, siegeEvent), siegeEvent);

    public override ExplainedNumber GetLostTroopCountForBreakingOutOfBesiegedSettlement(
        MobileParty party, SiegeEvent siegeEvent, bool isBreakingOutFromPort) =>
        Cut(base.GetLostTroopCountForBreakingOutOfBesiegedSettlement(party, siegeEvent, isBreakingOutFromPort), siegeEvent);

    private ExplainedNumber Cut(ExplainedNumber result, SiegeEvent siegeEvent)
    {
        // SumOfFactors is the engine's public total of every factor already on the number (ExplainedNumber.cs:147). No
        // description is passed: the base model builds its number without descriptions (DefaultTroopSacrificeModel), so
        // the engine drops it (ExplainedNumber.cs:243). A zero factor adds nothing (ExplainedNumber.cs:240).
        result.AddFactor(_service.CutFor(siegeEvent?.BesiegedSettlement?.StringId, result.SumOfFactors));
        result.LimitMin(0f);
        return result;
    }
}
