using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.FarWatch;
using TAOM.Features.FarWatch.Hooks;
using TAOM.Features.FieldCamp;
using TAOM.Features.HiddenHosts;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FarWatch;

/// <summary>
/// The behavior is built fresh for every campaign start, before any event and before a load's SyncData, so its constructor is
/// the reset of the process-lifetime store (docs/features/far-watch.md "Data and save"; the same pin as Hidden Hosts' and From
/// the Ashes'). The save round trip goes through the faithful engine data store into a FRESH behavior, so a dropped key cannot
/// hide behind the old instance, and all sixteen keys are present both ways. Hours are doubles, so the trip is exact.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FarWatchBehaviorTests
{
    private IModLogger _logger = null!;
    private FarWatchStore _store = null!;

    [TestInitialize]
    public void SetUp()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new FarWatchStore(_logger);
    }

    private FarWatchBehavior NewBehavior()
    {
        var camps = Substitute.For<ICampSettingsProvider>();
        var coop = Substitute.For<ICoopSessionProvider>();
        var world = Substitute.For<IFarWatchWorldAdapter>();
        var service = new FarWatchService(_store, world, new HiddenHostsService(Substitute.For<IHiddenHostsSettings>(), _store), coop,
            Substitute.For<IDedicatedServerProvider>(), camps, _logger);
        return new FarWatchBehavior(service, _store, Substitute.For<IGameMenuAdapter>(), world, Substitute.For<IFarWatchNotifier>(), _logger);
    }

    private void FillTheStore()
    {
        _store.StartRide(ScoutKind.Settlement, "hero_9", "Mirien", true, "town_3", "Edoras", 4321.5, 4.2, 150);
        _store.SetBuff(0.4f, 4000.25, 36d);
        _store.SetRest(4400.5, "Tamsin");
        _store.SetNamed(new[] { "p1", "p2", "p3" }, 4500.75);
    }

    [TestMethod]
    public void NewBehavior_StartsEmpty_WhateverTheLastCampaignLeft()
    {
        FillTheStore();

        NewBehavior();

        Assert.IsFalse(_store.HasRide);
        Assert.AreEqual(0f, _store.BuffLevel);
        Assert.AreEqual(0d, _store.RestUntilHour);
        Assert.AreEqual(0, _store.NamedCount);
    }

    [TestMethod]
    public void SaveThenLoad_IntoAFreshBehavior_RestoresEveryField()
    {
        var a = NewBehavior();
        FillTheStore();
        var data = new EngineDataStore { Loading = false };
        a.SyncData(data);

        data.Loading = true;
        var b = NewBehavior();
        Assert.IsFalse(_store.HasRide, "precondition: the fresh behavior reset the store");
        b.SyncData(data);

        var r = _store.SaveInto();
        Assert.AreEqual((int)ScoutKind.Settlement, r.Kind);
        Assert.AreEqual("hero_9", r.HeroId);
        Assert.AreEqual("Mirien", r.HeroName);
        Assert.IsTrue(r.SelfScout);
        Assert.AreEqual("town_3", r.TargetId);
        Assert.AreEqual("Edoras", r.TargetName);
        Assert.AreEqual(4321.5, r.StartHour);
        Assert.AreEqual(4.2, r.DurationHours);
        Assert.AreEqual(150, r.Skill);
        Assert.AreEqual(0.4f, r.BuffLevel);
        Assert.AreEqual(4000.25, r.BuffStartHour);
        Assert.AreEqual(36d, r.BuffHoldHours);
        Assert.AreEqual(4400.5, r.RestUntilHour);
        Assert.AreEqual("Tamsin", r.RestRiderName);
        CollectionAssert.AreEqual(new[] { "p1", "p2", "p3" }, r.NamedIds);
        Assert.AreEqual(4500.75, r.NamedUntilHour);
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void Save_WritesAllSixteenKeys_EvenForAnEmptyStore_AndNeverANull()
    {
        var data = new EngineDataStore { Loading = false };

        NewBehavior().SyncData(data);

        foreach (var key in new[]
                 {
                     "_taomFarWatchKind", "_taomFarWatchHero", "_taomFarWatchName", "_taomFarWatchSelf", "_taomFarWatchTarget",
                     "_taomFarWatchTargetName", "_taomFarWatchStart", "_taomFarWatchHours", "_taomFarWatchSkill", "_taomFarWatchBuff",
                     "_taomFarWatchBuffStart", "_taomFarWatchBuffHold", "_taomFarWatchRest", "_taomFarWatchRestName", "_taomFarWatchNamed", "_taomFarWatchNamedUntil",
                 })
            Assert.IsTrue(data.Has(key), key + " is not saved");
        Assert.IsNotNull(data.Get<List<string>>("_taomFarWatchNamed"));
        Assert.IsNotNull(data.Get<string>("_taomFarWatchHero"));
        Assert.IsNotNull(data.Get<string>("_taomFarWatchTargetName"));
        Assert.IsNotNull(data.Get<string>("_taomFarWatchRestName"));
    }

    [TestMethod]
    public void SaveWithNothingRunning_LoadsAsNothing_AndWarnsOfNothing()
    {
        var a = NewBehavior();
        var data = new EngineDataStore { Loading = false };
        a.SyncData(data);

        data.Loading = true;
        NewBehavior().SyncData(data);

        Assert.IsFalse(_store.HasRide);
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void LoadOfASaveFromBeforeTheFeature_LoadsAsNoRideAndNoSight()
    {
        NewBehavior().SyncData(new EngineDataStore { Loading = true });

        Assert.IsFalse(_store.HasRide);
        Assert.AreEqual(0f, _store.BuffLevel);
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void LoadOfARecordWithABadPart_DropsThatPartOnly_AndNeverThrows()
    {
        var data = new EngineDataStore { Loading = true };
        data.Seed("_taomFarWatchKind", 7);
        data.Seed("_taomFarWatchRest", 12345.5d);

        NewBehavior().SyncData(data);

        Assert.IsFalse(_store.HasRide);
        Assert.AreEqual(12345.5d, _store.RestUntilHour);
        _logger.ReceivedWithAnyArgs(1).LogWarning(default!);
    }

    [TestMethod]
    public void Save_DoesNotChangeTheLiveStore()
    {
        var a = NewBehavior();
        FillTheStore();

        a.SyncData(new EngineDataStore { Loading = false });

        Assert.AreEqual(ScoutKind.Settlement, _store.Kind);
        Assert.AreEqual(3, _store.NamedCount);
        Assert.AreEqual("hero_9", _store.HeroId);
    }
}
