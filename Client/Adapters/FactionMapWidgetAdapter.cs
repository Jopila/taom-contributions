using TAOM.Adapters;
using TAOM.Features.FactionMap.Widgets;

namespace TAOM.Client.Adapters;

/// <summary>
/// Reports the culture-stage map widgets' static session state to core (ADR-013): the hovered faction and the
/// last clicked region that <c>FactionSelectionVM</c> reads, and the reset for each culture stage.
/// </summary>
internal sealed class FactionMapWidgetAdapter : IFactionMapWidgetAdapter
{
    public string HoveredFactionName => PolygonWidget.HoveredFactionName ?? "";

    public string LastClickedRegionName => PolygonWidget.LastClickedRegionName ?? "";

    public void ResetSession() => PolygonWidget.ResetSession();
}
