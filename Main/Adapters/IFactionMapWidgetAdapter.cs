namespace TAOM.Adapters;

/// <summary>
/// What the culture-stage map widgets report to FactionSelectionVM: the hovered faction, the last clicked
/// region, and the reset for each culture stage (ADR-013). The widgets live in TAOM.Client and a dedicated server
/// has none, so core registers NullFactionMapWidgetAdapter and the client composition replaces it.
/// </summary>
public interface IFactionMapWidgetAdapter
{
    string HoveredFactionName { get; }

    string LastClickedRegionName { get; }

    void ResetSession();
}
