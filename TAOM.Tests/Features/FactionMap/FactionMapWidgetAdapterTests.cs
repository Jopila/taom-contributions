using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Adapters;
using TAOM.Features.FactionMap.Widgets;

namespace TAOM.Tests.Features.FactionMap;

/// <summary>ADR-013, B4a. The client adapter reports PolygonWidget's static session state to core.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FactionMapWidgetAdapterTests
{
    [TestCleanup]
    public void Cleanup() => PolygonWidget.ResetSession();

    private static void SetStatic(string property, string value) =>
        typeof(PolygonWidget).GetProperty(property, BindingFlags.Public | BindingFlags.Static)!.SetValue(null, value);

    [TestMethod]
    public void Adapter_WidgetStaticsSet_ReportsHoveredFactionAndLastClickedRegion()
    {
        SetStatic("HoveredFactionName", "gondor");
        SetStatic("LastClickedRegionName", "region_x");

        var adapter = new FactionMapWidgetAdapter();

        Assert.AreEqual("gondor", adapter.HoveredFactionName);
        Assert.AreEqual("region_x", adapter.LastClickedRegionName);
    }

    [TestMethod]
    public void ResetSession_EmptiesTheHoveredFaction()
    {
        SetStatic("HoveredFactionName", "gondor");

        new FactionMapWidgetAdapter().ResetSession();

        Assert.AreEqual("", PolygonWidget.HoveredFactionName);
    }
}
