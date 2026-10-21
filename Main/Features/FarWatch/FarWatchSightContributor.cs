using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Features.FieldCamp;

namespace TAOM.Features.FarWatch;

/// <summary>
/// The scout's effect on the main party's sight, through <see cref="IPartySpottingContributor"/> (docs/features/far-watch.md
/// "The seam"). A surroundings ride raises sight and its raised sight holds and fades after it; a settlement ride lowers it
/// while it runs. Both members are asked per spotting query: one toggle read, one party test, then the service's arithmetic on
/// plain fields. Nothing of its own is allocated, no IoC, null-tolerant on the party. Gated on FieldCamp's master toggle as the lookout is, so
/// a disabled feature cannot keep a simulation-relevant sight bonus alive.
/// </summary>
internal sealed class FarWatchSightContributor : IPartySpottingContributor
{
    private readonly FarWatchService _service;
    private readonly IFarWatchWorldAdapter _world;
    private readonly ICampSettingsProvider _camps;

    public FarWatchSightContributor(FarWatchService service, IFarWatchWorldAdapter world, ICampSettingsProvider camps)
    {
        _service = service;
        _world = world;
        _camps = camps;
    }

    public float GetSpottingRangeBonusFactor(MobileParty party) =>
        _camps.Enabled && party != null && party.IsMainParty ? _service.SightFactor(_world.NowHours) : 0f;

    public float GetSpottingRangeCapBonus(MobileParty party) =>
        _camps.Enabled && party != null && party.IsMainParty ? _service.SightCapBonus(_world.NowHours) : 0f;
}
