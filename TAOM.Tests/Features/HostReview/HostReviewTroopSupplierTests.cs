using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TAOM.Features.HostReview.Hooks;

namespace TAOM.Tests.Features.HostReview;

/// <summary>
/// The supplier hands the expanded lineup to the engine's spawn logic in order, never more than asked
/// for, and reports what is left. The origin factory is a fake so no engine object is built.
/// </summary>
[TestClass]
public class HostReviewTroopSupplierTests
{
    private static HostReviewTroopSupplier Supplier(int count, Func<CharacterObject, int, IAgentOriginBase>? factory = null) =>
        new HostReviewTroopSupplier(new CharacterObject[count], factory ?? ((_, _) => Substitute.For<IAgentOriginBase>()));

    [TestMethod]
    public void SupplyTroops_AskingForLessThanRemains_ReturnsExactlyThatMany()
    {
        var supplier = Supplier(10);

        Assert.AreEqual(4, supplier.SupplyTroops(4).Count());
        Assert.AreEqual(6, supplier.NumTroopsNotSupplied);
        Assert.IsTrue(supplier.AnyTroopRemainsToBeSupplied);
    }

    [TestMethod]
    public void SupplyTroops_AskingForMoreThanRemains_ReturnsOnlyWhatRemains()
    {
        var supplier = Supplier(3);

        Assert.AreEqual(3, supplier.SupplyTroops(50).Count());
        Assert.AreEqual(0, supplier.NumTroopsNotSupplied);
    }

    [TestMethod]
    public void SupplyTroops_Exhausted_ReportsNothingRemaining()
    {
        var supplier = Supplier(2);
        supplier.SupplyTroops(2);

        Assert.IsFalse(supplier.AnyTroopRemainsToBeSupplied);
        Assert.AreEqual(0, supplier.SupplyTroops(5).Count());
    }

    [TestMethod]
    public void SupplyTroops_ZeroOrNegative_SuppliesNothing()
    {
        var supplier = Supplier(3);

        Assert.AreEqual(0, supplier.SupplyTroops(0).Count());
        Assert.AreEqual(0, supplier.SupplyTroops(-4).Count());
        Assert.AreEqual(3, supplier.NumTroopsNotSupplied);
    }

    [TestMethod]
    public void SupplyOneTroop_LineupExhausted_ReturnsNullAfterTheLast()
    {
        var supplier = Supplier(2);

        Assert.IsNotNull(supplier.SupplyOneTroop());
        Assert.IsNotNull(supplier.SupplyOneTroop());
        Assert.IsNull(supplier.SupplyOneTroop());
        Assert.IsFalse(supplier.AnyTroopRemainsToBeSupplied);
    }

    [TestMethod]
    public void SupplyTroops_MixedWithSupplyOneTroop_PassesRanksInLineupOrder()
    {
        var ranks = new List<int>();
        var supplier = Supplier(4, (_, rank) => { ranks.Add(rank); return Substitute.For<IAgentOriginBase>(); });

        supplier.SupplyTroops(3);
        supplier.SupplyOneTroop();

        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, ranks);
    }

    [TestMethod]
    public void SupplyTroops_EmptyLineup_ReturnsNothing()
    {
        var supplier = Supplier(0);

        Assert.AreEqual(0, supplier.SupplyTroops(10).Count());
        Assert.IsNull(supplier.SupplyOneTroop());
        Assert.IsFalse(supplier.AnyTroopRemainsToBeSupplied);
        Assert.AreEqual(0, supplier.NumTroopsNotSupplied);
        Assert.IsNull(supplier.GetGeneralCharacter());
        Assert.AreEqual(0, supplier.GetNumberOfPlayerControllableTroops());
    }

    [TestMethod]
    public void NumRemovedTroops_AfterSupplyingEveryone_ReturnsZero()
    {
        var supplier = Supplier(3);
        supplier.SupplyTroops(3);

        Assert.AreEqual(0, supplier.NumRemovedTroops);
    }

    [TestMethod]
    public void GetAllTroops_FiveTroops_ReturnsAllWithoutConsuming()
    {
        var supplier = Supplier(5);

        Assert.AreEqual(5, supplier.GetAllTroops().Count());
        Assert.AreEqual(5, supplier.NumTroopsNotSupplied);
        Assert.AreEqual(5, supplier.GetNumberOfPlayerControllableTroops());
    }
}
