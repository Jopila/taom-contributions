using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Arena.Hooks;

namespace TAOM.Tests.Features.Arena;

/// <summary>
/// Pins the swallow decision of Patch69_TournamentEndGuard. Singleplayer keeps containing the
/// null-dereference; under co-op the exception is returned so the failure is not hidden behind a
/// half-drawn tournament screen (#491).
/// </summary>
[TestClass]
public class Patch69TournamentEndGuardDecisionTests
{
    [TestMethod]
    public void ShouldSwallow_NullReference_NoCoop_ReturnsTrue()
    {
        Assert.IsTrue(Patch69_TournamentEndGuard.ShouldSwallow(new NullReferenceException(), coopActive: false));
    }

    [TestMethod]
    public void ShouldSwallow_NullReference_CoopActive_ReturnsFalse()
    {
        Assert.IsFalse(Patch69_TournamentEndGuard.ShouldSwallow(new NullReferenceException(), coopActive: true));
    }

    [TestMethod]
    public void ShouldSwallow_OtherException_NeverSwallowed()
    {
        Assert.IsFalse(Patch69_TournamentEndGuard.ShouldSwallow(new InvalidOperationException(), coopActive: false));
        Assert.IsFalse(Patch69_TournamentEndGuard.ShouldSwallow(new InvalidOperationException(), coopActive: true));
    }
}
