using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client;
using TAOM.Composition;
using System.Reflection;
using TAOM.Features.Enlistment;
using TAOM.Features.MapPerf.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MapPerf;

/// <summary>ADR-013, B4b. The three TAOM MapViews live in TAOM.Client, so the views scan reads core and the client assembly.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MapProfilerTargetsTests
{
    [ClassInitialize]
    public static void Init(TestContext _) => GameAssemblies.EnsureLoaded();

    [TestCleanup]
    public void Cleanup() => ClientComposition.Current = null;

    [TestMethod]
    public void ViewAssemblies_SlotFilled_ScansCoreThenClient()
    {
        ClientComposition.Current = new TaomClientComposition();

        var assemblies = MapProfilerTargets.ViewAssemblies().ToArray();

        CollectionAssert.AreEqual(new[] { typeof(MapProfilerTargets).Assembly, typeof(ClientSubModule).Assembly }, assemblies);
    }

    [TestMethod]
    public void ViewAssemblies_EmptySlot_ScansCoreOnly()
    {
        ClientComposition.Current = null;

        var assemblies = MapProfilerTargets.ViewAssemblies().ToArray();

        CollectionAssert.AreEqual(new[] { typeof(MapProfilerTargets).Assembly }, assemblies);
    }

    [TestMethod]
    public void MenuTargets_ResolveToTheThreeMenuProbeMethods_InOrder()
    {
        var targets = MapProfilerTargets.MenuTargets();

        Assert.AreEqual(3, targets.Count);
        Assert.IsTrue(targets.All(t => t != null), "A menu probe target did not resolve.");
        Assert.AreEqual("GameMenuVM.OnFrameTick", targets[0]!.DeclaringType!.Name + "." + targets[0]!.Name);
        Assert.AreEqual(0, targets[0]!.GetParameters().Length);
        Assert.AreEqual("GameMenuVM.set_ContextText", targets[1]!.DeclaringType!.Name + "." + targets[1]!.Name);
        CollectionAssert.AreEqual(new[] { typeof(string) }, targets[1]!.GetParameters().Select(p => p.ParameterType).ToArray());
        Assert.AreEqual(typeof(ServiceStatusService).GetMethod(nameof(ServiceStatusService.RefreshIfChanged)), targets[2]);
        Assert.AreEqual(0, targets[2]!.GetParameters().Length);
    }
}
