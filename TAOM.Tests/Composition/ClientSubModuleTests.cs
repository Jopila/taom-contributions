using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client;
using TAOM.Composition;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Composition;

/// <summary>The client submodule hands core its composition from its constructor (ADR-013).</summary>
[TestClass]
public class ClientSubModuleTests
{
    [ClassInitialize]
    public static void Init(TestContext _) => GameAssemblies.EnsureLoaded();

    [TestCleanup]
    public void Cleanup() => ClientComposition.Current = null;

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Constructor_SetsTheClientCompositionSlot()
    {
        ClientComposition.Current = null;

        _ = new ClientSubModule();

        Assert.IsInstanceOfType(ClientComposition.Current, typeof(TaomClientComposition));
    }

    // Each move batch (B4a to B5) adds its rows here. This is the container round trip: DryIoc's
    // constructor selection fails at Register time, which no other test exercises for the client.
    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TaomClientComposition_RegisterServices_RegistersExactlyTheExpectedServices()
    {
        var container = new Container();
        ClientComposition.Current = new TaomClientComposition();

        ClientComposition.Apply(container);

        var expected = new[]
        {
            typeof(TAOM.Adapters.IFactionMapWidgetAdapter),
            typeof(TAOM.Adapters.IFrontEndSpriteTableAdapter),
            typeof(TAOM.Features.CustomBattles.ICustomBattleFactionSelectionFactory),
            typeof(TAOM.Features.WarOfTheRingMomentum.UI.IMomentumUIService),
            typeof(TAOM.Adapters.IRealmBordersMapViewAdapter),
            typeof(TAOM.Adapters.IFieldCampOverlayAdapter),
            typeof(TAOM.Features.FiefManagement.IFiefsNavigationElementFactory),
            typeof(TAOM.Features.HallOfFire.IMapConversationProviderWrapper),
            typeof(TAOM.Features.AdvancedCombat.IAutonomousMovementControllerFactory),
            typeof(TAOM.Features.CompanionTactics.BattleActionBar.IBattleActionBarViewFactory),
            typeof(TAOM.Features.HostReview.IHostReviewOrderProviderRegistrar),
            typeof(TAOM.Features.ShaderPrecompilation.IShaderPrecompileGameManagerFactory),
            typeof(TAOM.Features.ShaderCompileNotice.IShaderCompileNoticeWindowHost),
        };
        var actual = container.GetServiceRegistrations().Select(r => r.ServiceType).ToArray();
        CollectionAssert.AreEquivalent(expected, actual);
    }
}
