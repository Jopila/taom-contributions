using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;
using TAOM.Features.FieldCamp;
using TAOM.Features.FieldCamp.Domain;
using TAOM.Features.Refuge;
using TAOM.Features.Refuge.Domain;
using TAOM.Features.SupplyLines;
using TaleWorlds.Library;

namespace TAOM.Tests.Composition;

/// <summary>
/// ADR-013, B7. With no map scene (a dedicated server, or a client where TAOM.Client did not load) the camp,
/// refuge and supply route visuals are core's nulls: Show reports "not ready" so the caller keeps retrying, and
/// every other call does nothing.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class NullMapVisualServicesTests
{
    [TestInitialize]
    public void RequireGame()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestMethod]
    public void NullCampVisualService_ShowAndIsShownReportNothingStands_RemoveAndClearAllDoNothing()
    {
        var sut = new NullCampVisualService();

        Assert.IsFalse(sut.Show("party_1", CampType.Field, default(Vec2)));
        Assert.IsFalse(sut.IsShown("party_1"));
        sut.Remove("party_1");
        sut.ClearAll();
    }

    [TestMethod]
    public void NullRefugeVisualService_ShowReportsNotReady_OtherCallsDoNothing()
    {
        var sut = new NullRefugeVisualService();

        Assert.IsFalse(sut.Show("refuge_1", RefugeTier.Stronghold, true, default(Vec2)));
        sut.Remove("refuge_1");
        sut.ClearAll();
        sut.TickWind();
    }

    [TestMethod]
    public void NullSupplyRouteVisualService_UpdateAndClearAllDoNothing()
    {
        var sut = new NullSupplyRouteVisualService();

        sut.Update();
        sut.ClearAll();
    }
}
