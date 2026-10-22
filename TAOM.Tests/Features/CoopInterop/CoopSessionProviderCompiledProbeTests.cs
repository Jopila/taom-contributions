using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CoopInterop;

/// <summary>
/// The session probe runs on mission-tick paths since #967, so it binds compiled delegates instead of a
/// MethodInfo.Invoke per call. The answers stay LIVE: only the call mechanism changed.
/// </summary>
[TestClass]
public class CoopSessionProviderCompiledProbeTests
{
    public static class FakeContainerProvider
    {
        public static bool Alive;
        public static int Calls;

        public static bool TryGetContainer(out object scope)
        {
            Calls++;
            scope = null!;
            return Alive;
        }
    }

    public static class FakeModInformation
    {
        public static bool Server;

        public static bool IsServer => Server;
    }

    private static CoopSessionProvider Bound()
    {
        var provider = new CoopSessionProvider(Substitute.For<IModLogger>(), name =>
            name.EndsWith("ContainerProvider") ? typeof(FakeContainerProvider)
            : name.EndsWith("ModInformation") ? typeof(FakeModInformation)
            : null);
        provider.EnsureBound();
        return provider;
    }

    [TestInitialize]
    public void Reset()
    {
        FakeContainerProvider.Alive = false;
        FakeContainerProvider.Calls = 0;
        FakeModInformation.Server = false;
    }

    [TestMethod]
    public void EnsureBound_CompilesBothProbes()
    {
        // Probe itself is gated by CoopPresence.IsActive, which a unit-test host never has, so the bind is
        // what runs here; the delegates read the fakes' statics live on every call.
        var provider = Bound();

        Assert.IsTrue(provider.RoleProbeAvailable);
        Assert.IsTrue(provider.CompiledProbesBound, "the probe must be compiled, not reflected, on the hot path");
    }

    [TestMethod]
    public void EnsureBound_RoleMissing_CompilesNothing()
    {
        var provider = new CoopSessionProvider(Substitute.For<IModLogger>(),
            name => name.EndsWith("ContainerProvider") ? typeof(FakeContainerProvider) : null);
        provider.EnsureBound();

        Assert.IsFalse(provider.RoleProbeAvailable);
        Assert.IsFalse(provider.CompiledProbesBound);
    }

    [TestMethod]
    public void Probe_SourceUsesCompiledDelegatesBeforeInvoke()
    {
        var src = RepoPaths.ReadSource("Main/Features/CoopInterop/CoopSessionProvider.cs", stripComments: true);
        var probe = src.Substring(src.IndexOf("private (bool sessionActive, bool isServer) Probe()", StringComparison.Ordinal));
        probe = probe.Substring(0, probe.IndexOf("internal void EnsureBound", StringComparison.Ordinal));
        StringAssert.Contains(probe, "_containerAlive");
        StringAssert.Contains(probe, "_serverRole");
    }
}
