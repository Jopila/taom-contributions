using TaleWorlds.Localization;

namespace TAOM.Features.InnRounds.Hooks;

/// <summary>
/// The dialog texts of Inn Rounds that carry a number: each is its own TextObject with its own variables
/// (docs/features/inn-rounds.md), set as the whole text of one line, so no number rides in a shared global. The reveal line is
/// Notable Services' own (<c>NotableServicesTexts.RevealLine</c>). The other lines are literals in
/// <see cref="InnRoundsDialogLines"/>, because the engine takes them as plain strings.
/// </summary>
internal static class InnRoundsTexts
{
    private const string CoinIcon = "<img src=\"General\\Icons\\Coin@2x\" extend=\"8\">";

    internal static TextObject PartyLine(int price) =>
        new TextObject("{=taom_round_party}Stand a round for my whole company ({PRICE}{GOLD_ICON}).")
            .SetTextVariable("PRICE", price).SetTextVariable("GOLD_ICON", CoinIcon);

    internal static TextObject HouseLine(int price) =>
        new TextObject("{=taom_round_house}A round for everyone in the house, on me ({PRICE}{GOLD_ICON}).")
            .SetTextVariable("PRICE", price).SetTextVariable("GOLD_ICON", CoinIcon);

    /// <summary>The reason both round lines are greyed once the day's rounds are used up.</summary>
    internal static TextObject CapReached() => new("{=taom_round_cap}Enough rounds have been poured for one day.");
}
