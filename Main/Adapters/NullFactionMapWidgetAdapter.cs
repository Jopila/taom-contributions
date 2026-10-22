namespace TAOM.Adapters;

/// <summary>The map widgets are absent (a dedicated server, or a client where TAOM.Client did not load): nothing is hovered or clicked.</summary>
public sealed class NullFactionMapWidgetAdapter : IFactionMapWidgetAdapter
{
    public string HoveredFactionName => "";

    public string LastClickedRegionName => "";

    public void ResetSession()
    {
    }
}
