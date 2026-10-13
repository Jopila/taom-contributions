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
/// campaign is pinned in <see cref="EyeOfSauronWiringTests"/>.
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
        _adapter.SpawnHunterParty(HuntTroopLists.WitchKingId).Returns("wk_party");
        _adapter.PinAndEngagePlayer("wk_party").Returns(true);

        var alignment = Substitute.For<IAlignmentService>();
        alignment.GetKingdomSide("empire_w").Returns(FactionSide.Free);
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

        _service = new EyeOfSauronService(_adapter, alignment, settings, Substitute.For<ICoopSessionProvider>(),
            Substitute.For<IDedicatedServerProvider>(), Substitute.For<IModLogger>());
        _service.AddScore(FactionSide.Evil, 12f);
        _service.TryStartHunt(ignoreScoreAndCooldown: false);
        Assert.IsTrue(_service.IsHunting, "precondition: a hunt from the previous campaign");
    }

    private EyeOfSauronBehavior NewBehavior() => new(_service, Substitute.For<IModLogger>());

    private static void Sync(EyeOfSauronBehavior behavior, bool loading)
    {
        var store = Substitute.For<IDataStore>();
        store.IsLoading.Returns(loading);
        store.IsSaving.Returns(!loading);
        behavior.SyncData(store);
    }

    [TestMethod]
    public void Constructor_ResetsTheHuntAndTheScores_ForANewCampaign()
    {
        NewBehavior();

        Assert.IsFalse(_service.IsHunting);
        Assert.AreEqual(0f, _service.GetScore(FactionSide.Evil));
        Assert.AreEqual(0f, _service.GetHostSizeBonus("wk_party", 10f, 0f, 60, () => 60f));
        _adapter.DidNotReceive().ReleasePin(Arg.Any<string>());
    }

    [TestMethod]
    public void SaveThenLoad_RestoresTheHuntOverTheReset()
    {
        var behavior = NewBehavior();
        _service.AddScore(FactionSide.Evil, 12f);
        _service.TryStartHunt(ignoreScoreAndCooldown: false);
        Sync(behavior, loading: false);

        _service.ResetForNewSession();
        Sync(behavior, loading: true);

        Assert.IsTrue(_service.IsHunting);
        Assert.AreEqual(HuntTroopLists.WitchKingId, _service.HunterHeroId);
        Assert.AreEqual("wk_party", _service.HunterPartyId);
        Assert.IsTrue(_service.PinnedByUs);
        Assert.AreEqual(12f, _service.GetScore(FactionSide.Evil), 1e-3f);
    }

    [TestMethod]
    public void SaveWithoutAHunt_LoadsWithoutOne()
    {
        var behavior = NewBehavior();
        Sync(behavior, loading: false);

        Sync(behavior, loading: true);

        Assert.IsFalse(_service.IsHunting);
        Assert.IsNull(_service.HunterPartyId);
    }
}
