using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FieldCamp;
using TAOM.Features.FieldCamp.Hooks;
using TAOM.Features.SupplyLines;

namespace TAOM.Tests.Features.FieldCamp;

/// <summary>ADR-013, B4b. The tick asks the overlay adapter to attach the map view, then ticks the camps.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FieldCampOverlayTickTests
{
    [TestCleanup]
    public void Cleanup() => FieldCampCampaignBehavior.Overlay = new NullFieldCampOverlayAdapter();

    [TestMethod]
    public void OnTick_AsksTheOverlayToAttach_ThenTicksTheCamps()
    {
        var camps = Substitute.For<ICampService>();
        var overlay = Substitute.For<IFieldCampOverlayAdapter>();
        FieldCampCampaignBehavior.Overlay = overlay;
        var sut = new FieldCampCampaignBehavior(
            camps,
            Substitute.For<ICampSettingsProvider>(),
            Substitute.For<ICampVisualService>(),
            Substitute.For<ISupplyLinesSettingsProvider>(),
            Substitute.For<IGameMenuAdapter>(),
            Substitute.For<IModLogger>());

        sut.OnTick(0f);

        Received.InOrder(() =>
        {
            overlay.EnsureAttached();
            camps.FrameTick();
        });
    }
}
