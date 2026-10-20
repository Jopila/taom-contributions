using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;

namespace TAOM.Tests.Features.TokensOfEsteem;

/// <summary>
/// The one pure rule in the hero adapter: the campaign day the cooldown counts in. <c>(int)double.NaN</c> and a cast past
/// <c>int.MaxValue</c> both give <c>int.MinValue</c> on net472, so a bad engine value must hide the line rather than become a day
/// (csharp-architecture.md, "Engine-Float Decision Gates").
/// </summary>
[TestClass]
public class GiftHeroAdapterTests
{
    [DataTestMethod]
    [DataRow(0.0, 0)]
    [DataRow(1.0, 1)]
    [DataRow(100.0, 100)]
    [DataRow(100.9, 100)]
    [DataRow(2147483647.0, int.MaxValue)]
    public void WholeDay_AFiniteDayInRange_IsItsFloor(double days, int expected)
    {
        Assert.AreEqual(expected, GiftHeroAdapter.WholeDay(days));
    }

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    [DataRow(-0.1)]
    [DataRow(-1.0)]
    [DataRow(2147483648.0)]
    [DataRow(1e30)]
    [DataRow(double.MaxValue)]
    [DataRow(double.MinValue)]
    public void WholeDay_NotAUsableDay_IsNull(double days)
    {
        Assert.IsNull(GiftHeroAdapter.WholeDay(days));
    }
}
