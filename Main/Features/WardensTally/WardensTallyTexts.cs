using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.Localization;
using TAOM.Core.Validation;

namespace TAOM.Features.WardensTally;

/// <summary>The words of Warden's Tally (docs/features/wardens-tally.md): a view becomes lines of text, and the dungeon menu's intro becomes vanilla's sentence followed by them.</summary>
internal static class WardensTallyTexts
{
    // Vanilla's sentence from PlayerTownVisitCampaignBehavior.town_keep_dungeon_on_init (v1.5.6 :786), under its own key so the engine's translations apply.
    internal const string VanillaPrisonersKey = "gAc8SWDt";
    internal const string VanillaPrisoners = "There {?(PRISONER_COUNT > 1)}are {PRISONER_COUNT} prisoners{?}is 1 prisoner{\\?} here.";

    /// <summary>Vanilla's sentence for the number of prisoner lords (at least one), rebuilt with the engine's key.</summary>
    internal static TextObject VanillaIntro(int lords) =>
        new TextObject("{=" + VanillaPrisonersKey + "}" + VanillaPrisoners).SetTextVariable("PRISONER_COUNT", lords);

    /// <summary>The text of the menu: vanilla's sentence, a line break, then the tally.</summary>
    internal static TextObject Intro(WardensTallyView view) =>
        new TextObject("{=!}{VANILLA_INTRO}{NEWLINE}{TALLY}")
            .SetTextVariable("VANILLA_INTRO", VanillaIntro(view.Lords))
            .SetTextVariable("NEWLINE", "\n")
            .SetTextVariable("TALLY", string.Join("\n", Lines(view).Select(line => line.ToString())));

    /// <summary>The tally's lines in reading order: held, the odds, the boosts, the vetoes.</summary>
    internal static List<TextObject> Lines(WardensTallyView view)
    {
        var lines = new List<TextObject> { Held(view) };

        if (view.Odds != null)
            AddOdds(lines, view.Odds);

        foreach (var boost in view.Boosts)
            lines.Add(new TextObject("{=taom_wt_boost}{NAME}: {CHANCE}% a day with the war's escape boost.")
                .SetTextVariable("NAME", boost.Name).SetTextVariable("CHANCE", Percent(boost.CombinedChance)));

        foreach (var name in view.Vetoed)
            lines.Add(new TextObject("{=taom_wt_veto}{NAME} cannot escape.").SetTextVariable("NAME", name));

        return lines;
    }

    private static TextObject Held(WardensTallyView view)
    {
        if (view.Lords == 1)
            return new TextObject("{=taom_wt_held_lord}Held: {HELD} of {CAPACITY}, 1 of them a lord.")
                .SetTextVariable("HELD", view.Held).SetTextVariable("CAPACITY", view.Capacity);

        return new TextObject("{=taom_wt_held_lords}Held: {HELD} of {CAPACITY}, {LORDS} of them lords.")
            .SetTextVariable("HELD", view.Held).SetTextVariable("CAPACITY", view.Capacity).SetTextVariable("LORDS", view.Lords);
    }

    private static bool HasBreakdown(TallyOdds odds) => odds.Halved || odds.Terms.Count > 0;

    private static void AddOdds(List<TextObject> lines, TallyOdds odds)
    {
        lines.Add(new TextObject("{=taom_wt_odds}Each lord's daily chance to escape: {CHANCE}%")
            .SetTextVariable("CHANCE", Percent(odds.Chance)));

        if (!HasBreakdown(odds))
            return;

        lines.Add(new TextObject("{=taom_wt_base}Base chance: {CHANCE}%").SetTextVariable("CHANCE", Percent(odds.Base)));
        if (odds.Halved)
            lines.Add(new TextObject("{=taom_wt_halved}Held by your clan: halved"));

        foreach (var term in odds.Terms)
            lines.Add(new TextObject("{=taom_wt_term}{DESCRIPTION}: {CHANGE}% of the base chance")
                .SetTextVariable("DESCRIPTION", term.Description).SetTextVariable("CHANGE", SignedPercent(term.Change)));
    }

    /// <summary>A fraction as a percent with one decimal and no sign ("4.0"), because the row holds the "%"; a non-finite value reads as zero.</summary>
    internal static string Percent(float fraction)
    {
        var percent = FiniteFloatValidator.IsFinite(fraction) ? Math.Round(fraction * 100d, 1, MidpointRounding.AwayFromZero) : 0d;
        return percent.ToString("0.0", CultureInfo.InvariantCulture);
    }

    /// <summary>A term's change as a signed percent of the base chance with at most one decimal ("-25", "+10", "-12.5"); the engine adds the terms, so they sum to the total change.</summary>
    internal static string SignedPercent(float change)
    {
        var percent = FiniteFloatValidator.IsFinite(change) ? Math.Round(change * 100d, 1, MidpointRounding.AwayFromZero) : 0d;
        if (percent == 0d)
            return "0";
        var text = percent.ToString("0.#", CultureInfo.InvariantCulture);
        return percent > 0d ? "+" + text : text;
    }
}
