using TaleWorlds.CampaignSystem;
using TAOM.Features.LordCourtesies.Domain;

namespace TAOM.Features.LordCourtesies.Hooks;

/// <summary>
/// The conversation lines of Lord Courtesies, one call each. Two player options sit on <c>hero_main_options</c>
/// (offer troops, ask about the lord's holdings); the lord's reply to the second reads the answer the
/// consequence stored. Priority 111 outranks vanilla's 100, as in the other TAOM dialog features.
/// </summary>
internal static class LordCourtesiesDialogLines
{
    private const int Priority = 111;

    internal static void Register(CampaignGameStarter s, LordCourtesiesDialogBehavior b)
    {
        s.AddPlayerLine("taom_lc_gift", "hero_main_options", "taom_lc_gift_reply",
            "{=taom_lc_gift}I have men to spare. Take them into your service, as a gift.",
            b.CanGift, b.Gift, Priority);
        s.AddDialogLine("taom_lc_gift_reply", "taom_lc_gift_reply", "close_window",
            "{=taom_lc_gift_reply}Good men are always welcome. Let me look them over.",
            null, null, Priority);

        s.AddPlayerLine("taom_lc_domain", "hero_main_options", "taom_lc_domain_reply",
            "{=taom_lc_domain}Name the holdings of your house, if you are willing to share them.",
            b.CanAskDomain, b.AskDomain, Priority);
        s.AddDialogLine("taom_lc_domain_revealed", "taom_lc_domain_reply", "lord_pretalk",
            "{=taom_lc_domain_revealed}You have earned a plain answer. Mark these places well.",
            () => b.Answered(DomainOutcome.Revealed), null, Priority + 2);
        s.AddDialogLine("taom_lc_domain_landless", "taom_lc_domain_reply", "lord_pretalk",
            "{=taom_lc_domain_landless}My house holds no lands to name. Why would you ask me that?",
            () => b.Answered(DomainOutcome.Landless), null, Priority + 1);
        s.AddDialogLine("taom_lc_domain_refused", "taom_lc_domain_reply", "lord_pretalk",
            "{=taom_lc_domain_refused}I do not know you well enough to speak of my house's lands.",
            b.AnsweredWithRefusal, null, Priority);
    }
}
