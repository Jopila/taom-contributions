using System;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Localization;

namespace TAOM.Features.FarWatch.Hooks;

/// <summary>
/// The player-facing words of Far Watch outside the report (#847): the camp option and why it is greyed, the pick, the wait
/// and the one-line messages of a ride that ended early. Every string is a <c>{=taom_fwatch_*}</c> key with its English
/// default; the report is in <see cref="FarWatchReportText"/>. A name reaches a template only through <see cref="Safe"/>:
/// the player may have typed it, and a brace in a variable is read again as markup.
/// </summary>
internal static class FarWatchTexts
{
    /// <summary>
    /// "5 hours", "1 day and 3 hours": vanilla's hours-and-days text. A seam so a test needs no game text (the engine's text needs a
    /// running Game); production never sets it.
    /// </summary>
    internal static readonly Func<int, TextObject> DefaultHoursText = hours => CampaignUIHelper.GetHoursAndDaysTextFromHourValue(hours);

    internal static Func<int, TextObject> HoursText { get; set; } = DefaultHoursText;

    internal static TextObject OptionLabel(ScoutOffer offer)
    {
        if (offer.Status == ScoutOfferStatus.Running)
            return offer.SelfScout
                ? new TextObject("{=taom_fwatch_opt_await_self}Continue the ride")
                : new TextObject("{=taom_fwatch_opt_await}Await your scout");
        return offer.SelfScout
            ? new TextObject("{=taom_fwatch_opt_self}Ride out to scout")
            : new TextObject("{=taom_fwatch_opt_send}Send a scout");
    }

    /// <summary>The tooltip of a greyed option, or null for a status that is not a block.</summary>
    internal static TextObject? BlockReason(ScoutOffer offer)
    {
        if (offer.Status != ScoutOfferStatus.Blocked) return null;
        switch (offer.Block)
        {
            case ScoutBlock.SharedCampaign:
                return new TextObject("{=taom_fwatch_block_coop}Scouting from camp is not available in a shared campaign.");
            case ScoutBlock.Busy:
                return new TextObject("{=taom_fwatch_block_busy}You cannot send a scout during an encounter.");
            case ScoutBlock.CampNotReady:
                return new TextObject("{=taom_fwatch_block_camp}The camp must be raised before a scout rides out.");
            case ScoutBlock.NoScout:
                return new TextObject("{=taom_fwatch_block_noscout}Name a scout for your party, or travel alone to scout yourself.");
            case ScoutBlock.Wounded:
                return new TextObject("{=taom_fwatch_block_wounded}{SCOUT} is too badly wounded to ride.")
                    .SetTextVariable("SCOUT", Safe(offer.ScoutName));
            case ScoutBlock.Resting:
                return new TextObject("{=taom_fwatch_block_rest}{SCOUT} is resting after the last ride. Ready in {TIME}.")
                    .SetTextVariable("SCOUT", Safe(offer.ScoutName))
                    .SetTextVariable("TIME", HoursText(offer.RestHoursLeft < 1 ? 1 : offer.RestHoursLeft));
            default:
                return null;
        }
    }

    internal static string PickBody(string? scout) =>
        new TextObject("{=taom_fwatch_pick_body}Where should {SCOUT} ride?").SetTextVariable("SCOUT", Safe(scout)).ToString();

    internal static string OptionSite(string? settlement, int sector) =>
        new TextObject("{=taom_fwatch_opt_site}Look over {SETTLEMENT} ({DIRECTION})").SetTextVariable("SETTLEMENT", Safe(settlement))
            .SetTextVariable("DIRECTION", Direction(sector)).ToString();

    internal static string OptionRecall(string? scout, bool selfScout) => selfScout
        ? new TextObject("{=taom_fwatch_opt_recall_self}Ride back to camp").ToString()
        : new TextObject("{=taom_fwatch_opt_recall}Call {SCOUT} back").SetTextVariable("SCOUT", Safe(scout)).ToString();

