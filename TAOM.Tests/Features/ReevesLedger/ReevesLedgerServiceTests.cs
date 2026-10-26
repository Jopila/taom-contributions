using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CoopInterop;
using TAOM.Features.ReevesLedger;

namespace TAOM.Tests.Features.ReevesLedger;

[TestClass]
public class ReevesLedgerServiceTests
{
    private ICoopSessionProvider _coop = null!;
    private IDedicatedServerProvider _dedicated = null!;
    private ReevesLedgerService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        _dedicated = Substitute.For<IDedicatedServerProvider>();
        _sut = new ReevesLedgerService(_coop, _dedicated);
    }

    private static ProfitInputs Fief(bool garrison = true, int wage = 40) => new()
    {
        SettlementTax = 120,
        Tariffs = 30,
        Projects = 10,
        Villages = new[] { new VillageIncome("Adara", 15), new VillageIncome("Bree", 5) },
        HasGarrison = garrison,
        GarrisonWage = wage,
    };

    // --- ShowsPatrolRow (#992) --------------------------------------------------------------------------------------

    [DataTestMethod]
    [DataRow(true, false, false, false, true)]
    [DataRow(true, false, true, true, true)]
    [DataRow(false, true, true, false, true)]
    [DataRow(false, true, false, true, true)]
    [DataRow(false, true, true, true, true)]
    [DataRow(false, true, false, false, false)]
    [DataRow(false, false, false, false, false)]
    [DataRow(false, false, true, true, false)]
    public void ShowsPatrolRow_ATown_Always_ACastleWithAPatrolOrAModelThatAllowsOne_NothingElse(
        bool isTown, bool isCastle, bool patrolAlive, bool modelAllows, bool expected)
    {
        Assert.AreEqual(expected, _sut.ShowsPatrolRow(isTown, isCastle, patrolAlive, modelAllows));
    }

    // --- ComposeProfit ----------------------------------------------------------------------------------------------

    [TestMethod]
    public void ComposeProfit_SumsTaxTariffsProjectsVillages_MinusWage()
    {
        var profit = _sut.ComposeProfit(Fief());

        Assert.AreEqual(120 + 30 + 10 + 15 + 5 - 40, profit.Total);
        CollectionAssert.AreEqual(
            new[] { ProfitLineKind.SettlementTax, ProfitLineKind.Tariffs, ProfitLineKind.Projects, ProfitLineKind.Village, ProfitLineKind.Village, ProfitLineKind.GarrisonWages },
            profit.Lines.Select(l => l.Kind).ToArray());
        Assert.AreEqual(-40, profit.Lines.Last().Amount, "the wage is an expense line");
        Assert.AreEqual("Adara", profit.Lines[3].Name);
        Assert.AreEqual(15, profit.Lines[3].Amount);
        Assert.AreEqual(profit.Total, profit.Lines.Sum(l => l.Amount), "the total is the sum of the lines shown");
    }

    [TestMethod]
    public void ComposeProfit_NoGarrison_OmitsWageLine()
    {
        var profit = _sut.ComposeProfit(Fief(garrison: false, wage: 999));

        Assert.IsFalse(profit.Lines.Any(l => l.Kind == ProfitLineKind.GarrisonWages));
        Assert.AreEqual(120 + 30 + 10 + 15 + 5, profit.Total, "a stale wage with no garrison must not be charged");
    }

    [TestMethod]
    public void ComposeProfit_NegativeTotal_NotClamped()
    {
        var inputs = new ProfitInputs { SettlementTax = 20, HasGarrison = true, GarrisonWage = 500 };

        var profit = _sut.ComposeProfit(inputs);

        Assert.AreEqual(-480, profit.Total);
    }

    [TestMethod]
    public void ComposeProfit_SumBeyondIntRange_Saturates()
    {
        var profit = _sut.ComposeProfit(new ProfitInputs { SettlementTax = int.MaxValue, Tariffs = 1 });

        Assert.AreEqual(int.MaxValue, profit.Total);
    }

    [TestMethod]
    public void ComposeProfit_NegativeSumBeyondIntRange_SaturatesAtMinValue()
    {
        var profit = _sut.ComposeProfit(new ProfitInputs { SettlementTax = int.MinValue, Tariffs = -1 });

        Assert.AreEqual(int.MinValue, profit.Total);
    }

    [TestMethod]
    public void ComposeProfit_NoVillages_ListsTheThreeFixedLines()
    {
        var profit = _sut.ComposeProfit(new ProfitInputs { SettlementTax = 1, Tariffs = 2, Projects = 3 });

        Assert.AreEqual(3, profit.Lines.Count);
        Assert.AreEqual(6, profit.Total);
    }

    // --- PlanReserveStep --------------------------------------------------------------------------------------------

    [TestMethod]
    public void PlanReserveStep_Add_CapsAt10000()
    {
        var step = _sut.PlanReserveStep(2500, 50000, ReserveDirection.Add);

        Assert.AreEqual(10000, step.Amount);
        Assert.AreEqual(12500, step.Target);
        Assert.IsTrue(step.HasStep);
    }

    [TestMethod]
    public void PlanReserveStep_Add_CapsAtGold()
    {
        var step = _sut.PlanReserveStep(0, 640, ReserveDirection.Add);

        Assert.AreEqual(640, step.Amount);
        Assert.AreEqual(640, step.Target);
    }

    [DataTestMethod]
    [DataRow(-5)]
    [DataRow(0)]
    public void PlanReserveStep_Add_NegativeGold_NoStep(int gold)
    {
        var step = _sut.PlanReserveStep(300, gold, ReserveDirection.Add);

        Assert.IsFalse(step.HasStep);
        Assert.AreEqual(0, step.Amount);
        Assert.AreEqual(300, step.Target, "no step leaves the reserve where it is");
    }

    [TestMethod]
    public void PlanReserveStep_Add_NearIntMax_NoOverflow()
    {
        var reserve = int.MaxValue - 100;

        var step = _sut.PlanReserveStep(reserve, 50000, ReserveDirection.Add);

        Assert.AreEqual(100, step.Amount, "the reserve cannot pass int.MaxValue");
        Assert.AreEqual(int.MaxValue, step.Target);
        Assert.IsFalse(_sut.PlanReserveStep(int.MaxValue, 50000, ReserveDirection.Add).HasStep);
    }

    [TestMethod]
    public void PlanReserveStep_Withdraw_CapsAtReserve()
    {
        var small = _sut.PlanReserveStep(250, 0, ReserveDirection.Withdraw);
        var large = _sut.PlanReserveStep(27350, 0, ReserveDirection.Withdraw);

        Assert.AreEqual(250, small.Amount);
        Assert.AreEqual(0, small.Target);
        Assert.AreEqual(10000, large.Amount);
        Assert.AreEqual(17350, large.Target, "a reserve that is not a multiple of 100 keeps its remainder");
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void PlanReserveStep_Withdraw_EmptyReserve_NoStep(int reserve)
    {
        var step = _sut.PlanReserveStep(reserve, 99999, ReserveDirection.Withdraw);

        Assert.IsFalse(step.HasStep);
        Assert.AreEqual(0, step.Amount);
        Assert.AreEqual(reserve, step.Target);
    }

    // --- SummarizeStash ---------------------------------------------------------------------------------------------

    [TestMethod]
    public void SummarizeStash_ByCountDesc_CapsWithMoreLine()
    {
        var entries = Enumerable.Range(1, 20).Select(i => new StashEntry("Item" + i.ToString("00"), i)).ToList();

        var summary = _sut.SummarizeStash(entries, 15);

        Assert.AreEqual(15, summary.Lines.Count);
        Assert.AreEqual(5, summary.MoreCount);
        Assert.AreEqual("Item20", summary.Lines[0].Name, "the biggest stack comes first");
        Assert.AreEqual("Item06", summary.Lines[14].Name);
        for (var i = 1; i < summary.Lines.Count; i++)
            Assert.IsTrue(summary.Lines[i - 1].Count >= summary.Lines[i].Count);
        Assert.IsFalse(summary.IsEmpty);
    }

    [TestMethod]
    public void SummarizeStash_EqualCounts_SortByNameSoTheTooltipDoesNotShuffle()
    {
        var entries = new[] { new StashEntry("Wine", 3), new StashEntry("Ale", 3), new StashEntry("Iron", 9) };

        var summary = _sut.SummarizeStash(entries, 15);

        CollectionAssert.AreEqual(new[] { "Iron", "Ale", "Wine" }, summary.Lines.Select(l => l.Name).ToArray());
        Assert.AreEqual(0, summary.MoreCount);
    }

    [TestMethod]
    public void SummarizeStash_Empty_Marker()
    {
        Assert.IsTrue(_sut.SummarizeStash(new List<StashEntry>(), 15).IsEmpty);
        Assert.IsTrue(_sut.SummarizeStash(new[] { new StashEntry("Ghost", 0) }, 15).IsEmpty, "a zero-count entry is not stored goods");
    }

    // --- SummarizePrisoners -----------------------------------------------------------------------------------------

    [TestMethod]
    public void SummarizePrisoners_HeroesFirst_ThenRegulars()
    {
        var entries = new[]
        {
            new PrisonerEntry("Orc Raider", 12, false),
            new PrisonerEntry("Lord Zed", 1, true),
            new PrisonerEntry("Warg Rider", 4, false),
            new PrisonerEntry("Lady Amy", 1, true),
        };

        var summary = _sut.SummarizePrisoners(entries, 15);

        CollectionAssert.AreEqual(new[] { "Lady Amy", "Lord Zed", "Orc Raider", "Warg Rider" }, summary.Lines.Select(l => l.Name).ToArray());
        Assert.AreEqual(18, summary.Total);
        Assert.AreEqual(0, summary.MoreCount);
    }

    [TestMethod]
    public void SummarizePrisoners_OverTheCap_CountsEntriesLeftOffAndKeepsTheTotal()
    {
        var entries = Enumerable.Range(1, 18).Select(i => new PrisonerEntry("Troop" + i.ToString("00"), i, false)).ToList();

        var summary = _sut.SummarizePrisoners(entries, 15);

        Assert.AreEqual(15, summary.Lines.Count);
        Assert.AreEqual(3, summary.MoreCount);
        Assert.AreEqual(Enumerable.Range(1, 18).Sum(), summary.Total);
    }

    [TestMethod]
    public void SummarizePrisoners_DropsZeroCountEntries()
    {
        var entries = new[] { new PrisonerEntry("Ghost", 0, false), new PrisonerEntry("Orc Raider", 2, false) };

        var summary = _sut.SummarizePrisoners(entries, 15);

        CollectionAssert.AreEqual(new[] { "Orc Raider" }, summary.Lines.Select(l => l.Name).ToArray());
        Assert.AreEqual(0, summary.MoreCount);
        Assert.AreEqual(2, summary.Total);
    }

    [TestMethod]
    public void SummarizeStash_DropsZeroCountEntries()
    {
        var summary = _sut.SummarizeStash(new[] { new StashEntry("Empty Sack", 0) }, 15);

        Assert.IsTrue(summary.IsEmpty);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-3)]
    public void Summaries_NonPositiveCap_ShowNoLinesAndCountEverythingAsMore(int cap)
    {
        var stash = _sut.SummarizeStash(new[] { new StashEntry("Grain", 4), new StashEntry("Iron", 2) }, cap);
        var prison = _sut.SummarizePrisoners(new[] { new PrisonerEntry("Orc Raider", 4, false) }, cap);

        Assert.AreEqual(0, stash.Lines.Count);
        Assert.AreEqual(2, stash.MoreCount);
        Assert.AreEqual(0, prison.Lines.Count);
        Assert.AreEqual(1, prison.MoreCount);
    }

    [TestMethod]
    public void SummarizePrisoners_Empty()
    {
        var summary = _sut.SummarizePrisoners(new List<PrisonerEntry>(), 15);

        Assert.IsTrue(summary.IsEmpty);
        Assert.AreEqual(0, summary.Total);
    }

    // --- MayWriteWorld ----------------------------------------------------------------------------------------------

    [TestMethod]
    public void MayWriteWorld_TrueOnlyInPlainSinglePlayer()
    {
        Assert.IsTrue(_sut.MayWriteWorld);
    }

    [DataTestMethod]
    [DataRow(true, false, false, true)]
    [DataRow(false, true, false, true)]
    [DataRow(false, false, true, true)]
    [DataRow(false, false, false, false)]
    public void MayWriteWorld_FalseInEachCoopState(bool sessionActive, bool deferToHost, bool dedicated, bool authority)
    {
        _coop.IsSessionActive.Returns(sessionActive);
        _coop.ShouldDeferToHost.Returns(deferToHost);
        _dedicated.IsDedicatedServer.Returns(dedicated);
        _coop.IsAuthority.Returns(authority);

        Assert.IsFalse(_sut.MayWriteWorld);
    }
}
