using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.HiddenHosts;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>
/// #846 phase 3: what the service does with the memory. A reading that reaches the player records a count; a nearer reading
/// later is exact, a farther one falls back to the memory with a "~"; the memory fades by the hour; an enemy host read from
/// afar trains Scouting once per stage per day; a save round trips and a reload or a new campaign starts clean. Counts and
/// ratios are chosen from the threshold table: 123 reads 1 digit at 0.5, 2 digits at 0.3, 3 digits at 0.1 and none at 0.7.
/// </summary>
[TestClass]
public class HiddenHostsMemoryServiceTests
{
    private IHiddenHostsSettings _settings = null!;
    private HiddenHostsService _sut = null!;

    [TestInitialize]
    public void SetUp()
    {
        _settings = Substitute.For<IHiddenHostsSettings>();
        _settings.Enabled.Returns(true);
        _sut = new HiddenHostsService(_settings);
    }

    private static PartyIntelSnapshot Snap(int count, float ratio, string id = "p1", int hour = 100, bool atWar = false,
        bool visible = true, int skill = 0) => new()
    {
        HasData = true, StringId = id, HealthyCount = count, OwnHealthyCount = count, DistanceToMain = ratio * 100f,
        MainSeeingRange = 100f, SpottingRatio = 1f, ScoutingSkill = skill, IsVisible = visible, AtWar = atWar, NowHour = hour,
    };

    private static SettlementIntelSnapshot Town(int defenders, float ratio, string id = "town_1", int hour = 100) => new()
    {
        HasData = true, StringId = id, DefenderCount = defenders, DistanceToMain = ratio * 100f, MainSeeingRange = 100f, NowHour = hour,
    };

    private static PartyIntelSnapshot StampSnap(string id, int healthy, bool visible, int hour, int scout = 0) => new()
    {
        HasData = true, StringId = id, HealthyCount = healthy, OwnHealthyCount = healthy, IsVisible = visible, NowHour = hour,
        ScoutingSkill = scout, InPlainSight = true,
    };

    private void Stamp(string id, int healthy, bool visible, int hour, int scout = 0) => _sut.Stamp(StampSnap(id, healthy, visible, hour, scout));

    private void StampTown(string id, int defenders, int hour) => _sut.StampSettlement(new SettlementIntelSnapshot
    {
        HasData = true, StringId = id, DefenderCount = defenders, IsPlayerSettlement = true, NowHour = hour,
    });

    private static Func<string, bool?> NoSettlementSight => id => throw new AssertFailedException("no settlement row in this test");

    private string? Far(int count, float ratio = 0.7f, string id = "p1", int hour = 101) => _sut.Evaluate(Snap(count, ratio, id, hour)).CountText;

    // ---- Nameplate display

    [TestMethod]
    public void Evaluate_AFarReadingAfterANearOne_ShowsTheRememberedCount_WithATilde()
    {
        _sut.Evaluate(Snap(123, 0.1f));

        var intel = _sut.Evaluate(Snap(123, 0.5f));

        Assert.IsTrue(intel.Masked);
        Assert.IsFalse(intel.IsHorizon);
        Assert.AreEqual("~123", intel.CountText);
    }

    [TestMethod]
    public void Evaluate_ARememberedPartialCount_KeepsItsQuestionMarks()
    {
        _sut.Evaluate(Snap(123, 0.3f));

        Assert.AreEqual("~12?", Far(123));
    }

    [TestMethod]
    public void Evaluate_NothingRemembered_ShowsThePlainMask()
    {
        Assert.AreEqual("???", Far(123));
        Assert.AreEqual("1??", Far(123, 0.5f, id: "other"));
    }

    [TestMethod]
    public void Evaluate_ANearerReadingThanTheMemory_ShowsTheLiveCount_AndRefreshesTheMemory()
    {
        _sut.Evaluate(Snap(123, 0.5f));

        Assert.AreEqual("12?", _sut.Evaluate(Snap(123, 0.3f)).CountText, "live, no tilde");
        Assert.AreEqual("~12?", Far(123), "and the memory now holds two digits");
    }

    [TestMethod]
    public void Evaluate_AReadingEqualToTheMemory_ShowsLive()
    {
        _sut.Evaluate(Snap(123, 0.5f));

        Assert.AreEqual("1??", _sut.Evaluate(Snap(123, 0.5f, hour: 140)).CountText);
    }

