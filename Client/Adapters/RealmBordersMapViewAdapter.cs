using SandBox.View.Map;
using TAOM.Adapters;
using TAOM.Features.RealmBorders.UI;

namespace TAOM.Client.Adapters;

/// <summary>
/// Attaches the realm borders' map view when the current map screen lacks it (ADR-013). The screen is rebuilt on every
/// load and does not exist at session launch, so the campaign behavior asks from its tick.
/// </summary>
internal sealed class RealmBordersMapViewAdapter : IRealmBordersMapViewAdapter
{
    public void EnsureAttached()
    {
        var mapScreen = MapScreen.Instance;
        if (mapScreen == null || mapScreen.GetMapView<RealmBordersMapView>() != null)
            return;
        mapScreen.AddMapView<RealmBordersMapView>();
    }
}
