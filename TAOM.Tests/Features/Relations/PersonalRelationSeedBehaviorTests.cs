using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Relations;
using TAOM.Features.Relations.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Relations;

/// <summary>
/// The handlers are reachable because they are internal + InternalsVisibleTo. The seeding rules are
/// <see cref="PersonalRelationSeedServiceTests"/>; these cover the saved flag: it round-trips, an old save without the key
/// seeds, and once set the seeding never repeats, on the session start or on any daily tick.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class PersonalRelationSeedBehaviorTests
{
    private const string FlagKey = "_taom_personalRelationsSeeded";

    private IPersonalRelationSeedAdapter _adapter = null!;
    private IPersonalRelationSettingsProvider _settings = null!;
    private IModLogger _logger = null!;
    private ICoopSessionProvider _coop = null!;
    private IDedicatedServerProvider _server = null!;
    private IPersonalRelationService _personal = null!;
    private PersonalRelationSeedBehavior _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<IPersonalRelationSeedAdapter>();
        _adapter.Snapshot().Returns(new List<SeedRow>
        {
            new("denethor", "g", true, false, 25),
            new("faramir", "g", false, false, 0),
        });
        _adapter.SetRelation(Arg.Any<string>(), Arg.Any<int>()).Returns(true);
        _settings = Substitute.For<IPersonalRelationSettingsProvider>();
        _settings.Enabled.Returns(true);
        _logger = Substitute.For<IModLogger>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _server = Substitute.For<IDedicatedServerProvider>();
        _personal = Substitute.For<IPersonalRelationService>();
        _sut = Build(_adapter);
    }

    private PersonalRelationSeedBehavior Build(IPersonalRelationSeedAdapter adapter)
    {
        return new PersonalRelationSeedBehavior(
            new PersonalRelationSeedService(adapter, _settings, _coop, _server, _logger), _personal, _logger);
    }

    [TestMethod]
    public void OnSessionLaunched_SeedsOldSave_AndSetsTheFlag()
    {
        _sut.OnSessionLaunched(null);

        _adapter.Received(1).SetRelation("faramir", 25);
        Assert.IsTrue(_sut.Seeded);
    }

    [TestMethod]
    public void OnDailyTick_AfterTheSessionLaunchSeeded_DoesNothingMore()
    {
        _sut.OnSessionLaunched(null);
        _adapter.ClearReceivedCalls();

        _sut.OnDailyTick();
        _sut.OnDailyTick();

        _adapter.DidNotReceiveWithAnyArgs().Snapshot();
        _adapter.DidNotReceiveWithAnyArgs().SetRelation(default!, default);
    }

    [TestMethod]
    public void OnDailyTick_FeatureSwitchedOnLater_SeedsOnTheFirstTickItIsOn()
    {
        _settings.Enabled.Returns(false);
        _sut.OnSessionLaunched(null);
        Assert.IsFalse(_sut.Seeded);
        _adapter.DidNotReceiveWithAnyArgs().SetRelation(default!, default);

        _settings.Enabled.Returns(true);
        _sut.OnDailyTick();

        _adapter.Received(1).SetRelation("faramir", 25);
        Assert.IsTrue(_sut.Seeded);
    }

    [TestMethod]
    public void SyncData_TheFlagRoundTripsThroughASave_AndTheLoadedBehaviorNeverSeedsAgain()
    {
        _sut.OnSessionLaunched(null);
        var store = new EngineDataStore();
        _sut.SyncData(store);
        Assert.IsTrue(store.Get<bool>(FlagKey));

        var loadedAdapter = Substitute.For<IPersonalRelationSeedAdapter>();
        var loaded = Build(loadedAdapter);
        store.Loading = true;
        loaded.SyncData(store);
        loaded.OnSessionLaunched(null);
        loaded.OnDailyTick();

        Assert.IsTrue(loaded.Seeded);
        loadedAdapter.DidNotReceiveWithAnyArgs().Snapshot();
    }

    [TestMethod]
    public void SyncData_ASaveWithoutTheKey_LeavesTheFlagUnset_SoTheOldSaveSeeds()
    {
        var store = new EngineDataStore { Loading = true };
        _sut.SyncData(store);

        Assert.IsFalse(_sut.Seeded);
        _sut.OnSessionLaunched(null);

        _adapter.Received(1).SetRelation("faramir", 25);
    }

    [TestMethod]
    public void SyncData_AnUnseededBehavior_SavesAFalseFlag()
    {
        var store = new EngineDataStore();

        _sut.SyncData(store);

        Assert.IsFalse(store.Get<bool>(FlagKey));
    }

    // --- engaging the service ---

    [TestMethod]
    public void Behavior_NewCampaign_Disengages_ThenEngagesOnceSeeded()
    {
        // The constructor runs in every campaign's OnGameStart: it disarms whatever the last campaign left armed.
        _personal.Received(1).Engage(false);
        _personal.ClearReceivedCalls();

        _sut.OnSessionLaunched(null);

        _personal.Received(1).Engage(true);
        _personal.DidNotReceive().Engage(false);
    }

    [TestMethod]
    public void Behavior_NotSinglePlayer_StaysDisengaged_AndKeepsTheLoadedFlag()
    {
        _coop.IsSessionActive.Returns(true);
        var store = new EngineDataStore { Loading = true };
        store.Seed("_taom_personalRelationsSeeded", true);
        _sut.SyncData(store);
        _personal.ClearReceivedCalls();

        _sut.OnSessionLaunched(null);
        _sut.OnDailyTick();

        Assert.IsTrue(_sut.Seeded, "the loaded flag is another peer's to keep");
        _personal.DidNotReceive().Engage(true);
        _personal.Received(2).Engage(false);
        _adapter.DidNotReceiveWithAnyArgs().SetRelation(default!, default);
    }

    [TestMethod]
    public void Behavior_SeenOff_DisengagesAndReArms_ThenEngagesWhenSwitchedBackOn()
    {
        _sut.OnSessionLaunched(null);
        _personal.ClearReceivedCalls();

        _settings.Enabled.Returns(false);
        _sut.OnDailyTick();
        Assert.IsFalse(_sut.Seeded);
        _personal.Received(1).Engage(false);

        _settings.Enabled.Returns(true);
        _sut.OnDailyTick();

        Assert.IsTrue(_sut.Seeded);
        _personal.Received(1).Engage(true);
    }

    [TestMethod]
    public void Behavior_AFault_Disengages()
    {
        var adapter = Substitute.For<IPersonalRelationSeedAdapter>();
        adapter.Snapshot().Throws(new InvalidOperationException("boom"));
        var behavior = Build(adapter);
        _personal.ClearReceivedCalls();

        behavior.OnSessionLaunched(null);

        _personal.Received(1).Engage(false);
        _personal.DidNotReceive().Engage(true);
    }

    [TestMethod]
    public void AServiceFault_IsLoggedAndSwallowed_AndTheFlagStaysUnset()
    {
        var adapter = Substitute.For<IPersonalRelationSeedAdapter>();
        adapter.Snapshot().Throws(new InvalidOperationException("boom"));
        var behavior = Build(adapter);

        behavior.OnSessionLaunched(null);

        Assert.IsFalse(behavior.Seeded);
        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("boom")));
    }
}
