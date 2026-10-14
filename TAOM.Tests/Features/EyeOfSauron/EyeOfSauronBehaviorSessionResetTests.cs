using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.EyeOfSauron;
using TAOM.Features.Execution;

namespace TAOM.Tests.Features.EyeOfSauron;

/// <summary>
/// #765 M8: the hunt service is a process-lifetime singleton. The behavior is built fresh for every campaign start,
/// before any event and before a load's SyncData, so its constructor resets the service and a load then restores the
/// saved record over the reset (csharp-architecture.md, the session-reset rule). That the module keeps building it per
/// campaign is pinned in <see cref="EyeOfSauronWiringTests"/>. The save round trip goes through a dictionary-backed
/// store into a FRESH behavior, so a dropped SyncData key or LoadFrom line cannot hide behind the old instance.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class EyeOfSauronBehaviorSessionResetTests
{
    private IHunterPartyAdapter _adapter = null!;
    private EyeOfSauronService _service = null!;

    [TestInitialize]
    public void SetUp()
    {
        _adapter = Substitute.For<IHunterPartyAdapter>();
        _adapter.NowDays.Returns(1000d);
        _adapter.PlayerKingdomId.Returns("empire_w");
        _adapter.PlayerStrength.Returns(100f);
        _adapter.ReadParty(Arg.Any<string>()).Returns(HuntPartySnapshot.Missing);
        _adapter.ReadHero(Arg.Any<string>()).Returns(HunterHeroSnapshot.Missing);
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(new HunterHeroSnapshot
        {
            Exists = true, IsAlive = true, IsActive = true, FactionId = "empire_s", AtWarWithPlayerKingdom = true,
            CanLeadParty = true, IsLord = true,
        });
        _adapter.SpawnHunterParty(HuntTroopLists.WitchKingId, Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>())
            .Returns("wk_party");
        _adapter.PinAndEngagePlayer("wk_party").Returns(true);

        var settings = Substitute.For<IEyeOfSauronSettings>();
        settings.Enabled.Returns(true);
        settings.Threshold.Returns(10f);
        settings.HalfLifeDays.Returns(30f);
        settings.CooldownDays.Returns(60);
        settings.HuntTimeoutDays.Returns(20);
        settings.HunterSpeedMultiplier.Returns(1.3f);
        settings.HunterStrengthRatio.Returns(1.3f);
        settings.HunterMaxTroops.Returns(250);
        settings.HunterStepCap.Returns(15f);

        _service = new EyeOfSauronService(_adapter, EyeOfSauronServiceTests.NewAlignment(), settings,
            Substitute.For<ICoopSessionProvider>(), Substitute.For<IDedicatedServerProvider>(), Substitute.For<IModLogger>());
        _service.AddScore(FactionSide.Evil, 12f);
        _service.TryStartHunt(ignoreScoreAndCooldown: false);
        Assert.IsTrue(_service.IsHunting, "precondition: a hunt from the previous campaign");
    }

    private EyeOfSauronBehavior NewBehavior() => new(_service, Substitute.For<IModLogger>());

    [TestMethod]
    public void Constructor_ResetsTheHuntAndTheScores_ForANewCampaign()
    {
        NewBehavior();

        Assert.IsFalse(_service.IsHunting);
        Assert.AreEqual(0f, _service.GetScore(FactionSide.Evil));
        Assert.AreEqual(0f, _service.GetHostSizeBonus("wk_party", 10f, 0f, 60, null, _ => 60f));
        _adapter.DidNotReceive().ReleasePin(Arg.Any<string>());
    }

    [TestMethod]
    public void SaveThenLoad_IntoAFreshBehavior_RestoresEveryField()
    {
        var seed = EyeOfSauronServiceTests.Seed();
        EyeOfSauronServiceTests.AssertSeedIsComplete(seed);
        var a = NewBehavior();
        _service.LoadFrom(seed);
        var store = new FakeDataStore { Mode = FakeDataStore.StoreMode.Saving };
        a.SyncData(store);

        store.Mode = FakeDataStore.StoreMode.Loading;
        var b = NewBehavior();
        Assert.IsFalse(_service.IsHunting, "precondition: the fresh behavior's constructor reset the service");
        b.SyncData(store);

        EyeOfSauronServiceTests.AssertSameRecord(seed, _service.CaptureState());
    }

    [TestMethod]
    public void SaveWithoutAHunt_LoadsWithoutOne()
    {
        var a = NewBehavior();
        var store = new FakeDataStore { Mode = FakeDataStore.StoreMode.Saving };
        a.SyncData(store);

        store.Mode = FakeDataStore.StoreMode.Loading;
        NewBehavior().SyncData(store);

        Assert.IsFalse(_service.IsHunting);
        Assert.IsNull(_service.HunterPartyId);
    }

    [TestMethod]
    public void LoadOfASaveWithoutTheKeys_LoadsNoHunt_NoScore_NoCooldown()
    {
        var store = new FakeDataStore { Mode = FakeDataStore.StoreMode.Loading };

        NewBehavior().SyncData(store);

        var state = _service.CaptureState();
        Assert.IsFalse(_service.IsHunting);
        Assert.AreEqual(0f, state.ScoreVsEvil);
        Assert.AreEqual(0f, state.ScoreVsFree);
        Assert.AreEqual(0f, state.CooldownUntilDay);
    }

    /// <summary>
    /// Mirrors the engine's behavior data store (copied from SpecialResourcesBehaviorSessionResetTests): a load of a
    /// missing key returns false and leaves the ref unchanged; a save records what it is handed, null included, and a
    /// second save of the same key throws, as the engine's <c>Add</c> does.
    /// </summary>
    private sealed class FakeDataStore : IDataStore
    {
        public enum StoreMode { Saving, Loading }

        private readonly Dictionary<string, object?> _data = new();

        public StoreMode Mode { get; set; }

        public bool SyncData<T>(string key, ref T data)
        {
            if (Mode == StoreMode.Loading)
            {
                if (!_data.TryGetValue(key, out var stored)) return false;
                data = (T)stored!;
                return true;
            }

            _data.Add(key, data);
            return true;
        }

        public bool IsSaving => Mode == StoreMode.Saving;

        public bool IsLoading => Mode == StoreMode.Loading;
    }
}
