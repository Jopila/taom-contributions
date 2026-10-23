using SandBox.View.Map.Navigation;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.FiefManagement;

/// <summary>
/// The "Fiefs" button for one map navigation handler (ADR-013: the element type is client-only). Core registers
/// NullFiefsNavigationElementFactory, which leaves the bar as vanilla built it; TAOM.Client replaces it.
/// </summary>
public interface IFiefsNavigationElementFactory
{
    INavigationElement? Create(MapNavigationHandler handler);
}
