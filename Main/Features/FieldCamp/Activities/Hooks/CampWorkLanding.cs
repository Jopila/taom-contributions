using TaleWorlds.CampaignSystem.GameMenus;
using TAOM.Adapters;

namespace TAOM.Features.FieldCamp.Activities.Hooks;

/// <summary>
/// Where the player goes when camp work's wait ends, whatever ended it: the one exit of <see cref="CampWorkWaitMenu"/>. A wait
/// menu has no menu behind it, so leaving it needs a destination. The engine's own choice comes first (an encounter or a siege
/// menu), else the campaign map. Never the camp panel: the work is timed, and the map is where time runs and the player decides
/// what comes next (docs/features/field-camp.md "Camp work").
/// </summary>
internal sealed class CampWorkLanding
{
    private readonly IGameMenuAdapter _menus;

    public CampWorkLanding(IGameMenuAdapter menus)
    {
        _menus = menus;
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
        if (!string.IsNullOrEmpty(engineChoice) && engineChoice != CampWorkWaitMenu.WaitMenuId && engineChoice != CampWorkMenu.PickMenuId
            && _menus.SwitchTo(engineChoice))
            return;

        _menus.ExitToLast();
    }
}
