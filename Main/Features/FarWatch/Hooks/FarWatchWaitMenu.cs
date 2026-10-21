using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.FarWatch.Hooks;

/// <summary>
/// The wait menu of a ride (#847). It follows the field camp's wait menu (docs/features/field-camp.md): the condition is the
/// literal <c>true</c> (a false one hides every option, the recall included, #567), progress derives from the stored start, the
/// tick writes it, and the recall is the only isLeave option, so the engine ends the wait before it runs
/// (<c>GameMenu.RunMenuOptionConsequence</c>). When the progress reaches 1 the engine ends the wait and runs the completion.
/// Time runs here, so the AI and the hourly tick do. Every exit lands through <see cref="FarWatchLanding"/>.
/// </summary>
internal sealed class FarWatchWaitMenu
{
    public const string WaitMenuId = "taom_fc_scout_wait";

    private readonly FarWatchService _service;
    private readonly IFarWatchWorldAdapter _world;
    private readonly FarWatchLanding _landing;
    private readonly FarWatchPresenter _presenter;
    private readonly IModLogger _logger;
    private bool _faultLogged;

    public FarWatchWaitMenu(FarWatchService service, IFarWatchWorldAdapter world, FarWatchLanding landing, FarWatchPresenter presenter, IModLogger logger)
    {
        _service = service;
        _world = world;
        _landing = landing;
        _presenter = presenter;
        _logger = logger;
    }

    public void AddMenu(CampaignGameStarter starter)
    {
        starter.AddWaitGameMenu(WaitMenuId, "{=!}{TAOM_FWATCH_WAIT}", OnWaitInit, args => true, OnWaitComplete, OnWaitTick,
            GameMenu.MenuAndOptionType.WaitMenuShowProgressAndHoursOption, GameMenu.MenuOverlayType.None, 0f);
        starter.AddGameMenuOption(WaitMenuId, "taom_fc_scout_recall", "{=!}{TAOM_FWATCH_RECALL}",
            args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; }, args => OnRecall(), isLeave: true, index: 0);
    }

    internal void OnWaitInit(MenuCallbackArgs args) => Guard("wait init", () =>
    {
        var menu = args.MenuContext.GameMenu;
        // A wait pre-empted by an encounter never ended; the engine starts a fresh one only when none is active.
        if (menu.IsWaitActive)
            menu.EndWait();
        var progress = _service.Progress(_world.NowHours);
        if (!progress.Running)
        {
            _landing.Land();
            return;
        }

        menu.SetTargetedWaitingTimeAndInitialProgress((float)progress.TotalHours, progress.Fraction);
        MBTextManager.SetTextVariable("TAOM_FWATCH_WAIT", FarWatchTexts.WaitBody(progress), false);
        MBTextManager.SetTextVariable("TAOM_FWATCH_RECALL", FarWatchTexts.OptionRecall(progress.ScoutName, progress.SelfScout), false);
    });

    // Its own try/catch and no lambda: the tick runs every frame.
    internal void OnWaitTick(MenuCallbackArgs args, CampaignTime dt)
    {
        try
        {
            var progress = _service.Progress(_world.NowHours);
            if (progress.Running)
            {
                args.MenuContext.GameMenu.SetProgressOfWaitingInMenu(progress.Fraction);
                return;
            }

            _landing.LeaveWaitMenu(args.MenuContext.GameMenu);
        }
        catch (Exception ex)
        {
            Fault("wait tick", ex, args.MenuContext?.GameMenu);
        }
    }

    internal void OnWaitComplete(MenuCallbackArgs args) => Guard("wait complete", () =>
    {
        var report = _service.Complete(_world.NowHours);
        _landing.Land();
        _presenter.Report(report);
    });

    internal void OnRecall() => Guard("recall", () =>
    {
        var notice = _service.End(_world.NowHours, ScoutEndReason.CallBack);
        _landing.Land();
        _presenter.End(notice);
    });

    private void Guard(string callback, Action body)
    {
        try
        {
            body();
        }
        catch (Exception ex)
        {
            Fault(callback, ex, null);
        }
    }

    /// <summary>Logs the first fault of this class, drops the ride (silently: the player was not told it started badly) and lands.</summary>
    private void Fault(string callback, Exception ex, GameMenu? waitingMenu)
    {
        if (!_faultLogged)
        {
            _faultLogged = true;
            _logger.LogError($"[FarWatch] the {callback} callback failed and the ride was dropped: {ex}");
        }

        try
        {
            _service.End(_world.NowHours, ScoutEndReason.CallBack);
        }
        catch
        {
            // The store is plain data; nothing more can be done for it here.
        }

        _landing.LeaveWaitMenu(waitingMenu);
    }
}
