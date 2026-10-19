using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Features.FromTheAshes.Hooks;

/// <summary>
/// The one wait menu of From the Ashes. It follows the field camp's wait
/// menu (docs/features/field-camp.md): the condition is the literal <c>true</c> (a false one hides every option, Stop
/// included, #567), progress derives from the stored start, the tick writes the progress, the status line when the
/// project's name changes and a project's construction points, and Stop is the only isLeave option, so the engine ends the
/// wait before it runs. Every exit lands through <see cref="FromTheAshesLanding"/>.
/// </summary>
public sealed class FromTheAshesWaitMenu
{
    public const string WaitMenuId = "taom_ashes_wait";

    private readonly FromTheAshesService _service;
    private readonly FromTheAshesLanding _landing;
    private readonly IModLogger _logger;
    private bool _faultLogged;
    private string? _shownProject;

    public FromTheAshesWaitMenu(FromTheAshesService service, FromTheAshesLanding landing, IModLogger logger)
    {
        _service = service;
        _landing = landing;
        _logger = logger;
    }

    private static double Now => CampaignTime.Now.ToHours;

    public void AddMenu(CampaignGameStarter starter)
    {
        starter.AddWaitGameMenu(WaitMenuId, "{=!}{TAOM_ASHES_STATUS}", OnWaitInit, args => true, OnWaitComplete, OnWaitTick,
            GameMenu.MenuAndOptionType.WaitMenuShowProgressAndHoursOption, GameMenu.MenuOverlayType.None, 0f);
        starter.AddGameMenuOption(WaitMenuId, "taom_ashes_stop", "{=taom_ashes_opt_stop}Lay down your tools",
            args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; }, args => OnStop(), isLeave: true, index: 0);
    }

    private void OnWaitInit(MenuCallbackArgs args) => Guard("wait init", () =>
    {
        var menu = args.MenuContext.GameMenu;
        // A wait pre-empted by an encounter never ended; the engine starts a fresh one only when none is active.
        if (menu.IsWaitActive)
            menu.EndWait();
        var result = _service.Advance(Now, finishWhenDue: false);
        if (result.Outcome != AshesOutcome.Running)
        {
            FromTheAshesTexts.Report(result);
            _landing.Land();
            return;
        }

        menu.SetTargetedWaitingTimeAndInitialProgress(result.TotalHours, result.Progress);
        SetStatus(result);
    });

    // Its own try/catch and no lambda: the tick runs every frame.
    private void OnWaitTick(MenuCallbackArgs args, CampaignTime dt)
    {
        try
        {
            var result = _service.Advance(Now, finishWhenDue: false);
            if (result.Outcome == AshesOutcome.Running)
            {
                args.MenuContext.GameMenu.SetProgressOfWaitingInMenu(result.Progress);
                if (!ReferenceEquals(result.ProjectName, _shownProject))
                    SetStatus(result);
                return;
            }

            FromTheAshesTexts.Report(result);
            _landing.LeaveWaitMenu(args.MenuContext.GameMenu);
        }
        catch (Exception ex)
        {
            Fault("wait tick", ex, args.MenuContext?.GameMenu);
        }
    }

    private void OnWaitComplete(MenuCallbackArgs args) => Guard("wait complete", () =>
    {
        FromTheAshesTexts.Report(_service.Complete(Now));
        _landing.Land();
    });

    private void OnStop() => Guard("stop", () =>
    {
        _service.Stop();
        _landing.Land();
    });

    private void SetStatus(AshesResult result)
    {
        _shownProject = result.ProjectName;
        MBTextManager.SetTextVariable("TAOM_ASHES_STATUS", FromTheAshesTexts.Status(result), false);
    }

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
    /// Logs the first fault of this class (the callback, and whether a job was dropped), drops the job and lands. The tick
    /// passes its menu so the wait is ended first; the engine ends it itself for every other callback.
    /// </summary>
    private void Fault(string callback, Exception ex, GameMenu? waitingMenu)
    {
        var jobDropped = _service.HasJob;
        if (!_faultLogged)
        {
            _faultLogged = true;
            _logger.LogError($"[FromTheAshes] the {callback} callback failed{(jobDropped ? " and the job was dropped" : "; no job was running")}: {ex}");
        }

        _service.Abandon();
        _landing.LeaveWaitMenu(waitingMenu);
    }
}
