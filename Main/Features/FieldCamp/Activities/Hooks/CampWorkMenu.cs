using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FieldCamp.Hooks;

namespace TAOM.Features.FieldCamp.Activities.Hooks;

/// <summary>
/// The camp panel's option and the pick menu of camp work (#860). The option, "Set the men to work" (or "Return to the work" while
/// one is saved), sits on the Field Camp sub-menu at index 5 and is added from <c>OnAfterSessionLaunched</c>, as Far Watch's is: Refuge
/// 4, Review 5, Scout 6, Work 7, Leave 8 (pinned by FarWatchWiringTests). A saved work is resumed through that same option, and only
/// where <see cref="CampActivityService.GlobalBlock()"/> is clear. The pick is a plain menu: time stands while the player
/// chooses and nothing timed has started, so it is not the "menus stop time" trap. A choice goes through
/// <see cref="CampActivityService.TryStart"/>, which runs the offer's checks again, then opens the wait menu; if that does not
/// open, the work is dropped, so nothing runs behind a menu the player cannot see. Conditions read the service when the menu is
/// built or refreshed, not per frame.
/// </summary>
internal sealed class CampWorkMenu
{
    public const string OptionId = "taom_fc_work_open";
    public const string PickMenuId = "taom_fc_work";

    private static readonly (CampActivity Activity, string Id, string Label)[] Choices =
    {
        (CampActivity.Hunt, "taom_fc_work_hunt", "{=taom_camp_opt_hunt}Hunt for game"),
        (CampActivity.Timber, "taom_fc_work_timber", "{=taom_camp_opt_timber}Fell timber"),
        (CampActivity.Fish, "taom_fc_work_fish", "{=taom_camp_opt_fish}Fish the waters"),
    };

    private readonly CampActivityService _service;
    private readonly ICampWorkAdapter _work;
    private readonly IGameMenuAdapter _menus;
    private readonly IModLogger _logger;
    private readonly ScreenFaultLog _faults;

    public CampWorkMenu(CampActivityService service, ICampWorkAdapter work, IGameMenuAdapter menus, IModLogger logger, ScreenFaultLog faults)
    {
        _service = service;
        _work = work;
        _menus = menus;
        _logger = logger;
        _faults = faults;
    }

    public void AddOption(CampaignGameStarter starter) =>
        starter.AddGameMenuOption(FieldCampCampaignBehavior.CampSubMenuId, OptionId, "{=!}{TAOM_CAMPWORK_OPTION}", OptionCondition, OptionConsequence, isLeave: false, index: 5);

    public void AddMenus(CampaignGameStarter starter)
    {
        starter.AddGameMenu(PickMenuId,
            "{=taom_camp_pick_body}What should the men turn their hands to? Each task takes some hours and brings in goods when it is done.",
            null, GameMenu.MenuOverlayType.None);
        for (int slot = 0; slot < Choices.Length; slot++)
        {
            var activity = Choices[slot].Activity;
            starter.AddGameMenuOption(PickMenuId, Choices[slot].Id, Choices[slot].Label,
                args => ChoiceCondition(args, activity), args => Start(activity), isLeave: false, index: slot);
        }

        starter.AddGameMenuOption(PickMenuId, "taom_fc_work_back", "{=taom_camp_opt_back}Back to camp",
            args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
            args => _faults.Run("back", () => _menus.EnsureMenuOpen(FieldCampCampaignBehavior.CampSubMenuId)), isLeave: true, index: Choices.Length);
    }

    internal bool OptionCondition(MenuCallbackArgs args) => _faults.Condition("option condition", () =>
    {
        bool working = _service.HasWork;
        if (!_service.Enabled) return false;

        args.optionLeaveType = working ? GameMenuOption.LeaveType.Wait : GameMenuOption.LeaveType.Submenu;
        MBTextManager.SetTextVariable("TAOM_CAMPWORK_OPTION", CampWorkTexts.OptionWork(working), false);
        // A saved work resumes only when nothing global blocks it: not while the camp is raised again after Fortify, not while a
        // scout ride is out, not in an encounter.
        var block = _service.GlobalBlock();
        if (block != CampWorkBlock.None)
        {
            args.IsEnabled = false;
            args.Tooltip = CampWorkTexts.BlockReason(block);
        }

        return true;
    });

    internal void OptionConsequence(MenuCallbackArgs args) => _faults.Run("option consequence", () =>
    {
        if (!_menus.EnsureMenuOpen(_service.HasWork ? CampWorkWaitMenu.WaitMenuId : PickMenuId))
            _logger.LogWarning("[FieldCamp] the camp work menu did not open");
    });

    /// <summary>A choice shows always; it is enabled while the offer is ready, and a greyed one says why.</summary>
    internal bool ChoiceCondition(MenuCallbackArgs args, CampActivity activity) => _faults.Condition("choice condition", () =>
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Mission;
        var offer = _service.GetOffer(activity);
        if (!offer.Ready)
        {
            args.IsEnabled = false;
            args.Tooltip = CampWorkTexts.BlockReason(offer.Block, activity, offer.ToolsNeeded);
        }

        return true;
    });

    internal void Start(CampActivity activity)
    {
        try
        {
            var offer = _service.TryStart(activity);
            if (!offer.Ready)
            {
                var reason = CampWorkTexts.BlockReason(offer.Block, activity, offer.ToolsNeeded);
                if (reason != null) _work.ShowMessage(reason.ToString());
                return;
            }

            if (_menus.EnsureMenuOpen(CampWorkWaitMenu.WaitMenuId)) return;

            _logger.LogWarning("[FieldCamp] the camp work wait menu did not open; the work is dropped");
            _service.Stop();
        }
        catch (Exception ex)
        {
            _faults.LogOnce("start", ex);
            try
            {
                _service.Stop();
            }
            catch (Exception stopFault)
            {
                _faults.LogOnce("start drop", stopFault);
            }
        }
    }
}
