using System.Reflection;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Features.FactionMap;
using TAOM.Features.FactionUI.FactionScreen;
using TAOM.Features.FactionMap.Hooks;
using TAOM.Tests.Features.FactionUI;

namespace TAOM.Tests.Composition;

/// <summary>
/// ADR-013, B4a. The culture-stage graph is built by FactionMapIoC.InitializePatchStatics AFTER the client
/// composition, so the real front-end resource adapter in it holds the client's sprite table, not core's null.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ClientFrontEndCompositionTests
{
    private sealed class ProbeComposition : IClientComposition
    {
        public IFrontEndSpriteTableAdapter Table { get; } = Substitute.For<IFrontEndSpriteTableAdapter>();

        public void RegisterServices(IRegistrator registrator) => registrator.RegisterInstance(Table);
    }

    private static readonly System.Type[] PatchTypes =
    {
        typeof(CultureStageView_Constructor_Patch), typeof(CultureStageView_Tick_Patch),
        typeof(CultureStageView_Finalize_Patch),
    };

    private static FieldInfo HookField(System.Type patch) =>
        patch.GetField("_hook", BindingFlags.NonPublic | BindingFlags.Static)!;

    [TestCleanup]
    public void Cleanup()
    {
        ClientComposition.Current = null;
        foreach (var patch in PatchTypes) HookField(patch).SetValue(null, null);
    }

    [TestMethod]
    public void Apply_TheCultureStageGraph_SeesTheClientSpriteTable()
    {
        var container = FactionUIWiringTests.NewContainer();
        FactionMapIoC.RegisterFactionMapFeature(container);
        // NewContainer substitutes the selection service that FactionMap registers too: keep one default.
        container.RegisterInstance(Substitute.For<IFactionSelectionService>(), IfAlreadyRegistered.Replace);
        container.Register<IFrontEndResourceAdapter, FrontEndResourceAdapter>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        var probe = new ProbeComposition();
        ClientComposition.Current = probe;

        ClientComposition.Apply(container);
        FactionMapIoC.InitializePatchStatics(container);

        var texture = new FrontEndTexture(new object(), 0);
        container.Resolve<IFrontEndResourceAdapter>().RegisterSprite("taom_x", texture, null);
        probe.Table.Received(1).RegisterSprite("taom_x", texture, null);
        Assert.AreSame<object>(container.Resolve<FactionScreenLauncher>(), container.Resolve<ICultureStageMovieOverride>());
        // The patches call `_hook?.`, which fails silently: pin that each one received its resolved hook.
        Assert.AreSame(container.Resolve<IOnCultureStageViewCreated>(), HookField(PatchTypes[0]).GetValue(null));
        Assert.AreSame(container.Resolve<IOnCultureStageViewTick>(), HookField(PatchTypes[1]).GetValue(null));
        Assert.AreSame(container.Resolve<IOnCultureStageViewFinalize>(), HookField(PatchTypes[2]).GetValue(null));
    }
}
