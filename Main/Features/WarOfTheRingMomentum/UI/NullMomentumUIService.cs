namespace TAOM.Features.WarOfTheRingMomentum.UI;

/// <summary>
/// The map meter is client-only (ADR-013): a dedicated server, or a client where TAOM.Client did not load, shows none.
/// TAOM.Client replaces this registration with the service that adds the meter's map view.
/// </summary>
public sealed class NullMomentumUIService : IMomentumUIService
{
    public void AddMapMeter()
    {
    }

    public void RemoveMapMeter()
    {
    }
}
