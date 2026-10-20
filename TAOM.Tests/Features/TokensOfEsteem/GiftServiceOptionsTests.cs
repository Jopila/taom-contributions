using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.TokensOfEsteem;
using TAOM.Features.TokensOfEsteem.Models;

namespace TAOM.Tests.Features.TokensOfEsteem;

/// <summary>
/// The list the player picks from: which held goods appear, what each is worth to this hero, the companion-only purses and
/// the reasons a line is greyed.
/// </summary>
[TestClass]
public class GiftServiceOptionsTests
{
    private GiftFixture _f = null!;

    [TestInitialize]
    public void Setup() => _f = new GiftFixture();

    private System.Collections.Generic.IReadOnlyList<GiftOption> Lord() => _f.Sut.Options(GiftFixture.HeroId, GiftEntry.Lord);

    private System.Collections.Generic.IReadOnlyList<GiftOption> Companion()
    {
        _f.Recipient = GiftFixture.Companion();
        return _f.Sut.Options(GiftFixture.HeroId, GiftEntry.Companion);
    }

    [TestMethod]
    public void Options_NothingHeld_LordGetsAnEmptyList()
    {
        Assert.AreEqual(0, Lord().Count);
    }

    [TestMethod]
    public void Options_UnknownRecipient_IsEmpty()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        _f.Recipient = null;

