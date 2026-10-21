using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.FireAndTribute;
using TAOM.Features.FireAndTribute.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FireAndTribute;

/// <summary>
/// #870: the service is a process-lifetime singleton. The behavior is built fresh for every campaign start, before any
/// event and before a load's SyncData, so its constructor resets the service and a load then restores the saved record
/// over the reset (csharp-architecture.md, the session-reset rule). That the module keeps building it per campaign is
/// pinned in <see cref="FireAndTributeWiringTests"/>. The save round trip goes through a dictionary-backed store into a
/// FRESH behavior, so a dropped SyncData key or LoadFrom line cannot hide behind the old instance.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FireAndTributeBehaviorSessionResetTests
{
    private FireAndTributeRig _rig = null!;
    private RaidHostRecord _host = null!;

    [TestInitialize]
    public void SetUp()
    {
        _rig = new FireAndTributeRig();
        _rig.Random.DoubleFallback = 0.99;
        _host = _rig.SpawnEvil();
        Assert.AreEqual(1, _rig.LiveRecords().Count, "precondition: a host from the previous campaign");
    }

    private FireAndTributeBehavior NewBehavior() => new(_rig.Service, Substitute.For<IModLogger>());

    [TestMethod]
    public void Constructor_ResetsTheService_ForANewCampaign()
    {
        _rig.Snapshots.Day = 44;
        _rig.Service.OnDailyTick();

        NewBehavior();

        Assert.AreEqual(0, _rig.LiveRecords().Count);
        Assert.AreEqual(new FireAndTributeRig().Service.CaptureState(), _rig.Service.CaptureState());
        Assert.AreEqual(0, _rig.Adapter.Count("release:"), "the previous campaign's parties are gone: nothing to release");
    }

    [TestMethod]
    public void SaveThenLoad_IntoAFreshBehavior_RestoresEveryField()
    {
        _rig.Service.LoadFrom(SeedJson());
        string before = _rig.Service.CaptureState();
        var a = NewBehavior();
        _rig.Service.LoadFrom(before);
        var store = new EngineDataStore { Loading = false };
        a.SyncData(store);

        store.Loading = true;
        var b = NewBehavior();
        Assert.AreEqual(0, _rig.LiveRecords().Count, "precondition: the fresh behavior's constructor reset the service");
        b.SyncData(store);

        Assert.AreEqual(before, _rig.Service.CaptureState());
        Assert.AreEqual(2, _rig.LiveRecords().Count);
    }

    [TestMethod]
    public void SaveWritesOneKey_AJsonString()
    {
        var store = new EngineDataStore { Loading = false };

        NewBehavior().SyncData(store);

        Assert.IsTrue(store.Has("_taom_fire_and_tribute_v1"));
        StringAssert.StartsWith(store.Get<string>("_taom_fire_and_tribute_v1"), "{\"v\":1");
    }

    [TestMethod]
    public void LoadOfASaveWithoutTheKey_LoadsNothing()
    {
        var store = new EngineDataStore { Loading = true };

        NewBehavior().SyncData(store);

        Assert.AreEqual(0, _rig.LiveRecords().Count);
    }

    [TestMethod]
    public void LoadOfADamagedRecord_LoadsNothing_AndDoesNotThrow()
    {
        var store = new EngineDataStore { Loading = true };
        store.Seed("_taom_fire_and_tribute_v1", "{ broken");

        NewBehavior().SyncData(store);

        Assert.AreEqual(0, _rig.LiveRecords().Count);
    }

    [TestMethod]
    public void ARecordFromCampaignA_NeverReachesCampaignBsFirstSave()
    {
        var campaignA = NewBehavior();
        _rig.Service.LoadFrom(SeedJson());
        var storeA = new EngineDataStore { Loading = false };
        campaignA.SyncData(storeA);
        StringAssert.Contains(storeA.Get<string>("_taom_fire_and_tribute_v1"), "bandit_party_7");

        var campaignB = NewBehavior(); // a brand-new campaign: no load, no SyncData
        var storeB = new EngineDataStore { Loading = false };
        campaignB.SyncData(storeB);

        StringAssert.Contains(storeB.Get<string>("_taom_fire_and_tribute_v1"), "\"hosts\":[]");
        Assert.IsFalse(storeB.Get<string>("_taom_fire_and_tribute_v1").Contains("bandit_party_7"));
    }

    [TestMethod]
    public void Handlers_AThrowingService_IsLoggedNotRaised()
    {
        var service = Substitute.For<IFireAndTributeService>();
        service.When(s => s.OnDailyTick()).Do(_ => throw new InvalidOperationException("boom"));
        service.When(s => s.OnHourlyTick()).Do(_ => throw new InvalidOperationException("boom"));
        service.When(s => s.OnSessionLaunched()).Do(_ => throw new InvalidOperationException("boom"));
        service.When(s => s.OnVillageStateChanged(Arg.Any<bool>(), Arg.Any<string?>())).Do(_ => throw new InvalidOperationException("boom"));
        service.When(s => s.OnPartyDestroyed(Arg.Any<string?>())).Do(_ => throw new InvalidOperationException("boom"));
        var logger = Substitute.For<IModLogger>();
        var sut = new FireAndTributeBehavior(service, logger);

        Invoke(sut, "OnDailyTick");
        Invoke(sut, "OnHourlyTick");
        Invoke(sut, "OnSessionLaunched", new object?[] { null });
        Invoke(sut, "OnVillageStateChanged", new object?[] { null, TaleWorlds.CampaignSystem.Settlements.Village.VillageStates.Looted, TaleWorlds.CampaignSystem.Settlements.Village.VillageStates.Looted, null });
        Invoke(sut, "OnMobilePartyDestroyed", new object?[] { null, null });

        logger.Received(5).LogError(Arg.Is<string>(s => s.Contains("[FireAndTribute]") && s.Contains("boom")));
    }

    [TestMethod]
    public void Handlers_RouteTheEventsToTheService()
    {
        var service = Substitute.For<IFireAndTributeService>();
        var sut = new FireAndTributeBehavior(service, Substitute.For<IModLogger>());

        Invoke(sut, "OnDailyTick");
        Invoke(sut, "OnHourlyTick");
        Invoke(sut, "OnSessionLaunched", new object?[] { null });
        Invoke(sut, "OnVillageStateChanged", new object?[] { null, TaleWorlds.CampaignSystem.Settlements.Village.VillageStates.Normal, TaleWorlds.CampaignSystem.Settlements.Village.VillageStates.Looted, null });
        Invoke(sut, "OnVillageStateChanged", new object?[] { null, TaleWorlds.CampaignSystem.Settlements.Village.VillageStates.Normal, TaleWorlds.CampaignSystem.Settlements.Village.VillageStates.BeingRaided, null });
        Invoke(sut, "OnMobilePartyDestroyed", new object?[] { null, null });

        service.Received(1).ResetForNewSession();
        service.Received(1).OnDailyTick();
        service.Received(1).OnHourlyTick();
        service.Received(1).OnSessionLaunched();
        service.Received(1).OnVillageStateChanged(true, null);
        service.Received(1).OnVillageStateChanged(false, null);
        service.Received(1).OnPartyDestroyed(null);
    }

    /// <summary>Two live hosts and a cooldown, as a record a save would hold.</summary>
    private static string SeedJson() =>
        "{\"v\":1,\"seen\":44,\"cool\":{\"Free\":300},\"hosts\":["
        + "{\"p\":\"bandit_party_7\",\"h\":\"gundabad\",\"s\":\"Evil\",\"t\":\"empire_w\",\"d\":91,\"r\":3,\"q\":9,\"ret\":false},"
        + "{\"p\":\"bandit_party_9\",\"h\":\"umbar\",\"s\":\"Neutral\",\"t\":\"erebor\",\"d\":95,\"r\":11,\"q\":11,\"ret\":true}]}";

    private static void Invoke(object target, string method, object?[]? args = null) =>
        target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args ?? new object?[0]);
}
