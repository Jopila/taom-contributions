using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Features.FireAndTribute;

namespace TAOM.Features.UnlookedForAid.Models;

/// <summary>
/// The campaign's one <c>MobilePartyAIModel</c>, for two features. Help Unlooked-For (#872, docs/features/unlooked-for-aid.md):
/// vanilla's avoid rule first, then the service decides. Fire and Tribute (#870, docs/features/fire-and-tribute.md): a party
/// of a kingdom on a raiding host's own side neither attacks nor avoids that host, so the host lives to reach its target.
/// Every other member is vanilla's.
/// <para>
/// Called on a background AI worker (Campaign.PartiesThink), overlapping the main thread, and also on the main thread
/// (CheckPartyNeedsUpdate runs Tick(0)). So it reads only the two parties' own fields and strengths, as vanilla does at the
/// same call, and touches no IoC, MCM, logger or random number per call (the services hold the captured switch, the
/// loaded ratio and the hosts' immutable own-side snapshot).
/// </para>
/// </summary>
public sealed class TaomMobilePartyAIModel : DefaultMobilePartyAIModel
{
    private readonly UnlookedForAidService _service;
    private readonly IRaidHostOwnSideQuery _hosts;

    public TaomMobilePartyAIModel(UnlookedForAidService service, IRaidHostOwnSideQuery hosts)
    {
        _service = service;
        _hosts = hosts;
    }

    public override bool ShouldConsiderAttacking(MobileParty party, MobileParty targetParty) =>
        !OwnSideMeetsHost(party, targetParty) && base.ShouldConsiderAttacking(party, targetParty);

    public override bool ShouldConsiderAvoiding(MobileParty party, MobileParty targetParty) =>
        !OwnSideMeetsHost(party, targetParty)
        && base.ShouldConsiderAvoiding(party, targetParty) && _service.Decide(SiegeAvoidanceFactsReader.Read(party, targetParty));

    // The fast path reads no party field while no host lives; the service decides. MapFaction is the party's clan's map
    // faction (vanilla's IsEnemy reads it for the same pair), so a lord's party, a mercenary in a kingdom's service and an
    // army member all answer with the kingdom.
    private bool OwnSideMeetsHost(MobileParty party, MobileParty targetParty) =>
        _hosts.AnyHost && _hosts.IsOwnSide(KingdomIdOf(party), targetParty?.StringId);

    private static string? KingdomIdOf(MobileParty party) =>
        party?.MapFaction is { IsKingdomFaction: true } faction ? faction.StringId : null;
}
