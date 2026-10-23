using SandBox.View.Map.Navigation;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.FiefManagement;
using TAOM.Features.FiefManagement.UI;

namespace TAOM.Client.Features.FiefManagement;

/// <summary>
/// Builds the "Fiefs" navigation button (ADR-013: the element type lives in TAOM.Client). Patch36 asks for one per
/// <c>MapNavigationHandler</c> construction; the opener and logger are held here, resolved when the factory itself is first resolved (the first Patch36 postfix).
/// </summary>
internal sealed class TaomFiefsNavigationElementFactory : IFiefsNavigationElementFactory
{
    private readonly FiefHubOpener _opener;
    private readonly IModLogger _logger;

    public TaomFiefsNavigationElementFactory(FiefHubOpener opener, IModLogger logger)
    {
        _opener = opener;
        _logger = logger;
    }

    public INavigationElement? Create(MapNavigationHandler handler) =>
        new TaomFiefsNavigationElement(handler, _opener, _logger);
}
