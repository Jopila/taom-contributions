using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.FarWatch;
using TAOM.Features.HiddenHosts;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>
/// What Far Watch (#847) changes in Hidden Hosts, all through <see cref="IScoutIntelQuery"/> and the service's own rules
/// (docs/features/far-watch.md "Hidden Hosts"): a scout away on a settlement ride reads Scouting as 0 for how far digits
/// reach (and for nothing else); a host the scout named keeps its name and its encyclopedia key past the horizon while its
/// count stays "..."; <see cref="HiddenHostsService.RaiseByScout"/> and <see cref="HiddenHostsService.RecordScoutedSettlement"/>
/// write what the scout learned into the memory. Counts and ratios come from the threshold table: 123 reads 1 digit at 0.5,
/// 2 at 0.3, 3 at 0.1.
/// </summary>
[TestClass]
public class HiddenHostsScoutSeamTests
{
    private IHiddenHostsSettings _settings = null!;
    private IScoutIntelQuery _scouting = null!;
    private HiddenHostsService _sut = null!;

    [TestInitialize]
    public void SetUp()
    {
        _settings = Substitute.For<IHiddenHostsSettings>();
        _settings.Enabled.Returns(true);
        _scouting = Substitute.For<IScoutIntelQuery>();
        _sut = new HiddenHostsService(_settings, _scouting);
    }

    private static PartyIntelSnapshot Party(int count, float ratio, string id = "p1", int skill = 0, bool visible = true,
        int hour = -1, bool sameRealm = false) => new()
    {
        HasData = true, StringId = id, HealthyCount = count, OwnHealthyCount = count, DistanceToMain = ratio * 100f,
        MainSeeingRange = 100f, SpottingRatio = 1f, ScoutingSkill = skill, IsVisible = visible, NowHour = hour, SameFaction = sameRealm,
    };

    private static SettlementIntelSnapshot Town(int defenders, float ratio, int skill = 0, int hour = 100, string id = "town_1") => new()
    {
        HasData = true, StringId = id, DefenderCount = defenders, DistanceToMain = ratio * 100f, MainSeeingRange = 100f,
        ScoutingSkill = skill, NowHour = hour,
    };

    private void ScoutAway() => _scouting.IsScoutAway.Returns(true);

    // ---- The scout away: Scouting reads 0 for how far digits reach, at all five sites

    [TestMethod]
    public void Evaluate_ScoutHome_ReadsTheFullSkill() =>
        Assert.AreEqual("12?", _sut.Evaluate(Party(123, 0.40f, skill: 300)).CountText);

    [TestMethod]
    public void Evaluate_ScoutAway_ReadsSkillAsZero()
    {
        ScoutAway();

        Assert.AreEqual("1??", _sut.Evaluate(Party(123, 0.40f, skill: 300)).CountText);
    }

    [TestMethod]
    public void TooltipParty_ScoutAway_MultiplierIsTheUnskilledOne()
    {
        Assert.AreEqual(1.72f, _sut.EvaluateTooltipParty(Party(123, 0.40f, skill: 300)).Multiplier, 0.0001f);

        ScoutAway();

        Assert.AreEqual(1f, _sut.EvaluateTooltipParty(Party(123, 0.40f, skill: 300)).Multiplier, 0.0001f);
    }

    [TestMethod]
    public void TooltipParty_ScoutAway_TheSameRealmDoublingStays()
    {
        ScoutAway();

        Assert.AreEqual(2f, _sut.EvaluateTooltipParty(Party(123, 0.40f, skill: 300, sameRealm: true)).Multiplier, 0.0001f);
    }

    [TestMethod]
    public void TooltipSettlement_ScoutAway_MultiplierIsTheUnskilledOne()
    {
        Assert.AreEqual(1.72f, _sut.EvaluateTooltipSettlement(Town(123, 0.40f, skill: 300)).Multiplier, 0.0001f);

        ScoutAway();

        Assert.AreEqual(1f, _sut.EvaluateTooltipSettlement(Town(123, 0.40f, skill: 300)).Multiplier, 0.0001f);
    }

