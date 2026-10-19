using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TaomBuildings;

namespace TAOM.Tests.Features.TaomBuildings;

[TestClass]
public class TaomBuildingCatalogTests
{
    [TestMethod]
    public void CanAdd_OnlyMatchingVariant()
    {
        var catalog = TaomBuildingFixtures.Catalog();

        Assert.AreEqual(true, catalog.CanAdd(TaomBuildingFixtures.ElfId, "lindon"));
        Assert.AreEqual(false, catalog.CanAdd(TaomBuildingFixtures.DefaultId, "lindon"));
        Assert.AreEqual(true, catalog.CanAdd(TaomBuildingFixtures.DefaultId, "mordor"), "an unlisted culture gets the default");
        Assert.AreEqual(false, catalog.CanAdd(TaomBuildingFixtures.ElfId, "mordor"));
        Assert.AreEqual(true, catalog.CanAdd(TaomBuildingFixtures.DefaultId, null), "no culture is an unlisted culture");
    }

    [TestMethod]
    public void NonTaomId_ReturnsNull()
    {
        var catalog = TaomBuildingFixtures.Catalog();

        Assert.IsNull(catalog.CanAdd("building_settlement_barracks", "lindon"));
        Assert.IsNull(catalog.SpriteFor("building_settlement_barracks", false));
        Assert.IsNull(catalog.LevelText("building_settlement_barracks", 1));
    }

    [TestMethod]
    public void Sprite_SmallVariant_AppendsT()
    {
        var catalog = TaomBuildingFixtures.Catalog();

        Assert.AreEqual("taom_building_test_wall", catalog.SpriteFor(TaomBuildingFixtures.ElfId, false));
        Assert.AreEqual("taom_building_test_wall_t", catalog.SpriteFor(TaomBuildingFixtures.ElfId, true));
    }

    [TestMethod]
    public void LevelText_Level0And4_Null()
    {
        var catalog = TaomBuildingFixtures.Catalog();

        Assert.IsNull(catalog.LevelText(TaomBuildingFixtures.DefaultId, 0));
        Assert.IsNull(catalog.LevelText(TaomBuildingFixtures.DefaultId, 4));
        Assert.AreEqual("20", catalog.LevelText(TaomBuildingFixtures.DefaultId, 2)!.Value);
    }

    [TestMethod]
    public void DuplicateIds_Rejected()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new TaomBuildingCatalog(new[] { TaomBuildingFixtures.Wall(), TaomBuildingFixtures.Wall() }));
    }

    [TestMethod]
    public void TwoFamilies_ClaimingOneVariantId_Rejected()
    {
        var other = TaomBuildingFixtures.Wall() with { FamilyId = "other_family" };

        Assert.ThrowsException<ArgumentException>(() => new TaomBuildingCatalog(new[] { TaomBuildingFixtures.Wall(), other }));
    }

    [DataTestMethod]
    [DataRow(2)]
    [DataRow(4)]
    public void Definition_WithoutExactlyThreeCosts_Rejected(int count)
    {
        var bad = TaomBuildingFixtures.Wall() with { Costs = new int[count] };

        Assert.ThrowsException<ArgumentException>(() => new TaomBuildingCatalog(new[] { bad }));
    }

    [TestMethod]
    public void VariantFor_PicksTheCultureVariant_OrTheDefault()
    {
        var catalog = TaomBuildingFixtures.Catalog();

        Assert.AreEqual(TaomBuildingFixtures.ElfId, catalog.VariantFor(TaomBuildingFixtures.Family, "rivendell"));
        Assert.AreEqual(TaomBuildingFixtures.DefaultId, catalog.VariantFor(TaomBuildingFixtures.Family, "gondor"));
        Assert.IsNull(catalog.VariantFor("unknown_family", "gondor"));
    }
}
