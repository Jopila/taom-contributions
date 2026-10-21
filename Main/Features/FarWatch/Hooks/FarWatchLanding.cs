using TaleWorlds.CampaignSystem.GameMenus;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FieldCamp.Hooks;

namespace TAOM.Features.FarWatch.Hooks;

/// <summary>
/// Where the player goes when a ride's wait ends, whatever ended it: the one exit of <see cref="FarWatchWaitMenu"/>. The wait
/// menu has no menu behind it, so leaving it needs a destination (docs/features/far-watch.md "What ends the wait"). The engine's
/// own choice comes first (an interrupt such as a siege menu), then the camp panel when the camp still stands (a wait menu, so
/// time keeps running), then the map. A camp is never inside a settlement (FieldCamp folds it on entry); if the party is in
/// one anyway, the camp panel is no landing and the exit is the map with a warning, as a bare menu exit there strands the party.
/// </summary>
internal sealed class FarWatchLanding
{
    private readonly IGameMenuAdapter _menus;
    private readonly IFarWatchWorldAdapter _world;
    private readonly IModLogger _logger;

    public FarWatchLanding(IGameMenuAdapter menus, IFarWatchWorldAdapter world, IModLogger logger)
    {
        _menus = menus;
        _world = world;
        _logger = logger;
    }

    /// <summary>Ends a wait nothing else will end (a forced exit), then lands.</summary>
    internal void LeaveWaitMenu(GameMenu? menu)
    {
        if (menu != null && menu.IsWaitActive) menu.EndWait();
        Land();
    }

    internal void Land()
    {
        var engineChoice = _menus.GenericStateMenuId;
        if (!string.IsNullOrEmpty(engineChoice) && engineChoice != FarWatchWaitMenu.WaitMenuId && engineChoice != FarWatchPickMenu.PickMenuId
            && _menus.SwitchTo(engineChoice))
            return;

        var camp = _world.ReadCamp();
        if (camp.InSettlement)
        {
            _logger.LogWarning("[FarWatch] the party is inside a settlement after a ride; leaving by the map");
        }
        else if (camp.CampExists && _menus.EnsureMenuOpen(FieldCampCampaignBehavior.CampSubMenuId))
        {
            return;
        }

        _menus.ExitToLast();
    }
}
