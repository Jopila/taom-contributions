using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.Localization;

namespace TAOM.Features.FieldCamp.Activities.Hooks;

/// <summary>
/// The player-facing words of camp work (#860): the camp option, why a work is greyed, the wait and the lines a
/// finished work prints. Every string is a <c>{=taom_camp_*}</c> key with its English default. A number reaches a template only
/// as a text variable.
/// </summary>
internal static class CampWorkTexts
{
    internal static TextObject OptionWork(bool resume) => resume
        ? new TextObject("{=taom_camp_opt_resume_work}Return to the work")
        : new TextObject("{=taom_camp_opt_work}Set the men to work");

    internal static TextObject WaitBody(CampActivity activity)
    {
        switch (activity)
        {
            case CampActivity.Timber: return new TextObject("{=taom_camp_wait_timber}The men are felling timber for the camp.");
            case CampActivity.Fish: return new TextObject("{=taom_camp_wait_fish}The men are fishing the waters near the camp.");
            default: return new TextObject("{=taom_camp_wait_hunt}The men are out hunting game for the camp.");
        }
    }

    /// <summary>The tooltip of a greyed option, or null for no block.</summary>
    internal static TextObject? BlockReason(CampWorkBlock block, CampActivity activity = CampActivity.None, int toolsNeeded = 0)
    {
        switch (block)
        {
            case CampWorkBlock.SharedCampaign:
                return new TextObject("{=taom_camp_block_coop}Camp work is not available in a shared campaign.");
            case CampWorkBlock.Busy:
                return new TextObject("{=taom_camp_block_busy}The men cannot be set to work during an encounter.");
            case CampWorkBlock.CampNotReady:
                return new TextObject("{=taom_camp_block_camp}Raise a field or fortified camp first.");
            case CampWorkBlock.ScoutRideOut:
                return new TextObject("{=taom_camp_block_scout}The men cannot work while a scout is out.");
            case CampWorkBlock.AlreadyWorking:
                return new TextObject("{=taom_camp_block_working}The men are already at work.");
            case CampWorkBlock.FeatureDisabled:
                return new TextObject("{=taom_camp_block_off}Camp work is off while field camps are disabled in the mod options.");
            case CampWorkBlock.NoMen:
                return new TextObject("{=taom_camp_block_nomen}No man in the party is fit to work.");
            case CampWorkBlock.NoTools:
                return new TextObject("{=taom_camp_block_tools}This many men need {N} tools, and the party holds too few.")
                    .SetTextVariable("N", toolsNeeded);
            case CampWorkBlock.NothingHere:
                return NothingHere(activity);
            default:
                return null;
        }
    }

    private static TextObject NothingHere(CampActivity activity)
    {
        switch (activity)
        {
            case CampActivity.Timber: return new TextObject("{=taom_camp_nothing_timber}Only a forest has timber worth felling.");
            case CampActivity.Fish: return new TextObject("{=taom_camp_nothing_fish}There is no water close enough to fish.");
            default: return new TextObject("{=taom_camp_nothing_hunt}There is no game to hunt on this ground.");
        }
    }

    /// <summary>The message lines of a finished work, in order: what came in, the morale the work cost, the worn tools.</summary>
    internal static IReadOnlyList<string> ReportLines(CampWorkReport report)
    {
        var lines = new List<string>();
        lines.Add(Goods(report));
        if (report.MoraleCost > 0f)
            lines.Add(new TextObject("{=taom_camp_done_morale}The long work wears on the men (morale -{N}).")
                .SetTextVariable("N", report.MoraleCost.ToString("0.#", CultureInfo.InvariantCulture)).ToString());
        if (report.ToolsUsed == 1)
            lines.Add(new TextObject("{=taom_camp_done_tools_one}A tool is worn out.").ToString());
        else if (report.ToolsUsed > 1)
            lines.Add(new TextObject("{=taom_camp_done_tools_many}{N} tools are worn out.").SetTextVariable("N", report.ToolsUsed).ToString());
        return lines;
    }

    private static string Goods(CampWorkReport report)
    {
        if (report.Goods <= 0)
            return new TextObject("{=taom_camp_done_none}The men return empty-handed.").ToString();
        TextObject text;
        switch (report.Activity)
        {
            case CampActivity.Timber:
                text = new TextObject("{=taom_camp_done_timber}The woodcutters return with {N} hardwood.");
                break;
            case CampActivity.Fish:
                text = new TextObject("{=taom_camp_done_fish}The fishers return with {N} fish.");
                break;
            default:
                text = new TextObject("{=taom_camp_done_hunt}The hunters return with {N} meat.");
                break;
        }

        return text.SetTextVariable("N", report.Goods).ToString();
    }

    internal static string Injured(int men) => men == 1
        ? new TextObject("{=taom_camp_injured_one}A man was hurt at the work.").ToString()
        : new TextObject("{=taom_camp_injured_many}{N} men were hurt at the work.").SetTextVariable("N", men).ToString();

    internal static string SharedCampaignEnded() =>
        new TextObject("{=taom_camp_ended_coop}Camp work is not available in a shared campaign. The men stand down.").ToString();

    internal static string Stopped() => new TextObject("{=taom_camp_stopped}The men stand down. The work is abandoned.").ToString();
}
