using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Adapters;

public sealed class HiddenHostsHintAdapter : IHiddenHostsHintAdapter
{
    public bool IsMapFree() => MapPopupGate.IsMapFree();

    public void ShowSymbolHint(string maskExample, string horizon)
    {
        var title = new TextObject("{=taom_hhost_hint_title}Hidden Hosts");
        var body = new TextObject("{=taom_hhost_hint_body}Troop counts on the map now blur with distance. A count such as {MASK_EXAMPLE} means you can read only its first digit. {HORIZON} marks a host at the very edge of sight, too far to count. Counts sharpen as you close in, and as your scout's Scouting grows. Hosts of your own realm are easier to count. AI lords always see the true numbers. The toggle is under Map UI in the mod options.");
        body.SetTextVariable("MASK_EXAMPLE", maskExample);
        body.SetTextVariable("HORIZON", horizon);
        InformationManager.ShowInquiry(new InquiryData(title.ToString(), body.ToString(), true, false,
            GameTexts.FindText("str_ok").ToString(), string.Empty, null, null), pauseGameActiveState: true);
    }
}
