using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Localization;

namespace TAOM.Features.LoremastersLine.UI;

/// <summary>
/// The tooltip lines of the Loremaster's Line (#877). They live in the client assembly because TooltipProperty is in
/// TaleWorlds.Core.ViewModelCollection, which the headless server never loads. Callers guard each call.
/// </summary>
internal static class LoremastersLineTooltips
{
    /// <summary>The band's tooltip: title, the level, and what learning is worth past it.</summary>
    public static List<TooltipProperty> ForFloor(LearningFloor floor)
    {
        return new List<TooltipProperty>
        {
            new TooltipProperty("", new TextObject("{=taom_ll_tt_title}Skill Limit").ToString(), 0,
                onlyShowWhenExtended: false, TooltipProperty.TooltipPropertyFlags.Title),
            Line(new TextObject("{=taom_ll_tt_after}Slowest learning after {LEVEL}").SetTextVariable("LEVEL", floor.Level).ToString()),
            Line(RateLine(floor)),
        };
    }

    /// <summary>The extra line on vanilla's learning limit tooltip.</summary>
    public static TooltipProperty LimitLine() =>
        Line(new TextObject("{=taom_ll_limit_line}Up to the learning limit this skill learns at full speed; past it, learning slows until the red marker.").ToString());

    private static string RateLine(LearningFloor floor)
    {
        var percent = LearningFloorCalculator.ShownPercent(floor);
        if (percent == null)
            return new TextObject("{=taom_ll_tt_capped}Past it, practice no longer improves this skill").ToString();

        var shown = GameTexts.FindText("str_NUMBER_percent").SetTextVariable("NUMBER", percent.Value).ToString();
        return new TextObject("{=taom_ll_tt_rate}Past it, learning keeps {PERCENT} of full speed")
            .SetTextVariable("PERCENT", shown).ToString();
    }

    private static TooltipProperty Line(string text) =>
        new TooltipProperty("", text, 0, onlyShowWhenExtended: false, TooltipProperty.TooltipPropertyFlags.MultiLine);
}
