using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FieldCamp.Hooks;

namespace TAOM.Features.FarWatch.Hooks;

/// <summary>
/// Where the scout rides (#847): the surroundings, or one of up to eight towns and castles within 50 map units, nearest first,
/// each with its compass direction. A plain menu: time stands while the player chooses and nothing timed has started, so it is
/// not the "menus stop time" trap. The site list is rebuilt at every <c>OnInit</c> (a load into this menu re-inits) and lives on
/// this hook as ids and names for the one open menu; the eight option slots read it through text variables. A choice goes
/// through <see cref="FarWatchService.TryStart"/>, which runs the offer's checks again, then opens the wait menu; if that does
/// not open, the ride is dropped silently, so nothing runs behind a menu the player cannot see.
/// </summary>
internal sealed class FarWatchPickMenu
{
    public const string PickMenuId = "taom_fc_scout_pick";

    private readonly FarWatchService _service;
    private readonly IFarWatchWorldAdapter _world;
    private readonly IGameMenuAdapter _menus;
    private readonly IFarWatchNotifier _notifier;
    private readonly IModLogger _logger;
    private readonly List<ScoutSite> _sites = new();
    private bool _faultLogged;

    public FarWatchPickMenu(FarWatchService service, IFarWatchWorldAdapter world, IGameMenuAdapter menus, IFarWatchNotifier notifier, IModLogger logger)
    {
        _service = service;
        _world = world;
        _menus = menus;
        _notifier = notifier;
        _logger = logger;
    }

    public void AddMenus(CampaignGameStarter starter)
    {
        starter.AddGameMenu(PickMenuId, "{=!}{TAOM_FWATCH_PICK}", new OnInitDelegate(OnInit), GameMenu.MenuOverlayType.None);
        starter.AddGameMenuOption(PickMenuId, "taom_fc_scout_around", "{=taom_fwatch_opt_around}Sweep the surroundings",
            args => StartCondition(args, -1), args => Start(ScoutKind.Surroundings, -1), isLeave: false, index: 0);
        for (int slot = 0; slot < FarWatchMath.MaxSites; slot++)
        {
            int s = slot;
            starter.AddGameMenuOption(PickMenuId, "taom_fc_scout_site_" + s, "{=!}{TAOM_FWATCH_SITE_" + s + "}",
                args => StartCondition(args, s), args => Start(ScoutKind.Settlement, s), isLeave: false, index: 1 + s);
        }

        starter.AddGameMenuOption(PickMenuId, "taom_fc_scout_back", "{=taom_fwatch_opt_back}Back to camp",
            args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; }, args => Back(), isLeave: true, index: 1 + FarWatchMath.MaxSites);
    }

    internal void OnInit(MenuCallbackArgs args)
    {
        try
        {
            _service.PickSites(_sites);
            for (int slot = 0; slot < FarWatchMath.MaxSites; slot++)
            {
                string label = slot < _sites.Count
                    ? FarWatchTexts.OptionSite(_sites[slot].Name, FarWatchMath.CompassSector(_sites[slot].Dx, _sites[slot].Dy))
                    : string.Empty;
                MBTextManager.SetTextVariable("TAOM_FWATCH_SITE_" + slot, label, false);
            }

            var offer = _service.GetOffer(_world.ReadCamp(), _world.NowHours);
            MBTextManager.SetTextVariable("TAOM_FWATCH_PICK", FarWatchTexts.PickBody(offer.ScoutName), false);
        }
        catch (Exception ex)
        {
            Fault("init", ex);
        }
    }

    /// <summary>A slot shows while it holds a site (the surroundings, slot -1, always); it is enabled while the offer is Ready.</summary>
    internal bool StartCondition(MenuCallbackArgs args, int slot)
    {
        try
        {
            if (slot >= _sites.Count) return false;
            args.optionLeaveType = GameMenuOption.LeaveType.Mission;
            var offer = _service.GetOffer(_world.ReadCamp(), _world.NowHours);
            if (offer.Status != ScoutOfferStatus.Ready)
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

    internal void Start(ScoutKind kind, int slot)
    {
        try
        {
            var site = kind == ScoutKind.Settlement && slot >= 0 && slot < _sites.Count ? _sites[slot] : default;
            double now = _world.NowHours;
            var offer = _service.TryStart(kind, site.Id, site.Name, _world.ReadCamp(), now);
            if (offer.Status != ScoutOfferStatus.Ready)
            {
                var reason = FarWatchTexts.BlockReason(offer);
                if (reason != null) _notifier.ShowMessage(reason.ToString());
                return;
            }

            if (_menus.EnsureMenuOpen(FarWatchWaitMenu.WaitMenuId)) return;

            _logger.LogWarning("[FarWatch] the wait menu did not open; the ride is dropped");
            _service.End(now, ScoutEndReason.CallBack);
        }
        catch (Exception ex)
        {
            Fault("start", ex);
        }
    }

    internal void Back() => _menus.EnsureMenuOpen(FieldCampCampaignBehavior.CampSubMenuId);

    private void Fault(string callback, Exception ex)
    {
        if (_faultLogged) return;
        _faultLogged = true;
        _logger.LogError($"[FarWatch] the pick menu's {callback} failed: {ex}");
    }
}
