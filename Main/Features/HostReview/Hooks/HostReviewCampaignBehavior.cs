using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.FieldCamp.Hooks;
using TAOM.Features.HostReview.Domain;

namespace TAOM.Features.HostReview.Hooks;

/// <summary>
/// Campaign entry point for Host Review (#891): the "Review the host" option on the Field Camp sub-menu.
/// Index 4 is reserved for the Refuge options and the Leave option follows, so this takes index 5. It is
/// added from OnAfterSessionLaunched, which fires after every OnSessionLaunched listener, so index 5 lands
/// after Refuge's index 4 and before Leave whatever the listener order. The option is always listed and is
/// greyed with the reason when the review cannot open; every decision is made by
/// <see cref="HostReviewService"/>. Holds no state and saves nothing.
/// </summary>
public sealed class HostReviewCampaignBehavior : CampaignBehaviorBase
{
    public const string OptionId = "taom_fc_review";

    private readonly HostReviewService _service;

    public HostReviewCampaignBehavior(HostReviewService service)
    {
        _service = service;
    }

    public override void RegisterEvents() =>
        CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, AddOption);

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void AddOption(CampaignGameStarter starter)
    {
        starter.AddGameMenuOption(FieldCampCampaignBehavior.CampSubMenuId, OptionId,
            "{=taom_fcamp_opt_review}Review the host",
            Condition,
            Consequence,
            isLeave: false, index: 5);
    }

    private bool Condition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Mission;
        var block = _service.GetBlock();
        if (block != HostReviewBlock.None)
        {
            args.IsEnabled = false;
            args.Tooltip = ReasonText(block);
        }

        return true;
    }

    private void Consequence(MenuCallbackArgs args)
    {
        var block = _service.Launch();
        if (block != HostReviewBlock.None)
            InformationManager.DisplayMessage(new InformationMessage(ReasonText(block).ToString(), Colors.Red));
    }

    private static TextObject ReasonText(HostReviewBlock block)
    {
        switch (block)
        {
            case HostReviewBlock.Enlisted:
                return new TextObject("{=taom_fcamp_review_enlisted}You cannot review the host while serving in another lord's army.");
            case HostReviewBlock.InEncounter:
                return new TextObject("{=taom_fcamp_review_busy}You cannot review the host during an encounter.");
            case HostReviewBlock.CoopSession:
                return new TextObject("{=taom_fcamp_review_coop}You cannot review your troops in a co-op session.");
            case HostReviewBlock.HeroWounded:
                return new TextObject("{=taom_fcamp_review_wounded}You are too badly wounded to review the host.");
            case HostReviewBlock.TooFewHealthy:
                return new TextObject("{=taom_fcamp_review_few}You need at least 2 healthy men to review the host.");
            case HostReviewBlock.OpenFailed:
                return new TextObject("{=taom_fcamp_review_failed}The host could not be mustered here.");
            default:
                return new TextObject("{=taom_fcamp_review_failed}The host could not be mustered here.");
        }
    }
}
