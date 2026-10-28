using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.TimeAcceleration;
using TAOM.Features.TimeAcceleration.Hooks;

namespace TAOM.Tests.Features.TimeAcceleration;

/// <summary>
/// Patch124 (#1040): while the turbo latch is open, a released map click is not a v1.5.6 parley click.
/// The postfix is called directly, so no game objects are needed.
/// </summary>
[TestClass]
public class TurboClickPatchTests
{
    private ITimeAccelerationService _service = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = Substitute.For<ITimeAccelerationService>();
        _logger = Substitute.For<IModLogger>();
        MapScreen_IsParleyClickReleased_TurboClick_Patch.Initialize(_service, _logger);
    }

    [TestCleanup]
    public void Cleanup() => MapScreen_IsParleyClickReleased_TurboClick_Patch.Initialize(null, null);

    private static bool Run(bool vanilla)
    {
        var result = vanilla;
        MapScreen_IsParleyClickReleased_TurboClick_Patch.Postfix(ref result);
        return result;
    }

    [TestMethod]
    public void Postfix_TurboActive_TurnsAParleyClickIntoAnOrdinaryClick()
    {
        _service.IsTurboActive.Returns(true);
        Assert.IsFalse(Run(true));
    }

    [TestMethod]
    public void Postfix_TurboInactive_KeepsVanillasParley()
    {
        _service.IsTurboActive.Returns(false);
        Assert.IsTrue(Run(true));
    }

    [TestMethod]
    public void Postfix_VanillaFalse_StaysFalse_AndReadsNothing()
    {
        _service.IsTurboActive.Returns(true);
        Assert.IsFalse(Run(false));
        _ = _service.DidNotReceive().IsTurboActive;
    }

    [TestMethod]
    public void Postfix_ServiceFaults_KeepsVanillasAnswer_AndLogsOnce()
    {
        _service.IsTurboActive.Returns(_ => throw new InvalidOperationException("boom"));
        Assert.IsTrue(Run(true));
        Assert.IsTrue(Run(true));
        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("Patch124")));
    }
}
