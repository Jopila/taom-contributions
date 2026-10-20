using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.ReevesLedger;

/// <summary>
/// Source pins for the engine side of the Reeve's Ledger. The adapter needs a running campaign, so what matters about each
/// engine call is pinned in the comment-stripped source (docs/features/reeves-ledger.md).
/// </summary>
[TestClass]
public class FiefLedgerAdapterPinTests
{
    private static string Ledger() => RepoPaths.ReadSource("Main/Adapters/FiefLedgerAdapter.cs", stripComments: true);

    [TestMethod]
    public void Profit_NeverAppliesWithdrawals_BecauseTrueTakesAFifthOfTheTradeTaxPerCall()
    {
        var src = Ledger();

        Assert.IsFalse(Regex.IsMatch(src, @"applyWithdrawals\s*:\s*true"), "true takes a fifth of the accumulated trade tax out of the town per call (RevenueSmoothenFraction = 5)");
        Assert.AreEqual(2, Regex.Matches(src, @"applyWithdrawals:\s*false").Count, "tariffs and each village are read without withdrawing");
        Assert.IsFalse(src.Contains("CalculateClanGoldChange") || src.Contains("CalculateClanIncome"),
            "the clan-wide calls sum every fief; this window shows one");
    }

    [TestMethod]
    public void Profit_ReadsTheCampaignModelSlot_SoTheTariffFeatsApply()
    {
        var src = Ledger();

        StringAssert.Contains(src, "Campaign.Current.Models.ClanFinanceModel");
        StringAssert.Contains(src, "CalculateTownIncomeFromTariffs(");
        StringAssert.Contains(src, "CalculateTownIncomeFromProjects(");
        StringAssert.Contains(src, "CalculateVillageIncome(");
        StringAssert.Contains(src, "Campaign.Current.Models.SettlementTaxModel");
        Assert.IsFalse(src.Contains("new DefaultClanFinanceModel"), "a private default model would skip TaomClanFinanceModel");
    }

    [TestMethod]
    public void Profit_TruncatesTheWholeNumbersLikeTheClanFinanceScreen()
    {
        var src = Ledger();

        Assert.AreEqual(2, Regex.Matches(src, @"\(int\)Campaign\.Current\.Models\.(SettlementTaxModel|ClanFinanceModel)\.").Count, "tax and tariffs are cast to int as AddSettlementIncome does");
    }

    [TestMethod]
    public void Garrison_IsCountedOnlyWhileTheGarrisonPartyIsActive()
    {
        var src = Ledger();

        StringAssert.Contains(src, "town.GarrisonParty");
        StringAssert.Contains(src, "IsActive");
        StringAssert.Contains(src, ".TotalWage");
    }

    [TestMethod]
    public void Patrol_NullGuardsTheBehaviourBeforeTheEnginesTooltipRuns()
    {
        var src = Ledger();

        // GetTownPatrolTooltip dereferences the behaviour when the town has no patrol party.
        var guard = src.IndexOf("GetCampaignBehavior<IPatrolPartiesCampaignBehavior>() == null", System.StringComparison.Ordinal);
        var call = src.IndexOf("CampaignUIHelper.GetTownPatrolTooltip(", System.StringComparison.Ordinal);
        Assert.IsTrue(guard >= 0 && call > guard, "the behaviour check precedes the engine call");
        StringAssert.Contains(src, "PatrolParty?.MobileParty");
    }

    [TestMethod]
    public void Lists_ReadTheStashAndThePrisonRosterOfTheSettlement()
    {
        var src = Ledger();

        StringAssert.Contains(src, "settlement.Stash");
        StringAssert.Contains(src, "settlement.Party.PrisonRoster");
        StringAssert.Contains(src, "IsHero");
    }
}
