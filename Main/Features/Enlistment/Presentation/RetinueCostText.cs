using TaleWorlds.Localization;

namespace TAOM.Features.Enlistment.Presentation;

/// <summary>
/// The one sentence stating what the player's own men cost a day next to what service pays (#839), used by
/// the status board and the commander's oath question. One key, so the two places cannot diverge.
/// </summary>
public static class RetinueCostText
{
    public static TextObject Build(int cost, int pay)
    {
        var line = new TextObject("{=taom_enlist_retinue_cost}Your own men cost {COST}{COIN} a day; service pays {PAY}{COIN}.");
        line.SetTextVariable("COST", cost.ToString());
        line.SetTextVariable("PAY", pay.ToString());
        line.SetTextVariable("COIN", ServiceStatusTextWriter.CoinIcon);
        return line;
    }

    /// <summary>
    /// A dialog condition body: sets the oath question's optional retinue sentence (empty when he keeps no
    /// paid men, so the question reads as before) and returns true, so it can never hide the question.
    /// </summary>
    public static bool SetOathLine((int Cost, int Pay) retinue)
    {
        TextObject sentence = new TextObject("");
        if (retinue.Cost > 0)
        {
            sentence = new TextObject("{=!} {RETINUE_LINE}");
            sentence.SetTextVariable("RETINUE_LINE", Build(retinue.Cost, retinue.Pay));
        }
        MBTextManager.SetTextVariable("TAOM_ENLIST_OATH_RETINUE", sentence);
        return true;
    }
}
