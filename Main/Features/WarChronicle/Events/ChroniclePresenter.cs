using TAOM.Adapters;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.WarChronicle.Events;

/// <summary>
/// Production <see cref="IChroniclePresenter"/>: a plain one-button <c>InformationManager.ShowInquiry</c>
/// (the <c>InquiryData</c> shape TAOM already uses in CareerQuest and FieldCommission), never a scene
/// notification, which NREs for custom races. The map-free test is <see cref="MapPopupGate.IsMapFree"/>.
/// <c>{TARGET}</c> is the target settlement's name and <c>{DAYS}</c> the window length.
/// </summary>
public sealed class ChroniclePresenter : IChroniclePresenter
{
    public bool IsMapFree() => MapPopupGate.IsMapFree();

    public void Show(ChronicleAnnouncement announcement)
    {
        var title = Text(announcement.Title, announcement).ToString();
        var body = announcement.Text == null ? string.Empty : Text(announcement.Text, announcement).ToString();
        var button = new TextObject("{=taom_wotr_continue}Continue").ToString();

        // SingleQueryPopUpVM null-checks the affirmative action: the button only closes the popup.
        InformationManager.ShowInquiry(
            new InquiryData(title, body, true, false, button, null, null, null),
            true,
            false);
    }

    private static TextObject Text(string text, ChronicleAnnouncement announcement)
    {
        // A fresh TextObject per call: SetTextVariable on a shared one would leak into its other users.
        var result = new TextObject(text);
        var target = announcement.TargetSettlementId == null ? null : Settlement.Find(announcement.TargetSettlementId);
        result.SetTextVariable("TARGET", target?.Name ?? TextObject.GetEmpty());
        result.SetTextVariable("DAYS", announcement.Days);
        return result;
    }
}
