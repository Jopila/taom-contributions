using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.HallOfFire;

namespace TAOM.Tests.Features.HallOfFire;

/// <summary>The tag rewrite behind TaomMapConversationDataProvider (docs/features/hall-of-fire.md).</summary>
[TestClass]
public class HallOfFireServiceTests
{
    private HallOfFireService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var cultures = new Dictionary<string, string>
        {
            ["gondor"] = "empire",
            ["erebor"] = "sturgia",
            ["umbar"] = "aserai",
            ["empire"] = "battania",
            ["battania"] = "khuzait",
        };
        var provider = Substitute.For<IHallOfFireConfigProvider>();
        provider.GetMap().Returns(new HallOfFireMap(cultures));
        _sut = new HallOfFireService(provider);
    }

    [DataTestMethod]
    [DataRow("conv_gondor_town_night_0", "conv_empire_town_night_0")]
    [DataRow("conv_gondor_town_sunset_0", "conv_empire_town_sunset_0")]
    [DataRow("conv_gondor_town_noon_0", "conv_empire_town_noon_0")]
    [DataRow("conv_gondor_tavern_0", "conv_empire_tavern_0")]
    [DataRow("conv_gondor_lordshall_0", "conv_empire_lordshall_0")]
    [DataRow("conv_gondor_shipyard_noon_0", "conv_empire_shipyard_noon_0")]
    public void Resolve_MappedCulture_SwapsThePrefixForTheVanillaSet(string tag, string expected)
        => Assert.AreEqual(expected, _sut.Resolve(tag, "gondor"));

    [TestMethod]
    public void Resolve_UnmappedCulture_ReturnsTheTagUnchanged()
        => Assert.AreEqual("conv_rohirrim_town_noon_0", _sut.Resolve("conv_rohirrim_town_noon_0", "rohirrim"));

    [TestMethod]
    public void Resolve_VanillaIdWithNoRow_ReturnsTheTagUnchanged()
        => Assert.AreEqual("conv_vlandia_town_noon_0", _sut.Resolve("conv_vlandia_town_noon_0", "vlandia"));

    [TestMethod]
    public void Resolve_NullCulture_ReturnsTheTagUnchanged()
        => Assert.AreEqual("conv_plains_noon_0", _sut.Resolve("conv_plains_noon_0", null));

    [TestMethod]
    public void Resolve_EmptyCulture_ReturnsTheTagUnchanged()
        => Assert.AreEqual("conv_plains_noon_0", _sut.Resolve("conv_plains_noon_0", "  "));

    [DataTestMethod]
    [DataRow("snow")]
    [DataRow("desert")]
    [DataRow("steppe")]
    [DataRow("forest")]
    [DataRow("plains")]
    public void Resolve_WildernessTagOfAHideout_ReturnsTheTagUnchanged(string terrain)
    {
        var tag = $"conv_{terrain}_noon_0";

        Assert.AreEqual(tag, _sut.Resolve(tag, "gondor"));
    }

    [TestMethod]
    public void Resolve_NullTag_ReturnsNull()
        => Assert.IsNull(_sut.Resolve(null, "gondor"));

    [TestMethod]
    public void Resolve_CultureIdWithCaseAndWhitespace_LooksUpNormalizedAndMatchesThePrefixAsWritten()
    {
        Assert.AreEqual("conv_empire_tavern_0", _sut.Resolve("conv_ Gondor _tavern_0", " Gondor "));
        Assert.AreEqual("conv_empire_tavern_0", _sut.Resolve("conv_Gondor_tavern_0", "Gondor"));
    }

    [TestMethod]
    public void Resolve_PrefixWrittenInAnotherCase_ReturnsTheTagUnchanged()
        => Assert.AreEqual("conv_gondor_tavern_0", _sut.Resolve("conv_gondor_tavern_0", "Gondor"));

    [TestMethod]
    public void Resolve_CultureIdThatIsOnlyAPrefixOfTheTagCulture_ReturnsTheTagUnchanged()
        => Assert.AreEqual("conv_gondor_soldiers_town_noon_0", _sut.Resolve("conv_gondor_soldiers_town_noon_0", "gondor_soldiers"));

    [TestMethod]
    public void Resolve_RemappedVanillaId_SwapsOnceAndDoesNotChain()
    {
        Assert.AreEqual("conv_battania_town_noon_0", _sut.Resolve("conv_empire_town_noon_0", "empire"));
        Assert.AreEqual("conv_khuzait_town_noon_0", _sut.Resolve("conv_battania_town_noon_0", "battania"));
    }
}
