using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Features.FieldCamp.Activities.Hooks;

/// <summary>
/// The wait menu of camp work (#860). It follows the field camp's wait menu (docs/features/field-camp.md): the condition is the
/// literal <c>true</c> (a false one hides every option, Stop included, #567), progress is the hours this wait's tick has added to
/// the camp's saved record (the engine gives the tick only the time that passed while this wait was active, so time spent in an
/// interrupting encounter or on the map does not count, and a start time would count it), and Stop is the only
/// isLeave option, so the engine ends the wait before it runs (<c>GameMenu.RunMenuOptionConsequence</c>). At progress 1 the engine
/// ends the wait and runs the completion. Every exit lands through <see cref="CampWorkLanding"/>. A callback the engine calls
/// goes through <see cref="ScreenFaultLog"/>; a fault drops the work and lands, and the log's once-per-site latch never gates that.
/// </summary>
internal sealed class CampWorkWaitMenu
{
    public const string WaitMenuId = "taom_fc_work_wait";

    private readonly CampActivityService _service;
    private readonly ICampWorkAdapter _work;
    private readonly CampWorkLanding _landing;
    private readonly ScreenFaultLog _faults;

    public CampWorkWaitMenu(CampActivityService service, ICampWorkAdapter work, CampWorkLanding landing, ScreenFaultLog faults)
    {
        _service = service;
        _work = work;
        _landing = landing;
        _faults = faults;
    }

    public void AddMenu(CampaignGameStarter starter)
    {
        starter.AddWaitGameMenu(WaitMenuId, "{=!}{TAOM_CAMPWORK_WAIT}", OnWaitInit, args => true, OnWaitComplete, OnWaitTick,
            GameMenu.MenuAndOptionType.WaitMenuShowProgressAndHoursOption, GameMenu.MenuOverlayType.None, 0f);
        starter.AddGameMenuOption(WaitMenuId, "taom_fc_work_stop", "{=taom_camp_opt_stop}Stop work",
            args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; }, args => OnStop(), isLeave: true, index: 0);
    }

    internal void OnWaitInit(MenuCallbackArgs args) => Guard("wait init", () =>
    {
        var menu = args.MenuContext.GameMenu;
        // A wait pre-empted by an encounter never ended; the engine starts a fresh one only when none is active.
        if (menu.IsWaitActive)
            menu.EndWait();
        var progress = _service.Current();
        if (!progress.Running)
        {
            _landing.Land();
            return;
        }

        menu.SetTargetedWaitingTimeAndInitialProgress((float)progress.TotalHours, progress.Fraction);
        MBTextManager.SetTextVariable("TAOM_CAMPWORK_WAIT", CampWorkTexts.WaitBody(progress.Activity), false);
    });

    // Its own try/catch and no lambda: the tick runs every frame.
    internal void OnWaitTick(MenuCallbackArgs args, CampaignTime dt)
    {
        try
        {
            var progress = _service.Advance(dt.ToHours);
            if (progress.EndedBySharedCampaign)
                _work.ShowMessage(CampWorkTexts.SharedCampaignEnded());
            if (progress.Injured > 0)
                _work.ShowMessage(CampWorkTexts.Injured(progress.Injured));
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
        var report = _service.Complete();
        _landing.Land();
        if (report == null)
            return;
        foreach (var line in CampWorkTexts.ReportLines(report.Value))
            _work.ShowMessage(line);
    });

    internal void OnStop() => Guard("stop", () =>
    {
        _service.Stop();
        _landing.Land();
        _work.ShowMessage(CampWorkTexts.Stopped());
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

    /// <summary>
    /// Logs the fault (once per callback), then always drops the work and lands: the log's latch decides only whether a line is
    /// written, never whether the player is freed. Each step has its own guard so one failing step cannot skip the next.
    /// </summary>
    private void Fault(string callback, Exception ex, GameMenu? waitingMenu)
    {
        _faults.LogOnce(callback, ex);
        try
        {
            _service.Stop();
        }
        catch (Exception stopFault)
        {
            _faults.LogOnce(callback + " drop", stopFault);
        }

        try
        {
            _landing.LeaveWaitMenu(waitingMenu);
        }
        catch (Exception landFault)
        {
            _faults.LogOnce(callback + " landing", landFault);
        }
    }
}
