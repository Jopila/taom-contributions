using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Provender;
using TAOM.Features.Provender.Models;

namespace TAOM.Tests.Features.Provender;

/// <summary>The pure arithmetic of the Provender-master: targets, purchase and slaughter plans, XP and the drink quote.</summary>
[TestClass]
public class ProvenderRulesTests
{
    private static Dictionary<string, int> Held(params (string id, int n)[] rows) => rows.ToDictionary(r => r.id, r => r.n);

    [DataTestMethod]
    [DataRow(100, 5, 20)]
    [DataRow(101, 5, 21)]
    [DataRow(1, 5, 1)]
    [DataRow(7, 1, 7)]
    [DataRow(0, 5, 0)]
    [DataRow(-3, 5, 0)]
    [DataRow(100, 0, 0)]
    [DataRow(100, -2, 0)]
    [DataRow(int.MaxValue, 2, 1073741824)]
    public void FoodTargetPerType_RoundsUp_AndIsZeroWithNoEatersOrNoStockedTypes(int eaters, int types, int expected)
    {
        Assert.AreEqual(expected, ProvenderRules.FoodTargetPerType(eaters, types));
    }

    [TestMethod]
    public void FoodToBuy_ShortfallWithinStockAndGold_IsTheShortfall()
    {
        Assert.AreEqual(16, ProvenderRules.FoodToBuy(new FoodOffer("grain", 10, 50), 4, 20, 1000));
    }

    [TestMethod]
    public void FoodToBuy_StockBelowShortfall_IsTheStock()
    {
        Assert.AreEqual(3, ProvenderRules.FoodToBuy(new FoodOffer("grain", 10, 3), 0, 20, 1000));
    }

    [TestMethod]
    public void FoodToBuy_GoldBelowShortfall_IsWhatTheGoldCoversAtTheFirstPrice()
    {
        Assert.AreEqual(7, ProvenderRules.FoodToBuy(new FoodOffer("grain", 10, 50), 0, 20, 75));
    }

