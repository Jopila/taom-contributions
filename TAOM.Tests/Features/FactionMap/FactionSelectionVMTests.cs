using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.FactionMap;
using TAOM.Features.FactionMap.Models;
using TAOM.Features.FactionMap.ViewModels;

namespace TAOM.Tests.Features.FactionMap;

/// <summary>
/// ADR-013, B4a. FactionSelectionVM reads the hovered faction and the clicked region from the map widgets
/// through IFactionMapWidgetAdapter, because the widgets live in TAOM.Client and core must not name them.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FactionSelectionVMTests
{
    private static (FactionSelectionVM Vm, IFactionMapWidgetAdapter Widgets, IFactionHoverService Hover, IFactionSelectionService Selection) NewVm()
    {
        var widgets = Substitute.For<IFactionMapWidgetAdapter>();
        var hover = Substitute.For<IFactionHoverService>();
        var selection = Substitute.For<IFactionSelectionService>();
        selection.SelectRegion(Arg.Any<string>()).Returns(new FactionSelectionResult());
        var vm = new FactionSelectionVM(
            _ => { }, () => { }, selection, hover,
            Substitute.For<ICultureResolverService>(), Substitute.For<ILandmarkService>(), widgets);
        return (vm, widgets, hover, selection);
    }

    [TestMethod]
    public void Tick_ReportsTheWidgetAdaptersHoveredFaction()
    {
        var (vm, widgets, hover, _) = NewVm();
        widgets.HoveredFactionName.Returns("gondor");

        vm.Tick();

        hover.Received().UpdateHover("gondor");
    }

    [TestMethod]
    public void ExecuteSelectRegion_SelectsTheWidgetAdaptersLastClickedRegion()
    {
        var (vm, widgets, _, selection) = NewVm();
        widgets.LastClickedRegionName.Returns("region_x");

        vm.ExecuteSelectRegion();

        selection.Received().SelectRegion("region_x");
    }

    [TestMethod]
    public void ExecuteSelectRegion_NoClickedRegion_SelectsNothing()
    {
        var (vm, widgets, _, selection) = NewVm();
        widgets.LastClickedRegionName.Returns("");

        vm.ExecuteSelectRegion();

        selection.DidNotReceiveWithAnyArgs().SelectRegion(default!);
    }
}