        Assert.AreEqual(0, Lord().Count);
    }

    [TestMethod]
    public void Options_ReadsOnlyTheCatalogsCategories()
    {
        Lord();

        _f.Items.Received(1).ReadHeldGoods(Arg.Is<System.Collections.Generic.IReadOnlyCollection<string>>(
            ids => ids.OrderBy(i => i).SequenceEqual(new[] { "horse", "jewelry", "noble_horse", "salt", "wine" })));
    }

    [TestMethod]
    public void Options_Lord_GainIsTheCatalogBase()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400, 3));
        _f.Held.Add(GiftFixture.Good("salt_sack", "salt", 20));

        var options = Lord();

        Assert.AreEqual(2, options.Count);
        var ring = options.Single(o => o.ItemId == "ring");
        Assert.AreEqual(GiftOptionKind.Good, ring.Kind);
        Assert.AreEqual(8, ring.BaseGain);
        Assert.AreEqual(0, ring.TasteBonus);
        Assert.AreEqual(8, ring.Gain);
        Assert.AreEqual(3, ring.Count);
        Assert.AreEqual(GiftBlock.None, ring.Block);
        Assert.AreEqual(1, options.Single(o => o.ItemId == "salt_sack").Gain);
    }

    [TestMethod]
    public void Options_Lord_IgnoresTheItemsValue()
    {
        _f.Held.Add(GiftFixture.Good("cheap_ring", "jewelry", 5));
        _f.Held.Add(GiftFixture.Good("rich_ring", "jewelry", 5000));

        Assert.IsTrue(Lord().All(o => o.BaseGain == 8));
    }

    [TestMethod]
    public void Options_Lord_NeverListsPurses()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));

        Assert.IsFalse(Lord().Any(o => o.Kind == GiftOptionKind.Purse));
    }

    [TestMethod]
    public void Options_HeldGoodOutsideTheCatalog_IsDropped()
    {
        _f.Held.Add(GiftFixture.Good("mystery", "not_in_the_catalog", 900));
        _f.Held.Add(GiftFixture.Good("empty_stack", "jewelry", 400, 0));

        Assert.AreEqual(0, Lord().Count);
    }

    [DataTestMethod]
    [DataRow(50, 1)]
    [DataRow(99, 1)]
    [DataRow(100, 2)]
    [DataRow(450, 3)]
    [DataRow(800, 4)]
    [DataRow(1000, 5)]
    [DataRow(4000, 5)]
    public void Options_Companion_GainComesFromTheItemsValue(int value, int expected)
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", value));

        var ring = Companion().Single(o => o.ItemId == "ring");

        Assert.AreEqual(expected, ring.BaseGain);
        Assert.AreEqual(expected, ring.Gain);
    }

    [TestMethod]
    public void Options_Companion_ListsThePursesFromTheSettings_Ascending()
    {
        var purses = Companion().Where(o => o.Kind == GiftOptionKind.Purse).ToList();

        CollectionAssert.AreEqual(new[] { 500, 2000, 5000 }, purses.Select(p => p.Amount).ToArray());
        CollectionAssert.AreEqual(new[] { 1, 4, 10 }, purses.Select(p => p.Gain).ToArray(), "one point per 500 gold, at most 10");
        Assert.IsTrue(purses.All(p => p.Block == GiftBlock.None));
    }

    [TestMethod]
    public void Options_Companion_PurseSizesFollowTheSettings()
    {
        _f.Settings = _f.Settings with { PurseSmall = 500, PurseMedium = 1000, PurseLarge = 3000 };

        var purses = Companion().Where(o => o.Kind == GiftOptionKind.Purse).Select(o => o.Amount).ToArray();

        CollectionAssert.AreEqual(new[] { 500, 1000, 3000 }, purses);
    }

    [TestMethod]
    public void Options_Companion_PursesOfTheSameSizeAreListedOnce_InAscendingOrder()
    {
        _f.Settings = _f.Settings with { PurseSmall = 2000, PurseMedium = 2000, PurseLarge = 1500 };

        var purses = Companion().Where(o => o.Kind == GiftOptionKind.Purse).Select(o => o.Amount).ToArray();

        CollectionAssert.AreEqual(new[] { 1500, 2000 }, purses);
    }

    [TestMethod]
    public void Options_Companion_PurseBeyondThePlayersGold_IsBlockedAsCannotAfford()
    {
        _f.Player = _f.Player! with { Gold = 1000 };

        var purses = Companion().Where(o => o.Kind == GiftOptionKind.Purse).ToList();

        Assert.AreEqual(GiftBlock.None, purses.Single(p => p.Amount == 500).Block);
        Assert.AreEqual(GiftBlock.CannotAfford, purses.Single(p => p.Amount == 2000).Block);
        Assert.AreEqual(GiftBlock.CannotAfford, purses.Single(p => p.Amount == 5000).Block);
    }

    [TestMethod]
    public void Options_Companion_PurseEqualToThePlayersGold_IsAffordable()
    {
        _f.Player = _f.Player! with { Gold = 2000 };

        var purse = Companion().Single(o => o.Amount == 2000);

        Assert.AreEqual(GiftBlock.None, purse.Block);
    }

    [TestMethod]
    public void Options_PursesAreNeverTasteMatched()
    {
        _f.Recipient = GiftFixture.Companion(race: "dwarf");

        var purses = _f.Sut.Options(GiftFixture.HeroId, GiftEntry.Companion).Where(o => o.Kind == GiftOptionKind.Purse);

        Assert.IsTrue(purses.All(p => p.TasteBonus == 0 && !p.Suited));
    }

    [TestMethod]
    public void Options_TasteByRace_AddsTheBonus()
    {
        _f.Recipient = GiftFixture.Lord(race: "dwarf", culture: "erebor");
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));

        var ring = Lord().Single();

        Assert.AreEqual(4, ring.TasteBonus);
        Assert.AreEqual(12, ring.Gain);
        Assert.IsTrue(ring.Suited);
    }

    [TestMethod]
    public void Options_TasteByCulture_AddsTheBonus()
    {
        _f.Recipient = GiftFixture.Lord(race: "human", culture: "vlandia", kingdom: "vlandia");
        _f.Held.Add(GiftFixture.Good("wine_jar", "wine", 100));

        Assert.AreEqual(2, Lord().Single().TasteBonus);
        Assert.AreEqual(7, Lord().Single().Gain);
    }

    [TestMethod]
    public void Options_TasteForOtherGoods_DoesNotApply()
    {
        _f.Recipient = GiftFixture.Lord(race: "dwarf");
        _f.Held.Add(GiftFixture.Good("wine_jar", "wine", 100));

        var wine = Lord().Single();

        Assert.AreEqual(0, wine.TasteBonus);
        Assert.IsFalse(wine.Suited);
        Assert.AreEqual(5, wine.Gain);
    }

    [TestMethod]
    public void Options_TwoMatchingGroups_TakeTheHigherBonus()
    {
        _f.Recipient = GiftFixture.Lord(race: "human", culture: "vlandia");
        _f.Held.Add(GiftFixture.Good("steed", "horse", 800));

        Assert.AreEqual(6, Lord().Single().TasteBonus);
    }

    [TestMethod]
    public void Options_TasteAppliesToACompanionsGoods()
    {
        _f.Recipient = GiftFixture.Companion(race: "dwarf");
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));

        var ring = _f.Sut.Options(GiftFixture.HeroId, GiftEntry.Companion).Single(o => o.Kind == GiftOptionKind.Good);

        Assert.AreEqual(3, ring.BaseGain);
        Assert.AreEqual(4, ring.TasteBonus);
        Assert.AreEqual(7, ring.Gain);
    }

    [TestMethod]
    public void Options_GainIsCappedBySetting()
    {
        _f.Recipient = GiftFixture.Lord(culture: "vlandia");
        _f.Held.Add(GiftFixture.Good("great_steed", "noble_horse", 5000));

        Assert.AreEqual(15, Lord().Single().Gain);

        _f.Settings = _f.Settings with { RelationCap = 12 };

        Assert.AreEqual(12, Lord().Single().Gain);
    }

    [TestMethod]
    public void Options_GainScalesWithTheRelation()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));

        _f.Recipient = GiftFixture.Lord(relation: 45);
        Assert.AreEqual(4, Lord().Single().Gain);

        _f.Recipient = GiftFixture.Lord(relation: 0);
        Assert.AreEqual(8, Lord().Single().Gain);
    }

    [TestMethod]
    public void Options_AtRelation90_EveryGiftIsBlockedAsNoEffect()
    {
        _f.Recipient = GiftFixture.Companion(relation: 90);
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));

        var options = _f.Sut.Options(GiftFixture.HeroId, GiftEntry.Companion);

        Assert.IsTrue(options.Count > 1);
        Assert.IsTrue(options.All(o => o.Gain == 0 && o.Block == GiftBlock.NoEffect));
    }

    [TestMethod]
    public void Options_AffordabilityComesBeforeNoEffect()
    {
        _f.Recipient = GiftFixture.Companion(relation: 90);
        _f.Player = _f.Player! with { Gold = 10 };

        var purse = _f.Sut.Options(GiftFixture.HeroId, GiftEntry.Companion).First(o => o.Kind == GiftOptionKind.Purse);

        Assert.AreEqual(GiftBlock.CannotAfford, purse.Block);
    }

    [TestMethod]
    public void Options_HostileRelationDoesNotReduceTheGain()
    {
        _f.Recipient = GiftFixture.Lord(relation: -30);
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));

        Assert.AreEqual(8, Lord().Single().Gain);
    }

    [TestMethod]
    public void Options_ListNeverChangesAnything()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        Companion();

        _f.Relations.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
        _f.Roster.DidNotReceiveWithAnyArgs().RemoveItem(default!, default);
        _f.Heroes.DidNotReceiveWithAnyArgs().PayGoldToHero(default!, default);
    }

    [TestMethod]
    public void Options_APurse_IsWorthItsOwnBand_NotTheItemValueBand()
    {
        var purse = Companion().Single(o => o.Amount == 2000);

        Assert.AreEqual(4, purse.BaseGain);
        Assert.AreEqual(GiftService.CompanionBase(2000), 5, "an item worth 2000 gold would be worth 5");
        Assert.AreNotEqual(GiftService.CompanionBase(2000), purse.BaseGain);
    }

    [TestMethod]
    public void Options_ModifiedStack_IsNeverListed()
    {
        _f.Held.Add(GiftFixture.Good("sharp_ring", "jewelry", 400, 2, modified: true));

        Assert.AreEqual(0, Lord().Count);
    }

    [TestMethod]
    public void Options_ModifiedAndPlainStackOfOneItem_ListOnlyThePlainOne()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400, 5, modified: true));
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400, 2));

        var ring = Lord().Single();

        Assert.AreEqual(2, ring.Count);
    }

    [TestMethod]
    public void Options_EmptyCatalog_GivesALordNothing_AndACompanionHerPursesOnly()
    {
        _f.Catalog = GiftCatalog.Empty;
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));

        Assert.AreEqual(0, Lord().Count);
        var companion = Companion();
        Assert.AreEqual(3, companion.Count);
        Assert.IsTrue(companion.All(o => o.Kind == GiftOptionKind.Purse));
        _f.Items.DidNotReceiveWithAnyArgs().ReadHeldGoods(default!);
    }

    [TestMethod]
    public void Options_NoPlayerFacts_IsEmpty()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        _f.Player = null;

        Assert.AreEqual(0, Lord().Count);
    }
}
