using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Adapters;

/// <summary>
/// Good Deeds' message-log line. One whole sentence, so each language can inflect it, and worded without a gendered verb or
/// adjective, because the player's gender is not known to the translation (the text uses "your", never a past participle).
/// </summary>
public sealed class GoodDeedsNoticeAdapter : IGoodDeedsNoticeAdapter
{
    public void ShowTournamentHonor(string townName) =>
        InformationManager.DisplayMessage(new InformationMessage(
            new TextObject("{=taom_gooddeeds_tournament}The people of {TOWN} honor your victory.")
                .SetTextVariable("TOWN", townName).ToString(),
            Colors.Green));
}
