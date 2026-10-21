using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.HallOfFire;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.HallOfFire;

/// <summary>
/// <c>MapConversationTableau.FirstTimeInit</c> calls the provider once per conversation, and again after the partner's
/// equipment changes; <c>OnTick</c> retries <c>FirstTimeInit</c> every tick until it completes, so a throw here would
/// repeat every tick. The wrapper therefore never throws after the inner call. Data is built with the public
/// <c>CreateFrom</c>; a settlement is an uninitialized instance with only its culture id set.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class TaomMapConversationDataProviderTests
{
    [ClassInitialize]
    public static void Init(TestContext _) => GameAssemblies.EnsureLoaded();

    private static MapConversationTableauData NoSettlementData() =>
        (MapConversationTableauData)FormatterServices.GetUninitializedObject(typeof(MapConversationTableauData));

    private static MapConversationTableauData DataFor(string cultureId)
    {
        var settlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
        var culture = (CultureObject)FormatterServices.GetUninitializedObject(typeof(CultureObject));
        culture.StringId = cultureId;
        settlement.Culture = culture;
        return MapConversationTableauData.CreateFrom(default, default, TerrainType.Plain, 12f, false, settlement, "center", false, false);
    }

    private static HallOfFireService ServiceWith(IHallOfFireConfigProvider config) => new HallOfFireService(config);

    [TestMethod]
    public void GetAtmosphereNameFromData_AMappedCultureSettlement_ReturnsTheVanillaSetTag()
    {
        var data = DataFor("gondor");
        var inner = Substitute.For<IMapConversationDataProvider>();
        inner.GetAtmosphereNameFromData(data).Returns("conv_gondor_town_noon_0");
        var config = Substitute.For<IHallOfFireConfigProvider>();
        config.GetMap().Returns(HallOfFireMap.Default());
        var sut = new TaomMapConversationDataProvider(inner, ServiceWith(config), Substitute.For<IModLogger>());

        Assert.AreEqual("conv_empire_town_noon_0", sut.GetAtmosphereNameFromData(data));
    }

    [TestMethod]
    public void GetAtmosphereNameFromData_WhenTheConfigThrows_ReturnsTheInnerTagAndLogsOnce()
    {
        var data = DataFor("gondor");
        var inner = Substitute.For<IMapConversationDataProvider>();
        inner.GetAtmosphereNameFromData(data).Returns("conv_gondor_town_noon_0");
        var config = Substitute.For<IHallOfFireConfigProvider>();
        config.GetMap().Returns(_ => throw new System.InvalidOperationException("bad map"));
        var logger = Substitute.For<IModLogger>();
        var sut = new TaomMapConversationDataProvider(inner, ServiceWith(config), logger);

        var first = sut.GetAtmosphereNameFromData(data);
        var second = sut.GetAtmosphereNameFromData(data);

        Assert.AreEqual("conv_gondor_town_noon_0", first);
        Assert.AreEqual("conv_gondor_town_noon_0", second);
        logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void GetAtmosphereNameFromData_NoSettlement_ReturnsTheInnerTagOnce()
    {
        var data = NoSettlementData();
        var inner = Substitute.For<IMapConversationDataProvider>();
        inner.GetAtmosphereNameFromData(data).Returns("conv_plains_noon_0");
        var config = Substitute.For<IHallOfFireConfigProvider>();
        config.GetMap().Returns(HallOfFireMap.Default());
        var sut = new TaomMapConversationDataProvider(inner, new HallOfFireService(config), Substitute.For<IModLogger>());

        var tag = sut.GetAtmosphereNameFromData(data);

        Assert.AreEqual("conv_plains_noon_0", tag);
        inner.Received(1).GetAtmosphereNameFromData(data);
        Assert.AreSame(inner, sut.Inner);
    }

    [TestMethod]
    public void GetAtmosphereNameFromData_InnerThrows_PropagatesBecauseTheCallIsOutsideTheGuard()
    {
        var data = NoSettlementData();
        var inner = Substitute.For<IMapConversationDataProvider>();
        inner.GetAtmosphereNameFromData(data).Returns(_ => throw new System.InvalidOperationException("vanilla"));
        var sut = new TaomMapConversationDataProvider(inner,
            new HallOfFireService(Substitute.For<IHallOfFireConfigProvider>()), Substitute.For<IModLogger>());

        Assert.ThrowsException<System.InvalidOperationException>(() => sut.GetAtmosphereNameFromData(data));
    }
}
