using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.EyeOfSauron;
using TAOM.Features.Execution;

namespace TAOM.Tests.Features.EyeOfSauron;

/// <summary>
/// #765 M8, the Eye of Sauron hunt: scores and decay, the start gates, hunter choice, the busy guard, the host top-up,
/// the out-of-sight steps, and that every end releases the pin the hunt set. The engine side is a substitute adapter.
/// </summary>
[TestClass]
public class EyeOfSauronServiceTests
{
    private const string Gondor = "empire_w";
    private const string MordorKingdom = "empire_s";
    private const string Party = "hunter_party_1";

    private IHunterPartyAdapter _adapter = null!;
    private IAlignmentService _alignment = null!;
    private IEyeOfSauronSettings _settings = null!;
    private ICoopSessionProvider _coop = null!;
    private IDedicatedServerProvider _dedicated = null!;
    private EyeOfSauronService _sut = null!;
    private double _now;

    [TestInitialize]
    public void SetUp()
    {
        _now = 1000d;
        _adapter = Substitute.For<IHunterPartyAdapter>();
        _adapter.NowDays.Returns(_ => _now);
        _adapter.PlayerKingdomId.Returns(Gondor);
        _adapter.PlayerStrength.Returns(100f);
        _adapter.ReadHero(Arg.Any<string>()).Returns(HunterHeroSnapshot.Missing);
        _adapter.ReadParty(Arg.Any<string>()).Returns(HuntPartySnapshot.Missing);
        _adapter.GetTroopPower(Arg.Any<string>(), Arg.Any<string>()).Returns(2f);
        _adapter.AddTroops(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>()).Returns(ci => ci.ArgAt<int>(2));
        _adapter.PinAndEngagePlayer(Arg.Any<string>()).Returns(true);
        _adapter.SpawnHunterParty(Arg.Any<string>()).Returns(Party);
        _adapter.ReadStepContext(Arg.Any<string>()).Returns(HuntStepContext.Blocked("test default"));
        _adapter.ReadSight(Arg.Any<string>()).Returns(HuntSightReading.Missing);

        _alignment = Substitute.For<IAlignmentService>();
        _alignment.GetKingdomSide(Arg.Any<string>()).Returns(FactionSide.Neutral);
        _alignment.GetKingdomSide(Gondor).Returns(FactionSide.Free);
        _alignment.GetKingdomSide("rivendell").Returns(FactionSide.Free);
        _alignment.GetKingdomSide(MordorKingdom).Returns(FactionSide.Evil);
        _alignment.GetKingdomSide("dolguldur").Returns(FactionSide.Evil);

        _settings = Substitute.For<IEyeOfSauronSettings>();
        _settings.Enabled.Returns(true);
        _settings.Threshold.Returns(10f);
        _settings.HalfLifeDays.Returns(30f);
        _settings.CooldownDays.Returns(60);
        _settings.HuntTimeoutDays.Returns(20);
        _settings.HunterSpeedMultiplier.Returns(1.3f);
        _settings.HunterStrengthRatio.Returns(1.3f);
        _settings.HunterMaxTroops.Returns(250);
        _settings.HunterStepCap.Returns(15f);

        _coop = Substitute.For<ICoopSessionProvider>();
        _dedicated = Substitute.For<IDedicatedServerProvider>();
        _sut = new EyeOfSauronService(_adapter, _alignment, _settings, _coop, _dedicated, Substitute.For<IModLogger>());
    }

    // ---------------------------------------------------------------- helpers

    private static HunterHeroSnapshot FreeHero(string faction = MordorKingdom, bool atWar = true) => new()
    {
        Exists = true, IsAlive = true, IsActive = true, FactionId = faction, AtWarWithPlayerKingdom = atWar,
        CanLeadParty = true, IsLord = true,
    };

    private static HunterHeroSnapshot Leading(string partyId, string faction = MordorKingdom) => new()
    {
        Exists = true, IsAlive = true, IsActive = true, FactionId = faction, AtWarWithPlayerKingdom = true,
        CanLeadParty = true, IsLord = true, IsInAParty = true, LedPartyId = partyId,
    };

    private static HuntPartySnapshot FreeParty(string leader, int heads = 50, float strength = 40f, bool engaging = true) => new()
    {
        Exists = true, IsActive = true, LeaderHeroId = leader, HeadCount = heads, Strength = strength,
        IsEngagingPlayer = engaging,
    };

    private void Battle(string? loserKingdom, DeedKind kind = DeedKind.Field, bool playerWon = true, bool playerEvent = true,
        bool involvesHunter = false) =>
        _sut.OnMapEventEnded(new HuntBattleSummary
        {
            IsPlayerEvent = playerEvent, PlayerWon = playerWon, LoserKingdomId = loserKingdom, Kind = kind,
            InvolvesHunter = involvesHunter,
        });

