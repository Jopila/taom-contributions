using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SandBox.View.Map;
using TAOM.Client.Features.HallOfFire;
using TAOM.Core.Logging;
using TAOM.Features.HallOfFire;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.HallOfFire;

/// <summary>ADR-013, B4b. The client wrapper builds and recognizes the TAOM conversation provider.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class HallOfFireConversationWrapperTests
{
    [ClassInitialize]
    public static void Init(TestContext _) => GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void Wrap_BuildsTheTaomProviderAroundTheVanillaOne_AndRecognizesOnlyIt()
    {
        var config = Substitute.For<IHallOfFireConfigProvider>();
        config.GetMap().Returns(HallOfFireMap.Default());
        var sut = new HallOfFireConversationWrapper(new HallOfFireService(config), Substitute.For<IModLogger>());
        var vanilla = Substitute.For<IMapConversationDataProvider>();

        var wrapped = sut.Wrap(vanilla);

        Assert.IsInstanceOfType(wrapped, typeof(TaomMapConversationDataProvider));
        Assert.AreSame(vanilla, ((TaomMapConversationDataProvider)wrapped!).Inner);
        Assert.IsTrue(sut.IsWrapper(wrapped!));
        Assert.IsFalse(sut.IsWrapper(vanilla));
    }
}
