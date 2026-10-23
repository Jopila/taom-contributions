using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// ADR-013, B7 slice 1. The camp, refuge and supply route map visuals draw on SandBox.View's map scene, so they
/// live in TAOM.Client under their original full names; the interfaces and the nulls stay in core.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ClientMapVisualTypesTests
{
    private static readonly Assembly Client = typeof(ClientSubModule).Assembly;
    private static readonly Assembly Core = typeof(SubModule).Assembly;

    [TestInitialize]
    public void RequireGame()
    {
        if (!TAOM.Tests.Migration.GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", TAOM.Tests.Migration.GameAssemblies.Diagnostics));
    }

    private static void AssertClientOnly(string fullName)
    {
        Assert.IsNotNull(Client.GetType(fullName), fullName + " must be declared in TAOM.Client");
        Assert.IsNull(Core.GetType(fullName), fullName + " must not be declared in TAOM.dll");
    }

    private static void AssertCoreOnly(string fullName)
    {
        Assert.IsNotNull(Core.GetType(fullName), fullName + " must be declared in TAOM.dll");
        Assert.IsNull(Client.GetType(fullName), fullName + " must not be declared in TAOM.Client");
    }

    [TestMethod]
    public void TheMapVisualServices_LiveInTaomClient()
    {
        AssertClientOnly("TAOM.Features.SupplyLines.SupplyRouteVisualService");
        AssertClientOnly("TAOM.Features.FieldCamp.CampVisualService");
        AssertClientOnly("TAOM.Features.Refuge.Visuals.RefugeVisualService");
        AssertClientOnly("TAOM.Features.FieldCamp.Visuals.CampLayoutBuilder");
        AssertClientOnly("TAOM.Features.FieldCamp.Visuals.CampLayoutMath");
    }

    [TestMethod]
    public void TheMapVisualInterfacesAndNulls_LiveInCore()
    {
        AssertCoreOnly("TAOM.Features.SupplyLines.ISupplyRouteVisualService");
        AssertCoreOnly("TAOM.Features.FieldCamp.ICampVisualService");
        AssertCoreOnly("TAOM.Features.Refuge.IRefugeVisualService");
        AssertCoreOnly("TAOM.Features.SupplyLines.NullSupplyRouteVisualService");
        AssertCoreOnly("TAOM.Features.FieldCamp.NullCampVisualService");
        AssertCoreOnly("TAOM.Features.Refuge.NullRefugeVisualService");
    }
}
