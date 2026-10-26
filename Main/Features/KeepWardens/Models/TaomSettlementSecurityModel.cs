using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Adapters;

namespace TAOM.Features.KeepWardens.Models;

/// <summary>
/// Keep Wardens (#889, docs/features/keep-wardens.md): a castle with a live patrol gains security per day, as a town already
/// does. The base model (<see cref="DefaultSettlementSecurityModel"/>) builds the whole number, including its own "Patrolling
/// Parties" line for a town's Guard House, and only then does this model add the castle line under the same label. A town
/// gets 0 from the service, so vanilla's line is never doubled. The body is one expression (gamemodels.md rule 4).
/// </summary>
public sealed class TaomSettlementSecurityModel : DefaultSettlementSecurityModel
{
    private readonly CastlePatrolService _service;
    private readonly ICastlePatrolAdapter _adapter;
    // Vanilla's own text, taken once the way DefaultSettlementSecurityModel takes it: the label of its town patrol line.
    private readonly TextObject _patrolPartiesText = GameTexts.FindText("str_patrol_parties");

    public TaomSettlementSecurityModel(CastlePatrolService service, ICastlePatrolAdapter adapter)
    {
        _service = service;
        _adapter = adapter;
    }

    public override ExplainedNumber CalculateSecurityChange(Town town, bool includeDescriptions = false) =>
        AddCastlePatrolSecurity(base.CalculateSecurityChange(town, includeDescriptions), town);

    // ExplainedNumber is a struct: the argument is this method's own copy, returned with the line added. Add(0) is a no-op, so a
    // town, a castle without a live patrol and one without a Guard House come back as base built them.
    private ExplainedNumber AddCastlePatrolSecurity(ExplainedNumber result, Town town)
    {
        result.Add(_service.SecurityPerDay(_adapter.Read(town.Settlement)), _patrolPartiesText);
        return result;
    }
}
