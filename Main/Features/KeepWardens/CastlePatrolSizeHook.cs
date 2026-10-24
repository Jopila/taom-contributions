using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;

namespace TAOM.Features.KeepWardens;

/// <summary>The boundary behind <see cref="ICastlePatrolSizeHook"/>: the adapter finds the castle's Guard House level, the service decides.</summary>
public sealed class CastlePatrolSizeHook : ICastlePatrolSizeHook
{
    private readonly CastlePatrolService _service;
    private readonly ICastlePatrolAdapter _adapter;

    public CastlePatrolSizeHook(CastlePatrolService service, ICastlePatrolAdapter adapter)
    {
        _service = service;
        _adapter = adapter;
    }

    public void ApplyCastlePatrolSize(PartyBase? party, ref ExplainedNumber result, bool includeDescriptions)
    {
        var limit = _service.SizeLimitOverride(_adapter.CastlePatrolGuardHouseLevel(party?.MobileParty), result.ResultNumber);
        // A fresh number, as vanilla's own town branch returns one: the base number is 0 here, so nothing is lost.
        if (limit > 0)
            result = new ExplainedNumber(limit, includeDescriptions);
    }
}
