using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client;
using TAOM.Composition;
using TAOM.Features.MapPerf.Hooks;

namespace TAOM.Tests.Features.MapPerf;

/// <summary>ADR-013, B4b. The three TAOM MapViews live in TAOM.Client, so the views scan reads core and the client assembly.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MapProfilerTargetsTests
{
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
}
