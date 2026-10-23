namespace TAOM.Adapters;

/// <summary>
/// Attaches the realm borders' map view to the campaign map screen when it is missing (ADR-013). The view is client-only,
/// so core registers NullRealmBordersMapViewAdapter and the client composition replaces it.
/// </summary>
public interface IRealmBordersMapViewAdapter
{
    /// <summary>Called every campaign tick; does nothing without a map screen or when the view is already attached.</summary>
    void EnsureAttached();
}
