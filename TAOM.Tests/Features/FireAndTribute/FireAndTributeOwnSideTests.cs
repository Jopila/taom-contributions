using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Execution;
using TAOM.Features.FireAndTribute;

namespace TAOM.Tests.Features.FireAndTribute;

/// <summary>
/// The own-side snapshot the party AI model reads: lords of a host's own side leave the host alone, so it lives to reach
/// its target. In the 2026-10-26 run every host was broken after 0 raids by lords of its own side.
/// </summary>
[TestClass]
public class FireAndTributeOwnSideTests
{
    private FireAndTributeRig _r = null!;

    private IRaidHostOwnSideQuery Query => _r.Service;

    [TestInitialize]
    public void SetUp()
    {
        _r = new FireAndTributeRig();
        _r.Random.DoubleFallback = 0.99;
    }

    [TestMethod]
    public void IsOwnSide_NoHost_IsFalseAndAnyHostIsFalse()
    {
        Assert.IsFalse(Query.AnyHost);
        Assert.IsFalse(Query.IsOwnSide("empire_s", "fat_party_1"));
    }

    [TestMethod]
    public void IsOwnSide_EvilHost_IsTrueForEveryEvilKingdom()
    {
        var host = _r.SpawnEvil();

        Assert.IsTrue(Query.AnyHost);
        Assert.IsTrue(Query.IsOwnSide("empire_s", host.PartyId));
        Assert.IsTrue(Query.IsOwnSide("isengard", host.PartyId));
    }

    [TestMethod]
    public void IsOwnSide_EvilHost_IsFalseForTheTargetSideAndForNeutralKingdoms()
    {
        var host = _r.SpawnEvil();

        Assert.IsFalse(Query.IsOwnSide("empire_w", host.PartyId), "the target's side still fights the host");
        Assert.IsFalse(Query.IsOwnSide("vlandia", host.PartyId));
        Assert.IsFalse(Query.IsOwnSide("umbar", host.PartyId));
    }

    [TestMethod]
    public void IsOwnSide_AnotherPartyOfTheHostClan_IsFalse()
    {
        _r.SpawnEvil();

        Assert.IsFalse(Query.IsOwnSide("empire_s", "some_ordinary_bandit_party"), "only the host itself is spared");
    }

    [TestMethod]
    public void IsOwnSide_NeutralHost_IsTrueOnlyForNeutralKingdoms()
    {
        _r.Service.TrySpawn(FactionSide.Neutral, dryRun: false);
        var host = _r.LiveRecords().Single(h => h.Side == FactionSide.Neutral);

        Assert.IsTrue(Query.IsOwnSide("umbar", host.PartyId));
        Assert.IsFalse(Query.IsOwnSide("empire_s", host.PartyId));
        Assert.IsFalse(Query.IsOwnSide("empire_w", host.PartyId));
    }

    [TestMethod]
    public void IsOwnSide_AfterTheHostIsBroken_IsFalse()
    {
        var host = _r.SpawnEvil();

        _r.Service.OnPartyDestroyed(host.PartyId);

        Assert.IsFalse(Query.AnyHost);
        Assert.IsFalse(Query.IsOwnSide("empire_s", host.PartyId));
    }

    [TestMethod]
    public void IsOwnSide_AfterResetForNewSession_IsFalse()
    {
        var host = _r.SpawnEvil();

        _r.Service.ResetForNewSession();

        Assert.IsFalse(Query.AnyHost);
        Assert.IsFalse(Query.IsOwnSide("empire_s", host.PartyId));
    }

    [TestMethod]
    public void IsOwnSide_LoadedHost_IsTrueOnceTheSessionLaunches()
    {
        var host = _r.SpawnEvil();
        string saved = _r.Service.CaptureState();
        _r.Service.ResetForNewSession();

        _r.Service.LoadFrom(saved);
        _r.Service.OnSessionLaunched();

        Assert.IsTrue(Query.IsOwnSide("empire_s", host.PartyId));
    }

    [TestMethod]
    public void IsOwnSide_HostReleasedToACoopSession_IsFalse()
    {
        var host = _r.SpawnEvil();
        _r.Coop.IsSessionActive.Returns(true);

        _r.Service.OnHourlyTick();

        Assert.IsFalse(Query.AnyHost);
        Assert.IsFalse(Query.IsOwnSide("empire_s", host.PartyId));
    }

    [TestMethod]
    public void IsOwnSide_KingdomThatChangesSide_FollowsItAtTheNextDay()
    {
        var host = _r.SpawnEvil();
        _r.Alignment.ByKingdom["vlandia"] = FactionSide.Evil;

        _r.Service.OnHourlyTick();
        Assert.IsFalse(Query.IsOwnSide("vlandia", host.PartyId), "the hourly step reads no kingdom");

        _r.Service.OnDailyTick();
        Assert.IsTrue(Query.IsOwnSide("vlandia", host.PartyId));
    }

    [TestMethod]
    public void IsOwnSide_HostThatGoesHome_IsFalse()
    {
        var host = _r.SpawnEvil();
        _r.Edit(s => s.Hosts.Single().Returning = true);
        _r.Service.OnSessionLaunched();
        _r.Adapter.Parties[host.PartyId].Snapshot.DistanceToHome = 0f;

        _r.Service.OnHourlyTick();

        Assert.AreEqual(0, _r.LiveRecords().Count, "precondition: the host went home");
        Assert.IsFalse(Query.IsOwnSide("empire_s", host.PartyId));
    }

    [TestMethod]
    public void IsOwnSide_NullIds_AreFalse()
    {
        var host = _r.SpawnEvil();

        Assert.IsFalse(Query.IsOwnSide(null, host.PartyId));
        Assert.IsFalse(Query.IsOwnSide("empire_s", null));
    }
}