    [TestMethod]
    public void Evaluate_ACountReadInFull_BeatsAMemoryOfAnotherSize()
    {
        _sut.Evaluate(Snap(123, 0.1f));

        var intel = _sut.Evaluate(Snap(5, 0.1f, hour: 130));

        Assert.IsFalse(intel.Masked, "a five-man host in plain view is not remembered as 123");
    }

    [TestMethod]
    public void Evaluate_AtTheHorizon_IgnoresTheMemory()
    {
        _sut.Evaluate(Snap(123, 0.1f));

        var intel = _sut.Evaluate(Snap(123, 0.95f));

        Assert.IsTrue(intel.IsHorizon);
        Assert.AreEqual("...", intel.CountText);
    }

    [TestMethod]
    public void Evaluate_AfterTheHorizon_TheMemoryIsStillThere()
    {
        _sut.Evaluate(Snap(123, 0.1f));
        _sut.Evaluate(Snap(123, 0.95f));

        Assert.AreEqual("~123", Far(123));
    }

    [TestMethod]
    public void Evaluate_AReadingThatSawNoDigit_RecordsNothing()
    {
        Far(123);

        Assert.AreEqual("HH1", _sut.EncodeMemory(101));
    }

    [TestMethod]
    public void Evaluate_AnExemptParty_RecordsNothing()
    {
        _sut.Evaluate(Snap(123, 0.1f) with { InPlayerClan = true });
        _sut.Evaluate(Snap(123, 0.1f, id: "b") with { InPlainSight = true });
        _sut.Evaluate(Snap(123, 0.1f, id: "c") with { IsInfoHidden = true });

        Assert.AreEqual("HH1", _sut.EncodeMemory(101));
    }

    [TestMethod]
    public void Evaluate_TheSettingOff_RecordsNothing_AndShowsNothing()
    {
        _settings.Enabled.Returns(false);

        Assert.IsFalse(_sut.Evaluate(Snap(123, 0.1f)).Masked);
        Assert.AreEqual("HH1", _sut.EncodeMemory(101));
    }

    [TestMethod]
    public void Evaluate_AnUnusableClock_RecordsNothing_AndShowsNoMemory()
    {
        _sut.Evaluate(Snap(123, 0.1f, hour: -1));
        Assert.AreEqual("HH1", _sut.EncodeMemory(-1));

        _sut.Evaluate(Snap(123, 0.1f));
        Assert.AreEqual("???", _sut.Evaluate(Snap(123, 0.7f, hour: -1)).CountText, "the memory is not consulted without a clock");
    }

    private sealed class AlwaysOn : IHiddenHostsSettings
    {
        public bool Enabled => true;
    }

    [TestMethod]
    public void Evaluate_AHostOfAnotherSizeThanRemembered_ShowsTheLiveView_NotTheMemory()
    {
        _sut.Evaluate(Snap(123, 0.1f));

        Assert.AreEqual("?", Far(7, 0.7f), "no digit readable, but the host plainly is not the 123 remembered");
        Assert.AreEqual("?", Far(7, 0.7f), "and the memory is gone, not just hidden");
    }

    [TestMethod]
    public void Evaluate_ALargerHostReadWithOneDigit_ReplacesTheMemoryOfAnArmyOfEighty()
    {
        _sut.Evaluate(Snap(80, 0.2f));

        var intel = _sut.Evaluate(Snap(300, 0.5f, hour: 101));

        Assert.AreEqual("3??", intel.CountText, "not '~80'");
    }

    [TestMethod]
    public void Evaluate_TheSameSizeHostReadWithFewerDigits_KeepsTheTilde()
    {
        _sut.Evaluate(Snap(150, 0.1f));

        Assert.AreEqual("~150", _sut.Evaluate(Snap(150, 0.3f, hour: 101)).CountText);
    }

