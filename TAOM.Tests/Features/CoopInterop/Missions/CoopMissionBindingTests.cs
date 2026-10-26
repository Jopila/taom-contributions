using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.CoopInterop.Missions;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.CoopInterop.Missions;

/// <summary>
/// Binding gate for the members <see cref="CoopMissionAuthority"/> and <see cref="CoopSessionProvider"/> bind by
/// name (#967, C7). Both classes fail open by design, so a BannerlordCoop rename would otherwise make every
/// ownership answer fall back to engine controllers with only a log line. This loads the installed Coop DLLs and
/// checks the shapes, then runs the real bind against them and expects no warning.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class CoopMissionBindingTests
{
    private static string? _skipReason;
    private static Assembly[] _coop = Array.Empty<Assembly>();
    private static ResolveEventHandler? _resolver;

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        if (!GameAssemblies.EnsureLoaded())
        {
            _skipReason = "Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics);
            return;
        }

        var folder = Path.Combine(GameAssemblies.GameDir, "Modules", "Coop", "bin", GameAssemblies.BinFolder);
        if (!Directory.Exists(folder))
        {
            _skipReason = "BannerlordCoop is not installed under " + folder;
            return;
        }

        // Coop's own references (Autofac, its sibling assemblies) sit beside it.
        _resolver = (_, args) =>
        {
            var path = Path.Combine(folder, new AssemblyName(args.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        AppDomain.CurrentDomain.AssemblyResolve += _resolver;

        _coop = new[] { "Common.dll", "GameInterface.dll", "Missions.dll" }
            .Select(name => Path.Combine(folder, name))
            .Where(File.Exists)
            .Select(Assembly.LoadFrom)
            .ToArray();
    }

    [ClassCleanup]
    public static void Cleanup()
    {
        if (_resolver != null) AppDomain.CurrentDomain.AssemblyResolve -= _resolver;
        _resolver = null;
    }

    private static Type? Find(string fullName) =>
        _coop.Select(a => a.GetType(fullName, throwOnError: false)).FirstOrDefault(t => t != null);

    private static Type Require(string fullName)
    {
        var type = Find(fullName);
        Assert.IsNotNull(type, fullName + " is gone from the installed BannerlordCoop; update the binding.");
        return type!;
    }

    [TestInitialize]
    public void SkipWithoutCoop()
    {
        if (_skipReason != null) Assert.Inconclusive(_skipReason);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ContainerProvider_HasTheGenericTryResolveAndTheTryGetContainer()
    {
        var provider = Require("GameInterface.ContainerProvider");

        var resolve = provider.GetMethod("TryResolve", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(resolve, "ContainerProvider.TryResolve<T>(out T)");
        Assert.IsTrue(resolve!.IsGenericMethodDefinition);
        Assert.AreEqual(typeof(bool), resolve.ReturnType);
        var parameters = resolve.GetParameters();
        Assert.AreEqual(1, parameters.Length);
        Assert.IsTrue(parameters[0].IsOut, "TryResolve takes its result as an out parameter");

        var container = provider.GetMethod("TryGetContainer", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(container, "ContainerProvider.TryGetContainer(out ILifetimeScope)");
        Assert.AreEqual(typeof(bool), container!.ReturnType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ModInformation_HasTheStaticIsServerProperty()
    {
        var info = Require("Common.ModInformation");

        var isServer = info.GetProperty("IsServer", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(isServer, "ModInformation.IsServer");
        Assert.AreEqual(typeof(bool), isServer!.PropertyType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void NetworkAgentRegistry_HasTheAgentOverloadsTheProbeUses()
    {
        var registry = Require("Missions.INetworkAgentRegistry");

        var tryGet = registry.GetMethods().SingleOrDefault(m =>
            m.Name == "TryGetAgentInfo" && m.ReturnType == typeof(bool)
            && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(Agent)
            && m.GetParameters()[1].IsOut);
        Assert.IsNotNull(tryGet, "INetworkAgentRegistry.TryGetAgentInfo(Agent, out CoopAgentInfo) -> bool");

        var isLocal = registry.GetMethod("IsLocallyControlled", new[] { typeof(Agent) });
        Assert.IsNotNull(isLocal, "INetworkAgentRegistry.IsLocallyControlled(Agent)");
        Assert.AreEqual(typeof(bool), isLocal!.ReturnType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CoopMissionController_IsAMissionBehavior()
    {
        var controller = Require("Missions.CoopMissionController");

        Assert.IsTrue(typeof(MissionBehavior).IsAssignableFrom(controller),
            "an unmanaged mission is told apart by this behavior type");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PlayerManager_HasTheRegistryProbeTheControlQueryCompiles()
    {
        var manager = Require("GameInterface.Services.Players.PlayerManager");

        var probe = manager.GetMethod("TryGetControlledObjectInfo", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(probe, "PlayerManager.TryGetControlledObjectInfo(object, out ControlledObjectInfo)");
        Assert.AreEqual(typeof(bool), probe!.ReturnType);
        var parameters = probe.GetParameters();
        Assert.AreEqual(2, parameters.Length);
        Assert.AreEqual(typeof(object), parameters[0].ParameterType);
        Assert.IsTrue(parameters[1].ParameterType.IsByRef, "the second parameter is the out ControlledObjectInfo");

        var logger = Substitute.For<IModLogger>();
        var session = Substitute.For<ICoopSessionProvider>();
        session.IsSessionActive.Returns(true);
        var query = new CoopPlayerControlQuery(session, logger, Find);

        Assert.IsTrue(query.CompiledProbeBound, "the real registry method binds and compiles");
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void RealBind_OfTheAuthorityAndTheSessionProvider_WarnsNothing()
    {
        var logger = Substitute.For<IModLogger>();
        var session = Substitute.For<ICoopSessionProvider>();
        session.IsSessionActive.Returns(true);
        var authority = new CoopMissionAuthority(session, logger, Find);

        // The bind runs on the first mission link; an empty mission is enough to force it.
        authority.LinkFor(new object(), _ => Array.Empty<object>());

        var provider = new CoopSessionProvider(logger, Find);
        provider.EnsureBound();

        Assert.IsTrue(provider.RoleProbeAvailable, "ContainerProvider.TryGetContainer and ModInformation.IsServer bind");
        Assert.IsTrue(provider.CompiledProbesBound);
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }
}
