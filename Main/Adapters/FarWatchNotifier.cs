using TaleWorlds.Library;

namespace TAOM.Adapters;

public sealed class FarWatchNotifier : IFarWatchNotifier
{
    public void ShowReport(string title, string body, string closeText)
    {
        InformationManager.ShowInquiry(
            new InquiryData(title, body, isAffirmativeOptionShown: true, isNegativeOptionShown: false, closeText, string.Empty, () => { }, null),
            pauseGameActiveState: true);
    }

    public void ShowMessage(string text) => InformationManager.DisplayMessage(new InformationMessage(text));
}