    [TestMethod]
    public void TooltipEvent_ScoutAway_MultiplierIsTheUnskilledOne()
    {
        var battle = new EventIntelSnapshot { HasData = true, TotalHealthy = 123, DistanceToMain = 40f, MainSeeingRange = 100f, ScoutingSkill = 300 };
        Assert.AreEqual(1.72f, _sut.EvaluateTooltipEvent(battle).Multiplier, 0.0001f);

        ScoutAway();

        Assert.AreEqual(1f, _sut.EvaluateTooltipEvent(battle).Multiplier, 0.0001f);
    }

    [TestMethod]
    public void SettlementPartyRow_ScoutAway_IsMaskedWithTheUnskilledReach()
    {
        var settlement = new TooltipVerdict(TooltipMode.Masked, 0.40f, 1f, 0, 500);
        var row = Party(123, 0.40f, skill: 300);
        Assert.AreEqual("12?", _sut.MaskSettlementPartyRow(settlement, row, includeAttachedParties: true));

        ScoutAway();

        Assert.AreEqual("1??", _sut.MaskSettlementPartyRow(settlement, row, includeAttachedParties: true));
    }

    [TestMethod]
    public void Memory_ScoutAway_KeepsTheRealSkillForHowLongACountIsHeld()
    {
        // 123 read in full at 0.1 from out of sight: the row's clock starts at the stamp plus the hold, which widens with the skill.
        var home = new HiddenHostsService(_settings, new NoScoutIntel());
        ScoutAway();
        var away = _sut;
        var snap = Party(123, 0.10f, skill: 150, visible: false, hour: 100);

        home.Evaluate(snap);
        away.Evaluate(snap);

        Assert.AreEqual(home.EncodeMemory(100), away.EncodeMemory(100), "the hold is memory, not reach");
    }

    // ---- A named host keeps its name past the horizon

    [TestMethod]
    public void Evaluate_AHorizonHostNobodyNamed_LosesItsName()
    {
        var intel = _sut.Evaluate(Party(123, 0.95f, hour: 100));

        Assert.AreEqual(HiddenHostsMath.HorizonText, intel.CountText);
        Assert.IsTrue(intel.HidesName);
        Assert.AreEqual("...", intel.CountText);
    }

    [TestMethod]
    public void Evaluate_AHorizonHostTheScoutNamed_KeepsItsName_AndItsCountStaysDots()
    {
        _scouting.IsNamedByScout("p1", 100).Returns(true);

        var intel = _sut.Evaluate(Party(123, 0.95f, hour: 100));

        Assert.AreEqual(HiddenHostsMath.HorizonText, intel.CountText);
        Assert.IsFalse(intel.HidesName);
        Assert.AreEqual("...", intel.CountText, "decision Q5: the count stays hidden past the horizon");
    }

    [TestMethod]
    public void Evaluate_NamedLookupIsByTheHostsIdAndTheClockHour()
    {
        _sut.Evaluate(Party(123, 0.95f, id: "lord_7", hour: 321));

        _scouting.Received(1).IsNamedByScout("lord_7", 321);
    }

    [TestMethod]
    public void Evaluate_InsideTheHorizon_AsksNobodyWhoIsNamed()
    {
        var intel = _sut.Evaluate(Party(123, 0.5f, hour: 100));

        Assert.IsFalse(intel.HidesName);
        _scouting.DidNotReceiveWithAnyArgs().IsNamedByScout(default!, default);
    }

    [TestMethod]
    public void NameplateIntel_AMaskedPlateThatIsNotAtTheHorizon_HidesNoName()
    {
        Assert.IsFalse(new NameplateIntel("1??", isHorizon: false).HidesName);
        Assert.IsTrue(new NameplateIntel("...", isHorizon: true).HidesName, "a horizon plate hides its name unless told otherwise");
        Assert.IsFalse(new NameplateIntel("...", isHorizon: true, named: true).HidesName);
    }

    [TestMethod]
    public void IsNameHidden_AtTheHorizonAndNotNamed_IsTrue() =>
        Assert.IsTrue(_sut.IsNameHidden(Party(123, 0.95f, hour: 100)));

    [TestMethod]
    public void IsNameHidden_WhenTheScoutNamedIt_IsFalse()
    {
        _scouting.IsNamedByScout("p1", 100).Returns(true);

        Assert.IsFalse(_sut.IsNameHidden(Party(123, 0.95f, hour: 100)));
    }

    [TestMethod]
    public void IsNameHidden_InsideTheHorizon_IsFalse() =>
        Assert.IsFalse(_sut.IsNameHidden(Party(123, 0.5f, hour: 100)));

