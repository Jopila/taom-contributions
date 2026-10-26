using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Domain;
using TAOM.Features.Encyclopedia;

namespace TAOM.Tests.Features.Encyclopedia;

/// <summary>Life stages and race grouping for the encyclopedia hero filters (#907).</summary>
[TestClass]
public class HeroFilterRulesTests
{
    private static HeroFilterConfig Config() => new()
    {
        DefaultGroup = "men",
        Groups = new List<HeroFilterGroup>
        {
            new() { Id = "men", Races = new List<string> { "human" }, AgeUpperBounds = new List<float> { 25, 35, 45, 55 } },
            new() { Id = "elves", Races = new List<string> { "elf" }, AgeUpperBounds = new List<float> { 24, 28, 32, 40 } },
        },
    };

    private static IRaceManager Races()
    {
        var races = Substitute.For<IRaceManager>();
        races.IsValidRaceId(Arg.Any<int>()).Returns(false);
        foreach (var (id, name) in new[] { (0, "human"), (1, "elf"), (2, "hobbit") })
        {
            races.IsValidRaceId(id).Returns(true);
            races.GetRaceNameFromId(id).Returns(name);
        }
        // The real manager answers "human" for an unknown id; the rules must never get that far.
        races.GetRaceNameFromId(99).Returns("human");
        return races;
    }

    [DataTestMethod]
    [DataRow(18f, LifeStage.Young)]
    [DataRow(24.9f, LifeStage.Young)]
    [DataRow(25f, LifeStage.Adult)]
    [DataRow(34.9f, LifeStage.Adult)]
    [DataRow(35f, LifeStage.Mature)]
    [DataRow(44.9f, LifeStage.Mature)]
    [DataRow(45f, LifeStage.Elder)]
    [DataRow(54.9f, LifeStage.Elder)]
    [DataRow(55f, LifeStage.Venerable)]
    [DataRow(900f, LifeStage.Venerable)]
    public void StageFor_UsesTheGroupBounds(float age, LifeStage expected)
        => Assert.AreEqual(expected, HeroFilterRules.StageFor(new float[] { 25, 35, 45, 55 }, age));

    [TestMethod]
    public void StageFor_BelowEighteen_IsNoStage()
        => Assert.IsNull(HeroFilterRules.StageFor(new float[] { 25, 35, 45, 55 }, 17.9f));

    [TestMethod]
    public void StageFor_NaNAge_IsNoStage()
        => Assert.IsNull(HeroFilterRules.StageFor(new float[] { 25, 35, 45, 55 }, float.NaN));

    [TestMethod]
    public void StageFor_ElfBounds_MoveEachStage()
    {
        var elf = new float[] { 24, 28, 32, 40 };

        Assert.AreEqual(LifeStage.Young, HeroFilterRules.StageFor(elf, 23.9f));
        Assert.AreEqual(LifeStage.Adult, HeroFilterRules.StageFor(elf, 24f));
        Assert.AreEqual(LifeStage.Venerable, HeroFilterRules.StageFor(elf, 40f));
    }

    [TestMethod]
    public void GroupIdForRaceId_ValidRace_NamesItsGroup()
    {
        Assert.AreEqual("men", HeroFilterRules.GroupIdForRaceId(Config(), Races(), 0));
        Assert.AreEqual("elves", HeroFilterRules.GroupIdForRaceId(Config(), Races(), 1));
    }

    [TestMethod]
    public void GroupIdForRaceId_InvalidRaceId_IsUngrouped_NotHuman()
        => Assert.IsNull(HeroFilterRules.GroupIdForRaceId(Config(), Races(), 99));

    [TestMethod]
    public void GroupIdForRaceId_RaceInNoGroup_IsUngrouped()
        => Assert.IsNull(HeroFilterRules.GroupIdForRaceId(Config(), Races(), 2));

    [TestMethod]
    public void MatchesRace_OnlyTheHeroesOwnGroup()
    {
        Assert.IsTrue(HeroFilterRules.MatchesRace(Config(), Races(), "elves", 1));
        Assert.IsFalse(HeroFilterRules.MatchesRace(Config(), Races(), "men", 1));
    }

    [TestMethod]
    public void MatchesRace_InvalidOrUngroupedRace_MatchesNoRaceItem()
    {
        foreach (var group in new[] { "men", "elves" })
        {
            Assert.IsFalse(HeroFilterRules.MatchesRace(Config(), Races(), group, 99), group + " invalid id");
            Assert.IsFalse(HeroFilterRules.MatchesRace(Config(), Races(), group, 2), group + " ungrouped race");
        }
    }

    [TestMethod]
    public void MatchesStage_UsesTheHeroRaceGroupBrackets()
    {
        Assert.IsTrue(HeroFilterRules.MatchesStage(Config(), Races(), LifeStage.Adult, 1, 24f));
        Assert.IsFalse(HeroFilterRules.MatchesStage(Config(), Races(), LifeStage.Adult, 0, 24f));
        Assert.IsTrue(HeroFilterRules.MatchesStage(Config(), Races(), LifeStage.Young, 0, 24f));
    }

    [DataTestMethod]
    [DataRow(99)]
    [DataRow(2)]
    public void MatchesStage_InvalidOrUngroupedRace_UsesTheDefaultGroupBrackets(int raceId)
    {
        Assert.IsTrue(HeroFilterRules.MatchesStage(Config(), Races(), LifeStage.Young, raceId, 24f));
        Assert.IsTrue(HeroFilterRules.MatchesStage(Config(), Races(), LifeStage.Adult, raceId, 25f));
    }

    [TestMethod]
    public void MatchesStage_NoGroupsAtAll_MatchesNothing()
        => Assert.IsFalse(HeroFilterRules.MatchesStage(new HeroFilterConfig(), Races(), LifeStage.Adult, 0, 30f));

    [TestMethod]
    public void KnownGroupIds_AreTheEightLabelledGroups()
        => CollectionAssert.AreEqual(
            new[] { "men", "elves", "dwarves", "orcs", "goblins", "uruks", "trolls", "maiar" },
            HeroFilterRules.KnownGroupIds.ToList());
}
