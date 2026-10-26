using TaleWorlds.Localization;

namespace TAOM.Features.NotableServices.Hooks;

/// <summary>
/// The dialog texts of Notable Services that carry a number: each is its own TextObject with its own variables
/// (docs/features/notable-services.md), set as the whole text of one line, so no number rides in a shared global. The
/// other lines are literals in <see cref="NotableServicesDialogLines"/>, because the engine takes them as plain strings.
/// </summary>
internal static class NotableServicesTexts
{
    private const string CoinIcon = "<img src=\"General\\Icons\\Coin@2x\" extend=\"8\">";

    internal static TextObject IntroduceLine(int count, int price) =>
        new TextObject("{=taom_ns_introduce}Introduce me to the other {?IS_PLURAL}notables{?}notable{\\?} here ({PRICE}{GOLD_ICON}).")
            .SetTextVariable("IS_PLURAL", count > 1 ? 1 : 0).SetTextVariable("PRICE", price).SetTextVariable("GOLD_ICON", CoinIcon);

    internal static TextObject RevealLine(int price) =>
        new TextObject("{=taom_ns_reveal}Point me to the holds near here ({PRICE}{GOLD_ICON}).")
            .SetTextVariable("PRICE", price).SetTextVariable("GOLD_ICON", CoinIcon);

    /// <summary>Vanilla's own "You don't have enough gold." by key, never copied.</summary>
    internal static TextObject NotEnoughGold() => new("{=d0kbtGYn}You don't have enough gold.");
}
