using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Execution;
using TAOM.Features.FireAndTribute;

namespace TAOM.Tests.Features.FireAndTribute;

/// <summary>
/// The own-side snapshot the party AI model reads: parties of a host's own side leave the host alone, so it lives to reach
/// its target. In the 2026-10-26 run every host was broken after 0 raids by lords of its own side.
/// </summary>
[TestClass]
public class FireAndTributeOwnSideTests
{
    private FireAndTributeRig _r = null!;

    [TestInitialize]
    public void SetUp()
    {
        _r = new FireAndTributeRig();
        _r.Random.DoubleFallback = 0.99;
    }

    // The rig's kingdoms use their own id as their culture id (FireAndTributeRig.Kingdom).
    private bool Own(string kingdomId, string? hostPartyId) =>
        ((IRaidHostOwnSideQuery)_r.Service).IsOwnSide(kingdomId, kingdomId, hostPartyId);

    private bool AnyHost => ((IRaidHostOwnSideQuery)_r.Service).AnyHost;

    private RaidHostRecord Spawn(FactionSide side)
    {
        _r.Service.TrySpawn(side, dryRun: false);
        return _r.LiveRecords().Single(h => h.Side == side);
    }

    [TestMethod]
    public void IsOwnSide_NoHost_IsFalseAndAnyHostIsFalse()
    {
        Assert.IsFalse(AnyHost);
        Assert.IsFalse(Own("empire_s", "fat_party_1"));
    }

    [TestMethod]
    public void IsOwnSide_EvilHost_IsTrueForEveryEvilKingdom()
    {
        var host = _r.SpawnEvil();

        Assert.IsTrue(AnyHost);
        Assert.IsTrue(Own("empire_s", host.PartyId));
        Assert.IsTrue(Own("isengard", host.PartyId));
    }

    [TestMethod]
    public void IsOwnSide_EvilHost_IsFalseForTheTargetSideAndForNeutralKingdoms()
    {
        var host = _r.SpawnEvil();

        Assert.IsFalse(Own("empire_w", host.PartyId), "the target's side still fights the host");
        Assert.IsFalse(Own("vlandia", host.PartyId));
        Assert.IsFalse(Own("umbar", host.PartyId));
    }

    [TestMethod]
    public void IsOwnSide_FreeHost_IsTrueOnlyForFreeKingdoms()
    {
        var host = Spawn(FactionSide.Free);

        Assert.IsTrue(Own("empire_w", host.PartyId));
        Assert.IsTrue(Own("vlandia", host.PartyId));
        Assert.IsFalse(Own("empire_s", host.PartyId));
        Assert.IsFalse(Own("umbar", host.PartyId));
    }

    [TestMethod]
    public void IsOwnSide_NeutralHost_IsTrueOnlyForNeutralKingdoms()
    {
        var host = Spawn(FactionSide.Neutral);

        Assert.IsTrue(Own("umbar", host.PartyId));
        Assert.IsFalse(Own("empire_s", host.PartyId));
        Assert.IsFalse(Own("empire_w", host.PartyId));
    }

    [TestMethod]
    public void IsOwnSide_AnotherPartyOfTheHostClan_IsFalse()
    {
        _r.SpawnEvil();

        Assert.IsFalse(Own("empire_s", "some_ordinary_bandit_party"), "only the host itself is spared");
    }

    // A realm founded after the spawn is not in the alignment map; AlignmentService resolves it by its culture.
    [TestMethod]
    public void IsOwnSide_ARealmFoundedAfterTheSpawn_ResolvesByItsCultureAtOnce()
    {
        var host = _r.SpawnEvil();
        var query = (IRaidHostOwnSideQuery)_r.Service;

        Assert.IsTrue(query.IsOwnSide("new_realm", "empire_s", host.PartyId), "an Evil culture puts the new realm on the host's side");
        Assert.IsFalse(query.IsOwnSide("new_realm", "empire_w", host.PartyId), "a Free culture does not");
    }

