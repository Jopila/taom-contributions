using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.Encyclopedia;
using TAOM.Features.Encyclopedia.Adapters;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Encyclopedia;

/// <summary>The two filter groups the adapter builds (#907): labels, item counts and the never-throw predicate wrapper.</summary>
[TestClass]
public class EncyclopediaHeroFilterAdapterTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static HeroFilterConfig EightGroups() => new()
    {
        DefaultGroup = "men",
        Groups = HeroFilterRules.KnownGroupIds.Select(id => new HeroFilterGroup
        {
            Id = id,
            Races = new List<string> { id == "men" ? "human" : id },
            AgeUpperBounds = new List<float> { 25, 35, 45, 55 },
        }).ToList(),
    };

    private static EncyclopediaHeroFilterAdapter Build(HeroFilterConfig config, IModLogger? logger = null)
    {
        var provider = Substitute.For<IHeroFilterConfigProvider>();
        provider.LoadConfig().Returns(config);
        return new EncyclopediaHeroFilterAdapter(provider, Substitute.For<IRaceManager>(), logger ?? Substitute.For<IModLogger>());
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void BuildGroups_ReturnsRaceThenAge_WithEightAndFiveItems()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var groups = Build(EightGroups()).BuildGroups();

        Assert.AreEqual(2, groups.Count);
        Assert.AreEqual("Race", groups[0].Name.ToString());
        Assert.AreEqual("Age", groups[1].Name.ToString());
        CollectionAssert.AreEqual(
            new[] { "Men", "Elves", "Dwarves", "Orcs", "Goblins", "Uruk-hai", "Trolls", "Maiar and Wraiths" },
            groups[0].Filters.Select(f => f.Name.ToString()).ToList());
        CollectionAssert.AreEqual(
            new[] { "Young", "Adult", "Mature", "Elder", "Venerable" },
            groups[1].Filters.Select(f => f.Name.ToString()).ToList());
    }

    [TestMethod]
    public void AgeGroupTitle_ReusesTheVanillaAgeKey_AndNoNewKeyIsInventedForIt()
    {
        var src = TAOM.Tests.Infrastructure.RepoPaths.ReadSource("Main/Features/Encyclopedia/Adapters/EncyclopediaHeroFilterAdapter.cs");

        StringAssert.Contains(src, "{=jaaQijQs}Age");
        Assert.IsFalse(src.Contains("taom_ency_filter_age"), "the Age title is vanilla's key");
    }

    [TestMethod]
    public void BuildGroups_NoValidGroups_ReturnsNothing()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        Assert.AreEqual(0, Build(new HeroFilterConfig()).BuildGroups().Count);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Predicates_OnANonHero_LogOnceAndReturnTrue()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        var logger = Substitute.For<IModLogger>();
        var groups = Build(EightGroups(), logger).BuildGroups();

        foreach (var item in groups.SelectMany(g => g.Filters))
            Assert.IsTrue(item.Predicate(new object()), "a predicate that cannot read the hero must not hide it");

        logger.Received(1).LogError(Arg.Any<string>());
    }

    private sealed class KnowledgeAdapter : EncyclopediaHeroFilterAdapter
    {
        private readonly bool _knows;

        public KnowledgeAdapter(IHeroFilterConfigProvider provider, IModLogger logger, bool knows)
            : base(provider, Substitute.For<IRaceManager>(), logger)
        {
            _knows = knows;
        }

        protected override bool KnowsDetailsOf(TaleWorlds.CampaignSystem.Hero hero) => _knows;
    }

    private static IReadOnlyList<EncyclopediaFilterGroup> BuildWithKnowledge(bool knows, IModLogger logger)
    {
        var provider = Substitute.For<IHeroFilterConfigProvider>();
        provider.LoadConfig().Returns(EightGroups());
        return new KnowledgeAdapter(provider, logger, knows).BuildGroups();
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void AgePredicates_HeroWhoseDetailsAreUnknown_MatchNoStage_AndDoNotLog()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        var logger = Substitute.For<IModLogger>();
        var hero = (TaleWorlds.CampaignSystem.Hero)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(TaleWorlds.CampaignSystem.Hero));

        var ageItems = BuildWithKnowledge(false, logger)[1].Filters;

        foreach (var item in ageItems)
            Assert.IsFalse(item.Predicate(hero), "an unmet hero's age is hidden on the page, so no Age stage may match it");
        logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void AgePredicates_HeroWhoseDetailsAreKnown_ReachTheStageRule()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        var logger = Substitute.For<IModLogger>();
        var hero = (TaleWorlds.CampaignSystem.Hero)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(TaleWorlds.CampaignSystem.Hero));

        var ageItems = BuildWithKnowledge(true, logger)[1].Filters;

        // An uninitialised hero cannot be read, so passing the gate shows as the logged let-through.
        Assert.IsTrue(ageItems[0].Predicate(hero));
        logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void RacePredicates_DoNotAskWhetherDetailsAreKnown()
    {
        var src = TAOM.Tests.Infrastructure.RepoPaths.ReadSource("Main/Features/Encyclopedia/Adapters/EncyclopediaHeroFilterAdapter.cs");

        Assert.AreEqual(1, src.Split(new[] { "KnowsDetailsOf(hero)" }, StringSplitOptions.None).Length - 1);
    }
}
