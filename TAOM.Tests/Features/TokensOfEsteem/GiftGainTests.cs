using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TokensOfEsteem;
using TAOM.Features.TokensOfEsteem.Models;

namespace TAOM.Tests.Features.TokensOfEsteem;

/// <summary>
/// The arithmetic of a gift: the companion value bands, the purse band, the scaling above relation 0 (always on), the cap,
/// and the catalog's taste lookup. Pure, so every band edge and both sides of the 45-point rounding are pinned.
/// </summary>
[TestClass]
public class GiftGainTests
{
    [DataTestMethod]
    [DataRow(-5, 1)]
    [DataRow(0, 1)]
    [DataRow(1, 1)]
    [DataRow(99, 1)]
    [DataRow(100, 2)]
    [DataRow(299, 2)]
    [DataRow(300, 3)]
    [DataRow(599, 3)]
    [DataRow(600, 4)]
    [DataRow(999, 4)]
    [DataRow(1000, 5)]
    [DataRow(1001, 5)]
    [DataRow(1000000, 5)]
    [DataRow(int.MaxValue, 5)]
    public void CompanionBase_ByItemValue_FollowsTheBands(int value, int expected)
    {
        Assert.AreEqual(expected, GiftService.CompanionBase(value));
    }

    [TestMethod]
    public void PurseBand_IsOnePointPerFiveHundredGold_UpToTen()
    {
        Assert.AreEqual(500, GiftService.PurseGoldPerPoint);
        Assert.AreEqual(10, GiftService.PurseMaxPoints);
    }

    [DataTestMethod]
    [DataRow(int.MinValue, 0)]
    [DataRow(-500, 0)]
    [DataRow(0, 0)]
    [DataRow(499, 0)]
    [DataRow(500, 1)]
    [DataRow(999, 1)]
    [DataRow(1000, 2)]
    [DataRow(2000, 4)]
    [DataRow(4999, 9)]
    [DataRow(5000, 10)]
    [DataRow(5001, 10)]
    [DataRow(100000, 10)]
    [DataRow(int.MaxValue, 10)]
    public void PurseGain_IsItsOwnBand_NotTheItemValueBand(int gold, int expected)
    {
        Assert.AreEqual(expected, GiftService.PurseGain(gold));
    }

    [TestMethod]
    public void ComputeGain_RelationZero_IsTheFullBase()
    {
        Assert.AreEqual(8, GiftService.ComputeGain(8, 0, 0, 15));
    }

    [TestMethod]
    public void ComputeGain_TasteBonus_AddsToTheBase()
    {
        Assert.AreEqual(12, GiftService.ComputeGain(8, 4, 0, 15));
    }

    [DataTestMethod]
    [DataRow(-100)]
    [DataRow(-51)]
    [DataRow(-1)]
    public void ComputeGain_NegativeRelation_IsNotScaled(int relation)
    {
        Assert.AreEqual(8, GiftService.ComputeGain(8, 0, relation, 15));
    }

    [DataTestMethod]
    [DataRow(0, 8)]
    [DataRow(1, 8)]
    [DataRow(30, 5)]
    [DataRow(45, 4)]
    [DataRow(60, 3)]
    [DataRow(89, 0)]
    [DataRow(90, 0)]
    [DataRow(100, 0)]
    public void ComputeGain_AboveZero_ScalesByNinetyMinusRelationOverNinety(int relation, int expected)
    {
        Assert.AreEqual(expected, GiftService.ComputeGain(8, 0, relation, 15));
    }

    [DataTestMethod]
    [DataRow(44, 1)]
    [DataRow(45, 1)]
    [DataRow(46, 0)]
    public void ComputeGain_OnePointGift_StopsWorkingNearRelation45(int relation, int expected)
    {
        Assert.AreEqual(expected, GiftService.ComputeGain(1, 0, relation, 15));
    }

    [TestMethod]
    public void ComputeGain_AboveTheCap_IsTheCap()
    {
        Assert.AreEqual(15, GiftService.ComputeGain(10, 10, 0, 15));
    }

    [TestMethod]
    public void ComputeGain_CapFollowsTheSetting()
    {
        Assert.AreEqual(12, GiftService.ComputeGain(10, 10, 0, 12));
        Assert.AreEqual(1, GiftService.ComputeGain(10, 10, 0, 1));
    }