    [TestMethod]
    public void IsNameHidden_SettingOffOrExemptOrNoData_IsFalse()
    {
        Assert.IsFalse(_sut.IsNameHidden(Party(123, 0.95f, hour: 100) with { InPlayerClan = true }));
        Assert.IsFalse(_sut.IsNameHidden(PartyIntelSnapshot.NoData));
        _settings.Enabled.Returns(false);
        Assert.IsFalse(_sut.IsNameHidden(Party(123, 0.95f, hour: 100)));
    }

    [TestMethod]
    public void TooltipParty_AtTheHorizon_CarriesWhetherTheScoutNamedIt()
    {
        var unnamed = _sut.EvaluateTooltipParty(Party(123, 0.95f, hour: 100));
        _scouting.IsNamedByScout("p1", 100).Returns(true);
        var named = _sut.EvaluateTooltipParty(Party(123, 0.95f, hour: 100));

        Assert.AreEqual(TooltipMode.Horizon, unnamed.Mode);
        Assert.IsFalse(unnamed.Named);
        Assert.AreEqual(TooltipMode.Horizon, named.Mode, "the numbers still read dots");
        Assert.IsTrue(named.Named);
    }

    [TestMethod]
    public void TooltipArmy_IsNamedByItsLeadersId()
    {
        _scouting.IsNamedByScout("leader_party", 100).Returns(true);

        var verdict = _sut.EvaluateTooltipArmy(Party(400, 0.95f, id: "leader_party", hour: 100) with { LeadsArmy = true });

        Assert.IsTrue(verdict.Named);
    }

    [TestMethod]
    public void TooltipFrame_ANamedHorizonParty_IsNotRenamed()
    {
        var named = new TooltipFrame(TooltipSubjectKind.Party, new TooltipVerdict(TooltipMode.Horizon, 0.9f, 1f, 0, 123, named: true));
        var unnamed = new TooltipFrame(TooltipSubjectKind.Party, new TooltipVerdict(TooltipMode.Horizon, 0.9f, 1f, 0, 123));
        var armyNamed = new TooltipFrame(TooltipSubjectKind.Army, new TooltipVerdict(TooltipMode.Horizon, 0.9f, 1f, 0, 123, named: true));

        Assert.IsFalse(named.RenamesTitle);
        Assert.IsTrue(unnamed.RenamesTitle);
        Assert.IsFalse(armyNamed.RenamesTitle);
        Assert.IsTrue(named.TrimsTroopRows, "the rows below the title still go");
    }

    [TestMethod]
    public void LayMask_ANamedHorizonPlate_ShowsDotsButKeepsTheName()
    {
        string count = "42", extra = "3 wounded", name = "Lord Aldric";

        VanillaBinds.LayMask(new NameplateIntel("...", isHorizon: true, named: true), "Unknown party", ref count, ref extra, ref name);

        Assert.AreEqual("...", count);
        Assert.AreEqual("Lord Aldric", name);
    }

    private sealed class AlwaysOn : IHiddenHostsSettings
    {
        public bool Enabled => true;
    }

