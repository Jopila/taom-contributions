using SandBox.View.Map;
using TAOM.Adapters;
using TAOM.Features.FieldCamp.UI;

namespace TAOM.Client.Adapters;

/// <summary>
/// Attaches the field camp's overlay map view when the current map screen lacks it (ADR-013). The screen is rebuilt on
/// every load and does not exist at session launch, so the campaign behavior asks from its tick.
/// </summary>
internal sealed class FieldCampOverlayAdapter : IFieldCampOverlayAdapter
{
    public void EnsureAttached()
    {
        var mapScreen = MapScreen.Instance;
        if (mapScreen == null)
            return;
        if (mapScreen.GetMapView<FieldCampMapView>() != null)
            return;

        mapScreen.AddMapView<FieldCampMapView>();
    }
}
