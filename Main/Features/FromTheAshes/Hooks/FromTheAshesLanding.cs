using TaleWorlds.CampaignSystem.GameMenus;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.FromTheAshes.Hooks;

/// <summary>
/// Where the player goes when a job ends, whatever ended it: the one exit of <see cref="FromTheAshesWaitMenu"/>. The wait
/// menu is not a settlement menu, so leaving it needs a destination.
/// </summary>
public sealed class FromTheAshesLanding
{
    private readonly IGameMenuAdapter _menus;
    private readonly IEncounterAdapter _encounter;
    private readonly IModLogger _logger;

    public FromTheAshesLanding(IGameMenuAdapter menus, IEncounterAdapter encounter, IModLogger logger)
    {
        _menus = menus;
        _encounter = encounter;
        _logger = logger;
    }

    /// <summary>Ends a wait nothing else will end (a forced exit), then lands.</summary>
    public void LeaveWaitMenu(GameMenu? menu)
    {
        if (menu != null && menu.IsWaitActive)
            menu.EndWait();
        Land();
    }

    /// <summary>
    /// Where the engine itself would put the player comes first (an interrupt such as a besieged fief's siege menu, as
    /// vanilla's SwitchToMenuIfThereIsAnInterrupt does), then the settlement's own menu, then, still inside a settlement, a
    /// finished encounter that walks the player out. A bare ExitToLast inside a settlement leaves no menu to land on (a
    /// village has no generic-state case), so it runs only when the party is in no settlement.
    /// </summary>
    public void Land()
    {
        var engineChoice = _menus.GenericStateMenuId;
        if (!string.IsNullOrEmpty(engineChoice) && engineChoice != FromTheAshesWaitMenu.WaitMenuId && _menus.SwitchTo(engineChoice))
            return;
        var settlementMenu = _menus.CurrentSettlementMenuId;
        if (!string.IsNullOrEmpty(settlementMenu) && _menus.SwitchTo(settlementMenu))
            return;
        if (!string.IsNullOrEmpty(settlementMenu) || _encounter.IsInsideSettlement)
        {
            if (!_encounter.Finish(forcePlayerOutFromSettlement: true))
                _logger.LogWarning("[FromTheAshes] could not finish the settlement encounter after the job ended");
            return;
        }

        _menus.ExitToLast();
    }
}
