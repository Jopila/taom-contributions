using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FieldCamp.Hooks;

namespace TAOM.Features.FarWatch.Hooks;

/// <summary>
/// The camp panel's option (#847): "Send a scout", "Ride out to scout" when the player travels alone, and "Await your scout"
/// while a ride runs. It sits on the Field Camp sub-menu at index 5 and is added from <c>OnAfterSessionLaunched</c>: FieldCamp
/// builds the panel in <c>OnSessionLaunched</c> (forage 0 to break 3, Leave appended), Refuge inserts at 4, and listeners of one
/// event run newest first, so this module's listener inserts first and Host Review, listed before it, inserts after at the
/// same index and pushes it down: Refuge 4, Review 5, Scout 6, Leave 7 (pinned by FarWatchWiringTests). The condition reads the
/// service's offer when the menu is built or refreshed, not per frame; it decides nothing itself.
/// </summary>
internal sealed class FarWatchCampOption
{
    public const string OptionId = "taom_fc_scout";

    private readonly FarWatchService _service;
    private readonly IFarWatchWorldAdapter _world;
    private readonly IGameMenuAdapter _menus;
    private readonly IModLogger _logger;
    private bool _faultLogged;

    /// <summary>What the last evaluation of the condition showed, packed: status, block and whole rest hours left (-1 before any).</summary>
    private int _shown = -1;

    public FarWatchCampOption(FarWatchService service, IFarWatchWorldAdapter world, IGameMenuAdapter menus, IModLogger logger)
    {
        _service = service;
        _world = world;
        _menus = menus;
        _logger = logger;
    }

    public void AddOption(CampaignGameStarter starter) =>
        starter.AddGameMenuOption(FieldCampCampaignBehavior.CampSubMenuId, OptionId, "{=!}{TAOM_FWATCH_OPTION}", Condition, Consequence, isLeave: false, index: 5);

    internal bool Condition(MenuCallbackArgs args)
    {
        try
        {
            var offer = _service.GetOffer(_world.ReadCamp(), _world.NowHours);
            _shown = Shown(offer);
            if (offer.Status == ScoutOfferStatus.Hidden) return false;

            args.optionLeaveType = offer.Status == ScoutOfferStatus.Running ? GameMenuOption.LeaveType.Wait : GameMenuOption.LeaveType.Submenu;
            MBTextManager.SetTextVariable("TAOM_FWATCH_OPTION", FarWatchTexts.OptionLabel(offer), false);
            if (offer.Status == ScoutOfferStatus.Blocked)
            {
                args.IsEnabled = false;
                args.Tooltip = FarWatchTexts.BlockReason(offer);
            }

            return true;
        }
        catch (Exception ex)
        {
            Fault("condition", ex);
            return false;
        }
    }

    /// <summary>
    /// A game menu runs an option's condition only when the menu is built or refreshed, so while the camp panel stays open the
    /// option keeps the state it was drawn with: a rest that ends, or a ride that ends or completes, would leave it greyed or
    /// reading "Await" until the player leaves and returns. The hourly tick calls this after the ride is judged; when the panel
    /// is the current menu and the offer is no longer what was drawn, the menu is refreshed in place (<see cref="IGameMenuAdapter.RefreshCurrent"/>
    /// re-runs the conditions without re-entering the menu, which would restart the camp's wait).
    /// </summary>
    internal void RefreshWhenStale()
    {
        try
        {
            if (_menus.CurrentMenuId != FieldCampCampaignBehavior.CampSubMenuId) return;
            if (Shown(_service.GetOffer(_world.ReadCamp(), _world.NowHours)) != _shown)
                _menus.RefreshCurrent();
        }
        catch (Exception ex)
        {
            Fault("refresh", ex);
        }
    }

    private static int Shown(ScoutOffer offer) => ((int)offer.Status * 16 + (int)offer.Block) * 1000 + offer.RestHoursLeft;

    internal void Consequence(MenuCallbackArgs args)
    {
        try
        {
            var running = _service.HasRide;
            if (!_menus.EnsureMenuOpen(running ? FarWatchWaitMenu.WaitMenuId : FarWatchPickMenu.PickMenuId))
                _logger.LogWarning("[FarWatch] the scout menu did not open");
        }
        catch (Exception ex)
        {
            Fault("consequence", ex);
        }
    }

    private void Fault(string callback, Exception ex)
    {
        if (_faultLogged) return;
        _faultLogged = true;
        _logger.LogError($"[FarWatch] the camp option's {callback} failed: {ex}");
    }
}
