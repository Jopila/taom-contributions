using TAOM.Features.Refuge.Domain;
using TaleWorlds.Library;

namespace TAOM.Features.Refuge;

/// <summary>There is no map scene to draw on (a dedicated server, or a client where TAOM.Client did not load); Show reports not ready, so RefugeService keeps retrying inside its game-time throttle, and each retry also computes the party position.</summary>
public sealed class NullRefugeVisualService : IRefugeVisualService
{
    public bool Show(string refugeId, RefugeTier tier, bool fortified, Vec2 position) => false;

    public void Remove(string refugeId)
    {
    }

    public void ClearAll()
    {
    }

    public void TickWind()
    {
    }
}