    [TestMethod]
    public void Evaluate_AHorizonPartyNamedByTheRealStore_AllocatesNothing_NamedOrNot()
    {
        var counter = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
        Assert.IsNotNull(counter, "the runtime must expose GC.GetAllocatedBytesForCurrentThread");
        var allocatedBytes = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), counter!);
        // The real store answers IsNamedByScout from its six fields; a hand-written settings fake, because a substitute allocates.
        var store = new FarWatchStore(Substitute.For<IModLogger>());
        store.SetNamed(new[] { "a", "b", "c", "d", "e", "p1" }, 500d);
        var sut = new HiddenHostsService(new AlwaysOn(), store);
        var named = Party(123, 0.95f, id: "p1", hour: 100);
        var stranger = Party(123, 0.95f, id: "p9", hour: 100);
        Assert.IsTrue(store.IsNamedByScout("p1", 100) && !store.IsNamedByScout("p9", 100), "the premise: p1 is named, p9 is not");
        for (int i = 0; i < 5; i++)
        {
            sut.Evaluate(named);
            sut.Evaluate(stranger);
            sut.IsNameHidden(named);
            sut.IsNameHidden(stranger);
        }

        long before = allocatedBytes();
        for (int i = 0; i < 1000; i++)
        {
            sut.Evaluate(named);
            sut.Evaluate(stranger);
            sut.IsNameHidden(named);
            sut.IsNameHidden(stranger);
        }
        long allocated = allocatedBytes() - before;

        Assert.AreEqual(0L, allocated, "a nameplate read per frame must not allocate, named or not");
        Assert.IsFalse(sut.Evaluate(named).HidesName);
        Assert.IsTrue(sut.Evaluate(stranger).HidesName);
    }

    // ---- RaiseByScout

    [TestMethod]
    public void RaiseByScout_AddsOneDigitToWhatTheDistanceLetsHimRead_AndRemembersIt()
    {
        var text = _sut.RaiseByScout(Party(123, 0.50f, hour: 100), stages: 1);

        Assert.AreEqual("12?", text);
        Assert.AreEqual("~12?", _sut.Evaluate(Party(123, 0.50f, hour: 101)).CountText, "the memory holds the raised digits");
    }

    [TestMethod]
    public void RaiseByScout_TwoStages_AddTwoDigits()
    {
        var text = _sut.RaiseByScout(Party(123, 0.70f, hour: 100), stages: 2);

        Assert.AreEqual("12?", text);
    }

    [TestMethod]
    public void RaiseByScout_NeverPastTheDigitCount()
    {
        var text = _sut.RaiseByScout(Party(7, 0.50f, hour: 100), stages: 2);

        Assert.AreEqual("7", text, "every digit known is the whole number");
    }

    [TestMethod]
    public void RaiseByScout_AtTheHorizon_StartsFromNothing()
    {
        var text = _sut.RaiseByScout(Party(123, 0.95f, hour: 100), stages: 1);

        Assert.AreEqual("1??", text);
    }

    [TestMethod]
    public void RaiseByScout_BuildsOnWhatIsAlreadyRemembered()
    {
        _sut.Evaluate(Party(123, 0.30f, hour: 90));

        var text = _sut.RaiseByScout(Party(123, 0.70f, hour: 100), stages: 1);

        Assert.AreEqual("123", text, "he remembered two digits and learned one more");
    }

    [DataTestMethod]
    [DataRow(523, DisplayName = "same size of host, other leading digit")]
    [DataRow(5000, DisplayName = "other number of digits")]
    public void RaiseByScout_ARememberedCountThatDisagreesWithTheHost_IsNotBuiltOn(int remembered)
    {
        _sut.Stamp(Party(remembered, 0.10f, hour: 90) with { InPlainSight = true });

        var text = _sut.RaiseByScout(Party(123, 0.50f, hour: 100), stages: 1);

        Assert.AreEqual("12?", text, "the live reading (one digit) plus one stage; the old figure is another host's");
    }

    [TestMethod]
    public void RaiseByScout_PaysNoScoutingXp()
    {
        _sut.RaiseByScout(Party(123, 0.50f, hour: 100) with { AtWar = true }, stages: 2);

        Assert.AreEqual(0, _sut.TakePendingScoutXp());
    }

    [TestMethod]
    public void RaiseByScout_ReadsTheLiveReachWithTheScoutAwayRule()
    {
        ScoutAway();

        // Skill 300 would read two digits at 0.40; away it reads one, so one stage gives two.
        var text = _sut.RaiseByScout(Party(123, 0.40f, skill: 300, hour: 100), stages: 1);

        Assert.AreEqual("12?", text);
    }

    [TestMethod]
    public void RaiseByScout_UnusableDistance_RaisesFromZero()
    {
        var text = _sut.RaiseByScout(Party(123, 0.5f, hour: 100) with { MainSeeingRange = 0f }, stages: 1);

        Assert.AreEqual("1??", text);
    }

    [TestMethod]
    public void RaiseByScout_SettingOff_ReturnsTheExactNumber_AndWritesNothing()
    {
        _settings.Enabled.Returns(false);

        var text = _sut.RaiseByScout(Party(123, 0.50f, hour: 100), stages: 1);

        Assert.AreEqual("123", text);
        _settings.Enabled.Returns(true);
        Assert.AreEqual("1??", _sut.Evaluate(Party(123, 0.50f, hour: 101)).CountText, "no memory row was written");
    }

    [TestMethod]
    public void RaiseByScout_NoClock_ReturnsTheExactNumber_AndWritesNothing()
    {
        var text = _sut.RaiseByScout(Party(123, 0.50f, hour: -1), stages: 1);

        Assert.AreEqual("123", text);
    }

    [DataTestMethod]
    [DataRow("main")]
    [DataRow("clan")]
    [DataRow("army")]
    [DataRow("truesight")]
    [DataRow("hidden")]
    [DataRow("nodata")]
    public void RaiseByScout_TheOwnSideAndWhatTheEngineHides_AreReportedExact_AndNeverRemembered(string kind)
    {
        var s = Party(123, 0.50f, hour: 100);
        s = kind switch
        {
            "main" => s with { IsMainParty = true },
            "clan" => s with { InPlayerClan = true },
            "army" => s with { InPlayerArmy = true },
            "truesight" => s with { TrueSight = true },
            "hidden" => s with { IsInfoHidden = true },
            _ => PartyIntelSnapshot.NoData with { HealthyCount = 123 },
        };

        var text = _sut.RaiseByScout(s, stages: 1);

        Assert.AreEqual("123", text, kind);
        Assert.AreEqual("1??", _sut.Evaluate(Party(123, 0.50f, hour: 101)).CountText, kind + " leaves no memory");
    }

    // ---- RecordScoutedSettlement

    [TestMethod]
    public void RecordScoutedSettlement_RemembersTheDefendersExactly_ForAnySettlementNotJustTheOneHeIsIn()
    {
        _sut.RecordScoutedSettlement(Town(245, 0.5f) with { IsPlayerSettlement = false });

        var verdict = _sut.EvaluateTooltipSettlement(Town(245, 0.70f, hour: 105));
        Assert.AreEqual("~245", verdict.MemoryText);
        Assert.AreEqual(5, verdict.MemoryHours);
    }

    [TestMethod]
    public void RecordScoutedSettlement_StartsTheClockAtTheStampPlusTheHold()
    {
        _sut.RecordScoutedSettlement(Town(245, 0.5f, skill: 150));

        // visible: false puts the row on the out-of-sight clock at once: a hold of 6 x (1 + 150 / 150) = 12 hours from the stamp,
        // taken from the skill of the report. (An in-sight row would start its clock at the next poll, from the poll's own skill:
        // 0 here, which would fade it at hour 106.)
        _sut.FadeHourly(nowHour: 106, scout: 0, settlementInSight: _ => false);
        Assert.AreEqual("~245", _sut.EvaluateTooltipSettlement(Town(245, 0.70f, hour: 106)).MemoryText);
        _sut.FadeHourly(nowHour: 111, scout: 0, settlementInSight: _ => false);
        Assert.AreEqual("~245", _sut.EvaluateTooltipSettlement(Town(245, 0.70f, hour: 111)).MemoryText);
        _sut.FadeHourly(nowHour: 112, scout: 0, settlementInSight: _ => false);
        Assert.AreEqual("~24?", _sut.EvaluateTooltipSettlement(Town(245, 0.70f, hour: 112)).MemoryText);
    }

    [TestMethod]
    public void RecordScoutedSettlement_SettingOffOrNoClockOrNoData_WritesNothing()
    {
        _sut.RecordScoutedSettlement(Town(245, 0.5f, hour: -1));
        _sut.RecordScoutedSettlement(Town(245, 0.5f) with { HasData = false });
        _sut.RecordScoutedSettlement(Town(245, 0.5f) with { StringId = null });
        _settings.Enabled.Returns(false);
        _sut.RecordScoutedSettlement(Town(245, 0.5f));
        _settings.Enabled.Returns(true);

        Assert.IsNull(_sut.EvaluateTooltipSettlement(Town(245, 0.70f, hour: 105)).MemoryText);
    }

    [DataTestMethod]
    [DataRow("owned")]
    [DataRow("hero")]
    [DataRow("truesight")]
    public void RecordScoutedSettlement_TheOwnSide_IsNeverRemembered(string kind)
    {
        var s = Town(245, 0.5f);
        s = kind switch
        {
            "owned" => s with { OwnedByPlayerClan = true },
            "hero" => s with { PlayerClanHeroInside = true },
            _ => s with { TrueSight = true },
        };

        _sut.RecordScoutedSettlement(s);

        Assert.IsNull(_sut.EvaluateTooltipSettlement(Town(245, 0.70f, hour: 105)).MemoryText, kind);
    }
}
