namespace TAOM.Adapters;

/// <summary>
/// Attaches the field camp's overlay map view to the campaign map screen when it is missing (ADR-013). The view is
/// client-only, so core registers NullFieldCampOverlayAdapter and the client composition replaces it.
/// </summary>
public interface IFieldCampOverlayAdapter
{
    /// <summary>Called every campaign tick; does nothing without a map screen or when the view is already attached.</summary>
    void EnsureAttached();
}
