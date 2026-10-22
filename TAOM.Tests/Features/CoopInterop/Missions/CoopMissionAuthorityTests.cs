using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.CoopInterop.Missions;

namespace TAOM.Tests.Features.CoopInterop.Missions;

/// <summary>
/// Pins the co-op ownership answers against fakes shaped like BannerlordCoop's types: the
/// reflection bind (overload choice, compiled delegates), the per-mission registry resolve, the live
/// (never cached) answers, and the singleplayer path that binds nothing.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class CoopMissionAuthorityTests
{
    public sealed class FakeInfo
    {
    }

    public interface IFakeRegistry
    {
        // The real interface overloads both calls by Guid and by Agent; the bind must pick the Agent pair.
        bool TryGetAgentInfo(Guid id, out FakeInfo info);
        bool TryGetAgentInfo(Agent agent, out FakeInfo info);
        bool IsLocallyControlled(Guid id);
        bool IsLocallyControlled(Agent agent);
    }

    // The registry after an upstream rename: the Agent overload of IsLocallyControlled is gone.
    public interface IRenamedRegistry
    {
        bool TryGetAgentInfo(Agent agent, out FakeInfo info);
        bool IsOwnedByThisPeer(Agent agent);
    }

    public sealed class FakeRegistry : IFakeRegistry
    {
        public readonly Dictionary<Agent, bool> Local = new();
        public readonly HashSet<Agent> ThrowFor = new();
        public bool Throw;

        public bool TryGetAgentInfo(Guid id, out FakeInfo info) { info = null!; return false; }

        public bool TryGetAgentInfo(Agent agent, out FakeInfo info)
        {
            if (Throw || ThrowFor.Contains(agent)) throw new InvalidOperationException("registry torn down");
            info = new FakeInfo();
            return Local.ContainsKey(agent);
        }

        public bool IsLocallyControlled(Guid id) => false;

        public bool IsLocallyControlled(Agent agent) => Local.TryGetValue(agent, out var local) && local;
    }

    public static class FakeContainerProvider
    {
        public static object? Registry;
        public static int Resolves;
        public static bool ThrowOnResolve;

        public static bool TryResolve<T>(out T instance) where T : class
        {
            Resolves++;
            if (ThrowOnResolve) throw new InvalidOperationException("container disposed");
            instance = (Registry as T)!;
            return instance != null;
        }
    }

    public class FakeMissionController
    {
    }

    private sealed class Facts : CoopMissionAuthority.IAgentFacts
    {
        public object Key { get; set; } = new object();
        public bool IsHuman { get; set; } = true;
        public bool HasLiveController { get; set; }
        public Facts? Rider { get; set; }

        public bool TryGetRider(out object riderKey, out bool riderHasLiveController)
        {
            riderKey = Rider?.Key!;
            riderHasLiveController = Rider?.HasLiveController == true;
            return Rider != null;
        }
    }

    private FakeRegistry _registry = null!;
    private IModLogger _logger = null!;
    private int _scans;

    private static Agent BareAgent() => (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));

    [TestInitialize]
    public void Setup()
    {
        _registry = new FakeRegistry();
        _logger = Substitute.For<IModLogger>();
        _scans = 0;
        FakeContainerProvider.Registry = _registry;
        FakeContainerProvider.Resolves = 0;
        FakeContainerProvider.ThrowOnResolve = false;
    }

    private Type? Find(string name)
    {
        _scans++;
        switch (name)
        {
            case "GameInterface.ContainerProvider": return typeof(FakeContainerProvider);
            case "Missions.INetworkAgentRegistry": return typeof(IFakeRegistry);
            case "Missions.CoopMissionController": return typeof(FakeMissionController);
            default: return null;
        }
    }

    private CoopMissionAuthority Authority(bool sessionActive = true, Func<string, Type?>? find = null)
    {
        var session = Substitute.For<ICoopSessionProvider>();
        session.IsSessionActive.Returns(sessionActive);
        return new CoopMissionAuthority(session, _logger, find ?? Find);
    }

    private static readonly object M1 = new object();
    private static readonly object M2 = new object();

    private static object[] BattleMission() => new object[] { new object(), new FakeMissionController() };

    // ---- singleplayer -------------------------------------------------------------------------------

    [TestMethod]
    public void IsLocallyControlled_NoSession_OwnedWithoutBinding()
    {
        var authority = Authority(sessionActive: false);

        Assert.IsTrue(authority.IsLocallyControlled(BareAgent()));
        Assert.AreEqual(0, _scans);
        Assert.AreEqual(0, FakeContainerProvider.Resolves);
    }

    // ---- ownership ----------------------------------------------------------------------------------

    [TestMethod]
    public void Decide_RegisteredHere_Owned()
    {
        var authority = Authority();
        var agent = new Facts { Key = BareAgent(), HasLiveController = false };
        _registry.Local[(Agent)agent.Key] = true;

        Assert.IsTrue(authority.Decide(authority.LinkFor(M1, _ => BattleMission()), agent));
    }

    [TestMethod]
    public void Decide_RegisteredElsewhere_NotOwnedEvenWithAController()
    {
        var authority = Authority();
        var agent = new Facts { Key = BareAgent(), HasLiveController = true };
        _registry.Local[(Agent)agent.Key] = false;

        Assert.IsFalse(authority.Decide(authority.LinkFor(M1, _ => BattleMission()), agent));
    }

    [TestMethod]
    public void Decide_UnregisteredHuman_FollowsItsController()
    {
        var authority = Authority();
        var link = authority.LinkFor(M1, _ => BattleMission());

        Assert.IsTrue(authority.Decide(link, new Facts { Key = BareAgent(), HasLiveController = true }));
        Assert.IsFalse(authority.Decide(link, new Facts { Key = BareAgent(), HasLiveController = false }));
    }

    [TestMethod]
    public void Decide_UnregisteredMount_FollowsRegisteredRider()
    {
        var authority = Authority();
        var link = authority.LinkFor(M1, _ => BattleMission());
        var rider = new Facts { Key = BareAgent(), HasLiveController = false };
        var mount = new Facts { Key = BareAgent(), IsHuman = false, HasLiveController = true, Rider = rider };

        _registry.Local[(Agent)rider.Key] = true;
        Assert.IsTrue(authority.Decide(link, mount), "rider registered here");

        _registry.Local[(Agent)rider.Key] = false;
        Assert.IsFalse(authority.Decide(link, mount), "rider registered elsewhere, whatever the mount's controller says");
    }

    [TestMethod]
    public void Decide_UnregisteredMount_UnregisteredRiderFollowsRiderController()
    {
        var authority = Authority();
        var link = authority.LinkFor(M1, _ => BattleMission());
        var rider = new Facts { Key = BareAgent(), HasLiveController = true };
        var mount = new Facts { Key = BareAgent(), IsHuman = false, HasLiveController = false, Rider = rider };

        Assert.IsTrue(authority.Decide(link, mount));

        rider.HasLiveController = false;
        Assert.IsFalse(authority.Decide(link, mount));
    }

    [TestMethod]
    public void Decide_UnregisteredMasterlessMount_Owned()
    {
        var authority = Authority();
        var mount = new Facts { Key = BareAgent(), IsHuman = false, HasLiveController = false };

        Assert.IsTrue(authority.Decide(authority.LinkFor(M1, _ => BattleMission()), mount));
    }

    [TestMethod]
    public void Decide_MissionWithoutCoopController_Owned()
    {
        var authority = Authority();
        var link = authority.LinkFor(M1, _ => new object[] { new object() });
        var agent = new Facts { Key = BareAgent(), HasLiveController = false };
        _registry.Local[(Agent)agent.Key] = false;

        Assert.IsTrue(authority.Decide(link, agent));
        Assert.AreEqual(0, FakeContainerProvider.Resolves, "an unmanaged mission never asks for the registry");
    }

    [TestMethod]
    public void Decide_AnswerFollowsTheRegistryLive_NotCached()
    {
        var authority = Authority();
        var link = authority.LinkFor(M1, _ => BattleMission());
        var agent = new Facts { Key = BareAgent() };

        _registry.Local[(Agent)agent.Key] = true;
        Assert.IsTrue(authority.Decide(link, agent));

        _registry.Local[(Agent)agent.Key] = false;
        Assert.IsFalse(authority.Decide(link, agent), "a handoff moved the agent");

        _registry.Local[(Agent)agent.Key] = true;
        Assert.IsTrue(authority.Decide(link, agent), "adopted again");
    }

    // ---- bind and failure ---------------------------------------------------------------------------

    [TestMethod]
    public void LinkFor_SameMission_ResolvesRegistryOnce_NewMissionResolvesAgain()
    {
        var authority = Authority();

        var first = authority.LinkFor(M1, _ => BattleMission());
        Assert.AreSame(first, authority.LinkFor(M1, _ => BattleMission()));
        Assert.AreSame(first, authority.LinkFor(M1, _ => BattleMission()));
        Assert.AreEqual(1, FakeContainerProvider.Resolves);

        authority.LinkFor(M2, _ => BattleMission());
        Assert.AreEqual(2, FakeContainerProvider.Resolves);
    }

    [TestMethod]
    public void Decide_TypesNotFound_FallsBackToControllerAndLogsOnce()
    {
        var authority = Authority(find: _ => null);
        var link = authority.LinkFor(M1, _ => BattleMission());

        Assert.IsTrue(authority.Decide(link, new Facts { Key = BareAgent(), HasLiveController = true }));
        Assert.IsFalse(authority.Decide(link, new Facts { Key = BareAgent(), HasLiveController = false }));
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Decide_RegistryNotResolvable_FallsBackToController()
    {
        FakeContainerProvider.Registry = null;
        var authority = Authority();
        var link = authority.LinkFor(M1, _ => BattleMission());

        Assert.IsTrue(authority.Decide(link, new Facts { Key = BareAgent(), HasLiveController = true }));
        Assert.IsFalse(authority.Decide(link, new Facts { Key = BareAgent(), HasLiveController = false }));
    }

    [TestMethod]
    public void LinkFor_RegistryResolveThrowsInTwoMissions_WarnsOncePerMission()
    {
        FakeContainerProvider.ThrowOnResolve = true;
        var authority = Authority();

        authority.LinkFor(M1, _ => BattleMission());
        authority.LinkFor(M1, _ => BattleMission());
        _logger.Received(1).LogWarning(Arg.Any<string>());

        authority.LinkFor(M2, _ => BattleMission());
        _logger.Received(2).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void LinkFor_BehaviorEnumerationAlwaysThrows_DoesNotReArmTheWarning()
    {
        // The caller logs the throw through LogOnce; a rebuild that fails before it finishes must not
        // re-arm the latch, or a mission whose behavior list always throws warns on every check.
        var authority = Authority();
        authority.LogOnce("first failure");

        for (var i = 0; i < 3; i++)
        {
            try { authority.LinkFor(M1, _ => ThrowsWhileEnumerated()); }
            catch (InvalidOperationException) { authority.LogOnce("ownership probe threw"); }
        }

        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    // Throws inside BuildLink's foreach, as a behavior list changed by another thread does.
    private static IEnumerable<object> ThrowsWhileEnumerated()
    {
        yield return new object();
        throw new InvalidOperationException("Collection was modified");
    }

    [TestMethod]
    public void Decide_CleanBind_LogsNoWarning()
    {
        var authority = Authority();
        var agent = new Facts { Key = BareAgent() };
        _registry.Local[(Agent)agent.Key] = true;

        Assert.IsTrue(authority.Decide(authority.LinkFor(M1, _ => BattleMission()), agent));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Decide_TypeFinderThrows_FallsBackToControllerAndWarnsOnce()
    {
        var authority = Authority(find: _ => throw new InvalidOperationException("assembly scan failed"));
        var link = authority.LinkFor(M1, _ => BattleMission());

        Assert.IsTrue(authority.Decide(link, new Facts { Key = BareAgent(), HasLiveController = true }));
        Assert.IsFalse(authority.Decide(link, new Facts { Key = BareAgent(), HasLiveController = false }));
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Decide_RegistryMemberRenamed_FallsBackToControllerAndWarnsOnce()
    {
        var authority = Authority(find: n => n == "Missions.INetworkAgentRegistry" ? typeof(IRenamedRegistry) : Find(n));
        var link = authority.LinkFor(M1, _ => BattleMission());

        Assert.IsTrue(authority.Decide(link, new Facts { Key = BareAgent(), HasLiveController = true }));
        Assert.IsFalse(authority.Decide(link, new Facts { Key = BareAgent(), HasLiveController = false }));
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Decide_RiderProbeThrows_FollowsTheRidersController()
    {
        var authority = Authority();
        var link = authority.LinkFor(M1, _ => BattleMission());
        var rider = new Facts { Key = BareAgent(), HasLiveController = true };
        var mount = new Facts { Key = BareAgent(), IsHuman = false, HasLiveController = false, Rider = rider };
        _registry.ThrowFor.Add((Agent)rider.Key);

        Assert.IsTrue(authority.Decide(link, mount));

        rider.HasLiveController = false;
        Assert.IsFalse(authority.Decide(link, mount));
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void IsLocallyControlled_NullAgentInASession_NotOwned()
    {
        var authority = Authority();

        Assert.IsFalse(authority.IsLocallyControlled(null));
        Assert.AreEqual(0, _scans, "a null agent binds nothing");
    }

    [TestMethod]
    public void Decide_RegistryThrows_FallsBackToControllerAndLogsOnce()
    {
        var authority = Authority();
        var link = authority.LinkFor(M1, _ => BattleMission());
        _registry.Throw = true;

        Assert.IsTrue(authority.Decide(link, new Facts { Key = BareAgent(), HasLiveController = true }));
        Assert.IsFalse(authority.Decide(link, new Facts { Key = BareAgent(), HasLiveController = false }));
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }
}
