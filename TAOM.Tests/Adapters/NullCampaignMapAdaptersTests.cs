using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SandBox.View.Map;
using TAOM.Adapters;
using TAOM.Features.FiefManagement;
using TAOM.Features.HallOfFire;
using TAOM.Features.WarOfTheRingMomentum.UI;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Adapters;

/// <summary>ADR-013, B4b. The null seams core keeps registered where TAOM.Client is absent (a dedicated server).</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class NullCampaignMapAdaptersTests
{
    [ClassInitialize]
    public static void Init(TestContext _) => GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void NullFiefsNavigationElementFactory_LeavesTheBarAsVanillaBuiltIt()
        => Assert.IsNull(new NullFiefsNavigationElementFactory().Create(null!));

    [TestMethod]
    public void NullMapConversationProviderWrapper_WrapsNothing_AndRecognizesNothing()
    {
        var vanilla = Substitute.For<IMapConversationDataProvider>();
        var sut = new NullMapConversationProviderWrapper();

        Assert.IsNull(sut.Wrap(vanilla));
        Assert.IsFalse(sut.IsWrapper(vanilla));
    }

    [TestMethod]
    public void TheAttachAdaptersAndTheMeterService_DoNothing()
    {
        new NullRealmBordersMapViewAdapter().EnsureAttached();
        new NullFieldCampOverlayAdapter().EnsureAttached();
        var meter = new NullMomentumUIService();
        meter.AddMapMeter();
        meter.RemoveMapMeter();
    }
}
