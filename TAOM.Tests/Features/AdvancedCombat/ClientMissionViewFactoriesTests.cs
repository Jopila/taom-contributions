using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.AdvancedCombat;
using TAOM.Client.Features.CompanionTactics;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>ADR-013, B4c. Each client factory builds a fresh view per mission, as the old `new` did.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ClientMissionViewFactoriesTests
{
    // The controller's constructor reads Vec3.Zero, which needs the engine assemblies.
    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) =>
        TAOM.Tests.Migration.GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void AutonomousMovementControllerFactory_Create_ReturnsANewControllerEachCall()
    {
        var factory = new AutonomousMovementControllerFactory();

        var first = factory.Create();
        var second = factory.Create();

        Assert.IsNotNull(first);
        Assert.AreEqual("AutonomousMovementPlayerController", first!.GetType().Name);
        Assert.AreNotSame(first, second);
    }

    [TestMethod]
    public void BattleActionBarViewFactory_Create_ReturnsANewViewEachCall()
    {
        var factory = new BattleActionBarViewFactory();

        var first = factory.Create();
        var second = factory.Create();

        Assert.IsNotNull(first);
        Assert.AreEqual("BattleActionBarMissionView", first!.GetType().Name);
        Assert.AreNotSame(first, second);
    }
}
