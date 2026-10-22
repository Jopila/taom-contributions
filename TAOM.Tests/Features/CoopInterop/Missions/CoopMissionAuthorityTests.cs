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
/// Pins the co-op ownership and host answers against fakes shaped like BannerlordCoop's types: the
/// reflection bind (overload choice, compiled delegates), the per-mission registry resolve, the live
/// (never cached) answers, and the singleplayer path that binds nothing.
/// </summary>
[TestClass]
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

    public sealed class FakeRegistry : IFakeRegistry
    {
        public readonly Dictionary<Agent, bool> Local = new();
        public bool Throw;

        public bool TryGetAgentInfo(Guid id, out FakeInfo info) { info = null!; return false; }

        public bool TryGetAgentInfo(Agent agent, out FakeInfo info)
        {
            if (Throw) throw new InvalidOperationException("registry torn down");
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

        public static bool TryResolve<T>(out T instance) where T : class
        {
            Resolves++;
            instance = (Registry as T)!;
            return instance != null;
        }
    }

    public class FakeMissionController
    {
    }

    public interface IFakeSession
    {
        bool IsLocalHost { get; }
    }

    public sealed class FakeSession : IFakeSession
    {
        public bool IsLocalHost { get; set; }
    }

    public sealed class FakeBattleController : FakeMissionController
    {
        public IFakeSession? Session { get; set; }
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
    }

    private Type? Find(string name)
    {
        _scans++;
        switch (name)
        {
            case "GameInterface.ContainerProvider": return typeof(FakeContainerProvider);
            case "Missions.INetworkAgentRegistry": return typeof(IFakeRegistry);
            case "Missions.CoopMissionController": return typeof(FakeMissionController);
            case "Missions.Battles.CoopBattleController": return typeof(FakeBattleController);
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

    private static object[] BattleMission() =>
        BattleMission(new FakeBattleController { Session = new FakeSession() });

    private static object[] BattleMission(FakeBattleController controller) =>
        new object[] { new object(), controller };

    // ---- singleplayer -------------------------------------------------------------------------------

    [TestMethod]
    public void IsLocallyControlled_NoSession_OwnedWithoutBinding()
    {
        var authority = Authority(sessionActive: false);

        Assert.IsTrue(authority.IsLocallyControlled(BareAgent()));
        Assert.AreEqual(0, _scans);
        Assert.AreEqual(0, FakeContainerProvider.Resolves);
    }

    [TestMethod]
    public void IsBattleHost_NoSession_HostWithoutBinding()
    {
        var authority = Authority(sessionActive: false);

        Assert.IsTrue(authority.IsBattleHost(null));
        Assert.AreEqual(0, _scans);
    }

    // ---- ownership ----------------------------------------------------------------------------------

    [TestMethod]
    public void Decide_RegisteredHere_Owned()
    {
        var authority = Authority();
        var agent = new Facts { Key = BareAgent(), HasLiveController = false };
        _registry.Local[(Agent)agent.Key] = true;

        Assert.IsTrue(authority.Decide(authority.LinkFor(M1, BattleMission), agent));
    }

    [TestMethod]
    public void Decide_RegisteredElsewhere_NotOwnedEvenWithAController()
    {
        var authority = Authority();
        var agent = new Facts { Key = BareAgent(), HasLiveController = true };
        _registry.Local[(Agent)agent.Key] = false;

        Assert.IsFalse(authority.Decide(authority.LinkFor(M1, BattleMission), agent));
    }

    [TestMethod]
    public void Decide_UnregisteredHuman_FollowsItsController()
    {
        var authority = Authority();
        var link = authority.LinkFor(M1, BattleMission);

        Assert.IsTrue(authority.Decide(link, new Facts { Key = BareAgent(), HasLiveController = true }));
        Assert.IsFalse(authority.Decide(link, new Facts { Key = BareAgent(), HasLiveController = false }));
    }

    [TestMethod]
    public void Decide_UnregisteredMount_FollowsRegisteredRider()
    {
        var authority = Authority();
        var link = authority.LinkFor(M1, BattleMission);
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
        var link = authority.LinkFor(M1, BattleMission);
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

        Assert.IsTrue(authority.Decide(authority.LinkFor(M1, BattleMission), mount));
    }

    [TestMethod]
    public void Decide_MissionWithoutCoopController_Owned()
    {
        var authority = Authority();
        var link = authority.LinkFor(M1, () => new object[] { new object() });
        var agent = new Facts { Key = BareAgent(), HasLiveController = false };
        _registry.Local[(Agent)agent.Key] = false;

        Assert.IsTrue(authority.Decide(link, agent));
        Assert.AreEqual(0, FakeContainerProvider.Resolves, "an unmanaged mission never asks for the registry");
    }

    [TestMethod]
    public void Decide_AnswerFollowsTheRegistryLive_NotCached()
    {
        var authority = Authority();
        var link = authority.LinkFor(M1, BattleMission);
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

        var first = authority.LinkFor(M1, BattleMission);
        Assert.AreSame(first, authority.LinkFor(M1, BattleMission));
        Assert.AreSame(first, authority.LinkFor(M1, BattleMission));
        Assert.AreEqual(1, FakeContainerProvider.Resolves);

        authority.LinkFor(M2, BattleMission);
        Assert.AreEqual(2, FakeContainerProvider.Resolves);
    }

    [TestMethod]
    public void Decide_TypesNotFound_FallsBackToControllerAndLogsOnce()
    {
        var authority = Authority(find: _ => null);
        var link = authority.LinkFor(M1, BattleMission);

        Assert.IsTrue(authority.Decide(link, new Facts { Key = BareAgent(), HasLiveController = true }));
        Assert.IsFalse(authority.Decide(link, new Facts { Key = BareAgent(), HasLiveController = false }));
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Decide_RegistryNotResolvable_FallsBackToController()
    {
        FakeContainerProvider.Registry = null;
        var authority = Authority();
        var link = authority.LinkFor(M1, BattleMission);

        Assert.IsTrue(authority.Decide(link, new Facts { Key = BareAgent(), HasLiveController = true }));
        Assert.IsFalse(authority.Decide(link, new Facts { Key = BareAgent(), HasLiveController = false }));
    }

    [TestMethod]
    public void Decide_RegistryThrows_FallsBackToControllerAndLogsOnce()
    {
        var authority = Authority();
        var link = authority.LinkFor(M1, BattleMission);
        _registry.Throw = true;

        Assert.IsTrue(authority.Decide(link, new Facts { Key = BareAgent(), HasLiveController = true }));
        Assert.IsFalse(authority.Decide(link, new Facts { Key = BareAgent(), HasLiveController = false }));
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    // ---- battle host --------------------------------------------------------------------------------

    [TestMethod]
    public void DecideHost_BeforeElection_NotHost_ThenFollowsTheSessionLive()
    {
        var session = new FakeSession { IsLocalHost = false };
        var authority = Authority();
        var link = authority.LinkFor(M1, () => BattleMission(new FakeBattleController { Session = session }));

        Assert.IsFalse(authority.DecideHost(link), "before the election");

        session.IsLocalHost = true;
        Assert.IsTrue(authority.DecideHost(link));

        session.IsLocalHost = false;
        Assert.IsFalse(authority.DecideHost(link), "a handoff moved the host");
    }

    [TestMethod]
    public void DecideHost_NoBattleController_Host()
    {
        var authority = Authority();
        var link = authority.LinkFor(M1, () => new object[] { new FakeMissionController() });

        Assert.IsTrue(authority.DecideHost(link), "a tournament or tavern has nothing to elect");
    }

    [TestMethod]
    public void DecideHost_ControllerWithoutSession_NotHost()
    {
        var authority = Authority();
        var link = authority.LinkFor(M1, () => BattleMission(new FakeBattleController { Session = null }));

        Assert.IsFalse(authority.DecideHost(link));
    }

    [TestMethod]
    public void DecideHost_BattleControllerTypeMissing_Host()
    {
        var authority = Authority(find: n => n == "Missions.Battles.CoopBattleController" ? null : Find(n));
        var link = authority.LinkFor(M1, BattleMission);

        Assert.IsTrue(authority.DecideHost(link));
    }
}
