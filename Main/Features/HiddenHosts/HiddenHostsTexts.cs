using System;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TAOM.Features.HiddenHosts;

/// <summary>
/// The player-facing text of Hidden Hosts' nameplates and tooltips (#846): "Unknown party", the age of a remembered count, and
/// the two engine labels the tooltip patches find their rows by. The popups' text lives with their adapter.
/// </summary>
internal static class HiddenHostsTexts
{
    private static string? _unknownParty;
    private static string? _unknownPartyLanguage;

    /// <summary>
    /// The label vanilla gives a settlement tooltip's defenders row (the game text <c>str_map_tooltip_defenders</c>, the same
    /// id the tooltip builder passes). The postfix finds the row by this label, so it matches whatever language the game
    /// runs in. Read once per tooltip build, never per frame, so it is not cached. Main thread only.
    /// </summary>
    internal static string DefendersLabel => GameTexts.FindText("str_map_tooltip_defenders").ToString();

    /// <summary>
    /// The label vanilla gives a party tooltip's troops section (the game text <c>str_map_tooltip_troops</c>, the id the
    /// builder passes). The troop-total wrapper is installed only for the section with this title, never for the prisoners.
    /// Read once per troops section built, not cached. Main thread only.
    /// </summary>
    internal static string TroopsLabel => GameTexts.FindText("str_map_tooltip_troops").ToString();

    /// <summary>
    /// "Counted 5 hours ago" or "Counted 1 day and 3 hours ago" (vanilla's <c>str_days_hours</c>): the age of a remembered count, in the vanilla hours-and-days
    /// text (<c>CampaignUIHelper.GetHoursAndDaysTextFromHourValue</c>, v1.5.5), at least one hour. Called once per row
    /// refresh, every two seconds while a tooltip is open.
    /// </summary>
    internal static string CountedAgo(int hours)
    {
        var text = new TextObject("{=taom_hhost_counted_ago}Counted {TIME} ago");
        text.SetTextVariable("TIME", CampaignUIHelper.GetHoursAndDaysTextFromHourValue(Math.Max(1, hours)));
        return text.ToString();
    }

    /// <summary>
    /// The name of a party at the horizon. <c>TextObject.ToString</c> renders the text every call, so the string is
    /// kept and re-rendered only when the game language changes. Main thread only.
    /// </summary>
    internal static string UnknownParty
    {
        get
        {
            var language = MBTextManager.ActiveTextLanguage;
            if (_unknownParty == null || _unknownPartyLanguage != language)
            {
                _unknownParty = new TextObject("{=taom_hhost_unknown_party}Unknown party").ToString();
                _unknownPartyLanguage = language;
            }
            return _unknownParty;
        }
    }
}
