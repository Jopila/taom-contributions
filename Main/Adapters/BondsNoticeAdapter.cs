using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Adapters;

/// <summary>Bonds of Battle's message-log lines. One whole sentence per line, so each language can inflect it.</summary>
public sealed class BondsNoticeAdapter : IBondsNoticeAdapter
{
    public void ShowFoughtTogether(string lordName) =>
        Show(new TextObject("{=taom_bonds_together}You fought together with {LORD}.").SetTextVariable("LORD", lordName), Colors.Green);

    public void ShowFoughtAgainst(string lordName) =>
        Show(new TextObject("{=taom_bonds_against}You fought against {LORD}.").SetTextVariable("LORD", lordName), Colors.White);

    public void ShowBanditGratitude(string notableName, string settlementName) =>
        Show(new TextObject("{=taom_bonds_bandits}{NOTABLE} of {SETTLEMENT} thanks you for clearing the bandits.")
            .SetTextVariable("NOTABLE", notableName).SetTextVariable("SETTLEMENT", settlementName), Colors.Green);

    public void ShowHandover(string lordName) =>
        Show(new TextObject("{=taom_bonds_handover}{LORD} thanks you for the troops you handed over.")
            .SetTextVariable("LORD", lordName), Colors.Green);

    private static void Show(TextObject text, Color color) =>
        InformationManager.DisplayMessage(new InformationMessage(text.ToString(), color));
}
