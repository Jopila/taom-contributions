using System;
using TAOM.Core.Logging;
using TaleWorlds.ScreenSystem;

namespace TAOM.Features.LinesOfDescent.UI;

/// <summary>
/// Opens the Lines of Descent popup (#905), one at a time, on whatever screen is on top. Registered once in
/// <c>TaomClientComposition</c>; the two mixins capture it in their constructors. A second click while a popup is open does
/// nothing; a throw while opening closes the half-built layer and is logged, so the click is the only thing lost.
/// </summary>
internal sealed class FamilyTreePopup
{
    private readonly IFamilyTreeService _service;
    private readonly IModLogger _logger;
    private FamilyTreeLayer? _layer;

    public FamilyTreePopup(IFamilyTreeService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public bool IsOpen => _layer != null;

    /// <param name="heroId">The selected hero's string id.</param>
    /// <param name="fromClanMembers">True when opened from the clan screen's Members tab, which closes the popup on a tab change.</param>
    public void Open(string heroId, bool fromClanMembers)
    {
        if (IsOpen)
            return;
        var host = ScreenManager.TopScreen;
        if (host == null)
            return;
        FamilyTreeLayer? layer = null;
        try
        {
            var tree = _service.Build(heroId);
            if (tree == null)
                return;
            layer = new FamilyTreeLayer(host, fromClanMembers, Closed);
            _layer = layer;
            layer.Show(tree);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[FamilyTree] opening the popup for {heroId} threw: {ex}");
            _layer = null;
            layer?.Close();
        }
    }

    private void Closed(FamilyTreeLayer layer)
    {
        if (_layer == layer)
            _layer = null;
    }
}
