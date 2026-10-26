using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Features.Provender.Models;

namespace TAOM.Features.Provender.Hooks;

/// <summary>
/// The conversation tree of the Provender-master, one line per call, bound to one campaign's <see cref="ProvenderConversationFlow"/>.
/// One entry line sits on <c>hero_main_options</c> and shows only for the hero in the party's Quartermaster role. It leads to
/// the officer's greeting and the hub <see cref="HubToken"/>: the two standing orders (each shown as "start" or "stop" by its
/// state), the morale menu and "That is all." Another order joins the hub by adding its player lines on <see cref="HubToken"/>.
/// A morale line lists whenever its option has figures to show; a closed one is greyed with its reason. The drink has two
/// lines: from the stores, or with the priced purchase of the shortfall, so the player sees any gold before it is spent. Every id is a <c>taom_provender_</c> id, and every exit
/// goes to <c>lord_pretalk</c>.
/// </summary>
internal static class ProvenderDialogLines
{
    internal const string HubToken = "taom_provender_orders";
    internal const string GreetToken = "taom_provender_greet";
    private const string AckToken = "taom_provender_ack";
    private const string MoraleToken = "taom_provender_morale";
    private const string MoraleAskToken = "taom_provender_morale_ask";
    private const string MoraleDoneToken = "taom_provender_morale_done";
    private const int Priority = 100;

    internal static void Register(CampaignGameStarter s, ProvenderConversationFlow f)
    {
        s.AddPlayerLine("taom_provender_entry", "hero_main_options", GreetToken,
            "{=taom_provender_entry}I have orders for you, Provender-master.",
            f.CanTalk, null, Priority);
        s.AddDialogLine("taom_provender_greet", GreetToken, HubToken,
            "{=taom_provender_greet}The stores are in my keeping. What would you have me do?",
            null, null, Priority);

        s.AddPlayerLine("taom_provender_food_on", HubToken, AckToken,
            "{=taom_provender_food_on}Buy food for the host whenever we reach a market.",
            f.FoodOrderIsOff, () => f.SetFoodOrder(true), Priority + 5);
        s.AddPlayerLine("taom_provender_food_off", HubToken, AckToken,
            "{=taom_provender_food_off}Stop buying food at the markets.",
            f.FoodOrderIsOn, () => f.SetFoodOrder(false), Priority + 5);
        s.AddPlayerLine("taom_provender_slaughter_on", HubToken, AckToken,
            "{=taom_provender_slaughter_on}When the stores run low, slaughter some of the herd.",
            f.SlaughterOrderIsOff, () => f.SetSlaughterOrder(true), Priority + 4);
        s.AddPlayerLine("taom_provender_slaughter_off", HubToken, AckToken,
            "{=taom_provender_slaughter_off}Leave the herd alone from now on.",
            f.SlaughterOrderIsOn, () => f.SetSlaughterOrder(false), Priority + 4);
        s.AddPlayerLine("taom_provender_morale", HubToken, MoraleAskToken,
            "{=taom_provender_morale}The host needs its spirits lifted.",
            null, null, Priority + 3);
        ProvenderSaleDialogLines.Register(s, f, Priority + 2);
        s.AddPlayerLine("taom_provender_done", HubToken, "lord_pretalk",
            "{=taom_provender_done}That is all.",
            null, null, 1);

        s.AddDialogLine("taom_provender_ack", AckToken, HubToken,
            "{=taom_provender_ack}It will be done. Anything else?",
            null, null, Priority);

        s.AddDialogLine("taom_provender_morale_ask", MoraleAskToken, MoraleToken,
            "{=taom_provender_morale_ask}Coin or drink, it is your choice. Both cost us something.",
            null, null, Priority);
        // Each line has its own variables: the view resolves every option's text after all the conditions have run.
        MoraleLine(s, f, "taom_provender_pay_small", MoraleOption.PaySmall,
            "{=taom_provender_pay_small}Pay every soldier {PROV_SMALL_EACH} gold ({PROV_SMALL_TOTAL} in all).", Priority + 4, q =>
            {
                MBTextManager.SetTextVariable("PROV_SMALL_EACH", q.PerTroopGold);
                MBTextManager.SetTextVariable("PROV_SMALL_TOTAL", q.Gold);
                return true;
            });
        MoraleLine(s, f, "taom_provender_pay_large", MoraleOption.PayLarge,
            "{=taom_provender_pay_large}Pay every soldier {PROV_LARGE_EACH} gold ({PROV_LARGE_TOTAL} in all), a generous reward.", Priority + 3, q =>
            {
                MBTextManager.SetTextVariable("PROV_LARGE_EACH", q.PerTroopGold);
                MBTextManager.SetTextVariable("PROV_LARGE_TOTAL", q.Gold);
                return true;
            });
        MoraleLine(s, f, "taom_provender_drink", MoraleOption.Drink,
            "{=taom_provender_drink}Share out {PROV_DRINK_COUNT} {PROV_DRINK_ITEM} among the host.", Priority + 2, q =>
            {
                MBTextManager.SetTextVariable("PROV_DRINK_COUNT", q.ItemCount);
                MBTextManager.SetTextVariable("PROV_DRINK_ITEM", f.ItemName(q.ItemId));
                return q.ListsFromStores;
            });
        MoraleLine(s, f, "taom_provender_drink_buy", MoraleOption.Drink,
            "{=taom_provender_drink_buy}Share out {PROV_BUY_COUNT} {PROV_BUY_ITEM} among the host, buying {PROV_BUY_EXTRA} here for {PROV_BUY_GOLD} gold.", Priority + 1, q =>
            {
                MBTextManager.SetTextVariable("PROV_BUY_COUNT", q.ItemCount);
                MBTextManager.SetTextVariable("PROV_BUY_ITEM", f.ItemName(q.ItemId));
                MBTextManager.SetTextVariable("PROV_BUY_EXTRA", q.ToBuy);
                MBTextManager.SetTextVariable("PROV_BUY_GOLD", q.Gold);
                return q.QuotesPurchase;
            });
        s.AddPlayerLine("taom_provender_morale_back", MoraleToken, GreetToken,
            "{=taom_provender_morale_back}Not now.",
            null, null, 1);

        s.AddDialogLine("taom_provender_morale_lifted", MoraleDoneToken, "lord_pretalk",
            "{=taom_provender_morale_lifted}It is done. Listen to them: the mood of the camp is better already.",
            () => f.LastBuyLifted, null, Priority + 1);
        s.AddDialogLine("taom_provender_morale_failed", MoraleDoneToken, "lord_pretalk",
            "{=taom_provender_morale_failed}Something went wrong, and nothing was handed out.",
            null, null, Priority);
    }

