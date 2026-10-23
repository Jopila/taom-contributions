namespace TAOM.Features.SupplyLines;

/// <summary>There is no map scene to draw on (a dedicated server, or a client where TAOM.Client did not load); nothing is drawn.</summary>
public sealed class NullSupplyRouteVisualService : ISupplyRouteVisualService
{
    public void Update()
    {
    }

    public void ClearAll()
    {
    }
}
