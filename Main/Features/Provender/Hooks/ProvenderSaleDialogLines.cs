using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Features.Provender.Models;

namespace TAOM.Features.Provender.Hooks;

/// <summary>
/// The remote-sale lines of the Provender-master (#909), joined to the hub <see cref="ProvenderDialogLines.HubToken"/>. "I have
/// goods I need sold." lists, greyed with its reason when selling is closed, and takes the quote once. The officer answers,
/// then one row per town on offer (up to <see cref="ProvenderSettingsProvider.MaxSaleTownCount"/>), each with its own text
/// variable: the conversation view resolves every option after all the conditions have run. A chosen town ends the talk
/// (<c>close_window</c>), and the sale screen opens once it has ended.
/// </summary>
internal static class ProvenderSaleDialogLines
{
    private const string AskToken = "taom_provender_sell_ask";
    private const string ListToken = "taom_provender_sell_list";
    private const string DoneToken = "taom_provender_sell_done";

    internal static void Register(CampaignGameStarter s, ProvenderConversationFlow f, int priority)
    {
        s.AddPlayerLine("taom_provender_sell", ProvenderDialogLines.HubToken, AskToken,
            "{=taom_provender_sell}I have goods I need sold.",
            null, f.PrepareSale, priority,
            (out TextObject explanation) =>
            {
                var block = f.QuoteSale().Block;
                explanation = ProvenderDialogLines.BlockExplanation(block)!;
                return block == ProvenderBlock.None;
            });
        s.AddDialogLine("taom_provender_sell_ask", AskToken, ListToken,
            "{=taom_provender_sell_ask}I know the markets within reach. The farther the town, the bigger my cut.",
            null, null, priority);

        for (var rank = 0; rank < ProvenderSettingsProvider.MaxSaleTownCount; rank++)
            TownRow(s, f, rank, priority + ProvenderSettingsProvider.MaxSaleTownCount - rank);
        s.AddPlayerLine("taom_provender_sell_back", ListToken, ProvenderDialogLines.GreetToken,
            "{=taom_provender_sell_back}Not now.",
            null, null, 1);

        s.AddDialogLine("taom_provender_sell_go", DoneToken, "close_window",
            "{=taom_provender_sell_go}Show me what you want sold, and I will take it to market.",
            () => f.LastSaleOpened, null, priority + 1);
        s.AddDialogLine("taom_provender_sell_failed", DoneToken, "lord_pretalk",
            "{=taom_provender_sell_failed}That market will not have us just now.",
            null, null, priority);
    }

    private static void TownRow(CampaignGameStarter s, ProvenderConversationFlow f, int rank, int priority)
    {
        var variable = "PROV_SELL_ROW_" + rank;
        s.AddPlayerLine("taom_provender_sell_town_" + rank, ListToken, DoneToken, "{=!}{" + variable + "}",
            () =>
            {
                var offer = f.SaleOfferAt(rank);
                if (offer == null)
                    return false;
                var row = new TextObject("{=taom_provender_sell_row}{TOWN}, for a cut of {FEE} percent.");
                row.SetTextVariable("TOWN", offer.TownName);
                row.SetTextVariable("FEE", offer.FeePercent);
                MBTextManager.SetTextVariable(variable, row);
                return true;
            },
            () => f.Sell(rank), priority);
    }
}
