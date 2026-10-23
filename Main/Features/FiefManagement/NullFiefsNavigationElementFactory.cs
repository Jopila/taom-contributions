using SandBox.View.Map.Navigation;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.FiefManagement;

/// <summary>No button (a dedicated server, or a client where TAOM.Client did not load): the bar stays as vanilla built it.</summary>
public sealed class NullFiefsNavigationElementFactory : IFiefsNavigationElementFactory
{
    public INavigationElement? Create(MapNavigationHandler handler) => null;
}
