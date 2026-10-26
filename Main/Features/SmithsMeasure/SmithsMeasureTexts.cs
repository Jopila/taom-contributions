using TaleWorlds.Localization;

namespace TAOM.Features.SmithsMeasure;

/// <summary>
/// The player text of the Make Tools card (#910), as inline <c>{=taom_sm_*}default</c> literals so the harvester registers them.
/// The card speaks of the goods, never of the player, so no word is gendered.
/// </summary>
internal static class SmithsMeasureTexts
{
    internal static string Label() => new TextObject("{=taom_sm_make_tools}Make Tools").ToString();

    /// <summary>
    /// The hover text: the recipe and its cost, and above it the one thing missing when the button is off. Empty for a
    /// disabled feature, whose card is hidden.
    /// </summary>
    internal static string Hint(MakeToolsQuote quote)
    {
        if (quote.Verdict == MakeToolsVerdict.Disabled)
            return "";

        var recipe = new TextObject("{=taom_sm_make_tools_hint}One Iron and one Hardwood make one Tools. It costs {COST} smithy stamina and earns Smithing experience.")
            .SetTextVariable("COST", quote.StaminaCost)
            .ToString();
        var reason = Reason(quote.Verdict);
        return reason.Length == 0 ? recipe : reason + "\n" + recipe;
    }

    private static string Reason(MakeToolsVerdict verdict) => verdict switch
    {
        MakeToolsVerdict.NeedsIron => new TextObject("{=taom_sm_need_iron}You need one Iron.").ToString(),
        MakeToolsVerdict.NeedsHardwood => new TextObject("{=taom_sm_need_wood}You need one Hardwood.").ToString(),
        MakeToolsVerdict.TooTired => new TextObject("{=PRE5RKpp}You must rest and spend time before you can do this action.").ToString(),
        _ => "",
    };
}
