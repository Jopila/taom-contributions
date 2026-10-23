using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.SettlementNameplateRelation;

namespace TAOM.Tests.Features.SettlementNameplateRelation;

/// <summary>ADR-013, B4b. The plate widget lives in TAOM.Client and is built by the engine, so it reads core's context.</summary>
[TestClass]
public class NameplateRelationIoCTests
{
    [TestCleanup]
    public void Cleanup() => NameplateRelationWidgetContext.Initialize(null);

    [TestMethod]
    public void InitializeWidgetStatics_GivesTheWidgetContextTheContainersSettings()
    {
        using var container = new Container();
        var settings = Substitute.For<INameplateRelationSettingsProvider>();
        container.RegisterInstance(settings);

        NameplateRelationIoC.InitializeWidgetStatics(container);

        Assert.AreSame(settings, NameplateRelationWidgetContext.Settings);
    }
}