    /// <summary>Gives the player a Free kingdom, a score over the threshold, and the Witch-king free and at war.</summary>
    private void ArmWitchKingHunt()
    {
        _sut.AddScore(FactionSide.Evil, 12f);
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(FreeHero());
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId));
    }

    private void StartWitchKingHunt()
    {
        ArmWitchKingHunt();
        _sut.OnDailyTick();
        Assert.IsTrue(_sut.IsHunting, "precondition: the hunt started");
        _adapter.ClearReceivedCalls();
    }

    // ---------------------------------------------------------------- decay

    [TestMethod]
    public void Decay_OneHalfLife_Halves() => Assert.AreEqual(5f, EyeOfSauronService.Decay(10f, 30d, 30f), 1e-4f);

    [TestMethod]
    public void Decay_NaNScore_ReturnsZero() => Assert.AreEqual(0f, EyeOfSauronService.Decay(float.NaN, 1d, 30f));

    [TestMethod]
    public void Decay_InfiniteScore_ReturnsZero() =>
        Assert.AreEqual(0f, EyeOfSauronService.Decay(float.PositiveInfinity, 1d, 30f));

    [TestMethod]
    public void Decay_NegativeOrNaNElapsed_KeepsTheScore()
    {
        Assert.AreEqual(10f, EyeOfSauronService.Decay(10f, -5d, 30f));
        Assert.AreEqual(10f, EyeOfSauronService.Decay(10f, double.NaN, 30f));
    }

    [TestMethod]
    public void Decay_NaNHalfLife_KeepsTheScore() => Assert.AreEqual(10f, EyeOfSauronService.Decay(10f, 30d, float.NaN));

    [TestMethod]
    public void GetScore_DecaysFromTheLastDeed()
    {
        Battle(MordorKingdom, DeedKind.Assault);
        _now += 30d;

        Assert.AreEqual(1.5f, _sut.GetScore(FactionSide.Evil), 1e-3f);
    }

    // ---------------------------------------------------------------- deeds

    [DataTestMethod]
    [DataRow(DeedKind.Field, 1f)]
    [DataRow(DeedKind.SallyOrOutside, 2f)]
    [DataRow(DeedKind.Assault, 3f)]
    public void OnMapEventEnded_PlayerBeatsAnEvilKingdom_ScoresTheBattleWeight(DeedKind kind, float expected)
    {
        Battle(MordorKingdom, kind);

        Assert.AreEqual(expected, _sut.GetScore(FactionSide.Evil), 1e-4f);
        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Free));
    }

    [TestMethod]
    public void OnMapEventEnded_PlayerBeatsAFreeKingdom_ScoresAgainstFree()
    {
        Battle("rivendell");

        Assert.AreEqual(1f, _sut.GetScore(FactionSide.Free), 1e-4f);
        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Evil));
    }

    [TestMethod]
    public void OnMapEventEnded_NeutralLoser_NeverScores()
    {
        Battle("some_minor_kingdom");

        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Evil));
        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Free));
    }

    [TestMethod]
    public void OnMapEventEnded_LoserIsNoKingdom_NeverScores()
    {
        Battle(null, DeedKind.Assault);

        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Evil));
        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Free));
    }

    [TestMethod]
    public void OnMapEventEnded_PlayerLost_NeverScores()
    {
        Battle(MordorKingdom, playerWon: false);

        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Evil));
    }

    [TestMethod]
    public void OnMapEventEnded_NotThePlayersBattle_NeverScores()
    {
        Battle(MordorKingdom, playerEvent: false);

        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Evil));
    }

    [TestMethod]
    public void OnMapEventEnded_KingdomlessPlayer_StillScores_ButIsNeverHunted()
    {
        _adapter.PlayerKingdomId.Returns((string?)null);
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(FreeHero());
        for (int i = 0; i < 4; i++)
            Battle(MordorKingdom, DeedKind.Assault);

        _sut.OnDailyTick();

        Assert.AreEqual(12f, _sut.GetScore(FactionSide.Evil), 1e-3f);
        Assert.IsFalse(_sut.IsHunting);
        _adapter.DidNotReceive().SpawnHunterParty(Arg.Any<string>());
    }

    // ---------------------------------------------------------------- start gates

    [TestMethod]
    public void OnDailyTick_FreePlayerOverTheThreshold_SpawnsAndPinsTheWitchKing()
    {
        ArmWitchKingHunt();

        _sut.OnDailyTick();

        Assert.IsTrue(_sut.IsHunting);
        Assert.AreEqual(HuntTroopLists.WitchKingId, _sut.HunterHeroId);
        Assert.AreEqual(Party, _sut.HunterPartyId);
        Assert.IsTrue(_sut.PinnedByUs);
        _adapter.Received(1).SpawnHunterParty(HuntTroopLists.WitchKingId);
        _adapter.Received(1).PinAndEngagePlayer(Party);
        _adapter.Received(1).Announce(HuntAnnouncement.StartFree, HuntTroopLists.WitchKingId);
    }

    [TestMethod]
    public void OnDailyTick_BelowTheThreshold_StartsNoHunt()
    {
        ArmWitchKingHunt();
        _settings.Threshold.Returns(13f);

        _sut.OnDailyTick();

        Assert.IsFalse(_sut.IsHunting);
    }

    [TestMethod]
    public void OnDailyTick_NaNThreshold_StartsNoHunt()
    {
        ArmWitchKingHunt();
        _settings.Threshold.Returns(float.NaN);

        _sut.OnDailyTick();

        Assert.IsFalse(_sut.IsHunting);
    }

    [TestMethod]
    public void OnDailyTick_WarGateFalse_StartsNoHunt()
    {
        _sut.AddScore(FactionSide.Evil, 50f);
        foreach (var id in HuntTroopLists.WraithOrder)
            _adapter.ReadHero(id).Returns(FreeHero(atWar: false));

        _sut.OnDailyTick();

        Assert.IsFalse(_sut.IsHunting);
        _adapter.DidNotReceive().SpawnHunterParty(Arg.Any<string>());
    }

    [TestMethod]
    public void OnDailyTick_NeutralPlayerKingdom_StartsNoHunt()
    {
        ArmWitchKingHunt();
        _adapter.PlayerKingdomId.Returns("some_minor_kingdom");

        _sut.OnDailyTick();

        Assert.IsFalse(_sut.IsHunting);
    }

    [TestMethod]
    public void OnDailyTick_Disabled_StartsNoHunt()
    {
        ArmWitchKingHunt();
        _settings.Enabled.Returns(false);

        _sut.OnDailyTick();

        Assert.IsFalse(_sut.IsHunting);
    }

    [TestMethod]
    public void OnDailyTick_CoopSessionLive_StartsNoHunt()
    {
        ArmWitchKingHunt();
        _coop.IsSessionActive.Returns(true);

        _sut.OnDailyTick();

        Assert.IsFalse(_sut.IsHunting);
    }

    [TestMethod]
    public void OnDailyTick_DedicatedServer_StartsNoHunt()
    {
        ArmWitchKingHunt();
        _dedicated.IsDedicatedServer.Returns(true);

        _sut.OnDailyTick();

        Assert.IsFalse(_sut.IsHunting);
    }

    [TestMethod]
    public void OnDailyTick_InCooldown_StartsNoHunt_UntilItPasses()
    {
        StartWitchKingHunt();
        _sut.EndHunt(HuntEndReason.Manual);
        _sut.AddScore(FactionSide.Evil, 50f);

        _now += 59d;
        _sut.OnDailyTick();
        Assert.IsFalse(_sut.IsHunting, "inside the 60-day cooldown");

        _now += 2d;
        _sut.OnDailyTick();
        Assert.IsTrue(_sut.IsHunting, "the cooldown has passed");
    }

    // ---------------------------------------------------------------- hunter choice

    [TestMethod]
    public void TryStartHunt_FreePlayer_SendsTheWitchKingFirst()
    {
        _sut.AddScore(FactionSide.Evil, 12f);
        foreach (var id in HuntTroopLists.WraithOrder)
            _adapter.ReadHero(id).Returns(FreeHero());

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.AreEqual(HuntTroopLists.WitchKingId, _sut.HunterHeroId);
    }

    [TestMethod]
    public void TryStartHunt_WitchKingAtPeace_SendsTheNextWraith_AndKhamulOnlyLast()
    {
        _sut.AddScore(FactionSide.Evil, 12f);
        _adapter.ReadHero("lord_1_155").Returns(FreeHero());
        _adapter.ReadHero(HuntTroopLists.KhamulId).Returns(FreeHero("dolguldur"));

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);
        Assert.AreEqual("lord_1_155", _sut.HunterHeroId);

        _sut.EndHunt(HuntEndReason.Manual);
        _adapter.ReadHero("lord_1_155").Returns(HunterHeroSnapshot.Missing);
        _sut.TryStartHunt(ignoreScoreAndCooldown: true);
        Assert.AreEqual(HuntTroopLists.KhamulId, _sut.HunterHeroId);
        Assert.AreEqual(HuntTroopLists.KhamulId, HuntTroopLists.WraithOrder.Last());
    }

    [TestMethod]
    public void TryStartHunt_EvilPlayer_SendsGlorfindelOnly()
    {
        _adapter.PlayerKingdomId.Returns(MordorKingdom);
        _sut.AddScore(FactionSide.Free, 12f);
        _adapter.ReadHero(HuntTroopLists.GlorfindelId).Returns(FreeHero("rivendell"));

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.AreEqual(HuntTroopLists.GlorfindelId, _sut.HunterHeroId);
        _adapter.Received(1).Announce(HuntAnnouncement.StartEvil, HuntTroopLists.GlorfindelId);
        _adapter.DidNotReceive().ReadHero(HuntTroopLists.WitchKingId);
    }

    [TestMethod]
    public void TryStartHunt_EvilPlayer_RivendellAtPeace_NoHunt_AndNoWraithIsAsked()
    {
        _adapter.PlayerKingdomId.Returns(MordorKingdom);
        _sut.AddScore(FactionSide.Free, 12f);
        _adapter.ReadHero(HuntTroopLists.GlorfindelId).Returns(FreeHero("rivendell", atWar: false));

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.IsFalse(_sut.IsHunting);
        _adapter.DidNotReceive().ReadHero(HuntTroopLists.WitchKingId);
    }

    [TestMethod]
    public void TryStartHunt_FreePlayer_ScoreAgainstFreeDoesNotCount()
    {
        _sut.AddScore(FactionSide.Free, 50f);
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(FreeHero());

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.IsFalse(_sut.IsHunting);
    }

    [DataTestMethod]
    [DataRow("dead")]
    [DataRow("fugitive")]
    [DataRow("prisoner")]
    [DataRow("held")]
    [DataRow("deathmark")]
    [DataRow("missing")]
    public void TryStartHunt_WitchKingUnfit_IsSkippedForTheNextWraith(string state)
    {
        _sut.AddScore(FactionSide.Evil, 12f);
        var wk = FreeHero();
        switch (state)
        {
            case "dead": wk.IsAlive = false; break;
            case "fugitive": wk.IsFugitive = true; break;
            case "prisoner": wk.IsPrisoner = true; break;
            case "held": wk.IsHeldByAParty = true; break;
            case "deathmark": wk.IsMarkedForDeath = true; break;
            case "missing": wk = HunterHeroSnapshot.Missing; break;
        }
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(wk);
        _adapter.ReadHero("lord_1_155").Returns(FreeHero());

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.AreEqual("lord_1_155", _sut.HunterHeroId, state);
        _adapter.DidNotReceive().SpawnHunterParty(HuntTroopLists.WitchKingId);
    }

    // ---------------------------------------------------------------- busy guard, re-task, spawn

    [TestMethod]
    public void TryStartHunt_LeadsAFreeParty_RetasksThatParty_AndNeverSpawns()
    {
        _sut.AddScore(FactionSide.Evil, 12f);
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(Leading("wk_party"));
        _adapter.ReadParty("wk_party").Returns(FreeParty(HuntTroopLists.WitchKingId, engaging: false));

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.AreEqual("wk_party", _sut.HunterPartyId);
        _adapter.Received(1).PinAndEngagePlayer("wk_party");
        _adapter.DidNotReceive().SpawnHunterParty(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow("mapevent")]
    [DataRow("army")]
    [DataRow("siege")]
    [DataRow("settlement")]
    [DataRow("disbanding")]
    [DataRow("pinned")]
    [DataRow("inactive")]
    public void TryStartHunt_HisPartyIsBusy_WaitsAndNeverSpawns(string busy)
    {
        _sut.AddScore(FactionSide.Evil, 12f);
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(Leading("wk_party"));
        _adapter.ReadHero("lord_1_155").Returns(FreeHero());
        var party = FreeParty(HuntTroopLists.WitchKingId);
        switch (busy)
        {
            case "mapevent": party.InMapEvent = true; break;
            case "army": party.InArmy = true; break;
            case "siege": party.Besieging = true; break;
            case "settlement": party.InSettlement = true; break;
            case "disbanding": party.IsDisbanding = true; break;
            case "pinned": party.AiPinned = true; break;
            case "inactive": party.IsActive = false; break;
        }
        _adapter.ReadParty("wk_party").Returns(party);

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.IsFalse(_sut.IsHunting, busy);
        _adapter.DidNotReceive().SpawnHunterParty(Arg.Any<string>());
        _adapter.DidNotReceive().PinAndEngagePlayer(Arg.Any<string>());
    }

    [TestMethod]
    public void TryStartHunt_InAnotherLordsParty_WaitsAndNeverSpawns()
    {
        _sut.AddScore(FactionSide.Evil, 12f);
        var wk = FreeHero();
        wk.IsInAParty = true;
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(wk);

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.IsFalse(_sut.IsHunting);
        _adapter.DidNotReceive().SpawnHunterParty(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow("cannotlead")]
    [DataRow("notlord")]
    [DataRow("tournamentorsiege")]
    [DataRow("inactive")]
    public void TryStartHunt_FreeHeroUnfitToLead_WaitsAndNeverSpawns(string state)
    {
        _sut.AddScore(FactionSide.Evil, 12f);
        var wk = FreeHero();
        switch (state)
        {
            case "cannotlead": wk.CanLeadParty = false; break;
            case "notlord": wk.IsLord = false; break;
            case "tournamentorsiege": wk.SettlementBlocksSpawn = true; break;
            case "inactive": wk.IsActive = false; break;
        }
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(wk);

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.IsFalse(_sut.IsHunting, state);
        _adapter.DidNotReceive().SpawnHunterParty(Arg.Any<string>());
    }

    [TestMethod]
    public void TryStartHunt_SpawnRefused_NoHunt()
    {
        ArmWitchKingHunt();
        _adapter.SpawnHunterParty(HuntTroopLists.WitchKingId).Returns((string?)null);

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.IsFalse(_sut.IsHunting);
    }

    [TestMethod]
    public void TryStartHunt_PinRefused_EndsAtOnce_WithoutReleasingAPinItNeverSet()
    {
        ArmWitchKingHunt();
        _adapter.PinAndEngagePlayer(Party).Returns(false);

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.IsFalse(_sut.IsHunting);
        _adapter.DidNotReceive().ReleasePin(Arg.Any<string>());
    }

    [TestMethod]
    public void TryStartHunt_ConsoleForce_IgnoresScoreAndCooldown_ButHonoursTheWarGate()
    {
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(FreeHero(atWar: false));

        _sut.TryStartHunt(ignoreScoreAndCooldown: true);
        Assert.IsFalse(_sut.IsHunting, "the war gate holds even for the console");

        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(FreeHero());
        _sut.TryStartHunt(ignoreScoreAndCooldown: true);
        Assert.IsTrue(_sut.IsHunting, "a zero score does not stop the console");
    }

    // ---------------------------------------------------------------- host top-up and food

    [TestMethod]
    public void PlanTopUp_SplitsTheDeficitByShareAndPower()
    {
        var host = new[] { new HostTroop("a", 0.5f), new HostTroop("b", 0.5f) };

        var plan = EyeOfSauronService.PlanTopUp(host, 100f, id => id == "a" ? 2f : 5f, 500);

        CollectionAssert.AreEqual(new[] { ("a", 25), ("b", 10) }, plan.ToArray());
    }

    [TestMethod]
    public void PlanTopUp_CapsTheTotalAtTheRoom()
    {
        var host = new[] { new HostTroop("a", 0.5f), new HostTroop("b", 0.5f) };

        var plan = EyeOfSauronService.PlanTopUp(host, 1000f, _ => 1f, 30);

        Assert.AreEqual(30, plan.Sum(p => p.Count));
        Assert.AreEqual(15, plan.Single(p => p.TroopId == "a").Count);
    }

    [TestMethod]
    public void PlanTopUp_NonFinitePower_SkipsThatTroop()
    {
        var host = new[] { new HostTroop("a", 0.5f), new HostTroop("b", 0.5f), new HostTroop("c", 0.5f) };

        var plan = EyeOfSauronService.PlanTopUp(host, 100f,
            id => id == "a" ? float.NaN : id == "b" ? 0f : 5f, 500);

        CollectionAssert.AreEqual(new[] { ("c", 10) }, plan.ToArray());
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(0f)]
    [DataRow(-5f)]
    public void PlanTopUp_DeficitNotPositiveAndFinite_PlansNothing(float deficit)
    {
        var plan = EyeOfSauronService.PlanTopUp(HuntTroopLists.Mordor, deficit, _ => 2f, 500);

        Assert.AreEqual(0, plan.Count);
    }

    [TestMethod]
    public void PlanTopUp_NoRoom_PlansNothing()
    {
        Assert.AreEqual(0, EyeOfSauronService.PlanTopUp(HuntTroopLists.Mordor, 100f, _ => 2f, 0).Count);
    }

    [TestMethod]
    public void TryStartHunt_TopsUpToTheStrengthRatio_AfterTheHostIsRegistered()
    {
        ArmWitchKingHunt();
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, heads: 50, strength: 30f));
        float bonusSeenAtFirstAdd = -1f;
        _adapter.AddTroops(Party, Arg.Any<string>(), Arg.Any<int>()).Returns(ci =>
        {
            if (bonusSeenAtFirstAdd < 0f)
                bonusSeenAtFirstAdd = _sut.GetHostSizeBonus(Party, 10f, 0f, 60, () => 60f);
            return ci.ArgAt<int>(2);
        });

        _sut.OnDailyTick();

        // deficit = 1.3 * 100 - 30 = 100; Mordor shares 0.25/0.25/0.2/0.15/0.15 at power 2.
        _adapter.Received(1).AddTroops(Party, "mordor_num_temple_knight", 13);
        _adapter.Received(1).AddTroops(Party, "mordor_num_shadowbow", 10);
        Assert.IsTrue(bonusSeenAtFirstAdd > 0f, "the party must count as the hunter's host before its roster grows");
    }

    [TestMethod]
    public void TryStartHunt_PlayerStrengthNaN_AddsNoTroops()
    {
        ArmWitchKingHunt();
        _adapter.PlayerStrength.Returns(float.NaN);

        _sut.OnDailyTick();

        Assert.IsTrue(_sut.IsHunting);
        _adapter.DidNotReceive().AddTroops(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void TryStartHunt_HostAtTheHeadCap_AddsNoTroops()
    {
        ArmWitchKingHunt();
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, heads: 250, strength: 1f));

        _sut.OnDailyTick();

        _adapter.DidNotReceive().AddTroops(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void TryStartHunt_KhamulOfDolGuldur_GetsTheDolGuldurHost()
    {
        _sut.AddScore(FactionSide.Evil, 12f);
        _adapter.ReadHero(HuntTroopLists.KhamulId).Returns(FreeHero("dolguldur"));
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.KhamulId, strength: 0f));

        _sut.OnDailyTick();

        _adapter.Received().AddTroops(Party, "dg_khamul_shadow_knight", Arg.Any<int>());
        _adapter.DidNotReceive().AddTroops(Party, "mordor_num_temple_knight", Arg.Any<int>());
    }

    [TestMethod]
    public void TryStartHunt_Glorfindel_GetsTheElvenHost()
    {
        _adapter.PlayerKingdomId.Returns(MordorKingdom);
        _sut.AddScore(FactionSide.Free, 12f);
        _adapter.ReadHero(HuntTroopLists.GlorfindelId).Returns(FreeHero("rivendell"));
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.GlorfindelId, strength: 0f));

        _sut.OnDailyTick();

        _adapter.Received().AddTroops(Party, "rivendell_glorfindel_guard", Arg.Any<int>());
    }

    [TestMethod]
    public void FoodTarget_IsThreeDaysForTheHost() => Assert.AreEqual(38, EyeOfSauronService.FoodTarget(250));

    [TestMethod]
    public void OnDailyTick_WhileHunting_TopsUpThreeDaysOfFood()
    {
        StartWitchKingHunt();
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, heads: 100));

        _now += 1d;
        _sut.OnDailyTick();

        _adapter.Received(1).TopUpFood(Party, 15);
    }

    // ---------------------------------------------------------------- steps

    [TestMethod]
    public void ComputeStep_GapInsideTheSightMargin_IsZero() =>
        Assert.AreEqual(0f, EyeOfSauronService.ComputeStep(16f, 10f, 15f));

    [TestMethod]
    public void ComputeStep_LongGap_IsCappedAtTheStepCap() =>
        Assert.AreEqual(15f, EyeOfSauronService.ComputeStep(200f, 10f, 15f));

    [TestMethod]
    public void ComputeStep_ShortGap_LeavesOneAndAHalfSightsPlusThree() =>
        Assert.AreEqual(5f, EyeOfSauronService.ComputeStep(23f, 10f, 15f), 1e-4f);

    [DataTestMethod]
    [DataRow(float.NaN, 10f, 15f)]
    [DataRow(200f, float.NaN, 15f)]
    [DataRow(200f, 10f, float.NaN)]
    [DataRow(float.PositiveInfinity, 10f, 15f)]
    public void ComputeStep_NonFiniteInput_IsZero(float gap, float sight, float cap) =>
        Assert.AreEqual(0f, EyeOfSauronService.ComputeStep(gap, sight, cap));

    [TestMethod]
    public void PathLength_SumsTheLegsFromTheStart() =>
        Assert.AreEqual(7f, EyeOfSauronService.PathLength(0f, 0f, new[] { 3f, 3f }, new[] { 0f, 4f }), 1e-4f);

    [TestMethod]
    public void WalkPath_StopsPartWayAlongTheSecondLeg()
    {
        var (x, y) = EyeOfSauronService.WalkPath(0f, 0f, new[] { 3f, 3f }, new[] { 0f, 4f }, 5f);

        Assert.AreEqual(3f, x, 1e-4f);
        Assert.AreEqual(2f, y, 1e-4f);
    }

    [TestMethod]
    public void WalkPath_PastTheEnd_StopsAtTheLastPoint()
    {
        var (x, y) = EyeOfSauronService.WalkPath(0f, 0f, new[] { 3f }, new[] { 0f }, 50f);

        Assert.AreEqual(3f, x, 1e-4f);
        Assert.AreEqual(0f, y, 1e-4f);
    }

    private void StraightPathTo(float playerX, float sight)
    {
        _adapter.ReadStepContext(Party).Returns(new HuntStepContext
        {
            SeeingRange = sight, HunterX = 0f, HunterY = 0f, PlayerX = playerX, PlayerY = 0f,
            PathX = new[] { playerX / 2f, playerX }, PathY = new[] { 0f, 0f },
        });
        _adapter.ResolveLanding(Party, Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>())
            .Returns(ci => new HuntLanding(true, ci.ArgAt<float>(1), ci.ArgAt<float>(2)));
    }

    [TestMethod]
    public void OnHourlyTickParty_OutOfSight_StepsByTheCap_AndLandsOutsideOneAndAHalfSights()
    {
        StartWitchKingHunt();
        StraightPathTo(100f, 10f);

        _sut.OnHourlyTickParty(Party);

        _adapter.Received(1).Relocate(Party, Arg.Is<float>(x => Math.Abs(x - 15f) < 1e-3f), Arg.Is<float>(y => Math.Abs(y) < 1e-3f));
    }

    [TestMethod]
    public void OnHourlyTickParty_LandingNudgedTooClose_DoesNotRelocate()
    {
        StartWitchKingHunt();
        StraightPathTo(100f, 10f);
        _adapter.ResolveLanding(Party, Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>())
            .Returns(new HuntLanding(true, 90f, 0f));

        _sut.OnHourlyTickParty(Party);

        _adapter.DidNotReceive().Relocate(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void OnHourlyTickParty_LandingRefused_DoesNotRelocate()
    {
        StartWitchKingHunt();
        StraightPathTo(100f, 10f);
        _adapter.ResolveLanding(Party, Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>()).Returns(new HuntLanding(false, 0f, 0f));

        _sut.OnHourlyTickParty(Party);

        _adapter.DidNotReceive().Relocate(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void OnHourlyTickParty_AlreadyClose_DoesNotJump()
    {
        StartWitchKingHunt();
        StraightPathTo(17f, 10f);

        _sut.OnHourlyTickParty(Party);

        _adapter.DidNotReceive().ResolveLanding(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
        _adapter.DidNotReceive().Relocate(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void OnHourlyTickParty_Blocked_DoesNotRelocate()
    {
        StartWitchKingHunt();

        _sut.OnHourlyTickParty(Party);

        _adapter.DidNotReceive().Relocate(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>());
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(0f)]
    public void OnHourlyTickParty_SightNotPositive_DoesNotRelocate(float sight)
    {
        StartWitchKingHunt();
        StraightPathTo(100f, sight);

        _sut.OnHourlyTickParty(Party);

        _adapter.DidNotReceive().Relocate(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void OnHourlyTickParty_AnotherParty_DoesNothing()
    {
        StartWitchKingHunt();
        StraightPathTo(100f, 10f);

        _sut.OnHourlyTickParty("someone_else");

        _adapter.DidNotReceive().ReadStepContext(Arg.Any<string>());
    }

    [TestMethod]
    public void OnHourlyTickParty_OrderLost_ReissuesTheEngage_OnlyThen()
    {
        StartWitchKingHunt();
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, engaging: true));
        _sut.OnHourlyTickParty(Party);
        _adapter.DidNotReceive().PinAndEngagePlayer(Party);

        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, engaging: false));
        _sut.OnHourlyTickParty(Party);
        _adapter.Received(1).PinAndEngagePlayer(Party);
    }

    // ---------------------------------------------------------------- end conditions release the pin

    [DataTestMethod]
    [DataRow("beaten", HuntAnnouncement.Won)]
    [DataRow("otherbattle", HuntAnnouncement.Over)]
    [DataRow("destroyed", HuntAnnouncement.Over)]
    [DataRow("dead", HuntAnnouncement.Over)]
    [DataRow("captured", HuntAnnouncement.Over)]
    [DataRow("fugitive", HuntAnnouncement.Over)]
    [DataRow("partygone", HuntAnnouncement.Over)]
    [DataRow("newleader", HuntAnnouncement.Over)]
    [DataRow("peace", HuntAnnouncement.Over)]
    [DataRow("kingdomless", HuntAnnouncement.Over)]
    [DataRow("timeout", HuntAnnouncement.Over)]
    [DataRow("disabled", HuntAnnouncement.Over)]
    [DataRow("coopdaily", null)]
    [DataRow("coophourly", null)]
    [DataRow("manual", HuntAnnouncement.Over)]
    public void EveryEnd_ReleasesThePin_ClearsTheHunt_AndStartsTheCooldown(string end, HuntAnnouncement? shown)
    {
        StartWitchKingHunt();
        switch (end)
        {
            case "beaten": Battle(MordorKingdom, involvesHunter: true); break;
            case "otherbattle": Battle(null, playerEvent: false, involvesHunter: true); break;
            case "destroyed": _sut.OnPartyDestroyed(Party); break;
            case "dead": Daily(h => h.IsAlive = false); break;
            case "captured": Daily(h => h.IsPrisoner = true); break;
            case "fugitive": Daily(h => h.IsFugitive = true); break;
            case "partygone":
                _adapter.ReadParty(Party).Returns(HuntPartySnapshot.Missing);
                _sut.OnDailyTick();
                break;
            case "newleader":
                _adapter.ReadParty(Party).Returns(FreeParty("someone_else"));
                _sut.OnDailyTick();
                break;
            case "peace": Daily(h => h.AtWarWithPlayerKingdom = false); break;
            case "kingdomless":
                _adapter.PlayerKingdomId.Returns((string?)null);
                _sut.OnDailyTick();
                break;
            case "timeout":
                _now += 20d;
                _sut.OnDailyTick();
                break;
            case "disabled":
                _settings.Enabled.Returns(false);
                _sut.OnDailyTick();
                break;
            case "coopdaily":
                _coop.IsSessionActive.Returns(true);
                _sut.OnDailyTick();
                break;
            case "coophourly":
                _dedicated.IsDedicatedServer.Returns(true);
                _sut.OnHourlyTickParty(Party);
                break;
            case "manual": _sut.EndHunt(HuntEndReason.Manual); break;
        }

        Assert.IsFalse(_sut.IsHunting, end);
        Assert.IsFalse(_sut.PinnedByUs, end);
        _adapter.Received(1).ReleasePin(Party);
        Assert.AreEqual(0f, _sut.GetHostSizeBonus(Party, 10f, 0f, 60, () => 60f), "the size bonus lapses");
        if (shown.HasValue)
            _adapter.Received(1).Announce(shown.Value, HuntTroopLists.WitchKingId);
        else
            _adapter.DidNotReceive().Announce(Arg.Any<HuntAnnouncement>(), Arg.Any<string>());

        // The cooldown runs from the end: a forced restart is still allowed, an organic one is not.
        _sut.AddScore(FactionSide.Evil, 50f);
        _coop.IsSessionActive.Returns(false);
        _dedicated.IsDedicatedServer.Returns(false);
        _settings.Enabled.Returns(true);
        _adapter.PlayerKingdomId.Returns(Gondor);
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(FreeHero());
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId));
        _sut.OnDailyTick();
        Assert.IsFalse(_sut.IsHunting, "cooldown after " + end);
    }

    private void Daily(Action<HunterHeroSnapshot> change)
    {
        var hero = FreeHero();
        change(hero);
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(hero);
        _sut.OnDailyTick();
    }

    [TestMethod]
    public void EndHunt_PinNotOurs_NeverReleasesIt()
    {
        _sut.LoadFrom(new EyeOfSauronState
        {
            HunterHeroId = HuntTroopLists.WitchKingId, HunterPartyId = Party, PinnedByUs = false, HuntStartDay = 1000f,
        });

        _sut.EndHunt(HuntEndReason.Manual);

        Assert.IsFalse(_sut.IsHunting);
        _adapter.DidNotReceive().ReleasePin(Arg.Any<string>());
    }

    [TestMethod]
    public void EndHunt_ClearsTheSnapshot_BeforeInvalidatingTheSpeed()
    {
        StartWitchKingHunt();
        InSight();
        Assert.IsTrue(_sut.TryGetSpeedFloor(Party, 0f, out _), "precondition: the floor is live");
        float floorAtInvalidate = -1f;
        _adapter.When(a => a.InvalidateSpeed(Party)).Do(_ =>
            floorAtInvalidate = _sut.TryGetSpeedFloor(Party, 0f, out var f) ? f : 0f);

        _sut.EndHunt(HuntEndReason.Manual);

        Assert.AreEqual(0f, floorAtInvalidate, "the recalculated speed must not see the old floor");
        _adapter.Received().InvalidateSpeed(Party);
    }

    [TestMethod]
    public void EndHunt_NoHunt_DoesNothing()
    {
        _sut.EndHunt(HuntEndReason.Manual);

        _adapter.DidNotReceive().ReleasePin(Arg.Any<string>());
        _adapter.DidNotReceive().Announce(Arg.Any<HuntAnnouncement>(), Arg.Any<string>());
    }

    [TestMethod]
    public void OnMapEventEnded_BattleWithoutTheHunter_LeavesTheHunt()
    {
        StartWitchKingHunt();

        Battle(MordorKingdom);

        Assert.IsTrue(_sut.IsHunting);
    }

    [TestMethod]
    public void OnPartyDestroyed_AnotherParty_LeavesTheHunt()
    {
        StartWitchKingHunt();

        _sut.OnPartyDestroyed("someone_else");

        Assert.IsTrue(_sut.IsHunting);
    }

    // ---------------------------------------------------------------- the sight snapshot and the speed floor

    private void InSight(float playerSpeed = 4f, float distance = 5f, float sight = 10f)
    {
        _adapter.ReadSight(Party).Returns(new HuntSightReading
        {
            Valid = true, HunterActive = true, PlayerActive = true, SeeingRange = sight, Distance = distance,
            PlayerSpeed = playerSpeed,
        });
        _sut.OnTick();
    }

    [TestMethod]
    public void TryGetSpeedFloor_InSight_IsThePlayersSpeedTimesTheMultiplier()
    {
        StartWitchKingHunt();
        InSight(playerSpeed: 4f);

        Assert.IsTrue(_sut.TryGetSpeedFloor(Party, 3f, out var floor));
        Assert.AreEqual(5.2f, floor, 1e-4f);
    }

    [TestMethod]
    public void TryGetSpeedFloor_AnotherParty_None()
    {
        StartWitchKingHunt();
        InSight();

        Assert.IsFalse(_sut.TryGetSpeedFloor("someone_else", 3f, out _));
        Assert.IsFalse(_sut.TryGetSpeedFloor(null, 3f, out _));
    }

    [TestMethod]
    public void TryGetSpeedFloor_OutOfSight_None()
    {
        StartWitchKingHunt();
        InSight(distance: 11f, sight: 10f);

        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, 3f, out _));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void TryGetSpeedFloor_NonFiniteMultiplier_None(float k)
    {
        StartWitchKingHunt();
        _settings.HunterSpeedMultiplier.Returns(k);
        InSight();

        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, 3f, out _));
    }

    [TestMethod]
    public void TryGetSpeedFloor_NaNPlayerSpeed_None()
    {
        StartWitchKingHunt();
        InSight(playerSpeed: float.NaN);

        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, 3f, out _));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void OnTick_NonFiniteDistanceOrSight_IsOutOfSight(float bad)
    {
        StartWitchKingHunt();
        InSight(distance: bad);
        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, 3f, out _));

        InSight(sight: bad);
        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, 3f, out _));
    }

    [TestMethod]
    public void TryGetSpeedFloor_FloorNotAboveTheCurrentSpeed_None()
    {
        StartWitchKingHunt();
        InSight(playerSpeed: 4f);

        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, 6f, out _));
        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, float.NaN, out _));
    }

    [TestMethod]
    public void TryGetSpeedFloor_NeverUnderOne()
    {
        StartWitchKingHunt();
        InSight(playerSpeed: 0.5f);

        Assert.IsTrue(_sut.TryGetSpeedFloor(Party, 0.2f, out var floor));
        Assert.AreEqual(1f, floor);
    }

    [DataTestMethod]
    [DataRow("player at sea")]
    [DataRow("player in battle")]
    [DataRow("hunter in battle")]
    [DataRow("player inactive")]
    public void OnTick_ExcludedState_IsOutOfSight(string state)
    {
        StartWitchKingHunt();
        var r = new HuntSightReading
        {
            Valid = true, HunterActive = true, PlayerActive = true, SeeingRange = 10f, Distance = 5f, PlayerSpeed = 4f,
            PlayerAtSea = state == "player at sea", PlayerInMapEvent = state == "player in battle",
            HunterInMapEvent = state == "hunter in battle",
        };
        if (state == "player inactive") r.PlayerActive = false;
        _adapter.ReadSight(Party).Returns(r);

        _sut.OnTick();

        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, 3f, out _), state);
    }

    [TestMethod]
    public void OnTick_SightFlipsOrSpeedMoves_InvalidatesTheHuntersSpeed_OtherwiseNot()
    {
        StartWitchKingHunt();

        InSight(playerSpeed: 4f);
        _adapter.Received(1).InvalidateSpeed(Party);

        InSight(playerSpeed: 4.1f);
        _adapter.Received(1).InvalidateSpeed(Party);

        InSight(playerSpeed: 5f);
        _adapter.Received(2).InvalidateSpeed(Party);

        InSight(distance: 20f);
        _adapter.Received(3).InvalidateSpeed(Party);
    }

    [TestMethod]
    public void OnTick_NoHunt_ReadsNothing()
    {
        _sut.OnTick();

        _adapter.DidNotReceive().ReadSight(Arg.Any<string>());
    }

    // ---------------------------------------------------------------- the size bonus

    [TestMethod]
    public void GetHostSizeBonus_AnotherParty_ZeroAndTheRosterIsNeverWalked()
    {
        StartWitchKingHunt();
        bool walked = false;

        float bonus = _sut.GetHostSizeBonus("someone_else", 50f, 0f, 200, () => { walked = true; return 300f; });

        Assert.AreEqual(0f, bonus);
        Assert.IsFalse(walked);
    }

    [TestMethod]
    public void GetHostSizeBonus_NoHunt_Zero() => Assert.AreEqual(0f, _sut.GetHostSizeBonus(Party, 50f, 0f, 200, () => 300f));

    [TestMethod]
    public void GetHostSizeBonus_HostOverTheLimit_LiftsItToTheWeightedCount_WithHalfASlotOfHeadroom()
    {
        StartWitchKingHunt();

        Assert.AreEqual(250.5f, _sut.GetHostSizeBonus(Party, 50f, 0.5f, 200, () => 299.2f), 1e-3f);
    }

    [TestMethod]
    public void GetHostSizeBonus_WeightedBelowRaw_UsesTheRawCount()
    {
        StartWitchKingHunt();

        Assert.AreEqual(150.5f, _sut.GetHostSizeBonus(Party, 50f, 0f, 200, () => 120f), 1e-3f);
    }

    [TestMethod]
    public void GetHostSizeBonus_NaNWeighted_UsesTheRawCount()
    {
        StartWitchKingHunt();

        Assert.AreEqual(150.5f, _sut.GetHostSizeBonus(Party, 50f, 0f, 200, () => float.NaN), 1e-3f);
    }

    [TestMethod]
    public void GetHostSizeBonus_LimitAlreadyCoversTheHost_Zero()
    {
        StartWitchKingHunt();

        Assert.AreEqual(0f, _sut.GetHostSizeBonus(Party, 300f, 0f, 200, () => 300f));
    }

    [DataTestMethod]
    [DataRow(float.NaN, 0f)]
    [DataRow(float.PositiveInfinity, 0f)]
    [DataRow(50f, float.NaN)]
    [DataRow(50f, -1f)]
    public void GetHostSizeBonus_RejectedInput_Zero(float limit, float sumOfFactors)
    {
        StartWitchKingHunt();

        Assert.AreEqual(0f, _sut.GetHostSizeBonus(Party, limit, sumOfFactors, 200, () => 300f));
    }

    [TestMethod]
    public void GetHostSizeBonus_HugeDeficit_ClampsToTheMaxFlatBonus()
    {
        StartWitchKingHunt();

        Assert.AreEqual(TAOM.Features.AiPartySize.AiPartySizeService.MaxFlatBonus,
            _sut.GetHostSizeBonus(Party, 1f, 0f, 200, () => 5e6f));
    }

    // ---------------------------------------------------------------- session reset and persistence

    [TestMethod]
    public void ResetForNewSession_ClearsScoresTheHuntAndTheSnapshot()
    {
        StartWitchKingHunt();
        InSight();

        _sut.ResetForNewSession();

        Assert.IsFalse(_sut.IsHunting);
        Assert.IsFalse(_sut.PinnedByUs);
        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Evil));
        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, 0f, out _));
        Assert.AreEqual(0f, _sut.GetHostSizeBonus(Party, 10f, 0f, 60, () => 60f));
        _adapter.DidNotReceive().ReleasePin(Arg.Any<string>());
    }

    [TestMethod]
    public void ResetForNewSession_ClearsTheCooldown()
    {
        StartWitchKingHunt();
        _sut.EndHunt(HuntEndReason.Manual);

        _sut.ResetForNewSession();
        ArmWitchKingHunt();
        _sut.OnDailyTick();

        Assert.IsTrue(_sut.IsHunting);
    }

    [TestMethod]
    public void CaptureState_ThenLoadFrom_RoundTripsTheRecord()
    {
        StartWitchKingHunt();
        var saved = _sut.CaptureState();

        _sut.ResetForNewSession();
        _sut.LoadFrom(saved);
        var again = _sut.CaptureState();

        Assert.AreEqual(HuntTroopLists.WitchKingId, again.HunterHeroId);
        Assert.AreEqual(Party, again.HunterPartyId);
        Assert.IsTrue(again.PinnedByUs);
        Assert.AreEqual(saved.ScoreVsEvil, again.ScoreVsEvil);
        Assert.AreEqual(saved.HuntStartDay, again.HuntStartDay);
        Assert.AreEqual(saved.LastDecayDay, again.LastDecayDay);
        Assert.AreEqual(saved.CooldownUntilDay, again.CooldownUntilDay);
    }

    [TestMethod]
    public void LoadFrom_DropsTheOldSightState_ButKeepsTheHostRegistered()
    {
        StartWitchKingHunt();
        InSight();
        var saved = _sut.CaptureState();

        _sut.LoadFrom(saved);

        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, 0f, out _), "no in-sight state survives a load");
        Assert.IsTrue(_sut.GetHostSizeBonus(Party, 10f, 0f, 60, () => 60f) > 0f, "the loaded host is not shed");
    }

    [TestMethod]
    public void LoadFrom_NonFiniteScores_LoadAsZero()
    {
        _sut.LoadFrom(new EyeOfSauronState { ScoreVsEvil = float.NaN, ScoreVsFree = float.PositiveInfinity, LastDecayDay = 1000f });

        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Evil));
        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Free));
    }

    [TestMethod]
    public void OnSessionLaunched_HuntersPartyGone_EndsTheHunt()
    {
        _sut.LoadFrom(new EyeOfSauronState
        {
            HunterHeroId = HuntTroopLists.WitchKingId, HunterPartyId = Party, PinnedByUs = true, HuntStartDay = 1000f,
        });

        _sut.OnSessionLaunched();

        Assert.IsFalse(_sut.IsHunting);
    }

    [TestMethod]
    public void OnSessionLaunched_HuntersPartyThere_KeepsTheHunt()
    {
        _sut.LoadFrom(new EyeOfSauronState
        {
            HunterHeroId = HuntTroopLists.WitchKingId, HunterPartyId = Party, PinnedByUs = true, HuntStartDay = 1000f,
        });
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId));

        _sut.OnSessionLaunched();

        Assert.IsTrue(_sut.IsHunting);
    }

    // ---------------------------------------------------------------- console helpers

    [TestMethod]
    public void AddScore_NeverGoesBelowZero()
    {
        _sut.AddScore(FactionSide.Evil, 5f);
        _sut.AddScore(FactionSide.Evil, -50f);

        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Evil));
    }

    [TestMethod]
    public void AddScore_NonFinite_IsIgnored()
    {
        _sut.AddScore(FactionSide.Evil, 5f);
        _sut.AddScore(FactionSide.Evil, float.NaN);

        Assert.AreEqual(5f, _sut.GetScore(FactionSide.Evil), 1e-4f);
    }

    [TestMethod]
    public void Describe_NamesTheHuntAndTheScores()
    {
        StartWitchKingHunt();
        _adapter.HeroName(HuntTroopLists.WitchKingId).Returns("Witch-king of Angmar");

        var text = string.Join("\n", _sut.Describe());

        StringAssert.Contains(text, "Witch-king of Angmar");
        StringAssert.Contains(text, Party);
        StringAssert.Contains(text, "12.0");
    }
}
