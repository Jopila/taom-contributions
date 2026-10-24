using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace TAOM.Features.SettlementServices.Hooks;

/// <summary>
/// The conversation lines of Watch-Silver, one call each. The offer sits on <c>hero_main_options</c>; the governor names the
/// loyalty and the player picks one of up to three amounts, each a line of its own with its own text variable (options built
/// together share global variables, as vanilla's culture options do). The governor's reply reads the outcome the consequence
/// stored, and every reply returns to <c>lord_pretalk</c>, the governor's main options. Priority 111 outranks vanilla's 100,
/// as in the other TAOM dialog features. No line uses a persuasion or an idle action.
/// </summary>
internal static class WatchSilverDialogLines
{
    private const int Priority = 111;

    internal static void Register(CampaignGameStarter s, WatchSilverDialogBehavior b)
    {
        s.AddPlayerLine("taom_wsilver_offer", "hero_main_options", "taom_wsilver_ask",
            "{=taom_wsilver_offer}The watch here could use some silver to keep the peace.",
            b.CanOffer, b.StartOffer, Priority, b.OfferClickable);
        s.AddDialogLine("taom_wsilver_ask", "taom_wsilver_ask", "taom_wsilver_tiers",
            "{=taom_wsilver_ask}Silver in the right hands steadies the watch. Loyalty here stands at {TAOM_WSILVER_LOYALTY}. What will you spend?",
            b.NameLoyalty, null, Priority);

        s.AddPlayerLine("taom_wsilver_tier_0", "taom_wsilver_tiers", "taom_wsilver_result", "{=!}{TAOM_WSILVER_TIER_0}",
            () => b.TierShown(0), () => b.Pay(0), Priority + 2, (out TextObject why) => b.TierClickable(0, out why));
        s.AddPlayerLine("taom_wsilver_tier_1", "taom_wsilver_tiers", "taom_wsilver_result", "{=!}{TAOM_WSILVER_TIER_1}",
            () => b.TierShown(1), () => b.Pay(1), Priority + 1, (out TextObject why) => b.TierClickable(1, out why));
        s.AddPlayerLine("taom_wsilver_tier_2", "taom_wsilver_tiers", "taom_wsilver_result", "{=!}{TAOM_WSILVER_TIER_2}",
            () => b.TierShown(2), () => b.Pay(2), Priority, (out TextObject why) => b.TierClickable(2, out why));
        s.AddPlayerLine("taom_wsilver_never_mind", "taom_wsilver_tiers", "lord_pretalk", "{=D33fIGQe}Never mind.", null, null, Priority - 1);

        s.AddDialogLine("taom_wsilver_done", "taom_wsilver_result", "lord_pretalk",
            "{=taom_wsilver_done}It will be done. The watch is paid, and loyalty here now stands at {LOYALTY}.",
            b.PaidOut, null, Priority + 1);
        s.AddDialogLine("taom_wsilver_refused", "taom_wsilver_result", "lord_pretalk",
            "{=taom_wsilver_refused}That cannot be done now. Speak to me of it another time.",
            null, null, Priority);
    }
}
