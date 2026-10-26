using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.HarvestRoad;

namespace TAOM.Tests.Features.HarvestRoad;

/// <summary>
/// The gates and the sign filter for Harvest Road (#912). The patches hand the service facts, never engine types; the
/// map's notice reads the sign of the FIRST line to say "sold" or "bought", so a list it is given carries one sign only.
/// </summary>
[TestClass]
public class HarvestRoadServiceTests
{
    private static HarvestRoadService Service(bool enabled = true)
    {
        var settings = Substitute.For<IHarvestRoadSettingsProvider>();
        settings.Enabled.Returns(enabled);
        return new HarvestRoadService(settings);
    }

    [TestMethod]
    public void ShouldWatch_AnAiPartyInATownWithTheToggleOn_IsTrue()
        => Assert.IsTrue(Service().ShouldWatch(isMainParty: false, hasTown: true));

    [TestMethod]
    public void ShouldWatch_ToggleOff_IsFalse()
        => Assert.IsFalse(Service(enabled: false).ShouldWatch(isMainParty: false, hasTown: true));

    [TestMethod]
    public void ShouldWatch_TheMainParty_IsFalse()
        => Assert.IsFalse(Service().ShouldWatch(isMainParty: true, hasTown: true));

    [TestMethod]
    public void ShouldWatch_NoTown_IsFalse()
        => Assert.IsFalse(Service().ShouldWatch(isMainParty: false, hasTown: false));

    [TestMethod]
    public void ShouldWatchFoodPurchase_APositiveAmount_IsTrue()
        => Assert.IsTrue(Service().ShouldWatchFoodPurchase(isMainParty: false, hasTown: true, amount: 12));

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(int.MinValue)]
    public void ShouldWatchFoodPurchase_ZeroOrNegativeAmount_IsFalse(int amount)
        => Assert.IsFalse(Service().ShouldWatchFoodPurchase(isMainParty: false, hasTown: true, amount: amount));

    [TestMethod]
    public void ShouldWatchFoodPurchase_ToggleOffMainPartyOrNoTown_IsFalse()
    {
        Assert.IsFalse(Service(enabled: false).ShouldWatchFoodPurchase(false, true, 5));
        Assert.IsFalse(Service().ShouldWatchFoodPurchase(true, true, 5));
        Assert.IsFalse(Service().ShouldWatchFoodPurchase(false, false, 5));
    }

    [TestMethod]
    public void Sold_KeepsOnlyThePositiveLines_InOrder()
    {
        var diff = new List<(string, int)> { ("a", 4), ("b", -2), ("c", 1), ("d", 0) };

        CollectionAssert.AreEqual(new[] { ("a", 4), ("c", 1) }, Service().Sold(diff));
    }

    [TestMethod]
    public void Bought_KeepsOnlyTheNegativeLines_InOrder()
    {
        var diff = new List<(string, int)> { ("a", 4), ("b", -2), ("c", 1), ("d", -7) };

        CollectionAssert.AreEqual(new[] { ("b", -2), ("d", -7) }, Service().Bought(diff));
    }

    [TestMethod]
    public void Sold_WhenEveryLineIsABuy_IsEmpty_SoNothingIsRaised()
        => Assert.AreEqual(0, Service().Sold(new List<(string, int)> { ("a", -2) }).Count);

    [TestMethod]
    public void Bought_WhenEveryLineIsASale_IsEmpty_SoNothingIsRaised()
        => Assert.AreEqual(0, Service().Bought(new List<(string, int)> { ("a", 2) }).Count);

    [TestMethod]
    public void SoldAndBought_AnEmptyOrNullDiff_AreEmpty()
    {
        Assert.AreEqual(0, Service().Sold(new List<(string, int)>()).Count);
        Assert.AreEqual(0, Service().Bought(new List<(string, int)>()).Count);
        Assert.AreEqual(0, Service().Sold<string>(null).Count);
        Assert.AreEqual(0, Service().Bought<string>(null).Count);
    }

    [TestMethod]
    public void Sold_IntMinValueLine_IsNotASale()
        => Assert.AreEqual(0, Service().Sold(new List<(string, int)> { ("a", int.MinValue) }).Count);
}
