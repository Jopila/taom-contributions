using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.LordBanners;

namespace TAOM.Tests.Features.LordBanners;

/// <summary>The decision matrix behind TaomBannerItemModel (docs/features/lord-banners.md).</summary>
[TestClass]
public class LordBannerServiceTests
{
    private static readonly (string Id, string? Culture, int Level)[] Items =
    {
        ("empire_1", "empire", 1),
        ("empire_2", "empire", 2),
        ("sturgia_1", "sturgia", 1),
        ("aserai_1", "aserai", 1),
        ("neutral_1", "neutral_culture", 1),
        ("neutral_2", "neutral_culture", 2),
        ("cultureless_1", null, 1),
    };

    private LordBannerService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var rows = new Dictionary<string, IReadOnlyList<string>>
        {
            ["gondor"] = new[] { "empire" },
            ["erebor"] = new[] { "sturgia", "empire" },
            ["elves"] = new string[0],
            ["sturgia"] = new[] { "empire" },
        };
        var provider = Substitute.For<ILordBannerCulturesConfigProvider>();
        provider.GetMap().Returns(new LordBannerMap(rows));
        _sut = new LordBannerService(provider);
    }

    private string[] Allowed(string? culture, int level) =>
        _sut.Allowed(culture, level, Items, i => (i.Culture, i.Level)).Select(i => i.Id).OrderBy(i => i).ToArray();

    [TestMethod]
    public void Allowed_VanillaBannerCultureWithoutRow_IsExactlyTheEnginesRule()
    {
        CollectionAssert.AreEqual(new[] { "cultureless_1", "empire_1" }, Allowed("empire", 1));
        CollectionAssert.AreEqual(new[] { "aserai_1", "cultureless_1" }, Allowed("aserai", 1));
    }

    [TestMethod]
    public void Allowed_MappedCulture_GetsItsRowAndNeutralAndCultureless()
        => CollectionAssert.AreEqual(new[] { "cultureless_1", "empire_1", "neutral_1" }, Allowed("gondor", 1));

    [TestMethod]
    public void Allowed_MappedCultureWithTwoValues_GetsBoth()
        => CollectionAssert.AreEqual(new[] { "cultureless_1", "empire_1", "neutral_1", "sturgia_1" }, Allowed("erebor", 1));

    [TestMethod]
    public void Allowed_VanillaBannerCultureWithARow_GetsItsOwnTheRowsAndNeutral()
        => CollectionAssert.AreEqual(new[] { "cultureless_1", "empire_1", "neutral_1", "sturgia_1" }, Allowed("sturgia", 1));

    [TestMethod]
    public void Allowed_UnmappedTaomCulture_GetsNeutralOnly()
        => CollectionAssert.AreEqual(new[] { "cultureless_1", "neutral_1" }, Allowed("new_culture", 1));

    [TestMethod]
    public void Allowed_CultureWithAnEmptyRow_GetsNeutralOnly()
        => CollectionAssert.AreEqual(new[] { "cultureless_1", "neutral_1" }, Allowed("elves", 1));

    [TestMethod]
    public void Allowed_NeutralCultureHero_GetsNeutral()
        => CollectionAssert.AreEqual(new[] { "cultureless_1", "neutral_1" }, Allowed("neutral_culture", 1));

    [TestMethod]
    public void Allowed_OtherLevelIsExcluded()
    {
        CollectionAssert.AreEqual(new[] { "empire_2", "neutral_2" }, Allowed("gondor", 2));
        CollectionAssert.AreEqual(new string[0], Allowed("gondor", 3));
    }

    [TestMethod]
    public void Allowed_NullHeroCulture_GetsCulturelessItemsOnly()
    {
        CollectionAssert.AreEqual(new[] { "cultureless_1" }, Allowed(null, 1));
        CollectionAssert.AreEqual(new[] { "cultureless_1" }, Allowed("  ", 1));
    }

    [TestMethod]
    public void Allowed_CultureIdsAreNormalized()
        => CollectionAssert.AreEqual(new[] { "cultureless_1", "empire_1", "neutral_1" }, Allowed(" Gondor ", 1));

    [TestMethod]
    public void Allowed_ItemWithoutBannerComponent_LevelZeroNeverMatches()
    {
        var odd = new[] { ("not_a_banner", (string?)"empire", 0) };

        var result = _sut.Allowed("gondor", 1, odd, i => (i.Item2, i.Item3));

        Assert.AreEqual(0, result.Count);
    }
}
