using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Features.NotableServices.Domain;

namespace TAOM.Features.NotableServices.Hooks;

/// <summary>
/// The conversation lines of Notable Services, one call each. Three player options sit on <c>hero_main_options</c>: give men
/// to the settlement, be introduced to the others, have the holds nearby pointed out. The two priced lines carry their price
/// as the whole line text, built in the condition (<see cref="NotableServicesTexts"/>), and are greyed with a reason when the
/// player is short. The notable's replies read the answer a consequence stored and every reply but the gift's returns to
/// <c>lord_pretalk</c>. Priority 111 outranks vanilla's 100, as in the other TAOM dialog features.
/// </summary>
internal static class NotableServicesDialogLines
{
    private const int Priority = 111;

    internal static void Register(CampaignGameStarter s, NotableServicesDialogBehavior b)
    {
        s.AddPlayerLine("taom_ns_gift", "hero_main_options", "taom_ns_gift_reply",
            "{=taom_ns_gift}I have men to spare for your people.",
            b.CanGift, b.Gift, Priority);
        s.AddDialogLine("taom_ns_gift_reply", "taom_ns_gift_reply", "close_window",
            "{=taom_ns_gift_reply}That is a generous thought. Bring them forward and let us see them.",
            null, null, Priority);

        s.AddPlayerLine("taom_ns_introduce", "hero_main_options", "taom_ns_introduce_reply", "{=!}{TAOM_NS_INTRODUCE_LINE}",
            b.CanIntroduce, b.Introduce, Priority, (out TextObject why) => b.IntroduceClickable(out why));
        s.AddDialogLine("taom_ns_introduce_done", "taom_ns_introduce_reply", "lord_pretalk",
            "{=taom_ns_introduce_done}Gladly. Come, and I will name you to them.",
            b.Introduced, null, Priority + 1);
        s.AddDialogLine("taom_ns_introduce_failed", "taom_ns_introduce_reply", "lord_pretalk",
            "{=taom_ns_introduce_failed}That cannot be done just now.",
            null, null, Priority);

        s.AddPlayerLine("taom_ns_reveal", "hero_main_options", "taom_ns_reveal_reply", "{=!}{TAOM_NS_REVEAL_LINE}",
            b.CanReveal, b.Reveal, Priority, (out TextObject why) => b.RevealClickable(out why));
        Reply(s, "revealed", RevealOutcome.Revealed, b, Priority + 5,
            "{=taom_ns_reveal_done}Mark these well. You will find them as I say.");
        Reply(s, "war", RevealOutcome.AtWar, b, Priority + 4,
            "{=taom_ns_reveal_war}We are at war with your realm. I will tell you nothing of our roads.");
        Reply(s, "cold", RevealOutcome.Cold, b, Priority + 3,
            "{=taom_ns_reveal_cold}I do not know you well enough to speak of such things.");
        Reply(s, "nothing", RevealOutcome.NothingNew, b, Priority + 2,
            "{=taom_ns_reveal_nothing}I have nothing more to tell you of the holds around here.");
        s.AddDialogLine("taom_ns_reveal_failed", "taom_ns_reveal_reply", "lord_pretalk",
            "{=taom_ns_reveal_failed}That cannot be done just now.",
            null, null, Priority);
    }

    private static void Reply(CampaignGameStarter s, string name, RevealOutcome outcome, NotableServicesDialogBehavior b, int priority, string text) =>
        s.AddDialogLine("taom_ns_reveal_" + name, "taom_ns_reveal_reply", "lord_pretalk", text, () => b.Answered(outcome), null, priority);
}
