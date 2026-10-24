using TaleWorlds.Localization;

namespace TAOM.Features.BondOfHire.UI;

/// <summary>
/// The player text of the kingdom Clans panel line (#868), as inline <c>{=taom_boh_*}default</c> literals so the harvester
/// registers them. The subject is always the mercenaries, never the player, and no word is gendered, because the translation
/// does not know the player's gender.
/// </summary>
internal static class BondOfHireTexts
{
    /// <summary>The line for a contract: the days left (singular for one), "not known", or nothing for a hidden line.</summary>
    internal static string Term(ContractTerm term) => term.Kind switch
    {
        ContractTermKind.Known => new TextObject("{=taom_boh_term_days}These mercenaries agreed to serve for at least {DAYS} more {?IS_PLURAL}days{?}day{\\?}")
            .SetTextVariable("DAYS", term.Days)
            .SetTextVariable("IS_PLURAL", term.Days == 1 ? 0 : 1)
            .ToString(),
        ContractTermKind.Unknown => new TextObject("{=taom_boh_term_unknown}It is not known how long these mercenaries will serve").ToString(),
        _ => "",
    };
}
