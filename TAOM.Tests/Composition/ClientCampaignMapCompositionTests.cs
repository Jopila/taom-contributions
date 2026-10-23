using System;
using System.IO;
using System.Reflection;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SandBox.View.Map;
using TAOM.Adapters;
using TAOM.Client;
using TAOM.Client.Adapters;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Execution;
using TAOM.Features.CoopInterop;
using TAOM.Features.FieldCamp;
using TAOM.Features.FieldCamp.Hooks;
using TAOM.Features.HallOfFire;
using TAOM.Features.RealmBorders;
using TAOM.Features.RealmBorders.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Composition;

/// <summary>
/// ADR-013, B4b. Three consumers capture their client seam at different times; this builds the graph the way
/// IoC.Configure does (core registrations, the client composition, then the patch statics) and pins that each
/// consumer ends up holding the client's adapter, not core's null.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ClientCampaignMapCompositionTests
{
    [ClassInitialize]
    public static void Init(TestContext _) => GameAssemblies.EnsureLoaded();

    [TestCleanup]
    public void Cleanup()
    {
        ClientComposition.Current = null;
        FieldCampCampaignBehavior.Overlay = new NullFieldCampOverlayAdapter();
        typeof(PartyNameplateCampIconPatch).GetField("_campService", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, null);
    }

    [TestMethod]
    public void Apply_TheConsumersThatCaptureLate_GetTheClientAdapters()
    {
        using var container = new Container();
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Main", "_Module", "ModuleData")));
        container.RegisterInstance(paths);
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IAlignmentService>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        new RealmBordersModule().RegisterServices(container);
        var hallOfFire = new HallOfFireModule();
        hallOfFire.RegisterServices(container);
        var slot = Substitute.For<IMapConversationProviderSlot>();
        var vanilla = Substitute.For<IMapConversationDataProvider>();
        slot.Current.Returns(vanilla);
        container.RegisterInstance(slot, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        container.Register<IFieldCampOverlayAdapter, NullFieldCampOverlayAdapter>(Reuse.Singleton);
        container.RegisterInstance(Substitute.For<ICampService>());
        ClientComposition.Current = new TaomClientComposition();

        ClientComposition.Apply(container);
        FieldCampIoC.InitializePatchStatics(container);

        Assert.IsInstanceOfType(FieldCampCampaignBehavior.Overlay, typeof(FieldCampOverlayAdapter));
        var realm = container.Resolve<RealmBordersCampaignBehavior>();
        var held = typeof(RealmBordersCampaignBehavior).GetField("_mapView", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(realm);
        Assert.IsInstanceOfType(held, typeof(RealmBordersMapViewAdapter));
        hallOfFire.OnPhase(ApplyPhase.MainMenu, container);
        slot.Received(1).Set(NSubstitute.Arg.Any<IMapConversationDataProvider>());
    }
}
