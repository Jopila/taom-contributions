using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core.ViewModelCollection.Information;
using TAOM.Features.ReevesLedger;
using TAOM.Features.ReevesLedger.UI;

namespace TAOM.Tests.Features.ReevesLedger;

/// <summary>
/// The three tooltips of the Reeve's Ledger. An inline <c>{=key}default</c> text renders its default without the game, so
/// these run headless; the settlement tax label is the one vanilla text and is passed in.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ReevesLedgerTextsTests
{
    private const string TaxLabel = "Settlement Tax";

    private static string[] Rows(List<TooltipProperty> lines) =>
        lines.Select(l => l.DefinitionLabel + "|" + l.ValueLabel).ToArray();

    [TestMethod]
    public void Profit_ListsEveryLineThenTheSignedTotal()
    {
        var profit = new ReevesLedgerService(null!, null!).ComposeProfit(new ProfitInputs
        {
            SettlementTax = 120,
            Tariffs = 30,
            Projects = 0,
            Villages = new[] { new VillageIncome("Adara", 15) },
            HasGarrison = true,
            GarrisonWage = 400,
        });

        var rows = Rows(LedgerTooltips.Profit(profit, TaxLabel));

        CollectionAssert.AreEqual(
            new[] { "Profit|", "Settlement Tax|+120", "Tariffs|+30", "Projects|0", "Adara|+15", "Garrison wages|-400", "|", "Total|-235" },
            rows);
    }

    [TestMethod]
    public void Profit_EndsWithARundownSeparator_ThenTheTotalAsTheRundownResult()
    {
        var profit = new ReevesLedgerService(null!, null!).ComposeProfit(new ProfitInputs { SettlementTax = 5 });

        var lines = LedgerTooltips.Profit(profit, TaxLabel);

        Assert.AreEqual((int)TooltipProperty.TooltipPropertyFlags.RundownSeperator, lines[lines.Count - 2].PropertyModifier);
        Assert.AreEqual((int)TooltipProperty.TooltipPropertyFlags.RundownResult, lines[lines.Count - 1].PropertyModifier);
    }

    [TestMethod]
    public void Profit_NoGarrison_HasNoWageRow()
    {
        var profit = new ReevesLedgerService(null!, null!).ComposeProfit(new ProfitInputs { SettlementTax = 5 });

        Assert.IsFalse(Rows(LedgerTooltips.Profit(profit, TaxLabel)).Any(r => r.StartsWith("Garrison wages")));
    }

    [TestMethod]
    public void Stash_ListsStacksThenTheMoreLine()
    {
        var sut = new ReevesLedgerService(null!, null!);
        var entries = Enumerable.Range(1, 17).Select(i => new StashEntry("Item" + i.ToString("00"), i)).ToList();

        var rows = Rows(LedgerTooltips.Stash(sut.SummarizeStash(entries, LedgerTooltips.MaxLines)));

        var max = LedgerTooltips.MaxLines;
        Assert.AreEqual(1 + max + 1, rows.Length, "the title, the capped stacks and the more line");
        Assert.AreEqual("Stash|", rows[0]);
        Assert.AreEqual("Item17|17", rows[1]);
        Assert.AreEqual("|...and " + (17 - max) + " more", rows[max + 1]);
    }

    [TestMethod]
    public void Stash_Empty_ShowsTheMarkerOnly()
    {
        var sut = new ReevesLedgerService(null!, null!);

        var rows = Rows(LedgerTooltips.Stash(sut.SummarizeStash(new List<StashEntry>(), LedgerTooltips.MaxLines)));

        CollectionAssert.AreEqual(new[] { "Stash|", "|The stash is empty." }, rows);
    }

    [TestMethod]
    public void Prisoners_TitleCarriesTheHeadCount_HeroesShowNoCount()
    {
        var sut = new ReevesLedgerService(null!, null!);
        var entries = new[] { new PrisonerEntry("Orc Raider", 12, false), new PrisonerEntry("Lord Zed", 1, true) };

        var rows = Rows(LedgerTooltips.Prisoners(sut.SummarizePrisoners(entries, LedgerTooltips.MaxLines)));

        CollectionAssert.AreEqual(new[] { "Prisoners (13)|", "Lord Zed|", "Orc Raider|12" }, rows);
    }

    [TestMethod]
    public void Prisoners_Empty_ShowsTheMarker()
    {
        var sut = new ReevesLedgerService(null!, null!);

        var rows = Rows(LedgerTooltips.Prisoners(sut.SummarizePrisoners(new List<PrisonerEntry>(), LedgerTooltips.MaxLines)));

        CollectionAssert.AreEqual(new[] { "Prisoners (0)|", "|No prisoners are held here." }, rows);
    }

    [TestMethod]
    public void TheTitleRows_AreTitleFlagged_AndTheNotesAreMultiLine()
    {
        var sut = new ReevesLedgerService(null!, null!);

        var empty = LedgerTooltips.Stash(sut.SummarizeStash(new List<StashEntry>(), LedgerTooltips.MaxLines));

        Assert.AreEqual((int)TooltipProperty.TooltipPropertyFlags.Title, empty[0].PropertyModifier);
        Assert.AreEqual((int)TooltipProperty.TooltipPropertyFlags.MultiLine, empty[1].PropertyModifier);
    }
}
