using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Features.TokensOfEsteem.Models;

namespace TAOM.Features.TokensOfEsteem.Hooks;

/// <summary>
/// The conversation tree of Tokens of Esteem, one line per call, bound to one campaign's <see cref="GiftConversationFlow"/>.
/// One entry line sits on <c>hero_main_options</c> (companions in the party) and one on <c>lord_talk_speak_diplomacy_2</c>
/// (lords), both "I have a gift for you." and both leading to <c>taom_gift_asked</c>, where the hero answers: a refusal,
/// "you have nothing", or "what do you have for me?" and the list. The list is TAOM's first repeatable player line; every
/// row is the one line <c>{=!}{GIFT_ROW}</c>, whose condition must return true for the row to be listed, and "Never mind."
/// sits last (priority 1). Every id is a <c>taom_gift_</c> id so it cannot meet another feature's lines on the shared tokens.
/// Every exit goes to <c>lord_pretalk</c> ("Is there anything else?"), which companions use as well.
/// </summary>
internal static class GiftDialogLines
{
    private const int Priority = 100;

    internal static void Register(CampaignGameStarter s, GiftConversationFlow f)
    {
        Entry(s, f, "taom_gift_offer_companion", "hero_main_options", GiftEntry.Companion);
        Entry(s, f, "taom_gift_offer_lord", "lord_talk_speak_diplomacy_2", GiftEntry.Lord);

        s.AddDialogLine("taom_gift_refuse_hostile", "taom_gift_asked", "lord_pretalk",
            "{=taom_gift_refuse_hostile}I will not take gifts from you.",
            () => f.GateIs(GiftGateKind.RefusesHostile), null, Priority + 4);
        s.AddDialogLine("taom_gift_refuse_alignment", "taom_gift_asked", "lord_pretalk",
            "{=taom_gift_refuse_alignment}I take nothing from those who serve the Shadow.",
            () => f.GateIs(GiftGateKind.RefusesAlignment), null, Priority + 3);
        s.AddDialogLine("taom_gift_empty", "taom_gift_asked", "lord_pretalk",
            "{=taom_gift_empty}You carry nothing I can accept.",
            f.HasNoOptions, null, Priority + 2);
        s.AddDialogLine("taom_gift_prompt", "taom_gift_asked", "taom_gift_list",
            "{=taom_gift_prompt}What do you have for me?",
            () => f.GateIs(GiftGateKind.Open), null, Priority + 1);
        s.AddDialogLine("taom_gift_deferred", "taom_gift_asked", "lord_pretalk",
            "{=taom_gift_deferred}Another time, perhaps.",
            null, null, Priority);

        s.AddRepeatablePlayerLine("taom_gift_pick", "taom_gift_list", "taom_gift_given", "{=!}{GIFT_ROW}",
            "{=taom_gift_more}Let me look through my things again.", "taom_gift_asked",
            f.ListRow, f.Pick, Priority, (out TextObject explanation) => RowClickable(f, out explanation));
        s.AddPlayerLine("taom_gift_back", "taom_gift_list", "lord_pretalk",
            "{=taom_gift_back}Never mind.",
            null, null, 1);

        s.AddDialogLine("taom_gift_thanks_suited", "taom_gift_given", "lord_pretalk",
            "{=taom_gift_thanks_suited}You know what I like. Thank you.",
            f.GaveSuited, null, Priority + 2);
        s.AddDialogLine("taom_gift_thanks", "taom_gift_given", "lord_pretalk",
            "{=taom_gift_thanks}Thank you. That is kind of you.",
            f.Gave, null, Priority + 1);
        s.AddDialogLine("taom_gift_failed", "taom_gift_given", "lord_pretalk",
            "{=taom_gift_failed}Something is not right. Let us leave it.",
            null, null, Priority);
    }

    /// <summary>Why the entry line is greyed during a cooldown.</summary>
    internal static TextObject CooldownExplanation(int daysLeft)
    {
        var text = new TextObject("{=taom_gift_cooldown}It is too soon for another gift. Days left: {DAYS}.");
        text.SetTextVariable("DAYS", daysLeft);
        return text;
    }

    /// <summary>Why a row is greyed. <see cref="GiftBlock.None"/> has no explanation.</summary>
    internal static TextObject BlockExplanation(GiftBlock block) => block == GiftBlock.CannotAfford
        ? new TextObject("{=taom_gift_cannot_afford}You do not carry that much gold.")
        : new TextObject("{=taom_gift_no_effect}This would not improve your relation.");

    private static void Entry(CampaignGameStarter s, GiftConversationFlow f, string id, string inputToken, GiftEntry entry) =>
        s.AddPlayerLine(id, inputToken, "taom_gift_asked", "{=taom_gift_offer}I have a gift for you.",
            () => f.CanOffer(entry), () => f.Ask(entry), Priority, (out TextObject explanation) => ChooseClickable(f, entry, out explanation));

    private static bool ChooseClickable(GiftConversationFlow f, GiftEntry entry, out TextObject explanation)
    {
        var ok = f.CanChoose(entry, out var daysLeft);
        explanation = daysLeft > 0 ? CooldownExplanation(daysLeft) : null!;
        return ok;
    }

    private static bool RowClickable(GiftConversationFlow f, out TextObject explanation)
    {
        var block = f.RowBlock();
        explanation = block is { } b && b != GiftBlock.None ? BlockExplanation(b) : null!;
        return block == GiftBlock.None;
    }
}