    [TestMethod]
    public void IsOwnSide_OneOfTwoHostsIsBroken_TheOtherStaysSpared()
    {
        var evil = _r.SpawnEvil();
        var free = Spawn(FactionSide.Free);

        _r.Service.OnPartyDestroyed(evil.PartyId);

        Assert.IsTrue(AnyHost);
        Assert.IsTrue(Own("empire_w", free.PartyId));
        Assert.IsFalse(Own("empire_s", evil.PartyId));
    }

    [TestMethod]
    public void IsOwnSide_AfterTheHostIsBroken_IsFalse()
    {
        var host = _r.SpawnEvil();

        _r.Service.OnPartyDestroyed(host.PartyId);

        Assert.IsFalse(AnyHost);
        Assert.IsFalse(Own("empire_s", host.PartyId));
    }

    [TestMethod]
    public void IsOwnSide_HostThatGoesHome_IsFalse()
    {
        var host = _r.SpawnEvil();
        _r.Edit(s => s.Hosts.Single().Returning = true);
        Assert.IsTrue(Own("empire_s", host.PartyId), "precondition: the loaded host is spared");
        _r.Adapter.Parties[host.PartyId].Snapshot.DistanceToHome = 0f;

        _r.Service.OnHourlyTick();

        Assert.AreEqual(0, _r.LiveRecords().Count, "precondition: the host went home");
        Assert.IsFalse(Own("empire_s", host.PartyId));
    }

    [TestMethod]
    public void IsOwnSide_HostRetiredBecauseItsSwitchIsOff_IsFalse()
    {
        var host = _r.SpawnEvil();
        _r.Settings.EvilHostsEnabled = false;

        _r.Service.OnHourlyTick();

        Assert.AreEqual(0, _r.LiveRecords().Count, "precondition: the host was retired");
        Assert.IsFalse(Own("empire_s", host.PartyId));
    }

    [TestMethod]
    public void IsOwnSide_HostTheEngineLost_IsFalse()
    {
        var host = _r.SpawnEvil();
        _r.Adapter.Parties.Remove(host.PartyId);

        _r.Service.OnHourlyTick();

        Assert.AreEqual(0, _r.LiveRecords().Count, "precondition: the lost host was dropped");
        Assert.IsFalse(Own("empire_s", host.PartyId));
    }

    [TestMethod]
    public void IsOwnSide_AfterResetForNewSession_IsFalse()
    {
        var host = _r.SpawnEvil();

        _r.Service.ResetForNewSession();

        Assert.IsFalse(AnyHost);
        Assert.IsFalse(Own("empire_s", host.PartyId));
    }

    [TestMethod]
    public void IsOwnSide_LoadedHost_IsTrueAsSoonAsTheSaveIsRead()
    {
        var host = _r.SpawnEvil();
        string saved = _r.Service.CaptureState();
        _r.Service.ResetForNewSession();

        _r.Service.LoadFrom(saved);

        Assert.IsTrue(Own("empire_s", host.PartyId));
    }

    [TestMethod]
    public void IsOwnSide_HostReleasedToACoopSession_IsFalse()
    {
        var host = _r.SpawnEvil();
        _r.Coop.IsSessionActive.Returns(true);

        _r.Service.OnHourlyTick();

        Assert.IsFalse(AnyHost);
        Assert.IsFalse(Own("empire_s", host.PartyId));
    }

    [TestMethod]
    public void IsOwnSide_NullIds_AreFalse_AndANullCultureStillResolvesTheKingdom()
    {
        var host = _r.SpawnEvil();
        var query = (IRaidHostOwnSideQuery)_r.Service;

        Assert.IsFalse(query.IsOwnSide(null, "empire_s", host.PartyId));
        Assert.IsFalse(query.IsOwnSide("empire_s", "empire_s", null));
        Assert.IsTrue(query.IsOwnSide("empire_s", null, host.PartyId));
    }
}
