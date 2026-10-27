using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Features.NotableServices.Domain;

namespace TAOM.Features.InnRounds.Hooks;

/// <summary>
/// The conversation lines of Inn Rounds, one call each. Three player options sit on vanilla's <c>tavernkeeper_talk</c>: stand a
/// round for the party, stand a round for the house, have the holds nearby pointed out. Each carries its price as the whole line
/// text, built in the condition (<see cref="InnRoundsTexts"/>), under its own variable, and is greyed with a reason when the
/// player is short or the day's rounds are used up. A round goes straight back to <c>tavernkeeper_pretalk</c> (vanilla's "Anything
/// else?"); the reveal answers first, in the keeper's voice, from the outcome a consequence stored. Priority 111 outranks vanilla's
/// 100, as in the other TAOM dialog features.
/// </summary>
internal static class InnRoundsDialogLines
{
    internal const string PartyLineVariable = "TAOM_ROUND_PARTY_LINE";
    internal const string HouseLineVariable = "TAOM_ROUND_HOUSE_LINE";
    internal const string RevealLineVariable = "TAOM_ROUND_REVEAL_LINE";

    private const int Priority = 111;
    private const string Talk = "tavernkeeper_talk";
    private const string Pretalk = "tavernkeeper_pretalk";

    /// <summary>Vanilla's own "Anything else?" by key, never copied: what the keeper says when a reveal could not be done.</summary>
    private const string VanillaAnythingElse = "{=ds294zxi}Anything else?";

    internal static void Register(CampaignGameStarter s, InnRoundsDialogBehavior b)
    {
        s.AddPlayerLine("taom_round_party", Talk, Pretalk, "{=!}{" + PartyLineVariable + "}",
            b.CanParty, b.Party, Priority, (out TextObject why) => b.PartyClickable(out why));
        s.AddPlayerLine("taom_round_house", Talk, Pretalk, "{=!}{" + HouseLineVariable + "}",
            b.CanHouse, b.House, Priority, (out TextObject why) => b.HouseClickable(out why));

        s.AddPlayerLine("taom_round_reveal", Talk, "taom_round_reveal_reply", "{=!}{" + RevealLineVariable + "}",
            b.CanReveal, b.Reveal, Priority, (out TextObject why) => b.RevealClickable(out why));
        Reply(s, "done", RevealOutcome.Revealed, b, Priority + 5,
            "{=taom_round_reveal_done}Listen, then. Travellers have talked of little else but these holds all season, and I will tell you where each one lies.");
        Reply(s, "war", RevealOutcome.AtWar, b, Priority + 4,
            "{=taom_round_reveal_war}Your realm is at war with the one who holds this town. I will say nothing of the roads while the swords are out.");
        Reply(s, "cold", RevealOutcome.Cold, b, Priority + 3,
            "{=taom_round_reveal_cold}The ones who hold this town do not know you well enough yet. I keep my talk of the roads for friends.");
        Reply(s, "nothing", RevealOutcome.NothingNew, b, Priority + 2,
            "{=taom_round_reveal_nothing}I have no more news of the holds near here. You know all that I know.");

        s.AddDialogLine("taom_round_reveal_failed", "taom_round_reveal_reply", Talk, VanillaAnythingElse, null, null, Priority);
    }

    private static void Reply(CampaignGameStarter s, string name, RevealOutcome outcome, InnRoundsDialogBehavior b, int priority, string text) =>
        s.AddDialogLine("taom_round_reveal_" + name, "taom_round_reveal_reply", Pretalk, text, () => b.Answered(outcome), null, priority);
}
