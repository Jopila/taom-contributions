using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BondsOfBattle;

namespace TAOM.Tests.Features.BondsOfBattle;

[TestClass]
public class OverpayRelationCalculatorTests
{
    private const int VanillaCap = 3;

    /// <summary>
    /// The engine's DefaultBarterModel.CalculateOverpayRelationIncreaseCosts (v1.5.5) restated over plain numbers,
    /// the yardstick for parity at the vanilla cap. Hero.MainHero's Tribute perk is the <paramref name="tribute"/> argument.
    /// </summary>
    private static int Vanilla(int relation, float overpay, float tribute, Func<float> random)
    {
        float end = Math.Max(-100f, Math.Min(100f, relation + VanillaCap));
        float points = 0f;
        for (int i = relation; i < end; i++)
        {
            int cost = 1000 + 100 * (i * i);
            if (overpay >= cost)
            {
                overpay -= cost;
                points += 1f;
                continue;
            }

            if (random() <= overpay / cost)
                points += 1f;
            break;
        }

        if (tribute > 0f)
            points *= 1f + tribute;
        return (int)Math.Ceiling(points);
    }

    private static Func<float> Fixed(float value) => () => value;

    [DataTestMethod]
    [DataRow(0, 500f, 0.2f)]
    [DataRow(0, 500f, 0.9f)]
    [DataRow(0, 1000f, 0.5f)]
    [DataRow(0, 1500f, 0.0f)]
    [DataRow(0, 1500f, 0.9f)]
    [DataRow(0, 2100f, 0.5f)]
    [DataRow(0, 3500f, 0.5f)]
    [DataRow(0, 100000f, 0.5f)]
    [DataRow(10, 5000f, 0.3f)]
    [DataRow(-50, 400000f, 0.1f)]
    [DataRow(-100, 2000000f, 0.1f)]
    [DataRow(97, 2000000f, 0.1f)]
    [DataRow(99, 5000000f, 0.1f)]
    [DataRow(100, 5000000f, 0.1f)]
    [DataRow(-100, 1f, 0.0f)]
    public void Calculate_AtTheVanillaCap_MatchesTheEngine(int relation, float overpay, float roll)
    {
        foreach (var tribute in new[] { 0f, 0.25f, 0.5f })
        {
            var expected = Vanilla(relation, overpay, tribute, Fixed(roll));

            var actual = OverpayRelationCalculator.Calculate(relation, overpay, VanillaCap, tribute, Fixed(roll));

            Assert.AreEqual(expected, actual, $"relation {relation}, overpay {overpay}, roll {roll}, tribute {tribute}");
        }
    }

    [TestMethod]
    public void Calculate_FullPointsThenTheChanceOfTheLast_UsesTheRoll()
    {
        // 1000 pays point one; 500 of 1100 is left for point two: 500/1100 = 0.4545.
        Assert.AreEqual(2, OverpayRelationCalculator.Calculate(0, 1500f, 15, 0f, Fixed(0.45f)));
        Assert.AreEqual(1, OverpayRelationCalculator.Calculate(0, 1500f, 15, 0f, Fixed(0.46f)));
    }

    [TestMethod]
    public void Calculate_TheLastPointIsThenTheEnd_NoFurtherPointsAreRolled()
    {
        var rolls = 0;
        Func<float> counting = () => { rolls++; return 0f; };

        var result = OverpayRelationCalculator.Calculate(0, 1500f, 15, 0f, counting);

        Assert.AreEqual(2, result);
        Assert.AreEqual(1, rolls);
    }

    [TestMethod]
    public void Calculate_AtCap15_TheCapRaisesTheCeilingPastThree()
    {
        // Costs from relation 0: 1000, 1100, 1400, 1900, 2600, 3500. 10000 pays the first five (8000), 2000 of 3500 is left.
        Assert.AreEqual(5, OverpayRelationCalculator.Calculate(0, 10000f, 15, 0f, Fixed(0.99f)));
        Assert.AreEqual(6, OverpayRelationCalculator.Calculate(0, 10000f, 15, 0f, Fixed(0.0f)));
        Assert.AreEqual(3, OverpayRelationCalculator.Calculate(0, 10000f, VanillaCap, 0f, Fixed(0.0f)));
    }

    [TestMethod]
    public void Calculate_TheCapBoundsTheLoop_NoMatterTheOverpay()
    {
        Assert.AreEqual(15, OverpayRelationCalculator.Calculate(0, float.MaxValue, 15, 0f, Fixed(0f)));
        Assert.AreEqual(30, OverpayRelationCalculator.Calculate(-100, float.MaxValue, 30, 0f, Fixed(0f)));
    }

    [TestMethod]
    public void Calculate_NearPlus100_TheTargetClampsAtPlus100()
    {
        // relation 98 with cap 15 can only climb to 100: points at 98 and 99.
        Assert.AreEqual(2, OverpayRelationCalculator.Calculate(98, 5000000f, 15, 0f, Fixed(0f)));
        Assert.AreEqual(1, OverpayRelationCalculator.Calculate(99, 5000000f, 15, 0f, Fixed(0f)));
        Assert.AreEqual(0, OverpayRelationCalculator.Calculate(100, 5000000f, 15, 0f, Fixed(0f)));
    }

    [TestMethod]
    public void Calculate_AStartOutsideTheRelationRange_IsClamped()
    {
        Assert.AreEqual(0, OverpayRelationCalculator.Calculate(500, 5000000f, 15, 0f, Fixed(0f)));
        Assert.AreEqual(OverpayRelationCalculator.Calculate(-100, 3000000f, 15, 0f, Fixed(0.5f)),
            OverpayRelationCalculator.Calculate(-5000, 3000000f, 15, 0f, Fixed(0.5f)));
    }

    [TestMethod]
    public void Calculate_TributeBonus_MultipliesThePointsAndRoundsUp()
    {
        // 5 whole points at roll 0.99; x1.5 = 7.5 -> 8.
        Assert.AreEqual(8, OverpayRelationCalculator.Calculate(0, 10000f, 15, 0.5f, Fixed(0.99f)));
        // 5 x 1.1 = 5.5 -> 6.
        Assert.AreEqual(6, OverpayRelationCalculator.Calculate(0, 10000f, 15, 0.1f, Fixed(0.99f)));
        // A bonus on zero points stays zero.
        Assert.AreEqual(0, OverpayRelationCalculator.Calculate(0, 10f, 15, 0.5f, Fixed(0.99f)));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(-0.5f)]
    [DataRow(0f)]
    public void Calculate_ANonUsableTributeBonus_IsIgnored(float tribute)
    {
        Assert.AreEqual(5, OverpayRelationCalculator.Calculate(0, 10000f, 15, tribute, Fixed(0.99f)));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(-5f)]
    [DataRow(0f)]
    public void Calculate_NoUsableOverpay_IsZero(float overpay)
    {
        Assert.AreEqual(0, OverpayRelationCalculator.Calculate(0, overpay, 15, 0.5f, Fixed(0f)));
    }

    [TestMethod]
    public void Calculate_AZeroOrNegativeCap_IsZero()
    {
        Assert.AreEqual(0, OverpayRelationCalculator.Calculate(0, 1000000f, 0, 0f, Fixed(0f)));
        Assert.AreEqual(0, OverpayRelationCalculator.Calculate(0, 1000000f, -4, 0f, Fixed(0f)));
    }
}
