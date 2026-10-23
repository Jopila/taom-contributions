using System.Reflection;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.FieldCamp;
using TAOM.Features.FieldCamp.Hooks;

namespace TAOM.Tests.Features.FieldCamp;

/// <summary>ADR-013, B4b. The behavior is built with new, so its overlay adapter arrives through a static set after the client composition.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FieldCampIoCTests
{
    [TestCleanup]
    public void Cleanup()
    {
        FieldCampCampaignBehavior.Overlay = new NullFieldCampOverlayAdapter();
        typeof(PartyNameplateCampIconPatch).GetField("_campService", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, null);
    }

    [TestMethod]
    public void InitializePatchStatics_GivesTheBehaviorTheContainersOverlayAdapter()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<ICampService>());
        var overlay = Substitute.For<IFieldCampOverlayAdapter>();
        container.RegisterInstance(overlay);

        FieldCampIoC.InitializePatchStatics(container);

        Assert.AreSame(overlay, FieldCampCampaignBehavior.Overlay);
    }
}
