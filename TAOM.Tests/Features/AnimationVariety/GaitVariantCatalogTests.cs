using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AnimationVariety;

namespace TAOM.Tests.Features.AnimationVariety;

[TestClass]
public class GaitVariantCatalogTests
{
    private sealed class Rig
    {
        public readonly IGaitVarietyAdapter Engine = Substitute.For<IGaitVarietyAdapter>();
        public readonly GaitVariantCatalog Catalog;

        // The engine lists every loaded action set by name; "as_human_warrior" is always among them.
        public Rig(params string[] existing)
        {
            var loaded = new List<string> { "as_human_warrior", "as_orc_warrior" };
            loaded.AddRange(existing);
            Engine.LoadedActionSetNames().Returns(loaded);
            Catalog = new GaitVariantCatalog(Engine);
        }
    }

    [TestMethod]
    public void BaseSets_Listed_AreTheThreeSetsASoldierCanSpawnOn()
    {
        // as_orc_female_warrior is not here: the Armory orc Monster declares no female_action_set, so a
        // female orc spawns on as_orc_warrior and a variant row for the female set would be unreachable.
        CollectionAssert.AreEquivalent(
            new[] { "as_orc_warrior", "as_goblin_warrior", "as_goblin_female_warrior" },
            GaitVariantCatalog.BaseSets.ToArray());
    }

    [DataTestMethod]
    [DataRow("as_orc_warrior", true)]
    [DataRow("as_goblin_warrior", true)]
    [DataRow("as_goblin_female_warrior", true)]
    [DataRow("as_orc_female_warrior", false)]
    [DataRow("as_human_warrior", false)]
    [DataRow("as_orc_warrior_b", false)]
    [DataRow("AS_ORC_WARRIOR", false)]
    [DataRow("as_orc_villager", false)]
    [DataRow("as_troll_warrior", false)]
    [DataRow("", false)]
    [DataRow(null, false)]
    public void IsBaseSet_GivenAName_AcceptsOnlyTheThreeExactNames(string? name, bool expected)
    {
        Assert.AreEqual(expected, new Rig().Catalog.IsBaseSet(name!));
    }

    [TestMethod]
    public void ConfiguredVariants_OfABaseSet_AreItsBAndCSetsAndNeverAskTheEngine()
    {
        var rig = new Rig();

        CollectionAssert.AreEqual(new[] { "as_goblin_female_warrior_b", "as_goblin_female_warrior_c" },
            rig.Catalog.ConfiguredVariants("as_goblin_female_warrior").ToArray());
        rig.Engine.DidNotReceive().LoadedActionSetNames();
    }

    [DataTestMethod]
    [DataRow("as_human_warrior")]
    [DataRow("as_orc_female_warrior")]
    [DataRow(null)]
    public void ConfiguredVariants_OfANonBaseSet_AreEmpty(string? name)
    {
        Assert.AreEqual(0, new Rig().Catalog.ConfiguredVariants(name!).Count);
    }

    [TestMethod]
    public void VariantExists_NameIsLoaded_IsTrue()
    {
        var rig = new Rig("as_orc_warrior_b");

        Assert.IsTrue(rig.Catalog.VariantExists("as_orc_warrior_b"));
        Assert.IsFalse(rig.Catalog.VariantExists("as_orc_warrior_c"));
    }

    [TestMethod]
    public void VariantExists_DifferentCase_IsFalse()
    {
        var rig = new Rig("AS_ORC_WARRIOR_B");

        Assert.IsFalse(rig.Catalog.VariantExists("as_orc_warrior_b"));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void VariantExists_NullOrEmptyId_IsFalseEvenWhenTheEngineListsAnEmptyName(string? id)
    {
        var rig = new Rig(null!, "");

        Assert.IsFalse(rig.Catalog.VariantExists(id!));
    }

    [TestMethod]
    public void VariantExists_ListsTheEngineSetsOncePerProcess()
    {
        var rig = new Rig("as_orc_warrior_b");

        for (int i = 0; i < 5; i++)
        {
            rig.Catalog.VariantExists("as_orc_warrior_b");
            rig.Catalog.VariantExists("as_orc_warrior_c");
            rig.Catalog.AvailableSets("as_goblin_warrior");
        }

        rig.Engine.Received(1).LoadedActionSetNames();
    }

    [TestMethod]
    public void AvailableSets_NeverAsksTheEngineForASingleId()
    {
        // The listing is the only engine question: a per-id probe makes native GetIndexWithID log
        // "Action set could not be found" for every miss, which reads as a broken base set.
        var rig = new Rig();

        rig.Catalog.AvailableSets("as_orc_warrior");

        rig.Engine.Received(1).LoadedActionSetNames();
        Assert.AreEqual(1, rig.Engine.ReceivedCalls().Count());
    }

    [TestMethod]
    public void AvailableSets_NeverWarns()
    {
        // The catalog has no logger to warn with; the INFO line of GaitVarietyService reports =present/=missing.
        var constructors = typeof(GaitVariantCatalog).GetConstructors();

        Assert.AreEqual(1, constructors.Length);
        CollectionAssert.AreEqual(new[] { typeof(IGaitVarietyAdapter) },
            constructors[0].GetParameters().Select(p => p.ParameterType).ToArray());
    }

    [TestMethod]
    public void AvailableSets_EveryVariantLoaded_ListsTheBaseFirstThenBThenC()
    {
        var rig = new Rig("as_orc_warrior_c", "as_orc_warrior_b");

        CollectionAssert.AreEqual(new[] { "as_orc_warrior", "as_orc_warrior_b", "as_orc_warrior_c" },
            rig.Catalog.AvailableSets("as_orc_warrior").ToArray());
    }

    [TestMethod]
    public void AvailableSets_OneVariantMissing_SkipsIt()
    {
        var rig = new Rig("as_orc_warrior_c");

        CollectionAssert.AreEqual(new[] { "as_orc_warrior", "as_orc_warrior_c" },
            rig.Catalog.AvailableSets("as_orc_warrior").ToArray());
    }

    [TestMethod]
    public void AvailableSets_NoVariantLoaded_IsJustTheBase()
    {
        CollectionAssert.AreEqual(new[] { "as_goblin_warrior" },
            new Rig().Catalog.AvailableSets("as_goblin_warrior").ToArray());
    }

    [TestMethod]
    public void AvailableSets_CalledTwice_ReturnsTheCachedList()
    {
        var rig = new Rig("as_orc_warrior_b");

        Assert.AreSame(rig.Catalog.AvailableSets("as_orc_warrior"), rig.Catalog.AvailableSets("as_orc_warrior"));
    }

    [TestMethod]
    public void AvailableSets_NonBaseSet_IsJustThatSetAndNeverListsTheEngine()
    {
        var rig = new Rig();

        CollectionAssert.AreEqual(new[] { "as_human_warrior" }, rig.Catalog.AvailableSets("as_human_warrior").ToArray());
        rig.Engine.DidNotReceive().LoadedActionSetNames();
    }
}
