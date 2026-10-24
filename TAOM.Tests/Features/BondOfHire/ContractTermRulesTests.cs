using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BondOfHire;

namespace TAOM.Tests.Features.BondOfHire;

/// <summary>
/// What the kingdom Clans panel says about a clan's contract (#868). The days are the engine's remaining time to
/// <c>Clan.ShouldStayInKingdomUntil</c>, whoever set it, rounded DOWN so "at least N more days" is always true. Under one day, a
/// date already past and a value that is not a finite number all read as "not known".
/// </summary>
[TestClass]
public class ContractTermRulesTests
{
    [TestMethod]
    public void Describe_AClanThatIsNotAMercenary_IsHidden()
    {
        Assert.AreEqual(ContractTermKind.Hidden, ContractTermRules.Describe(false, false, 30f).Kind);
    }

    [TestMethod]
    public void Describe_ThePlayerClan_IsHidden_EvenAsAMercenaryWithDaysLeft()
    {
        Assert.AreEqual(ContractTermKind.Hidden, ContractTermRules.Describe(true, true, 30f).Kind);
    }

    [TestMethod]
    public void Describe_AVassalThatIsThePlayerClan_IsHidden()
    {
        Assert.AreEqual(ContractTermKind.Hidden, ContractTermRules.Describe(false, true, 0f).Kind);
    }

    [DataTestMethod]
    [DataRow(-0.5f)]
    [DataRow(-30f)]
    [DataRow(float.MinValue)]
    [DataRow(0f)]
    [DataRow(0.5f)]
    [DataRow(0.9999f)]
    public void Describe_AMercenaryWithUnderOneDayLeftOrPast_IsUnknown(float remaining)
    {
        Assert.AreEqual(ContractTermKind.Unknown, ContractTermRules.Describe(true, false, remaining).Kind);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void Describe_ARemainingTimeThatIsNotFinite_IsUnknown(float remaining)
    {
        Assert.AreEqual(ContractTermKind.Unknown, ContractTermRules.Describe(true, false, remaining).Kind);
    }

    [DataTestMethod]
    [DataRow(1.0f, 1)]
    [DataRow(1.9f, 1)]
    [DataRow(2f, 2)]
    [DataRow(29.99f, 29)]
    [DataRow(30f, 30)]
    [DataRow(119.4f, 119)]
    public void Describe_AMercenaryWithDaysLeft_IsKnownRoundedDown(float remaining, int expectedDays)
    {
        var term = ContractTermRules.Describe(true, false, remaining);

        Assert.AreEqual(ContractTermKind.Known, term.Kind);
        Assert.AreEqual(expectedDays, term.Days);
    }

    [TestMethod]
    public void Describe_AHugeFiniteRemainingTime_IsKnown_AndStopsAtTheLargestWholeNumber()
    {
        var term = ContractTermRules.Describe(true, false, float.MaxValue);

        Assert.AreEqual(ContractTermKind.Known, term.Kind);
        Assert.AreEqual(int.MaxValue, term.Days);
    }

    [TestMethod]
    public void Describe_HiddenAndUnknown_CarryNoDays()
    {
        Assert.AreEqual(0, ContractTermRules.Describe(false, false, 30f).Days);
        Assert.AreEqual(0, ContractTermRules.Describe(true, false, 0.5f).Days);
    }
}
