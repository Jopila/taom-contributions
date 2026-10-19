using TaleWorlds.CampaignSystem;
using TAOM.Features.HardQuestions.Domain;

namespace TAOM.Features.HardQuestions.Hooks;

/// <summary>
/// The conversation tree of Hard Questions, one line per call. Two player options sit on
/// <c>hero_main_options</c> (ask about lands, strip); the lord's refusal leads to a shared choice (bribe,
/// force, withdraw) whose conditions read the topic the dialog chose. Priority 111 outranks vanilla's 100.
/// </summary>
internal static class HardQuestionsDialogLines
{
    private const int Priority = 111;

    internal static void Register(CampaignGameStarter s, HardQuestionsDialogBehavior b)
    {
        AskLines(s, b, HardQuestionTopic.Clan, "clan",
            "{=taom_hq_ask_clan}Tell me where your kin hold their lands.",
            "{=taom_hq_refuse_clan}You ask me to sell my own blood. I will name no hall of my kin.");
        AskLines(s, b, HardQuestionTopic.Kingdom, "kingdom",
            "{=taom_hq_ask_kingdom}Tell me where your realm keeps its strength.",
            "{=taom_hq_refuse_kingdom}You ask me to betray my whole people. I will name no fortress of my realm.");

        s.AddPlayerLine("taom_hq_pay", "taom_hq_choice", "taom_hq_paid",
            "{=taom_hq_pay}Everyone has a price. Name yours: {PRICE} gold.",
            b.PriceLine, b.PayBribe, Priority, b.PriceIsPayable);
        s.AddPlayerLine("taom_hq_force", "taom_hq_choice", "taom_hq_forced",
            "{=taom_hq_force}I am not asking. Speak, or I will make you.",
            null, b.Force, Priority);
        s.AddPlayerLine("taom_hq_back", "taom_hq_choice", "lord_pretalk",
            "{=taom_hq_back}Never mind. Forget I asked.",
            null, null, Priority);

        s.AddDialogLine("taom_hq_paid_calc", "taom_hq_paid", "lord_pretalk",
            "{=taom_hq_paid_calc}Coin is a tongue I speak well. Listen, then.",
            () => b.RepliesAs(BribeReplyKind.Calculating), null, Priority + 2);
        s.AddDialogLine("taom_hq_paid_valor", "taom_hq_paid", "lord_pretalk",
            "{=taom_hq_paid_valor}I take no pride in this, but a captive's pride feeds no one. Listen, then.",
            () => b.RepliesAs(BribeReplyKind.Valor), null, Priority + 1);
        s.AddDialogLine("taom_hq_paid_plain", "taom_hq_paid", "lord_pretalk",
            "{=taom_hq_paid_plain}So be it. I will tell you what you want to know.",
            null, null, Priority);

        s.AddDialogLine("taom_hq_forced", "taom_hq_forced", "close_window",
            "{=taom_hq_forced}Then be damned, and take what you can tear from me.",
            null, null, Priority);

        s.AddPlayerLine("taom_hq_strip", "hero_main_options", "taom_hq_strip_warn",
            "{=taom_hq_strip}Your arms and armour are mine now.",
            b.CanStrip, null, Priority);
        s.AddDialogLine("taom_hq_strip_warn", "taom_hq_strip_warn", "taom_hq_strip_confirm",
            "{=taom_hq_strip_warn}You would leave a lord in rags, before his own people's eyes? Think what it will cost you.",
            null, null, Priority);
        s.AddPlayerLine("taom_hq_strip_yes", "taom_hq_strip_confirm", "taom_hq_strip_done",
            "{=taom_hq_strip_yes}Strip him.",
            null, b.Strip, Priority);
        s.AddPlayerLine("taom_hq_strip_no", "taom_hq_strip_confirm", "lord_pretalk",
            "{=taom_hq_strip_no}Leave him be.",
            null, null, Priority);
        s.AddDialogLine("taom_hq_strip_done", "taom_hq_strip_done", "close_window",
            "{=taom_hq_strip_done}Take it, then. The shame is yours, not mine.",
            null, null, Priority);
    }

    private static void AskLines(CampaignGameStarter s, HardQuestionsDialogBehavior b, HardQuestionTopic topic,
        string name, string askText, string refuseText)
    {
        s.AddPlayerLine("taom_hq_ask_" + name, "hero_main_options", "taom_hq_refuse_" + name, askText,
            () => b.CanAsk(topic), () => b.Choose(topic), Priority);
        s.AddDialogLine("taom_hq_refuse_" + name, "taom_hq_refuse_" + name, "taom_hq_choice", refuseText,
            null, null, Priority);
    }
}
