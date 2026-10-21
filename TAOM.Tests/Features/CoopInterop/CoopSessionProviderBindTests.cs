using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Tests.Features.CoopInterop;

/// <summary>
/// Pins the bind-once role probe: a container provider without a readable role, or a type finder
/// that throws, leaves the probe unavailable (so TAOM stays authoritative) and logs once.
/// </summary>
[TestClass]
public class CoopSessionProviderBindTests
{
    public static class FakeContainerProvider
    {
        public static bool TryGetContainer(out object scope) { scope = null; return false; }
    }

    public static class FakeModInformation
    {
        public static bool IsServer => false;
    }

    [TestMethod]
    public void BindLocked_IsServerMissing_RoleProbeUnavailable()
    {
        var logger = Substitute.For<IModLogger>();
        var provider = new CoopSessionProvider(logger,
            name => name.EndsWith("ContainerProvider") ? typeof(FakeContainerProvider) : null);

        provider.EnsureBound();

        Assert.IsFalse(provider.RoleProbeAvailable);
        logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void BindLocked_FinderThrows_RoleProbeUnavailable()
    {
        var logger = Substitute.For<IModLogger>();
        var provider = new CoopSessionProvider(logger,
            name => throw new InvalidOperationException("scan failed"));

        provider.EnsureBound();

        Assert.IsFalse(provider.RoleProbeAvailable);
        logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void EnsureBound_FailedBind_DoesNotRescanOnSecondCall()
    {
        var logger = Substitute.For<IModLogger>();
        var scans = new Dictionary<string, int>();
        var provider = new CoopSessionProvider(logger, name =>
        {
            scans.TryGetValue(name, out var count);
            scans[name] = count + 1;
            return null;
        });

        provider.EnsureBound();
        provider.EnsureBound();

        Assert.AreEqual(2, scans.Count, "one scan per type name");
        Assert.IsTrue(scans.Values.All(count => count == 1), "a failed bind is cached, not rescanned");
        Assert.IsFalse(provider.RoleProbeAvailable);
    }

    [TestMethod]
    public void EnsureBound_BothTypesPresent_ProbeAvailable_NoWarning()
    {
        var logger = Substitute.For<IModLogger>();
        var provider = new CoopSessionProvider(logger, name =>
            name.EndsWith("ContainerProvider") ? typeof(FakeContainerProvider)
            : name.EndsWith("ModInformation") ? typeof(FakeModInformation)
            : null);

        provider.EnsureBound();

        Assert.IsTrue(provider.RoleProbeAvailable);
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }
}
