namespace TAOM.Features.SupplyLines;

/// <summary>
/// Draws the on-map arrow trail for in-transit supply caravans. Engine boundary. The implementation draws on
/// SandBox.View's map scene, so it lives in TAOM.Client and core registers NullSupplyRouteVisualService (ADR-013).
/// </summary>
public interface ISupplyRouteVisualService
{
    /// <summary>Per-frame update; cheap when nothing changed. Gates itself on the setting with a
    /// clear-once latch so a toggle-off removes the entities exactly once.</summary>
    void Update();

    /// <summary>Removes every arrow entity. Safe to call with none present.</summary>
    void ClearAll();
}
