using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.UnlookedForAid.Models;

/// <summary>
/// Help Unlooked-For (#872, docs/features/unlooked-for-aid.md): vanilla's avoid rule first, then the service decides.
/// Only <c>ShouldConsiderAvoiding</c> is overridden; every other member is vanilla's.
/// <para>
/// Called on a background AI worker (Campaign.PartiesThink), overlapping the main thread, and also on the main thread
/// (CheckPartyNeedsUpdate runs Tick(0)). So it reads only the two parties' own fields and strengths, as vanilla does at the
/// same call, and touches no IoC, MCM, logger or random number per call (the service holds the captured switch and the
/// loaded ratio).
/// </para>
/// </summary>
public sealed class TaomMobilePartyAIModel : DefaultMobilePartyAIModel
{
    private readonly UnlookedForAidService _service;

    public TaomMobilePartyAIModel(UnlookedForAidService service)
    {
        _service = service;
    }

    public override bool ShouldConsiderAvoiding(MobileParty party, MobileParty targetParty) =>
        base.ShouldConsiderAvoiding(party, targetParty) && _service.Decide(SiegeAvoidanceFactsReader.Read(party, targetParty));
}
