using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.CompanionTactics.BattleActionBar;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>
/// ADR-013, B4c. The mission views are client-only; core's default factories add nothing, so a dedicated server
/// (or a client without TAOM.Client) gets no view behavior.
/// </summary>
[TestClass]
public class NullMissionViewFactoriesTests
{
    [TestMethod]
    public void NullAutonomousMovementControllerFactory_Create_ReturnsNull() =>
        Assert.IsNull(new NullAutonomousMovementControllerFactory().Create());

    [TestMethod]
    public void NullBattleActionBarViewFactory_Create_ReturnsNull() =>
        Assert.IsNull(new NullBattleActionBarViewFactory().Create());
}
