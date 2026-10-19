using TaleWorlds.CampaignSystem;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.TaomBuildings.Hooks;

/// <summary>
/// Seeds TAOM's buildings into the fiefs of a loaded save (see <see cref="TaomBuildingSeedingService"/>). It persists
/// nothing: the buildings it adds are ordinary town buildings, saved by the engine with the town. A peer without
/// authority (a co-op client) does not seed, because the building list is shared world state and the host writes it.
/// The service never throws, so this handler cannot loop the loading screen.
/// </summary>
internal sealed class TaomBuildingSeedingBehavior : CampaignBehaviorBase
{
    private readonly TaomBuildingSeedingService _service;
    private readonly ICoopSessionProvider _coop;

    public TaomBuildingSeedingBehavior(TaomBuildingSeedingService service, ICoopSessionProvider coop)
    {
        _service = service;
        _coop = coop;
    }

    public override void RegisterEvents() =>
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, _ => _service.Apply(_coop.IsAuthority));

    public override void SyncData(IDataStore dataStore)
    {
    }
}
