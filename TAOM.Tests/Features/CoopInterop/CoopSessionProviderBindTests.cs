using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Tests.Features.CoopInterop;

/// <summary>
/// Pins the role-probe bind retry: a bind that found nothing (co-op assembly not loaded yet) must
/// not be cached as final, and must not rescan the AppDomain on every hot-path call either.
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
        public static bool IsServer { get; } = true;
    }

    private static Type Finder(string name, bool present) => present
        ? (name.EndsWith("ContainerProvider") ? typeof(FakeContainerProvider) : typeof(FakeModInformation))
        : null;

    [TestMethod]
    public void EnsureBound_TypeAbsentFirstThenPresent_BindsOnLaterCall()
    {
        var present = false;
        long now = 0;
        var logger = Substitute.For<IModLogger>();
        var provider = new CoopSessionProvider(logger, name => Finder(name, present), () => now);

        provider.EnsureBound();
        Assert.IsFalse(provider.RoleProbeAvailable);

        present = true;
        now = CoopSessionProvider.BindRetryIntervalMs;
        provider.EnsureBound();

        Assert.IsTrue(provider.RoleProbeAvailable);
    }

    [TestMethod]
    public void EnsureBound_RetryBeforeInterval_DoesNotRescan()
    {
        var scans = 0;
        long now = 0;
        var provider = new CoopSessionProvider(Substitute.For<IModLogger>(),
            name => { scans++; return null; }, () => now);

        provider.EnsureBound();
        var afterFirst = scans;
        now = CoopSessionProvider.BindRetryIntervalMs - 1;
        provider.EnsureBound();
        provider.EnsureBound();

        Assert.AreEqual(afterFirst, scans);
    }

    [TestMethod]
    public void EnsureBound_RetryAfterInterval_Rescans()
    {
        var scans = 0;
        long now = 0;
        var provider = new CoopSessionProvider(Substitute.For<IModLogger>(),
            name => { scans++; return null; }, () => now);

        provider.EnsureBound();
        var afterFirst = scans;
        now = CoopSessionProvider.BindRetryIntervalMs;
        provider.EnsureBound();

        Assert.IsTrue(scans > afterFirst);
    }

    [TestMethod]
    public void EnsureBound_Succeeded_NeverRescans()
    {
        var scans = 0;
        long now = 0;
        var provider = new CoopSessionProvider(Substitute.For<IModLogger>(),
            name => { scans++; return Finder(name, true); }, () => now);

        provider.EnsureBound();
        var afterFirst = scans;
        now = CoopSessionProvider.BindRetryIntervalMs * 10;
        provider.EnsureBound();

        Assert.AreEqual(afterFirst, scans);
        Assert.IsTrue(provider.RoleProbeAvailable);
    }

    [TestMethod]
    public void EnsureBound_FailureThenSuccess_LogsFailureOnceAndRecoveryOnce()
    {
        var present = false;
        long now = 0;
        var logger = Substitute.For<IModLogger>();
        var provider = new CoopSessionProvider(logger, name => Finder(name, present), () => now);

        provider.EnsureBound();
        now = CoopSessionProvider.BindRetryIntervalMs;
        provider.EnsureBound();   // still absent: must not log the failure again
        logger.Received(1).LogWarning(Arg.Any<string>());

        present = true;
        now = CoopSessionProvider.BindRetryIntervalMs * 2;
        provider.EnsureBound();
        provider.EnsureBound();

        logger.Received(1).LogWarning(Arg.Any<string>());
        logger.Received(1).LogInfo(Arg.Any<string>());
    }
}
