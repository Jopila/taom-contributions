using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.FromTheAshes.Hooks;

/// <summary>
/// The start options of From the Ashes: "Help raise the village from its ashes" (and its thrall twin) on the looted village's
/// menu, and "Lend your company to the works" on both keep menus. Each callback is a guarded call into
/// <see cref="FromTheAshesService"/>; the wait menu they open is <see cref="FromTheAshesWaitMenu"/>.
/// </summary>
public sealed class FromTheAshesStartOptions
{
    private readonly FromTheAshesService _service;
    private readonly IGameMenuAdapter _menus;
    private readonly IModLogger _logger;
    private bool _faultLogged;

    public FromTheAshesStartOptions(FromTheAshesService service, IGameMenuAdapter menus, IModLogger logger)
    {
        _service = service;
        _menus = menus;
        _logger = logger;
    }

    private static double Now => CampaignTime.Now.ToHours;

    public void AddOptions(CampaignGameStarter starter)
    {
        // AddGameMenuOption resolves a presumed menu, so the order against the vanilla registrations does not matter.
        starter.AddGameMenuOption("village_looted", "taom_ashes_rebuild", "{=taom_ashes_opt_rebuild}Help raise the village from its ashes",
            args => StartCondition(args, AshesJobKind.VillageRebuild, AshesVariant.Free), args => Start(AshesJobKind.VillageRebuild), isLeave: false, index: 0);
        starter.AddGameMenuOption("village_looted", "taom_ashes_thralls", "{=taom_ashes_opt_thralls}Set your thralls to rebuild the village",
            args => StartCondition(args, AshesJobKind.VillageRebuild, AshesVariant.Thralls), args => Start(AshesJobKind.VillageRebuild), isLeave: false, index: 0);
        foreach (var keepMenu in new[] { "town_keep", "castle" })
            starter.AddGameMenuOption(keepMenu, "taom_ashes_project", "{=taom_ashes_opt_project}Lend your company to the works",
                args => StartCondition(args, AshesJobKind.ProjectHelp, AshesVariant.Project), args => Start(AshesJobKind.ProjectHelp), isLeave: false, index: 0);
    }

    private bool StartCondition(MenuCallbackArgs args, AshesJobKind kind, AshesVariant wanted)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Wait;
        try
        {
            var check = _service.CheckStart(kind);
            if (!FromTheAshesTexts.IsShown(check, wanted))
                return false;
            if (check.Allowed)
                return true;
            args.IsEnabled = false;
            args.Tooltip = FromTheAshesTexts.Reason(check);
            return true;
        }
        catch (Exception ex)
        {
            LogOnce("start condition", jobDropped: false, ex);
            return false;
        }
    }

    private void Start(AshesJobKind kind)
    {
        try
        {
            var check = _service.TryStart(kind, Now);
            if (!check.Allowed)
            {
                var reason = FromTheAshesTexts.Reason(check);
                if (reason != null)
                    FromTheAshesTexts.Show(reason, warning: true);
            }
            else if (!_menus.SwitchTo(FromTheAshesWaitMenu.WaitMenuId))
            {
                // The wait never opened, so there is no menu to stop or finish it: drop the job (a project is refunded).
                _service.Abandon();
                FromTheAshesTexts.Report(AshesResult.Of(AshesOutcome.Cancelled));
            }
        }
        catch (Exception ex)
        {
            LogOnce("start", jobDropped: true, ex);
            _service.Abandon();
        }
    }

    /// <summary>Logs the first fault of this class, naming the callback and whether a job was dropped; later ones stay quiet.</summary>
    private void LogOnce(string callback, bool jobDropped, Exception ex)
    {
        if (_faultLogged)
            return;
        _faultLogged = true;
        _logger.LogError($"[FromTheAshes] the {callback} callback failed{(jobDropped ? " and the job was dropped" : "; no job was touched")}: {ex}");
    }
}