    [TestMethod]
    public void Evaluate_SteadyStateWithAMemory_AllocatesNothing()
    {
        var counter = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
        Assert.IsNotNull(counter, "the runtime must expose GC.GetAllocatedBytesForCurrentThread");
        var allocatedBytes = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), counter!);
        // A hand-written settings fake: an NSubstitute substitute records every call, which allocates by itself.
        var sut = new HiddenHostsService(new AlwaysOn());
        sut.Evaluate(Snap(123, 0.1f));
        var far = Snap(123, 0.7f, hour: 101);
        var near = Snap(123, 0.1f, hour: 101);
        for (int i = 0; i < 5; i++)
        {
            sut.Evaluate(far);
            sut.Evaluate(near);
            sut.Evaluate(far);
        }

        long before = allocatedBytes();
        for (int i = 0; i < 1000; i++)
        {
            sut.Evaluate(far);
            sut.Evaluate(near);
        }
        long allocated = allocatedBytes() - before;

        Assert.AreEqual(0L, allocated, "a nameplate read per frame must not allocate, remembered or live");
    }

    [TestMethod]
    public void Evaluate_SteadyStateMemory_ReturnsTheSameStringInstance()
    {
        _sut.Evaluate(Snap(123, 0.1f));

        Assert.AreSame(Far(123), Far(123));
    }

    [TestMethod]
    public void Evaluate_TheTextCacheKnowsRememberedFromLive()
    {
        _sut.Evaluate(Snap(123, 0.5f));
        var live = _sut.Evaluate(Snap(123, 0.5f, hour: 102)).CountText;
        _sut.Evaluate(Snap(123, 0.3f));
        var remembered = Far(123);

        Assert.AreEqual("1??", live);
        Assert.AreEqual("~12?", remembered);
    }

    // ---- The fade

    [TestMethod]
    public void Fade_ARememberedCountLosesADigitAtTheHoldAndEveryStepAfter_AtScoutZero()
    {
        _sut.Evaluate(Snap(123, 0.1f, hour: 100));
        _sut.OnVisibilityChanged("p1", false, 0);

        _sut.FadeHourly(105, 0, NoSettlementSight);
        Assert.AreEqual("~123", Far(123, hour: 105));
        _sut.FadeHourly(106, 0, NoSettlementSight);
        Assert.AreEqual("~12?", Far(123, hour: 106));
        _sut.FadeHourly(113, 0, NoSettlementSight);
        Assert.AreEqual("~12?", Far(123, hour: 113));
        _sut.FadeHourly(114, 0, NoSettlementSight);
        Assert.AreEqual("~1??", Far(123, hour: 114));
        _sut.FadeHourly(122, 0, NoSettlementSight);
        Assert.AreEqual("???", Far(123, hour: 122), "nothing left to remember");
    }

    [TestMethod]
    public void Fade_AHostStillInSight_NeverFades()
    {
        _sut.Evaluate(Snap(123, 0.1f, hour: 100));

        _sut.FadeHourly(500, 0, NoSettlementSight);

        Assert.AreEqual("~123", Far(123, hour: 500));
    }

    [TestMethod]
    public void Fade_ScoutingSlowsTheFade()
    {
        _sut.Evaluate(Snap(123, 0.1f, hour: 100, skill: 150));
        _sut.OnVisibilityChanged("p1", false, 150);

        _sut.FadeHourly(106, 150, NoSettlementSight);
        Assert.AreEqual("~123", Far(123, hour: 106), "hold is 12 hours at 150 Scouting");
        _sut.FadeHourly(112, 150, NoSettlementSight);
        Assert.AreEqual("~12?", Far(123, hour: 112));
    }

    [TestMethod]
    public void Fade_ABackInSightHost_StopsFading()
    {
        _sut.Evaluate(Snap(123, 0.1f, hour: 100));
        _sut.OnVisibilityChanged("p1", false, 0);
        _sut.OnVisibilityChanged("p1", true, 0);

        _sut.FadeHourly(300, 0, NoSettlementSight);

        Assert.AreEqual("~123", Far(123, hour: 300));
    }

    [TestMethod]
    public void Forget_DropsTheRow()
    {
        _sut.Evaluate(Snap(123, 0.1f));

        _sut.Forget("p1");

        Assert.AreEqual("???", Far(123));
        _sut.Forget("never_seen");
    }

    [TestMethod]
    public void OnVisibilityChanged_APartyWithoutARow_DoesNothing()
    {
        _sut.OnVisibilityChanged("ghost", false, 0);

        Assert.AreEqual("HH1", _sut.EncodeMemory(101));
    }

    // ---- Exact stamps

    [TestMethod]
    public void Stamp_RemembersEveryDigit()
    {
        Stamp("p1", 123, visible: true, hour: 100);

        Assert.AreEqual("~123", Far(123));
    }

    [TestMethod]
    public void Stamp_OverwritesAWorseMemory_AndANewerStampWins()
    {
        _sut.Evaluate(Snap(123, 0.5f));

        Stamp("p1", 140, true, 110);

        Assert.AreEqual("~140", Far(140, hour: 111));
    }

    [TestMethod]
    public void Stamp_AHostInsideASettlement_IsNotVisible_SoItsClockRunsFromEveryStamp()
    {
        Stamp("p1", 123, visible: false, hour: 100);
        _sut.FadeHourly(106, 0, NoSettlementSight);
        Assert.AreEqual("~12?", Far(123, hour: 106));

        Stamp("p1", 123, false, 107);
        Assert.AreEqual("~123", Far(123, hour: 107), "a fresh stamp restores every digit");
    }

    [TestMethod]
    public void Stamp_TheSettingOff_NoClock_OrNoId_DoesNothing()
    {
        Stamp("a", 123, true, -1);
        Stamp(null!, 123, true, 100);
        Stamp("", 123, true, 100);
        _sut.Stamp(PartyIntelSnapshot.NoData);
        _settings.Enabled.Returns(false);
        Stamp("b", 123, true, 100);
        _settings.Enabled.Returns(true);

        Assert.AreEqual("HH1", _sut.EncodeMemory(100));
    }

    [TestMethod]
    public void Stamp_APartyNotInPlainSight_IsNotStamped()
    {
        _sut.Stamp(StampSnap("p1", 123, true, 100) with { InPlainSight = false });

        Assert.AreEqual("HH1", _sut.EncodeMemory(100));
    }

    [TestMethod]
    public void Stamp_EachOwnSideOrHiddenFact_KeepsAPartyOutOfTheMemory()
    {
        var plain = StampSnap("p1", 123, true, 100);
        _sut.Stamp(plain with { IsMainParty = true });
        _sut.Stamp(plain with { InPlayerClan = true });
        _sut.Stamp(plain with { InPlayerArmy = true });
        _sut.Stamp(plain with { TrueSight = true });
        _sut.Stamp(plain with { IsInfoHidden = true });

        Assert.AreEqual("HH1", _sut.EncodeMemory(100));
    }

    [TestMethod]
    public void Stamp_AndTheMaskShareOneOwnSidePredicate_ForEveryFact()
    {
        var facts = new System.Func<PartyIntelSnapshot, PartyIntelSnapshot>[]
        {
            s => s with { IsMainParty = true }, s => s with { InPlayerClan = true }, s => s with { InPlayerArmy = true },
            s => s with { TrueSight = true }, s => s with { IsInfoHidden = true },
        };
        foreach (var fact in facts)
        {
            var sut = new HiddenHostsService(_settings);
            var s = fact(StampSnap("p1", 123, true, 100) with { InPlainSight = false, DistanceToMain = 70f, MainSeeingRange = 100f, SpottingRatio = 1f });

            sut.Stamp(s with { InPlainSight = true });
            bool masked = sut.Evaluate(s).Masked;

            Assert.IsFalse(masked, "an own-side or hidden party is never masked");
            Assert.AreEqual("HH1", sut.EncodeMemory(100), "and never remembered");
        }
    }

    [TestMethod]
    public void StampSettlement_OnlyTheSettlementThePlayerIsIn_AndNeverTheirOwnSide()
    {
        var town = new SettlementIntelSnapshot { HasData = true, StringId = "town_1", DefenderCount = 245, NowHour = 100, IsPlayerSettlement = true };
        _sut.StampSettlement(town with { IsPlayerSettlement = false });
        _sut.StampSettlement(town with { OwnedByPlayerClan = true });
        _sut.StampSettlement(town with { PlayerClanHeroInside = true });
        _sut.StampSettlement(town with { TrueSight = true });
        _sut.StampSettlement(town with { HasData = false });
        _sut.StampSettlement(town with { StringId = null });
        _sut.StampSettlement(town with { NowHour = -1 });
        Assert.AreEqual("HH1", _sut.EncodeMemory(100));

        _sut.StampSettlement(town);
        _settings.Enabled.Returns(false);
        _sut.StampSettlement(town with { StringId = "town_2" });

        StringAssert.Contains(_sut.EncodeMemory(100), "s|town_1|245|3|100");
        Assert.IsFalse(_sut.EncodeMemory(100).Contains("town_2"));
    }

    [TestMethod]
    public void Stamp_PaysNoScoutXp_EvenForAnEnemyHost()
    {
        Stamp("p1", 123, true, 100);

        Assert.AreEqual(0, _sut.TakePendingScoutXp());
    }

    // ---- Tooltip verdicts

    [TestMethod]
    public void TooltipParty_AMemoryThatBeatsTheLiveReading_IsCarriedInTheVerdict_WithItsAge()
    {
        _sut.EvaluateTooltipParty(Snap(123, 0.1f, hour: 100));

        var v = _sut.EvaluateTooltipParty(Snap(123, 0.7f, hour: 109));

        Assert.AreEqual(TooltipMode.Masked, v.Mode);
        Assert.AreEqual("~123", v.MemoryText);
        Assert.AreEqual(9, v.MemoryHours);
    }

    [TestMethod]
    public void TooltipParty_ALiveReadingAsGoodAsTheMemory_CarriesNone()
    {
        _sut.EvaluateTooltipParty(Snap(123, 0.5f, hour: 100));

        Assert.IsNull(_sut.EvaluateTooltipParty(Snap(123, 0.5f, hour: 109)).MemoryText);
    }

    [TestMethod]
    public void TooltipParty_AtTheHorizon_CarriesNoMemory()
    {
        _sut.EvaluateTooltipParty(Snap(123, 0.1f));

        var v = _sut.EvaluateTooltipParty(Snap(123, 0.95f));

        Assert.AreEqual(TooltipMode.Horizon, v.Mode);
        Assert.IsNull(v.MemoryText);
    }

    [TestMethod]
    public void TooltipParty_ALeaderOfAnArmy_CarriesNoMemory_ButTheArmyTooltipDoes()
    {
        var leader = Snap(345, 0.1f) with { LeadsArmy = true, OwnHealthyCount = 45 };
        _sut.EvaluateTooltipArmy(leader);

        var party = _sut.EvaluateTooltipParty(leader with { DistanceToMain = 70f });
        var army = _sut.EvaluateTooltipArmy(leader with { DistanceToMain = 70f });

        Assert.IsNull(party.MemoryText, "a party tooltip's roster is the leader's own, the memory holds the army");
        Assert.AreEqual("~345", army.MemoryText);
    }

    [TestMethod]
    public void TooltipParty_MemoryAndNameplateShareOneRow()
    {
        _sut.Evaluate(Snap(123, 0.3f));

        Assert.AreEqual("~12?", _sut.EvaluateTooltipParty(Snap(123, 0.7f)).MemoryText);
    }

    [TestMethod]
    public void TooltipParty_AnUnusableClock_CarriesNoMemory()
    {
        _sut.EvaluateTooltipParty(Snap(123, 0.1f));

        Assert.IsNull(_sut.EvaluateTooltipParty(Snap(123, 0.7f, hour: -1)).MemoryText);
    }

    [TestMethod]
    public void TooltipSettlement_ARememberedDefenderCount_IsCarriedAndShownByTheDefendersRow()
    {
        _sut.EvaluateTooltipSettlement(Town(245, 0.1f, hour: 100));

        var v = _sut.EvaluateTooltipSettlement(Town(245, 0.7f, hour: 130));

        Assert.AreEqual("~245", v.MemoryText);
        Assert.AreEqual(30, v.MemoryHours);
        Assert.AreEqual("~245", _sut.DefendersText(v));
        Assert.IsTrue(_sut.MemoryDrawn);
    }

    [TestMethod]
    public void TooltipSettlement_NothingRemembered_ShowsTheMask()
    {
        var v = _sut.EvaluateTooltipSettlement(Town(245, 0.5f));

        Assert.IsNull(v.MemoryText);
        Assert.AreEqual("2??", _sut.DefendersText(v));
        Assert.IsFalse(_sut.MemoryDrawn);
    }

    [TestMethod]
    public void TooltipSettlement_Fades_ThroughTheSightPoll()
    {
        _sut.EvaluateTooltipSettlement(Town(245, 0.1f, hour: 100));

        _sut.FadeHourly(103, 0, id => false);
        _sut.FadeHourly(106, 0, id => false);

        Assert.AreEqual("~24?", _sut.EvaluateTooltipSettlement(Town(245, 0.7f, hour: 107)).MemoryText);
    }

    [TestMethod]
    public void TooltipSettlement_AGoneSettlement_IsForgotten()
    {
        _sut.EvaluateTooltipSettlement(Town(245, 0.1f));

        _sut.FadeHourly(101, 0, id => null);

        Assert.IsNull(_sut.EvaluateTooltipSettlement(Town(245, 0.7f, hour: 102)).MemoryText);
    }

    [TestMethod]
    public void StampSettlement_RemembersTheDefendersExactly()
    {
        StampTown("town_1", 245, 100);

        Assert.AreEqual("~245", _sut.EvaluateTooltipSettlement(Town(245, 0.7f, hour: 101)).MemoryText);
    }

    [TestMethod]
    public void TooltipEvent_NeverCarriesAMemory()
    {
        var v = _sut.EvaluateTooltipEvent(new EventIntelSnapshot
        {
            HasData = true, TotalHealthy = 500, DistanceToMain = 70f, MainSeeingRange = 100f,
        });

        Assert.IsNull(v.MemoryText);
    }

    [TestMethod]
    public void MaskTooltipTotal_WithAMemory_ReturnsIt_AndMarksItDrawn()
    {
        _sut.EvaluateTooltipParty(Snap(123, 0.1f));
        var v = _sut.EvaluateTooltipParty(Snap(123, 0.7f));

        Assert.AreEqual("~123", _sut.MaskTooltipTotal(v, 123));
        Assert.IsTrue(_sut.MemoryDrawn);
    }

    [TestMethod]
    public void MaskTooltipTotal_WithoutAMemory_IsTheLiveMask()
    {
        var v = _sut.EvaluateTooltipParty(Snap(123, 0.5f));

        Assert.AreEqual(_sut.MaskTooltipCount(v, 123), _sut.MaskTooltipTotal(v, 123));
        Assert.AreEqual("1??", _sut.MaskTooltipTotal(v, 123));
        Assert.IsFalse(_sut.MemoryDrawn);
    }

    [TestMethod]
    public void MaskTooltipCount_IgnoresTheMemory_SoOtherRowsStayLive()
    {
        _sut.EvaluateTooltipParty(Snap(123, 0.1f));
        var v = _sut.EvaluateTooltipParty(Snap(123, 0.7f));

        Assert.AreEqual("???", _sut.MaskTooltipCount(v, 123));
    }

    [TestMethod]
    public void MaskTooltipTotal_AtTheHorizon_IsThreeDots()
    {
        var v = _sut.EvaluateTooltipParty(Snap(123, 0.95f));

        Assert.AreEqual("...", _sut.MaskTooltipTotal(v, 123));
    }

    [TestMethod]
    public void MaskTooltipTotal_Vanilla_IsNull() => Assert.IsNull(_sut.MaskTooltipTotal(TooltipVerdict.Vanilla, 123));

    // ---- The nameplate marks the memory drawn

    [TestMethod]
    public void MemoryDrawn_IsSetWhenANameplateShowsATilde_AndOnlyThen()
    {
        _sut.Evaluate(Snap(123, 0.1f));
        Assert.IsFalse(_sut.MemoryDrawn, "a live count is not a memory");

        Far(123);

        Assert.IsTrue(_sut.MemoryDrawn);
    }

    // ---- Scouting XP

    [TestMethod]
    public void Xp_AnEnemyHostReadFromAfar_PaysTwentyThirtyFiftyAsItsDigitsAreRead()
    {
        _sut.Evaluate(Snap(123, 0.5f, atWar: true));
        Assert.AreEqual(20, _sut.TakePendingScoutXp());

        _sut.Evaluate(Snap(123, 0.3f, atWar: true));
        Assert.AreEqual(30, _sut.TakePendingScoutXp());

        _sut.Evaluate(Snap(123, 0.1f, atWar: true));
        Assert.AreEqual(50, _sut.TakePendingScoutXp());
    }

    [TestMethod]
    public void Xp_AJumpOverStages_PaysEveryStageItPassed()
    {
        _sut.Evaluate(Snap(123, 0.1f, atWar: true));

        Assert.AreEqual(100, _sut.TakePendingScoutXp());
    }

    [TestMethod]
    public void Xp_ARepeatedReading_PaysNothingMore()
    {
        _sut.Evaluate(Snap(123, 0.1f, atWar: true));
        _sut.TakePendingScoutXp();

        _sut.Evaluate(Snap(123, 0.1f, atWar: true, hour: 110));
        _sut.Evaluate(Snap(123, 0.5f, atWar: true, hour: 111));
        _sut.Evaluate(Snap(123, 0.1f, atWar: true, hour: 112));

        Assert.AreEqual(0, _sut.TakePendingScoutXp(), "stages never go down within a day");
    }

    [TestMethod]
    public void Xp_ANewDay_PaysAgain()
    {
        _sut.Evaluate(Snap(123, 0.1f, atWar: true, hour: 5 * 24 + 23));
        _sut.TakePendingScoutXp();

        _sut.Evaluate(Snap(123, 0.1f, atWar: true, hour: 6 * 24));

        Assert.AreEqual(100, _sut.TakePendingScoutXp());
    }

    [TestMethod]
    public void Xp_TheSameDay_DoesNotPayTwice()
    {
        _sut.Evaluate(Snap(123, 0.1f, atWar: true, hour: 5 * 24));
        _sut.TakePendingScoutXp();

        _sut.Evaluate(Snap(123, 0.1f, atWar: true, hour: 5 * 24 + 23));

        Assert.AreEqual(0, _sut.TakePendingScoutXp());
    }

    [TestMethod]
    public void Xp_AtPeace_PaysNothing()
    {
        _sut.Evaluate(Snap(123, 0.1f, atWar: false));

        Assert.AreEqual(0, _sut.TakePendingScoutXp());
    }

    [TestMethod]
    public void Xp_AnExemptEnemy_PaysNothing()
    {
        _sut.Evaluate(Snap(123, 0.1f, atWar: true) with { InPlainSight = true });
        _sut.Evaluate(Snap(123, 0.1f, id: "b", atWar: true) with { TrueSight = true });
        _sut.Evaluate(Snap(123, 0.1f, id: "c", atWar: true) with { IsInfoHidden = true });

        Assert.AreEqual(0, _sut.TakePendingScoutXp());
    }

    [TestMethod]
    public void Xp_ADisplayOfTheMemory_PaysNothing()
    {
        Stamp("p1", 123, true, 100);

        Far(123, 0.7f);

        Assert.AreEqual(0, _sut.TakePendingScoutXp());
    }

    [TestMethod]
    public void Xp_ReadingNothing_PaysNothing()
    {
        _sut.Evaluate(Snap(123, 0.7f, atWar: true));

        Assert.AreEqual(0, _sut.TakePendingScoutXp());
    }

    [DataTestMethod]
    [DataRow(7, 20)]
    [DataRow(45, 50)]
    [DataRow(345, 100)]
    [DataRow(12345, 100)]
    public void Xp_TopsOutByDigitCount_PerPartyPerDay(int count, int total)
    {
        _sut.Evaluate(Snap(count, 0.01f, atWar: true));

        Assert.AreEqual(total, _sut.TakePendingScoutXp());
    }

    [TestMethod]
    public void Xp_APartyTooltipPaysToo_AndTheNameplateAfterItDoesNot()
    {
        _sut.EvaluateTooltipParty(Snap(123, 0.3f, atWar: true));
        Assert.AreEqual(50, _sut.TakePendingScoutXp());

        _sut.Evaluate(Snap(123, 0.3f, atWar: true, hour: 101));

        Assert.AreEqual(0, _sut.TakePendingScoutXp());
    }

    [TestMethod]
    public void Xp_SettlementsAndBattlesPayNothing()
    {
        _sut.EvaluateTooltipSettlement(Town(245, 0.1f));
        _sut.EvaluateTooltipEvent(new EventIntelSnapshot { HasData = true, TotalHealthy = 500, DistanceToMain = 1f, MainSeeingRange = 100f });

        Assert.AreEqual(0, _sut.TakePendingScoutXp());
    }

    [TestMethod]
    public void Xp_AnUnusableClock_PaysNothing()
    {
        _sut.Evaluate(Snap(123, 0.1f, atWar: true, hour: -1));

        Assert.AreEqual(0, _sut.TakePendingScoutXp());
    }

    [TestMethod]
    public void TakePendingScoutXp_ReturnsAndZeroes()
    {
        _sut.Evaluate(Snap(123, 0.1f, atWar: true));

        Assert.AreEqual(100, _sut.TakePendingScoutXp());
        Assert.AreEqual(0, _sut.TakePendingScoutXp());
    }

    [TestMethod]
    public void Xp_TheLatchSurvivesAReload_SoAReplayOfTheSameReadsPaysNothing()
    {
        _sut.Evaluate(Snap(123, 0.1f, atWar: true, hour: 5 * 24));
        _sut.TakePendingScoutXp();
        var saved = _sut.EncodeMemory(5 * 24);

        _sut.LoadFrom(saved);
        _sut.Evaluate(Snap(123, 0.1f, atWar: true, hour: 5 * 24 + 1));

        Assert.AreEqual(0, _sut.TakePendingScoutXp());
    }

    // ---- Save, load and session reset

    [TestMethod]
    public void EncodeMemory_ThenLoadFrom_IntoAFreshService_RestoresTheMemory()
    {
        _sut.Evaluate(Snap(123, 0.3f));
        _sut.OnVisibilityChanged("p1", false, 0);
        StampTown("town_1", 245, 100);
        var saved = _sut.EncodeMemory(101);

        var fresh = new HiddenHostsService(_settings);
        var report = fresh.LoadFrom(saved);

        Assert.AreEqual(2, report.Loaded);
        Assert.AreEqual("~12?", fresh.Evaluate(Snap(123, 0.7f, hour: 101)).CountText);
        Assert.AreEqual("~245", fresh.EvaluateTooltipSettlement(Town(245, 0.7f, hour: 101)).MemoryText);
    }

    [TestMethod]
    public void EncodeMemory_DropsEmptyRowsThatHoldNoLatchForToday()
    {
        _sut.Evaluate(Snap(123, 0.3f));
        _sut.OnVisibilityChanged("p1", false, 0);
        _sut.FadeHourly(106, 0, NoSettlementSight);
        _sut.FadeHourly(114, 0, NoSettlementSight);

        Assert.AreEqual("HH1", _sut.EncodeMemory(114), "two digits lost, nothing left: the row is already gone");
    }

    [TestMethod]
    public void LoadFrom_StartsClean_EvenWithTheSameIds()
    {
        _sut.Evaluate(Snap(123, 0.2f, atWar: true));
        Assert.AreEqual("12?", _sut.Evaluate(Snap(123, 0.34f)).CountText, "precondition: the latch holds the second digit");

        _sut.LoadFrom("HH1");

        Assert.AreEqual("1??", _sut.Evaluate(Snap(123, 0.34f)).CountText, "the latch of the old session is gone");
        Assert.AreEqual(0, _sut.TakePendingScoutXp());
        Assert.IsFalse(_sut.MemoryDrawn);
    }

    [TestMethod]
    public void LoadFrom_ClearsPendingXp_AndTheDrawnFlag_AndTheRows()
    {
        _sut.Evaluate(Snap(123, 0.1f, atWar: true));
        Far(123);

        _sut.LoadFrom(string.Empty);

        Assert.AreEqual(0, _sut.TakePendingScoutXp());
        Assert.IsFalse(_sut.MemoryDrawn);
        Assert.AreEqual("???", Far(123));
    }

    [TestMethod]
    public void LoadFrom_ForeignPayload_LoadsNothing_AndReportsIt()
    {
        _sut.Evaluate(Snap(123, 0.1f));

        var report = _sut.LoadFrom("{\"not\":\"ours\"}");

        Assert.IsTrue(report.UnknownFormat);
        Assert.AreEqual("???", Far(123));
    }

    [TestMethod]
    public void ResetForNewSession_ClearsRows_PendingXp_AndTheDrawnFlag()
    {
        _sut.Evaluate(Snap(123, 0.1f, atWar: true));
        Far(123);
        Assert.IsTrue(_sut.MemoryDrawn);

        _sut.ResetForNewSession();

        Assert.AreEqual(0, _sut.TakePendingScoutXp());
        Assert.IsFalse(_sut.MemoryDrawn);
        Assert.AreEqual("HH1", _sut.EncodeMemory(101));
        Assert.AreEqual("???", Far(123));
    }

    [TestMethod]
    public void MemorySkippedInserts_ReportsWhatTheStoreRefused()
    {
        for (int i = 0; i < HiddenHostsMemory.MaxParties; i++)
            _sut.Evaluate(Snap(123, 0.1f, id: "p" + i));
        Assert.AreEqual(0, _sut.MemorySkippedInserts);

        _sut.Evaluate(Snap(123, 0.1f, id: "one_too_many"));

        Assert.AreEqual(1, _sut.MemorySkippedInserts);
    }
}
