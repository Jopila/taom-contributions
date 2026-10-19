using TaleWorlds.Localization;

namespace TAOM.Features.HiddenHosts;

/// <summary>The player-facing nameplate text of Hidden Hosts (#846). The popup's text lives with its adapter.</summary>
internal static class HiddenHostsTexts
{
    private static string? _unknownParty;
    private static string? _unknownPartyLanguage;

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
