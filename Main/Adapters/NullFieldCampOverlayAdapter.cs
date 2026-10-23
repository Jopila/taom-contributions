namespace TAOM.Adapters;

/// <summary>There is no overlay view to attach (a dedicated server, or a client where TAOM.Client did not load).</summary>
public sealed class NullFieldCampOverlayAdapter : IFieldCampOverlayAdapter
{
    public void EnsureAttached()
    {
    }
}