    /// <summary>Why a morale line is greyed. Null when it is open.</summary>
    internal static TextObject? BlockExplanation(ProvenderBlock block) => block switch
    {
        ProvenderBlock.None => null,
        ProvenderBlock.NotInPlace => new TextObject("{=taom_provender_block_place}Only in a settlement or a ready field camp."),
        ProvenderBlock.NoTroops => new TextObject("{=taom_provender_block_troops}You have no soldiers to reward."),
        ProvenderBlock.NoGold => new TextObject("{=taom_provender_block_gold}You do not carry enough gold."),
        ProvenderBlock.NoStock => new TextObject("{=taom_provender_block_stock}The stores and the market here do not hold enough."),
        ProvenderBlock.NoMarket => new TextObject("{=taom_provender_block_market}The stores do not hold enough, and there is no market here to buy the rest."),
        ProvenderBlock.NoBuyer => new TextObject("{=taom_provender_block_buyer}No town within reach will buy from us."),
        ProvenderBlock.InEncounter => new TextObject("{=taom_provender_block_encounter}Not while another party stands before us."),
        _ => new TextObject("{=taom_provender_block_closed}This cannot be done now."),
    };

    /// <summary>
    /// One morale line. <paramref name="show"/> fills the line's variables and says whether it lists; a listed line that is
    /// closed is greyed with its reason.
    /// </summary>
    private static void MoraleLine(CampaignGameStarter s, ProvenderConversationFlow f, string id, MoraleOption option, string text,
        int priority, Func<MoraleQuote, bool> show) =>
        s.AddPlayerLine(id, MoraleToken, MoraleDoneToken, text, () => show(f.Quote(option)), () => f.Buy(option), priority,
            (out TextObject explanation) =>
            {
                var block = f.Quote(option).Block;
                explanation = BlockExplanation(block)!;
                return block == ProvenderBlock.None;
            });
}