    [DataTestMethod]
    [DataRow(20)]
    [DataRow(90)]
    public void FoodToBuy_HeldAtOrAboveTarget_IsZero(int held)
    {
        Assert.AreEqual(0, ProvenderRules.FoodToBuy(new FoodOffer("grain", 10, 50), held, 20, 1000));
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-5)]
    public void FoodToBuy_NoTargetOrNoGold_IsZero(int value)
    {
        var offer = new FoodOffer("grain", 10, 50);

        Assert.AreEqual(0, ProvenderRules.FoodToBuy(offer, 0, value, 1000));
        Assert.AreEqual(0, ProvenderRules.FoodToBuy(offer, 0, 20, value));
    }

    [DataTestMethod]
    [DataRow(0, 10)]
    [DataRow(10, 0)]
    [DataRow(10, -4)]
    public void FoodToBuy_NoStockOrAPriceThatIsNotPositive_IsZero(int stock, int price)
    {
        Assert.AreEqual(0, ProvenderRules.FoodToBuy(new FoodOffer("grain", price, stock), 0, 10, 1000));
    }

    [TestMethod]
    public void FoodToBuy_ANegativeHeld_IsTreatedAsNone()
    {
        Assert.AreEqual(10, ProvenderRules.FoodToBuy(new FoodOffer("grain", 1, 50), -7, 10, 1000));
    }

    [TestMethod]
    public void FoodToBuy_AHugeGoldAndPrice_DoNotOverflow()
    {
        Assert.AreEqual(1, ProvenderRules.FoodToBuy(new FoodOffer("grain", int.MaxValue, 5), 0, 5, int.MaxValue));
    }

    // ---- slaughter

    private static LivestockStack Sheep(int count, int meat = 2) => new("sheep", count, meat);

    [TestMethod]
    public void PlanSlaughter_FoodAtOrAboveTheLowMark_SlaughtersNothing()
    {
        Assert.AreEqual(0, ProvenderRules.PlanSlaughter(new[] { Sheep(10) }, 3, 1f, 3, 2).Count);
        Assert.AreEqual(0, ProvenderRules.PlanSlaughter(new[] { Sheep(10) }, 9, 1f, 3, 2).Count);
    }

    [TestMethod]
    public void PlanSlaughter_FoodBelowTheLowMark_SlaughtersUntilTheBufferIsBack()
    {
        // 1 food a day, two meat a head: one day of food and three wanted, so one head brings the buffer to three days.
        var plan = ProvenderRules.PlanSlaughter(new[] { Sheep(10) }, 1, 1f, 3, 2);

        CollectionAssert.AreEqual(new[] { new SlaughterLine("sheep", 1) }, plan.ToArray());
    }

    [TestMethod]
    public void PlanSlaughter_NoFoodAtAll_StopsAtTheLowMark_NotAtEveryHead()
    {
        var plan = ProvenderRules.PlanSlaughter(new[] { Sheep(20, 2) }, 0, 1f, 5, 2);

        Assert.AreEqual(3, plan.Single().Heads, "0 days plus 2 a head: 3 heads reach 6 days, 2 heads only 4");
    }

    [TestMethod]
    public void PlanSlaughter_AlwaysKeepsTheConfiguredHeadsOfEachType()
    {
        var plan = ProvenderRules.PlanSlaughter(new[] { Sheep(4) }, 0, 1f, 30, 2);

        Assert.AreEqual(2, plan.Single().Heads, "four sheep, two kept");
    }

    [TestMethod]
    public void PlanSlaughter_AStackAtOrBelowTheKeepLevel_IsLeftAlone()
    {
        var plan = ProvenderRules.PlanSlaughter(new[] { Sheep(2), new LivestockStack("cow", 2, 4) }, 0, 1f, 30, 2);

        Assert.AreEqual(0, plan.Count);
    }

    [TestMethod]
    public void PlanSlaughter_KeepZero_MayTakeTheWholeStack()
    {
        var plan = ProvenderRules.PlanSlaughter(new[] { Sheep(3) }, 0, 1f, 30, 0);

        Assert.AreEqual(3, plan.Single().Heads);
    }

    [TestMethod]
    public void PlanSlaughter_TakesFromTheStackWithTheMostToSpare_OneHeadAtATime()
    {
        var stacks = new[] { new LivestockStack("sheep", 4, 2), new LivestockStack("cow", 8, 4) };

        var plan = ProvenderRules.PlanSlaughter(stacks, 0, 1f, 8, 2);

        Assert.AreEqual(2, plan.Single(l => l.ItemId == "cow").Heads, "cow has 6 to spare and sheep 2: two cows reach 8 days");
        Assert.IsFalse(plan.Any(l => l.ItemId == "sheep"));
    }

    [TestMethod]
    public void PlanSlaughter_StopsOnceTheSparesRunOut()
    {
        var stacks = new[] { new LivestockStack("sheep", 3, 1), new LivestockStack("cow", 4, 1) };

        var plan = ProvenderRules.PlanSlaughter(stacks, 0, 1f, 99, 2);

        Assert.AreEqual(3, plan.Sum(l => l.Heads), "one spare sheep and two spare cows");
    }

    [TestMethod]
    public void PlanSlaughter_AStackThatYieldsNoMeatOrIsEmpty_IsSkipped()
    {
        var stacks = new[] { new LivestockStack("a", 9, 0), new LivestockStack("b", 9, -2), new LivestockStack("c", 0, 3), new LivestockStack("d", -5, 3) };

        Assert.AreEqual(0, ProvenderRules.PlanSlaughter(stacks, 0, 1f, 9, 0).Count);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(0f)]
    [DataRow(-1f)]
    public void PlanSlaughter_AFoodBurnThatIsNotAPositiveNumber_SlaughtersNothing(float perDay)
    {
        Assert.AreEqual(0, ProvenderRules.PlanSlaughter(new[] { Sheep(10) }, 0, perDay, 5, 2).Count);
    }

    [TestMethod]
    public void PlanSlaughter_NegativeFood_IsAHungryParty_AndSlaughters()
    {
        // A negative store is the engine's hunger carry (RemainingFoodPercentage below zero): -1 food at 1 a day, 5 days
        // wanted, two meat a head: three heads bring it to 5.
        Assert.AreEqual(3, ProvenderRules.PlanSlaughter(new[] { Sheep(10) }, -1f, 1f, 5, 2).Single().Heads);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void PlanSlaughter_AFoodStoreThatIsNotFinite_SlaughtersNothing(float food)
    {
        Assert.AreEqual(0, ProvenderRules.PlanSlaughter(new[] { Sheep(10) }, food, 1f, 5, 2).Count);
    }

    [TestMethod]
    public void PlanSlaughter_CountsMeatInWholeUnits_SoFloatErrorTakesNoExtraHead()
    {
        // 20 food at 10 a day, three days wanted: 10 food short. One-meat sheep: exactly 10 heads (adding 0.1 days a head
        // in float would reach 2.9999998 after ten and take an eleventh).
        Assert.AreEqual(10, ProvenderRules.PlanSlaughter(new[] { Sheep(100, 1) }, 20f, 10f, 3, 2).Single().Heads);
        Assert.AreEqual(30, ProvenderRules.PlanSlaughter(new[] { Sheep(100, 1) }, 0f, 10f, 3, 2).Single().Heads);
    }

    [TestMethod]
    public void PlanSlaughter_AWantOverflowingToInfinity_SlaughtersNothing()
    {
        // Finite inputs whose product overflows: lowDays * foodPerDay is +Infinity, and no count of meat could meet it.
        Assert.AreEqual(0, ProvenderRules.PlanSlaughter(new[] { Sheep(10) }, 0f, float.MaxValue, 2, 2).Count);
    }

    [TestMethod]
    public void PlanSlaughter_APartOfADayShort_TakesOnlyTheHeadsNeeded()
    {
        // 29 food at 10 a day is 2.9 days; three wanted: one two-meat head (0.2 days) is enough. Whole engine days would
        // have read 2 and taken five.
        Assert.AreEqual(1, ProvenderRules.PlanSlaughter(new[] { Sheep(10) }, 29f, 10f, 3, 2).Single().Heads);
    }

    // A town's rising prices for six units (totals 53, 112, 177, 252, 341, 454).
    private static readonly int[] RisingBeer = { 53, 59, 65, 75, 89, 113 };

    [DataTestMethod]
    [DataRow(1000, 6)]
    [DataRow(454, 6)]
    [DataRow(453, 5)]
    [DataRow(251, 3)]
    [DataRow(252, 4)]
    [DataRow(52, 0)]
    [DataRow(0, 0)]
    public void AffordableCount_IsTheUnitsInOrderThatTheBudgetCovers(int budget, int expected)
    {
        Assert.AreEqual(expected, ProvenderRules.AffordableCount(RisingBeer, budget));
    }

    [TestMethod]
    public void AffordableCount_NoPrices_IsZero()
    {
        Assert.AreEqual(0, ProvenderRules.AffordableCount(new int[0], 1000));
    }

    [TestMethod]
    public void AffordableCount_ASumPastIntRange_DoesNotOverflow()
    {
        Assert.AreEqual(1, ProvenderRules.AffordableCount(new[] { int.MaxValue, 1 }, int.MaxValue));
    }

    [TestMethod]
    public void TotalCost_IsTheSum_OrMinusOnePastIntRange()
    {
        Assert.AreEqual(454, ProvenderRules.TotalCost(RisingBeer));
        Assert.AreEqual(0, ProvenderRules.TotalCost(new int[0]));
        Assert.AreEqual(-1, ProvenderRules.TotalCost(new[] { int.MaxValue, 1 }));
    }

    [DataTestMethod]
    [DataRow(1000, 100, 3, 700)]
    [DataRow(1000, 100, 0, 1000)]
    [DataRow(200, 100, 3, 0)]
    [DataRow(1000, 0, 3, 1000)]
    [DataRow(1000, -50, 3, 1000)]
    [DataRow(-5, 100, 3, 0)]
    [DataRow(int.MaxValue, int.MaxValue, 30, 0)]
    public void FoodBudget_KeepsTheWageDaysOutOfThePurse(int gold, int dailyWage, int wageDays, int expected)
    {
        Assert.AreEqual(expected, ProvenderRules.FoodBudget(gold, dailyWage, wageDays));
    }

    [TestMethod]
    public void PlanSlaughter_ALowMarkOfZeroOrLess_SlaughtersNothing()
    {
        Assert.AreEqual(0, ProvenderRules.PlanSlaughter(new[] { Sheep(10) }, 0, 1f, 0, 2).Count);
        Assert.AreEqual(0, ProvenderRules.PlanSlaughter(new[] { Sheep(10) }, 0, 1f, -3, 2).Count);
    }

    [TestMethod]
    public void PlanSlaughter_ANegativeKeepLevel_IsTreatedAsZero()
    {
        Assert.AreEqual(3, ProvenderRules.PlanSlaughter(new[] { Sheep(3) }, 0, 1f, 30, -4).Single().Heads);
    }

    // ---- xp and the drink quote

    [DataTestMethod]
    [DataRow(0, 0f)]
    [DataRow(-50, 0f)]
    [DataRow(100, 1f)]
    [DataRow(250, 2.5f)]
    public void TradeXp_IsAHundredthOfTheGoldSpent(int spent, float expected)
    {
        Assert.AreEqual(expected, ProvenderRules.TradeXp(spent));
    }

    [DataTestMethod]
    [DataRow(0, 0f)]
    [DataRow(-50, 0f)]
    [DataRow(200, 1f)]
    [DataRow(50, 0.25f)]
    public void StewardXp_IsATwoHundredthOfTheGoldSpent(int spent, float expected)
    {
        Assert.AreEqual(expected, ProvenderRules.StewardXp(spent));
    }

    [TestMethod]
    public void SlaughterXp_IsTenPerHead_AndNothingForNoHeads()
    {
        Assert.AreEqual(30f, ProvenderRules.SlaughterXp(3));
        Assert.AreEqual(0f, ProvenderRules.SlaughterXp(0));
        Assert.AreEqual(0f, ProvenderRules.SlaughterXp(-2));
    }

    [DataTestMethod]
    [DataRow(0, 7, 0)]
    [DataRow(-4, 7, 0)]
    [DataRow(1, 7, 1)]
    [DataRow(7, 7, 1)]
    [DataRow(8, 7, 2)]
    [DataRow(100, 5, 20)]
    [DataRow(101, 5, 21)]
    [DataRow(10, 0, 0)]
    [DataRow(10, -3, 0)]
    public void RationItems_IsTheTroopsOverTheRatioRoundedUp_AndZeroForBadInput(int troops, int perItem, int expected)
    {
        Assert.AreEqual(expected, ProvenderRules.RationItems(troops, perItem));
    }

    // ---- who gets which ration

    private static RationEntry Ration(string id, bool isDefault = false, string[]? races = null, string[]? cultures = null) =>
        new(id, id, 7, 15, isDefault, races ?? new string[0], cultures ?? new string[0]);

    private static readonly RationCatalog Catalog = new(new[]
    {
        Ration("beer", true, new[] { "dwarf" }, new[] { "vlandia" }),
        Ration("wine", false, new[] { "elf" }),
        Ration("meat", false, new[] { "orc", "uruk" }),
    });

    [TestMethod]
    public void MajorityRace_IsTheRaceWithTheMostTroops()
    {
        var counts = new Dictionary<string, int> { ["human"] = 10, ["orc"] = 30, ["elf"] = 5 };

        Assert.AreEqual("orc", ProvenderRules.MajorityRace(counts));
    }

    [TestMethod]
    public void MajorityRace_ATie_GoesToTheNameThatSortsFirst_SoTheAnswerIsStable()
    {
        var counts = new Dictionary<string, int> { ["orc"] = 10, ["elf"] = 10, ["human"] = 10 };

        Assert.AreEqual("elf", ProvenderRules.MajorityRace(counts));
    }

    [TestMethod]
    public void MajorityRace_NoTroops_IsNull()
    {
        Assert.IsNull(ProvenderRules.MajorityRace(new Dictionary<string, int>()));
        Assert.IsNull(ProvenderRules.MajorityRace(new Dictionary<string, int> { ["orc"] = 0, ["elf"] = -3 }));
    }

    [TestMethod]
    public void SelectRation_TheMajorityRacePrefersOne_ItWinsOverTheCulture()
    {
        var counts = new Dictionary<string, int> { ["orc"] = 30, ["human"] = 5 };

        Assert.AreEqual("meat", ProvenderRules.SelectRation(Catalog, counts, "vlandia")!.Id);
    }

    [TestMethod]
    public void SelectRation_NoRacePrefersOne_TheLeadersCultureDecides()
    {
        var counts = new Dictionary<string, int> { ["human"] = 30 };

        Assert.AreEqual("beer", ProvenderRules.SelectRation(Catalog, counts, "vlandia")!.Id);
    }

    [TestMethod]
    public void SelectRation_NeitherRaceNorCulture_IsTheDefault()
    {
        var catalog = new RationCatalog(new[] { Ration("wine", false, new[] { "elf" }), Ration("beer", true) });
        var counts = new Dictionary<string, int> { ["human"] = 30 };

        Assert.AreEqual("beer", ProvenderRules.SelectRation(catalog, counts, "empire")!.Id);
        Assert.AreEqual("beer", ProvenderRules.SelectRation(catalog, counts, null)!.Id);
    }

    [TestMethod]
    public void SelectRation_OnlyTheMajorityCounts_AMinorityOfElvesDoesNotWinWine()
    {
        var counts = new Dictionary<string, int> { ["human"] = 30, ["elf"] = 29 };

        Assert.AreEqual("beer", ProvenderRules.SelectRation(Catalog, counts, "gondor")!.Id);
    }

    [TestMethod]
    public void SelectRation_NoDefaultAndNoMatch_IsNull()
    {
        var catalog = new RationCatalog(new[] { Ration("wine", false, new[] { "elf" }) });

        Assert.IsNull(ProvenderRules.SelectRation(catalog, new Dictionary<string, int> { ["human"] = 3 }, "gondor"));
        Assert.IsNull(ProvenderRules.SelectRation(RationCatalog.Empty, new Dictionary<string, int>(), null));
    }

    [TestMethod]
    public void SelectRation_RaceAndCultureMatchWithoutRegardToCase()
    {
        var counts = new Dictionary<string, int> { ["Orc"] = 9 };

        Assert.AreEqual("meat", ProvenderRules.SelectRation(Catalog, counts, null)!.Id);
        Assert.AreEqual("beer", ProvenderRules.SelectRation(Catalog, new Dictionary<string, int> { ["human"] = 1 }, "VLANDIA")!.Id);
    }
}