    internal static string WaitBody(ScoutProgress progress) => progress.Kind == ScoutKind.Settlement
        ? new TextObject("{=taom_fwatch_wait_site}{SCOUT} is riding to look over {SETTLEMENT}.").SetTextVariable("SCOUT", Safe(progress.ScoutName))
            .SetTextVariable("SETTLEMENT", Safe(progress.TargetName)).ToString()
        : new TextObject("{=taom_fwatch_wait_around}{SCOUT} is sweeping the land around the camp.").SetTextVariable("SCOUT", Safe(progress.ScoutName)).ToString();

    /// <summary>The one-line message of a ride that ended early.</summary>
    internal static string EndMessage(ScoutEndNotice notice)
    {
        TextObject text;
        switch (notice.Reason)
        {
            case ScoutEndReason.CallBack:
                text = new TextObject("{=taom_fwatch_end_recall}{SCOUT} rides back to camp with nothing to report.");
                break;
            case ScoutEndReason.Battle:
                text = new TextObject("{=taom_fwatch_end_battle}{SCOUT} rides back to camp as trouble finds the party.");
                break;
            case ScoutEndReason.CampStruck:
                text = new TextObject("{=taom_fwatch_end_camp}The camp was struck; {SCOUT} rejoins the party.");
                break;
            case ScoutEndReason.SharedCampaign:
                text = new TextObject("{=taom_fwatch_end_coop}{SCOUT} rejoins the party: scouting from camp is not available in a shared campaign.");
                break;
            default:
                text = new TextObject("{=taom_fwatch_end_lost}{SCOUT} did not come back.");
                break;
        }

        return text.SetTextVariable("SCOUT", Safe(notice.ScoutName)).ToString();
    }

    /// <summary>The compass word of a sector (0 north, 1 north-east, and on clockwise); the neutral north for any other number.</summary>
    internal static string Direction(int sector)
    {
        switch (sector)
        {
            case 1: return new TextObject("{=taom_fwatch_dir_ne}north-east").ToString();
            case 2: return new TextObject("{=taom_fwatch_dir_e}east").ToString();
            case 3: return new TextObject("{=taom_fwatch_dir_se}south-east").ToString();
            case 4: return new TextObject("{=taom_fwatch_dir_s}south").ToString();
            case 5: return new TextObject("{=taom_fwatch_dir_sw}south-west").ToString();
            case 6: return new TextObject("{=taom_fwatch_dir_w}west").ToString();
            case 7: return new TextObject("{=taom_fwatch_dir_nw}north-west").ToString();
            default: return new TextObject("{=taom_fwatch_dir_n}north").ToString();
        }
    }

    /// <summary>The label of what a host is made of, a nominative that agrees with nothing around it.</summary>
    internal static string Kind(ScoutComposition composition)
    {
        switch (composition)
        {
            case ScoutComposition.Foot: return new TextObject("{=taom_fwatch_kind_foot}mostly foot").ToString();
            case ScoutComposition.Archers: return new TextObject("{=taom_fwatch_kind_archers}mostly archers").ToString();
            case ScoutComposition.Horse: return new TextObject("{=taom_fwatch_kind_horse}mostly horse").ToString();
            case ScoutComposition.HorseArchers: return new TextObject("{=taom_fwatch_kind_horsearchers}mostly horse archers").ToString();
            default: return new TextObject("{=taom_fwatch_kind_none}unclear").ToString();
        }
    }

    /// <summary>
    /// A name for a template variable: braces removed (a hero's name may be typed by the player, and a variable value is parsed
    /// again as markup, so a brace or a leading <c>{=key}</c> would be read as one), never null.
    /// </summary>
    internal static string Safe(string? name) => string.IsNullOrEmpty(name) ? string.Empty : name!.Replace("{", string.Empty).Replace("}", string.Empty);
}
