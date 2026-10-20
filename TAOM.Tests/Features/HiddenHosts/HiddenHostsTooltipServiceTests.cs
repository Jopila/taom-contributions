using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.HiddenHosts;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>
/// #846 phase 2: the verdict a tooltip subject gets, and the number texts the verdict produces. Plain snapshots in, plain
/// data out; every exemption has its own row and every float gate has a NaN row.
/// </summary>
[TestClass]
public class HiddenHostsTooltipServiceTests
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

    private static PartyIntelSnapshot Party(int count, float ratio, string id = "p1", float spotting = 1f, int skill = 0,
        bool visible = true, float range = 100f) => new()
    {
        HasData = true, StringId = id, HealthyCount = count, OwnHealthyCount = count, DistanceToMain = ratio * range,
        MainSeeingRange = range, SpottingRatio = spotting, ScoutingSkill = skill, IsVisible = visible,
    };

    private static SettlementIntelSnapshot Town(int defenders, float ratio, string id = "town_1", int skill = 0,
        bool sameFaction = false, float range = 100f) => new()
    {
        HasData = true, StringId = id, DefenderCount = defenders, DistanceToMain = ratio * range, MainSeeingRange = range,
        ScoutingSkill = skill, SameFaction = sameFaction,
    };

    private static EventIntelSnapshot Battle(int total, float ratio, int skill = 0, bool sameFaction = false, float range = 100f) => new()
    {
        HasData = true, TotalHealthy = total, DistanceToMain = ratio * range, MainSeeingRange = range, ScoutingSkill = skill,
        SameFaction = sameFaction,
    };

    private static void AssertVanilla(TooltipVerdict v) => Assert.AreEqual(TooltipMode.Vanilla, v.Mode);

    // ---- Party and army

    [TestMethod]
    public void Party_SettingOff_IsVanilla()
    {
        _settings.Enabled.Returns(false);

        AssertVanilla(_sut.EvaluateTooltipParty(Party(123, 0.7f)));
    }

    [TestMethod]
    public void Party_NoData_IsVanilla() => AssertVanilla(_sut.EvaluateTooltipParty(PartyIntelSnapshot.NoData));

    [TestMethod]
    public void Party_NullId_IsVanilla() => AssertVanilla(_sut.EvaluateTooltipParty(Party(123, 0.7f, id: null!)));

    [TestMethod]
    public void Party_InfoHidden_IsVanilla() => AssertVanilla(_sut.EvaluateTooltipParty(Party(123, 0.7f) with { IsInfoHidden = true }));

    [TestMethod]
    public void Party_MainParty_IsVanilla() => AssertVanilla(_sut.EvaluateTooltipParty(Party(123, 0.7f) with { IsMainParty = true }));

    [TestMethod]
    public void Party_PlayerClan_IsVanilla() => AssertVanilla(_sut.EvaluateTooltipParty(Party(123, 0.7f) with { InPlayerClan = true }));

    [TestMethod]
    public void Party_PlayerArmy_IsVanilla() => AssertVanilla(_sut.EvaluateTooltipParty(Party(123, 0.7f) with { InPlayerArmy = true }));

    [TestMethod]
    public void Party_TrueSight_IsVanilla() => AssertVanilla(_sut.EvaluateTooltipParty(Party(123, 0.7f) with { TrueSight = true }));

    [TestMethod]
    public void Party_InPlainSight_IsVanilla() => AssertVanilla(_sut.EvaluateTooltipParty(Party(123, 0.7f) with { InPlainSight = true }));

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(0f)]
    [DataRow(-5f)]
    public void Party_UnusableSeeingRange_IsVanilla(float range) =>
        AssertVanilla(_sut.EvaluateTooltipParty(Party(123, 0.7f) with { MainSeeingRange = range }));

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(-1f)]
    public void Party_UnusableDistance_IsVanilla(float distance) =>
        AssertVanilla(_sut.EvaluateTooltipParty(Party(123, 0.7f) with { DistanceToMain = distance }));

    [TestMethod]
    public void Party_RatioOverflowsToInfinity_IsVanilla() =>
        AssertVanilla(_sut.EvaluateTooltipParty(Party(123, 0.7f) with { DistanceToMain = 1e10f, MainSeeingRange = 1e-30f }));

    [TestMethod]
    public void Party_Far_IsMasked_WithTheRatioAndTheSubjectCount()
    {
        var v = _sut.EvaluateTooltipParty(Party(123, 0.7f));

        Assert.AreEqual(TooltipMode.Masked, v.Mode);
        Assert.AreEqual(0.7f, v.Ratio, 1e-5f);
        Assert.AreEqual(1f, v.Multiplier, 1e-5f);
        Assert.AreEqual(0, v.LatchedKnown);
        Assert.AreEqual(123, v.SubjectCount);
    }

    [TestMethod]
    public void Party_PartlyKnown_IsMasked_WithTheKnownDigits()
    {
        var v = _sut.EvaluateTooltipParty(Party(123, 0.3f));

        Assert.AreEqual(TooltipMode.Masked, v.Mode);
        Assert.AreEqual(2, v.LatchedKnown);
    }

    [TestMethod]
    public void Party_Close_AllDigitsKnown_IsVanilla() => AssertVanilla(_sut.EvaluateTooltipParty(Party(123, 0.1f)));

    [TestMethod]
    public void Party_VisibleAtTheHorizon_IsHorizon()
    {
        var v = _sut.EvaluateTooltipParty(Party(123, 0.95f));

        Assert.AreEqual(TooltipMode.Horizon, v.Mode);
        Assert.AreEqual(123, v.SubjectCount);
    }

    [TestMethod]
    public void Party_NotVisibleBeyondTheHorizonBand_HasNoHorizon_ItKeepsItsName()
    {
        var v = _sut.EvaluateTooltipParty(Party(123, 0.95f, visible: false));

        Assert.AreEqual(TooltipMode.Masked, v.Mode);
        Assert.AreEqual(0, v.LatchedKnown);
    }

    [TestMethod]
    public void Party_HorizonFollowsTheEnginesSpottingRatio()
    {
        Assert.AreEqual(TooltipMode.Horizon, _sut.EvaluateTooltipParty(Party(123, 0.46f, spotting: 0.5f)).Mode);
        Assert.AreEqual(TooltipMode.Masked, _sut.EvaluateTooltipParty(Party(123, 0.46f, id: "other", spotting: 1f)).Mode);
    }

    [TestMethod]
    public void Party_UnusableSpottingRatio_IsNeverHorizon_StillMasked() =>
        Assert.AreEqual(TooltipMode.Masked, _sut.EvaluateTooltipParty(Party(123, 0.95f, spotting: float.NaN)).Mode);

    [TestMethod]
    public void Party_SameFaction_DoublesTheReach()
    {
        var plain = _sut.EvaluateTooltipParty(Party(123, 0.45f, id: "a"));
        var friend = _sut.EvaluateTooltipParty(Party(123, 0.45f, id: "b") with { SameFaction = true });

        Assert.AreEqual(1, plain.LatchedKnown);
        Assert.AreEqual(2, friend.LatchedKnown);
        Assert.AreEqual(2f, friend.Multiplier, 1e-5f);
    }

    [TestMethod]
    public void Party_ArmyLeaderOfTheSameFaction_DoublesTheReachToo() =>
        Assert.AreEqual(2, _sut.EvaluateTooltipParty(Party(123, 0.45f) with { ArmyLeaderSameFaction = true }).LatchedKnown);

    [TestMethod]
    public void Party_ScoutingSkill_WidensTheReach() =>
        Assert.AreEqual(1.12f, _sut.EvaluateTooltipParty(Party(123, 0.45f, skill: 50)).Multiplier, 1e-5f);

    [TestMethod]
    public void Party_SharesTheNameplatesLatch()
    {
        // The nameplate reads one digit at 0.20; the tooltip at 0.30 holds it (0.28 * 1.1 = 0.308).
        _sut.Evaluate(Party(7, 0.20f));

        AssertVanilla(_sut.EvaluateTooltipParty(Party(7, 0.30f)));
        Assert.IsFalse(_sut.Evaluate(Party(7, 0.30f)).Masked, "and the nameplate sees what the tooltip kept");
    }

    [TestMethod]
    public void Party_TheTooltipSetsTheLatchTheNameplateReads()
    {
        _sut.EvaluateTooltipParty(Party(7, 0.20f));

        Assert.IsFalse(_sut.Evaluate(Party(7, 0.30f)).Masked);
    }

    [TestMethod]
    public void Party_HorizonForgetsTheLatch()
    {
        _sut.EvaluateTooltipParty(Party(7, 0.20f));
        Assert.AreEqual(TooltipMode.Horizon, _sut.EvaluateTooltipParty(Party(7, 0.95f)).Mode);

        Assert.AreEqual(TooltipMode.Masked, _sut.EvaluateTooltipParty(Party(7, 0.30f)).Mode);
    }

    [TestMethod]
    public void Nameplate_InPlainSight_IsNotMasked() =>
        Assert.IsFalse(_sut.Evaluate(Party(123, 0.7f) with { InPlainSight = true }).Masked);

    [TestMethod]
    public void IsHorizon_InPlainSight_IsFalse() =>
        Assert.IsFalse(_sut.IsHorizon(Party(123, 0.95f) with { InPlainSight = true }));

    // ---- Settlement

    [TestMethod]
    public void Settlement_SettingOff_IsVanilla()
    {
        _settings.Enabled.Returns(false);

        AssertVanilla(_sut.EvaluateTooltipSettlement(Town(200, 0.7f)));
    }

    [TestMethod]
    public void Settlement_NoData_IsVanilla() => AssertVanilla(_sut.EvaluateTooltipSettlement(SettlementIntelSnapshot.NoData));

    [TestMethod]
    public void Settlement_NullId_IsVanilla() => AssertVanilla(_sut.EvaluateTooltipSettlement(Town(200, 0.7f, id: null!)));

    [TestMethod]
    public void Settlement_PlayerIsInside_IsVanilla() =>
        AssertVanilla(_sut.EvaluateTooltipSettlement(Town(200, 0.7f) with { IsPlayerSettlement = true }));

    [TestMethod]
    public void Settlement_OwnedByThePlayerClan_IsVanilla() =>
        AssertVanilla(_sut.EvaluateTooltipSettlement(Town(200, 0.7f) with { OwnedByPlayerClan = true }));

    [TestMethod]
    public void Settlement_APlayerClanHeroIsInside_IsVanilla() =>
        AssertVanilla(_sut.EvaluateTooltipSettlement(Town(200, 0.7f) with { PlayerClanHeroInside = true }));

    [TestMethod]
    public void Settlement_TrueSight_IsVanilla() =>
        AssertVanilla(_sut.EvaluateTooltipSettlement(Town(200, 0.7f) with { TrueSight = true }));

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(0f)]
    [DataRow(-5f)]
    public void Settlement_UnusableSeeingRange_IsVanilla(float range) =>
        AssertVanilla(_sut.EvaluateTooltipSettlement(Town(200, 0.7f) with { MainSeeingRange = range }));

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(-1f)]
    public void Settlement_UnusableDistance_IsVanilla(float distance) =>
        AssertVanilla(_sut.EvaluateTooltipSettlement(Town(200, 0.7f) with { DistanceToMain = distance }));

    [TestMethod]
    public void Settlement_RatioOverflowsToInfinity_IsVanilla() =>
        AssertVanilla(_sut.EvaluateTooltipSettlement(Town(200, 0.7f) with { DistanceToMain = 1e10f, MainSeeingRange = 1e-30f }));

    [TestMethod]
    public void Settlement_Far_IsMasked_WithTheDefenderCountAsSubject()
    {
        var v = _sut.EvaluateTooltipSettlement(Town(245, 0.7f));

        Assert.AreEqual(TooltipMode.Masked, v.Mode);
        Assert.AreEqual(245, v.SubjectCount);
        Assert.AreEqual(0, v.LatchedKnown);
        Assert.AreEqual(0.7f, v.Ratio, 1e-5f);
    }

    [TestMethod]
    public void Settlement_VeryFar_IsStillMasked_NeverAHorizon() =>
        Assert.AreEqual(TooltipMode.Masked, _sut.EvaluateTooltipSettlement(Town(245, 5f)).Mode);

    [TestMethod]
    public void Settlement_Close_IsVanilla() => AssertVanilla(_sut.EvaluateTooltipSettlement(Town(245, 0.1f)));

    [TestMethod]
    public void Settlement_SameFaction_DoublesTheReach()
    {
        Assert.AreEqual(1, _sut.EvaluateTooltipSettlement(Town(245, 0.45f, id: "a")).LatchedKnown);
        Assert.AreEqual(2, _sut.EvaluateTooltipSettlement(Town(245, 0.45f, id: "b", sameFaction: true)).LatchedKnown);
    }

    [TestMethod]
    public void Settlement_ScoutingSkill_WidensTheReach() =>
        Assert.AreEqual(1.12f, _sut.EvaluateTooltipSettlement(Town(245, 0.45f, skill: 50)).Multiplier, 1e-5f);

    [TestMethod]
    public void Settlement_KeepsAKnownDigitInsideTenPercentMoreRange()
    {
        AssertVanilla(_sut.EvaluateTooltipSettlement(Town(7, 0.20f)));

        AssertVanilla(_sut.EvaluateTooltipSettlement(Town(7, 0.30f)));
        Assert.AreEqual(TooltipMode.Masked, _sut.EvaluateTooltipSettlement(Town(7, 0.31f)).Mode);
    }

    [TestMethod]
    public void Settlement_LatchIsPerSettlement_AndSeparateFromAPartyOfTheSameId()
    {
        _sut.EvaluateTooltipParty(Party(7, 0.20f, id: "x"));
        _sut.EvaluateTooltipSettlement(Town(7, 0.20f, id: "y"));

        Assert.AreEqual(TooltipMode.Masked, _sut.EvaluateTooltipSettlement(Town(7, 0.30f, id: "x")).Mode, "a party's latch is not a settlement's");
        AssertVanilla(_sut.EvaluateTooltipSettlement(Town(7, 0.30f, id: "y")));
    }

    // ---- Map events and sieges

    [TestMethod]
    public void Event_SettingOff_IsVanilla()
    {
        _settings.Enabled.Returns(false);

        AssertVanilla(_sut.EvaluateTooltipEvent(Battle(500, 0.7f)));
    }

    [TestMethod]
    public void Event_NoData_IsVanilla() => AssertVanilla(_sut.EvaluateTooltipEvent(EventIntelSnapshot.NoData));

    [TestMethod]
    public void Event_PlayerInvolved_IsVanilla() =>
        AssertVanilla(_sut.EvaluateTooltipEvent(Battle(500, 0.7f) with { PlayerInvolved = true }));

    [TestMethod]
    public void Event_TrueSight_IsVanilla() =>
        AssertVanilla(_sut.EvaluateTooltipEvent(Battle(500, 0.7f) with { TrueSight = true }));

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(0f)]
    [DataRow(-5f)]
    public void Event_UnusableSeeingRange_IsVanilla(float range) =>
        AssertVanilla(_sut.EvaluateTooltipEvent(Battle(500, 0.7f) with { MainSeeingRange = range }));

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(-1f)]
    public void Event_UnusableDistance_IsVanilla(float distance) =>
        AssertVanilla(_sut.EvaluateTooltipEvent(Battle(500, 0.7f) with { DistanceToMain = distance }));

    [TestMethod]
    public void Event_RatioOverflowsToInfinity_IsVanilla() =>
        AssertVanilla(_sut.EvaluateTooltipEvent(Battle(500, 0.7f) with { DistanceToMain = 1e10f, MainSeeingRange = 1e-30f }));

    [TestMethod]
    public void Event_Far_IsMasked_WithTheTotalAsSubject()
    {
        var v = _sut.EvaluateTooltipEvent(Battle(500, 0.7f));

        Assert.AreEqual(TooltipMode.Masked, v.Mode);
        Assert.AreEqual(500, v.SubjectCount);
        Assert.AreEqual(0, v.LatchedKnown);
    }

    [TestMethod]
    public void Event_VeryFar_IsNeverAHorizon() =>
        Assert.AreEqual(TooltipMode.Masked, _sut.EvaluateTooltipEvent(Battle(500, 5f)).Mode);

    [TestMethod]
    public void Event_Close_IsVanilla() => AssertVanilla(_sut.EvaluateTooltipEvent(Battle(500, 0.1f)));

    [TestMethod]
    public void Event_SameFactionAndSkill_WidenTheReach()
    {
        Assert.AreEqual(1, _sut.EvaluateTooltipEvent(Battle(500, 0.45f)).LatchedKnown);
        Assert.AreEqual(2, _sut.EvaluateTooltipEvent(Battle(500, 0.45f, sameFaction: true)).LatchedKnown);
        Assert.AreEqual(1.12f, _sut.EvaluateTooltipEvent(Battle(500, 0.45f, skill: 50)).Multiplier, 1e-5f);
    }

    // ---- Number texts

    [TestMethod]
    public void MaskTooltipCount_VanillaVerdict_IsNull() =>
        Assert.IsNull(_sut.MaskTooltipCount(TooltipVerdict.Vanilla, 123));

    [TestMethod]
    public void MaskTooltipCount_ForTheSubjectTotal_EqualsTheNameplate()
    {
        var snapshot = Party(123, 0.3f);
        var verdict = _sut.EvaluateTooltipParty(snapshot);

        var tooltip = _sut.MaskTooltipCount(verdict, 123);
        var plate = _sut.Evaluate(snapshot).CountText;

        Assert.AreEqual("12?", tooltip);
        Assert.AreEqual(plate, tooltip);
    }

    [TestMethod]
    public void MaskTooltipCount_ForAnotherNumber_UsesItsOwnDigits()
    {
        var verdict = _sut.EvaluateTooltipParty(Party(123, 0.3f));

        Assert.AreEqual("2?", _sut.MaskTooltipCount(verdict, 20), "two digits at 0.3: thresholds 0.50 and 0.26");

        var far = _sut.EvaluateTooltipParty(Party(123, 0.7f, id: "far"));
        Assert.AreEqual("????", _sut.MaskTooltipCount(far, 1234));
    }

    [TestMethod]
    public void MaskTooltipCount_ANumberReadInFull_KeepsVanillasText()
    {
        // At 0.2 the subject (123) is still masked, but a one-digit number is read in full (threshold 0.28).
        var verdict = _sut.EvaluateTooltipParty(Party(123, 0.2f));
        Assert.AreEqual(TooltipMode.Masked, verdict.Mode);

        Assert.IsNull(_sut.MaskTooltipCount(verdict, 5));
    }

    [TestMethod]
    public void MaskTooltipCount_ZeroAndNegative_AreOneDigit()
    {
        var verdict = _sut.EvaluateTooltipParty(Party(123, 0.7f));

        Assert.AreEqual("?", _sut.MaskTooltipCount(verdict, 0));
        Assert.AreEqual("?", _sut.MaskTooltipCount(verdict, -3));
    }

    [TestMethod]
    public void MaskTooltipCount_Horizon_IsThreeDots()
    {
        var verdict = _sut.EvaluateTooltipParty(Party(123, 0.95f));

        Assert.AreEqual("...", _sut.MaskTooltipCount(verdict, 123));
        Assert.AreEqual("...", _sut.MaskTooltipCount(verdict, 4));
    }

    [TestMethod]
    public void MaskTooltipCount_TheLatchHoldsADigitForAnotherNumberToo()
    {
        // 20 at ratio 0.28: digit 1 (threshold 0.26) is unknown, but a latched subject keeps it to 0.286.
        var held = new TooltipVerdict(TooltipMode.Masked, 0.28f, 1f, latchedKnown: 2, subjectCount: 123);
        var fresh = new TooltipVerdict(TooltipMode.Masked, 0.28f, 1f, latchedKnown: 0, subjectCount: 123);

        Assert.IsNull(_sut.MaskTooltipCount(held, 20));
        Assert.AreEqual("2?", _sut.MaskTooltipCount(fresh, 20));
    }

    [DataTestMethod]
    [DataRow(float.NaN, 1f)]
    [DataRow(float.PositiveInfinity, 1f)]
    [DataRow(0.5f, float.NaN)]
    [DataRow(0.5f, 0f)]
    public void MaskTooltipCount_ANonFiniteVerdict_KeepsVanillasText(float ratio, float multiplier) =>
        Assert.IsNull(_sut.MaskTooltipCount(new TooltipVerdict(TooltipMode.Masked, ratio, multiplier, 0, 123), 123));

    [TestMethod]
    public void MaskTooltipCount_SameStringForTheSameInputs_ComparesEqual() =>
        Assert.AreEqual(
            _sut.MaskTooltipCount(_sut.EvaluateTooltipParty(Party(123, 0.7f)), 123),
            _sut.MaskTooltipCount(_sut.EvaluateTooltipParty(Party(123, 0.7f)), 123));

    [TestMethod]
    public void MaskSettlementPartyRow_ALeaderOfAnArmy_NotIncludingAttachedParties_MasksItsOwnRoster()
    {
        // A 45-man leader of a 345-man army: vanilla's settlement row prints 45 (GetPartyNameplateText(party, false)).
        var settlement = new TooltipVerdict(TooltipMode.Masked, 0.40f, 1f, 0, 500);
        var leader = Party(345, 0f) with { OwnHealthyCount = 45 };

        Assert.AreEqual("4?", _sut.MaskSettlementPartyRow(settlement, leader, includeAttachedParties: false),
            "the number vanilla printed is 45, so the mask is 4?, never the army's ???");
    }

    [TestMethod]
    public void MaskSettlementPartyRow_ALeaderOfAnArmy_IncludingAttachedParties_MasksTheArmyTotal()
    {
        var settlement = new TooltipVerdict(TooltipMode.Masked, 0.40f, 1f, 0, 500);
        var leader = Party(345, 0f) with { OwnHealthyCount = 45 };

        Assert.AreEqual("3??", _sut.MaskSettlementPartyRow(settlement, leader, includeAttachedParties: true));
    }

    [TestMethod]
    public void MaskSettlementPartyRow_VanillaSettlement_IsNull() =>
        Assert.IsNull(_sut.MaskSettlementPartyRow(TooltipVerdict.Vanilla, Party(150, 0.3f), includeAttachedParties: false));

    [TestMethod]
    public void MaskSettlementPartyRow_UsesTheSettlementsDistance_AndThePartysOwnCount()
    {
        var settlement = _sut.EvaluateTooltipSettlement(Town(500, 0.3f));

        Assert.AreEqual("15?", _sut.MaskSettlementPartyRow(settlement, Party(150, 9f), includeAttachedParties: false));
        Assert.AreEqual("??", _sut.MaskSettlementPartyRow(_sut.EvaluateTooltipSettlement(Town(500, 0.7f, id: "far")), Party(15, 0f), includeAttachedParties: false));
    }

    [TestMethod]
    public void MaskSettlementPartyRow_UsesThePartysOwnRealmAndSkill()
    {
        var settlement = _sut.EvaluateTooltipSettlement(Town(500, 0.3f));

        Assert.IsNull(_sut.MaskSettlementPartyRow(settlement, Party(150, 0f) with { SameFaction = true }, includeAttachedParties: false),
            "0.62, 0.32 and 0.20 doubled all reach 0.3");
        Assert.IsNull(_sut.MaskSettlementPartyRow(settlement, Party(150, 0f) with { ArmyLeaderSameFaction = true }, includeAttachedParties: false));
    }

    [TestMethod]
    public void MaskSettlementPartyRow_APartyReadInFull_KeepsVanillasText()
    {
        var settlement = new TooltipVerdict(TooltipMode.Masked, 0.1f, 1f, 0, 5000);

        Assert.IsNull(_sut.MaskSettlementPartyRow(settlement, Party(150, 0f), includeAttachedParties: false), "0.1 is inside every threshold of a three-digit count");
    }

    [TestMethod]
    public void MaskSettlementPartyRow_TheMasterSettingOff_IsNull()
    {
        var settlement = _sut.EvaluateTooltipSettlement(Town(500, 0.7f));
        _settings.Enabled.Returns(false);

        Assert.IsNull(_sut.MaskSettlementPartyRow(settlement, Party(150, 0f), includeAttachedParties: false));
    }

    [TestMethod]
    public void MaskSettlementPartyRow_NoData_IsNull()
    {
        var settlement = _sut.EvaluateTooltipSettlement(Town(500, 0.7f));

        Assert.IsNull(_sut.MaskSettlementPartyRow(settlement, PartyIntelSnapshot.NoData, includeAttachedParties: false));
    }

    [TestMethod]
    public void MaskSettlementPartyRow_EachPartyExemption_KeepsVanillasText()
    {
        var settlement = _sut.EvaluateTooltipSettlement(Town(500, 0.7f));

        Assert.IsNull(_sut.MaskSettlementPartyRow(settlement, Party(150, 0f) with { IsMainParty = true }, includeAttachedParties: false));
        Assert.IsNull(_sut.MaskSettlementPartyRow(settlement, Party(150, 0f) with { InPlayerClan = true }, includeAttachedParties: false));
        Assert.IsNull(_sut.MaskSettlementPartyRow(settlement, Party(150, 0f) with { InPlayerArmy = true }, includeAttachedParties: false));
        Assert.IsNull(_sut.MaskSettlementPartyRow(settlement, Party(150, 0f) with { TrueSight = true }, includeAttachedParties: false));
        Assert.IsNull(_sut.MaskSettlementPartyRow(settlement, Party(150, 0f) with { InPlainSight = true }, includeAttachedParties: false));
        Assert.IsNull(_sut.MaskSettlementPartyRow(settlement, Party(150, 0f) with { IsInfoHidden = true }, includeAttachedParties: false));
    }

    [TestMethod]
    public void MaskSettlementPartyRow_DoesNotTouchThePartysNameplateLatch()
    {
        _sut.MaskSettlementPartyRow(new TooltipVerdict(TooltipMode.Masked, 0.1f, 1f, 0, 500), Party(7, 0f, id: "garrison"), includeAttachedParties: false);

        Assert.AreEqual("?", _sut.Evaluate(Party(7, 0.30f, id: "garrison")).CountText, "no digit was latched for the plate");
    }

    [TestMethod]
    public void DefendersText_VanillaVerdict_IsNull() => Assert.IsNull(_sut.DefendersText(TooltipVerdict.Vanilla));

    [TestMethod]
    public void DefendersText_Masked_ShowsTheKnownDigitsOfTheDefenderCount()
    {
        Assert.AreEqual("???", _sut.DefendersText(_sut.EvaluateTooltipSettlement(Town(245, 0.7f))));
        Assert.AreEqual("24?", _sut.DefendersText(_sut.EvaluateTooltipSettlement(Town(245, 0.3f, id: "near"))));
    }

    [TestMethod]
    public void DefendersText_Horizon_IsThreeDots() =>
        Assert.AreEqual("...", _sut.DefendersText(new TooltipVerdict(TooltipMode.Horizon, 1f, 1f, 0, 245)));

    [TestMethod]
    public void ResetForNewSession_ClearsTheTooltipLatchesToo()
    {
        _sut.EvaluateTooltipSettlement(Town(7, 0.20f));
        _sut.EvaluateTooltipParty(Party(7, 0.20f));
        _sut.ResetForNewSession();

        Assert.AreEqual(TooltipMode.Masked, _sut.EvaluateTooltipSettlement(Town(7, 0.30f)).Mode);
        Assert.AreEqual(TooltipMode.Masked, _sut.EvaluateTooltipParty(Party(7, 0.30f)).Mode);
    }

    [TestMethod]
    public void Party_ALeaderOfAnArmy_IsJudgedOnItsOwnRoster_WhileTheArmyIsJudgedOnTheTotal()
    {
        var leader = Party(345, 0.4f) with { LeadsArmy = true, OwnHealthyCount = 45 };

        var party = _sut.EvaluateTooltipParty(leader);
        var army = _sut.EvaluateTooltipArmy(leader);

        Assert.AreEqual(45, party.SubjectCount, "vanilla's party tooltip prints the leader's own roster");
        Assert.AreEqual(1, party.LatchedKnown, "two digits at 0.4: the first only");
        Assert.AreEqual(345, army.SubjectCount);
    }

    [TestMethod]
    public void Party_ALeaderOfAnArmy_NeverTouchesTheNameplateLatchOrTheMemory()
    {
        var leader = Party(345, 0.2f) with { LeadsArmy = true, OwnHealthyCount = 45 };
        _sut.Evaluate(leader);
        Assert.AreEqual("34?", _sut.Evaluate(leader with { DistanceToMain = 30f }).CountText, "precondition: the latch holds two digits to 0.352");

        _sut.EvaluateTooltipParty(leader with { DistanceToMain = 30f });

        Assert.AreEqual("34?", _sut.Evaluate(leader with { DistanceToMain = 34f }).CountText, "the party tooltip left the army's latch alone");
    }
}
