using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Localization;

namespace TAOM.Features.ReevesLedger.UI;

/// <summary>
/// The three tooltips of the Reeve's Ledger as tooltip property lists. Each takes a finished summary from
/// <see cref="ReevesLedgerService"/> and reads nothing from the engine except the localization manager, so the tooltip
/// function the window calls on hover costs one pass over at most a few dozen short lines. Every label is an inline
/// <c>{=key}default</c> text (the Profit, Projects, Total and Stash keys are vanilla's) except the settlement tax label, which
/// the caller passes in because it is a vanilla game text (<c>str_town_management_population_tax</c>) that only resolves
/// inside a running game. The profit list ends like the clan finance tooltips: a rundown separator, then the total.
/// </summary>
internal static class LedgerTooltips
{
    /// <summary>The most item or prisoner lines one tooltip shows before the "and N more" line.</summary>
    internal const int MaxLines = 15;

    internal static List<TooltipProperty> Profit(ProfitBreakdown profit, string settlementTaxLabel)
    {
        var lines = new List<TooltipProperty>(profit.Lines.Count + 3)
        {
            Title(Text("{=L1Q4AysI}Profit")),
        };
        foreach (var line in profit.Lines)
            lines.Add(new TooltipProperty(LineLabel(line, settlementTaxLabel), Signed(line.Amount), 0));
        lines.Add(new TooltipProperty("", "", 0, false, TooltipProperty.TooltipPropertyFlags.RundownSeperator));
        lines.Add(new TooltipProperty(Text("{=kWVbHPtT}Total"), Signed(profit.Total), 0, false, TooltipProperty.TooltipPropertyFlags.RundownResult));
        return lines;
    }

    internal static List<TooltipProperty> Stash(StashSummary summary)
    {
        var lines = new List<TooltipProperty> { Title(Text("{=nZbaYvVx}Stash")) };
        if (summary.IsEmpty)
        {
            lines.Add(Note(Text("{=taom_reeve_stash_empty}The stash is empty.")));
            return lines;
        }

        foreach (var entry in summary.Lines)
            lines.Add(new TooltipProperty(entry.Name, entry.Count.ToString(CultureInfo.InvariantCulture), 0));
        AddMore(lines, summary.MoreCount);
        return lines;
    }

    internal static List<TooltipProperty> Prisoners(PrisonerSummary summary)
    {
        var title = new TextObject("{=taom_reeve_prisoners}Prisoners ({COUNT})").SetTextVariable("COUNT", summary.Total).ToString();
        var lines = new List<TooltipProperty> { Title(title) };
        if (summary.IsEmpty)
        {
            lines.Add(Note(Text("{=taom_reeve_prison_empty}No prisoners are held here.")));
            return lines;
        }

        // A hero is one person with a name; a count beside it would only say 1.
        foreach (var entry in summary.Lines)
            lines.Add(new TooltipProperty(entry.Name, entry.IsHero ? "" : entry.Count.ToString(CultureInfo.InvariantCulture), 0));
        AddMore(lines, summary.MoreCount);
        return lines;
    }

    private static string LineLabel(ProfitLine line, string settlementTaxLabel)
    {
        switch (line.Kind)
        {
            case ProfitLineKind.SettlementTax:
                return settlementTaxLabel;
            case ProfitLineKind.Tariffs:
                return Text("{=taom_reeve_tariffs}Tariffs");
            case ProfitLineKind.Projects:
                return Text("{=LpsoPtOo}Projects");
            case ProfitLineKind.GarrisonWages:
                return Text("{=taom_reeve_garrison_wages}Garrison wages");
            default:
                return line.Name;
        }
    }

    private static void AddMore(List<TooltipProperty> lines, int moreCount)
    {
        if (moreCount > 0)
            lines.Add(Note(new TextObject("{=taom_reeve_more}...and {COUNT} more").SetTextVariable("COUNT", moreCount).ToString()));
    }

    private static string Text(string template) => new TextObject(template).ToString();

    private static string Signed(int amount) =>
        (amount > 0 ? "+" : "") + amount.ToString(CultureInfo.InvariantCulture);

    private static TooltipProperty Title(string text) =>
        new TooltipProperty(text, "", 0, false, TooltipProperty.TooltipPropertyFlags.Title);

    private static TooltipProperty Note(string text) =>
        new TooltipProperty("", text, 0, false, TooltipProperty.TooltipPropertyFlags.MultiLine);
}