    [TestMethod]
    public void ComputeGain_CapAppliesAfterTheScaling()
    {
        // 20 at relation 45 scales to 10, which is under a cap of 15 but over a cap of 8.
        Assert.AreEqual(10, GiftService.ComputeGain(10, 10, 45, 15));
        Assert.AreEqual(8, GiftService.ComputeGain(10, 10, 45, 8));
    }

    [DataTestMethod]
    [DataRow(0, 15)]
    [DataRow(-3, 15)]
    [DataRow(int.MinValue, 15)]
    [DataRow(8, 0)]
    [DataRow(8, -1)]
    [DataRow(8, int.MinValue)]
    public void ComputeGain_NoBaseOrNoCap_IsZero(int baseGain, int cap)
    {
        Assert.AreEqual(0, GiftService.ComputeGain(baseGain, 0, 0, cap));
    }

    [TestMethod]
    public void ComputeGain_ExtremeInputs_DoNotOverflow()
    {
        Assert.AreEqual(15, GiftService.ComputeGain(int.MaxValue, int.MaxValue, 0, 15));
        Assert.AreEqual(0, GiftService.ComputeGain(int.MaxValue, 0, int.MaxValue, 15));
    }

    [TestMethod]
    public void ComputeGain_EveryRelationAndCap_StaysBetweenZeroAndTheCap_AndNeverRisesWithRelation()
    {
        foreach (var cap in new[] { 1, 8, 15, 30 })
        {
            foreach (var baseGain in new[] { 1, 5, 10 })
            {
                foreach (var taste in new[] { 0, 4, 10 })
                {
                    var previous = int.MaxValue;
                    for (var relation = 0; relation <= 100; relation++)
                    {
                        var gain = GiftService.ComputeGain(baseGain, taste, relation, cap);
                        Assert.IsTrue(gain >= 0 && gain <= cap, $"gain {gain} out of 0..{cap} (base {baseGain}, taste {taste}, relation {relation})");
                        Assert.IsTrue(gain <= previous, $"gain rose from {previous} to {gain} at relation {relation}");
                        previous = gain;
                    }
                }
            }
        }
    }

    private static readonly GiftCatalogEntry Wine = new("wine", 5, new[]
    {
        new TasteGroup(new[] { "elf" }, new string[0], 3),
        new TasteGroup(new string[0], new[] { "vlandia" }, 2),
        new TasteGroup(new[] { "human" }, new[] { "gondor" }, 6),
    });

    private static readonly GiftCatalog Catalog = new(new[] { Wine });

    [TestMethod]
    public void TasteBonus_RaceListed_IsThatGroupsBonus()
    {
        Assert.AreEqual(3, Wine.TasteBonus("elf", "rivendell"));
    }

    [TestMethod]
    public void TasteBonus_CultureListed_IsThatGroupsBonus()
    {
        Assert.AreEqual(2, Wine.TasteBonus("dwarf", "vlandia"));
    }

    [TestMethod]
    public void TasteBonus_NeitherListed_IsZero()
    {
        Assert.AreEqual(0, Wine.TasteBonus("orc", "mordor"));
    }

    [TestMethod]
    public void TasteBonus_SeveralGroupsMatch_TakesTheHighestAndDoesNotStack()
    {
        Assert.AreEqual(6, Wine.TasteBonus("human", "vlandia"));
    }

    [TestMethod]
    public void TasteBonus_RaceOrCultureOfOneGroup_EitherSuffices()
    {
        Assert.AreEqual(6, Wine.TasteBonus("human", "mordor"));
        Assert.AreEqual(6, Wine.TasteBonus("orc", "gondor"));
    }

    [TestMethod]
    public void TasteBonus_MatchIgnoresCase()
    {
        Assert.AreEqual(3, Wine.TasteBonus("ELF", "x"));
    }

    [DataTestMethod]
    [DataRow("", "")]
    [DataRow(null, null)]
    public void TasteBonus_BlankRaceAndCulture_MatchNothing(string? race, string? culture)
    {
        Assert.AreEqual(0, Wine.TasteBonus(race!, culture!));
    }

    [TestMethod]
    public void TryGet_KnownCategory_ReturnsItsEntry()
    {
        Assert.IsTrue(Catalog.TryGet("wine", out var entry));
        Assert.AreSame(Wine, entry);
        Assert.AreEqual(5, entry.LordBase);
    }

    [DataTestMethod]
    [DataRow("mithril")]
    [DataRow("")]
    [DataRow(null)]
    public void TryGet_UnknownOrBlankCategory_IsFalse(string? category)
    {
        Assert.IsFalse(Catalog.TryGet(category!, out _));
    }
}
