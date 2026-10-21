using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Arena.Hooks;

namespace TAOM.Tests.Features.Arena;

/// <summary>
/// Pins the swallow decision of Patch69_TournamentEndGuard: only the null-dereference is contained,
/// in every mode. The swallowed NRE costs only the local winner panel, and a rethrow from this event
/// (raised inside OnMissionTick) would hang the next frame.
/// </summary>
[TestClass]
public class Patch69TournamentEndGuardDecisionTests
{
    [TestMethod]
    public void ShouldSwallow_NullReference_ReturnsTrue()
    {
        Assert.IsTrue(Patch69_TournamentEndGuard.ShouldSwallow(new NullReferenceException()));
    }

    [TestMethod]
    public void ShouldSwallow_OtherException_ReturnsFalse()
    {
        Assert.IsFalse(Patch69_TournamentEndGuard.ShouldSwallow(new InvalidOperationException()));
    }
}
