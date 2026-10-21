using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Adapters;

/// <summary>
/// Long Memory's message-log lines. One whole sentence each, so every language can inflect it, and worded without a gendered
/// verb or adjective, because the player's gender is not known to the translation (the villagers are the subject of both lines and
/// neither line names the player).
/// </summary>
public sealed class LongMemoryNoticeAdapter : ILongMemoryNoticeAdapter
{
    public void ShowRaid(string villageName) =>
        InformationManager.DisplayMessage(new InformationMessage(
            new TextObject("{=taom_longmemory_raid}The people of {VILLAGE} will long remember the raid.")
                .SetTextVariable("VILLAGE", villageName).ToString(),
            Colors.Red));

    public void ShowCoercion(string villageName) =>
        InformationManager.DisplayMessage(new InformationMessage(
            new TextObject("{=taom_longmemory_coercion}The people of {VILLAGE} will long remember what was taken from them by force.")
                .SetTextVariable("VILLAGE", villageName).ToString(),
            Colors.Red));
}
