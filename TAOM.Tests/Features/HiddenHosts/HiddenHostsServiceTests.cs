using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.HiddenHosts;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>#846: the decision a nameplate gets, from a plain snapshot of the party and the player's party.</summary>
[TestClass]
public class HiddenHostsServiceTests
{
    private IHiddenHostsSettings _settings = null!;
    private HiddenHostsService _sut = null!;

    [TestInitialize]
    public void SetUp()
    {
        _settings = Substitute.For<IHiddenHostsSettings>();
        _settings.Enabled.Returns(true);
        _sut = new HiddenHostsService(_settings, new NoScoutIntel());
    }

    /// <summary>A party <paramref name="ratio"/> of the player's seeing range away; the engine lets the player see it out to <paramref name="spotting"/>.</summary>
    private static PartyIntelSnapshot Snap(int count, float ratio, string id = "p1", float spotting = 1f, int skill = 0,
        bool sameRealm = false, bool infoHidden = false, float range = 100f)
        => new()
        {
            HasData = true,
            StringId = id,
            HealthyCount = count,
            DistanceToMain = ratio * range,
            MainSeeingRange = range,
            SpottingRatio = spotting,
            ScoutingSkill = skill,
            SameFaction = sameRealm,
            IsInfoHidden = infoHidden,
            // No usable clock: these cases test the mask and its latch alone, so no memory is kept or consulted.
            // The memory has its own cases in HiddenHostsMemoryServiceTests.
            NowHour = -1,
        };

    private static void AssertNotMasked(NameplateIntel intel)
    {
        Assert.IsFalse(intel.Masked);
        Assert.AreNotEqual(HiddenHostsMath.HorizonText, intel.CountText);
        Assert.IsNull(intel.CountText);
    }

    [TestMethod]
    public void Enabled_FollowsTheSetting()
    {
        Assert.IsTrue(_sut.Enabled);
        _settings.Enabled.Returns(false);
        Assert.IsFalse(_sut.Enabled);
    }

    [TestMethod]
    public void Evaluate_SettingOff_IsNotMasked()
    {
        _settings.Enabled.Returns(false);

        AssertNotMasked(_sut.Evaluate(Snap(123, 0.7f)));
    }

    [TestMethod]
    public void Evaluate_NoData_IsNotMasked() => AssertNotMasked(_sut.Evaluate(PartyIntelSnapshot.NoData));

    [TestMethod]
    public void Evaluate_MainParty_IsNotMasked() => AssertNotMasked(_sut.Evaluate(Snap(123, 0.7f) with { IsMainParty = true }));

    [TestMethod]
    public void Evaluate_PlayerClanParty_IsNotMasked() => AssertNotMasked(_sut.Evaluate(Snap(123, 0.7f) with { InPlayerClan = true }));

    [TestMethod]
    public void Evaluate_PlayerArmyParty_IsNotMasked() => AssertNotMasked(_sut.Evaluate(Snap(123, 0.7f) with { InPlayerArmy = true }));

    [TestMethod]
    public void Evaluate_TrueSight_IsNotMasked() => AssertNotMasked(_sut.Evaluate(Snap(123, 0.7f) with { TrueSight = true }));

    [TestMethod]
    public void Evaluate_InfoHidden_IsNotMasked() => AssertNotMasked(_sut.Evaluate(Snap(123, 0.7f, infoHidden: true)));

    [TestMethod]
    public void Evaluate_NullStringId_IsNotMasked() => AssertNotMasked(_sut.Evaluate(Snap(123, 0.7f, id: null!)));

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(-5f)]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void Evaluate_UnusableSeeingRange_IsNotMasked(float range)
    {
        var s = Snap(123, 0.7f) with { MainSeeingRange = range };

        AssertNotMasked(_sut.Evaluate(s));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(-1f)]
    public void Evaluate_UnusableDistance_IsNotMasked(float distance)
    {
        var s = new PartyIntelSnapshot
        {
            HasData = true, StringId = "p1", HealthyCount = 123, DistanceToMain = distance, MainSeeingRange = 100f,
            SpottingRatio = 1f,
        };

        AssertNotMasked(_sut.Evaluate(s));
    }

    [TestMethod]
    public void Evaluate_FarParty_MasksEveryDigitAndHidesTheExtraInfo()
    {
        var intel = _sut.Evaluate(Snap(123, 0.7f));

        Assert.IsTrue(intel.Masked);
        Assert.AreEqual("???", intel.CountText);
        Assert.AreNotEqual(HiddenHostsMath.HorizonText, intel.CountText);
    }

    [TestMethod]
    public void Evaluate_PartiallyKnown_ShowsThePrefix()
    {
        var intel = _sut.Evaluate(Snap(123, 0.3f));

        Assert.IsTrue(intel.Masked);
        Assert.AreEqual("12?", intel.CountText);
    }

    [TestMethod]
    public void Evaluate_CloseParty_ShowsTheExactCount_AndTheExtraInfo()
    {
        var intel = _sut.Evaluate(Snap(123, 0.1f));

        AssertNotMasked(intel);
    }

    [TestMethod]
    public void Evaluate_Horizon_ShowsDotsAndFlagsTheHorizon()
    {
        var intel = _sut.Evaluate(Snap(123, 0.95f));

        Assert.IsTrue(intel.Masked);
        Assert.AreEqual("...", intel.CountText);
        Assert.AreEqual(HiddenHostsMath.HorizonText, intel.CountText);
    }

    [TestMethod]
    public void Evaluate_HorizonScalesWithTheEnginesSpottingRatio()
    {
        // A party the engine lets the player see only out to half the range is at its horizon at 0.46.
        Assert.AreEqual(HiddenHostsMath.HorizonText, _sut.Evaluate(Snap(123, 0.46f, spotting: 0.5f)).CountText);
        Assert.AreNotEqual(HiddenHostsMath.HorizonText, _sut.Evaluate(Snap(123, 0.46f, spotting: 1f)).CountText);
    }

    [TestMethod]
    public void Evaluate_UnusableSpottingRatio_NeverHorizon_StillMasks()
    {
        var intel = _sut.Evaluate(Snap(123, 0.95f, spotting: float.NaN));

        Assert.IsTrue(intel.Masked);
        Assert.AreEqual("???", intel.CountText);
        Assert.AreNotEqual(HiddenHostsMath.HorizonText, intel.CountText);
    }

    [TestMethod]
    public void Evaluate_SameRealm_DoublesTheReach()
    {
        Assert.AreEqual("1??", _sut.Evaluate(Snap(123, 0.45f, id: "a")).CountText);
        Assert.AreEqual("12?", _sut.Evaluate(Snap(123, 0.45f, id: "b", sameRealm: true)).CountText);
    }

    [TestMethod]
    public void Evaluate_ArmyLeaderOfTheSameRealm_DoublesTheReachToo() =>
        Assert.AreEqual("12?", _sut.Evaluate(Snap(123, 0.45f) with { ArmyLeaderSameFaction = true }).CountText);

    [TestMethod]
    public void Evaluate_DistanceOverSeeingRangeOverflowsToInfinity_IsNotMasked()
    {
        // 1e10 / 1e-30 is +Infinity in a float: the ratio gate, not the two inputs, rejects it.
        var s = Snap(123, 0.7f) with { DistanceToMain = 1e10f, MainSeeingRange = 1e-30f };

        AssertNotMasked(_sut.Evaluate(s));
    }

    [TestMethod]
    public void Evaluate_ScoutingSkill_WidensTheReach()
    {
        Assert.AreEqual("?", _sut.Evaluate(Snap(7, 0.30f, id: "a")).CountText);

        var skilled = _sut.Evaluate(Snap(7, 0.30f, id: "b", skill: 50));

        Assert.IsFalse(skilled.Masked, "0.28 * 1.12 = 0.3136 reaches 0.30");
    }

    [TestMethod]
    public void Evaluate_Latch_KeepsAKnownDigitInsideTenPercentMoreRange()
    {
        Assert.IsFalse(_sut.Evaluate(Snap(7, 0.20f)).Masked, "known up close");

        Assert.IsFalse(_sut.Evaluate(Snap(7, 0.30f)).Masked, "0.28 * 1.1 = 0.308 still holds it");

        var gone = _sut.Evaluate(Snap(7, 0.31f));
        Assert.IsTrue(gone.Masked, "past 0.308 the digit drops");
        Assert.AreEqual("?", gone.CountText);
    }

    [TestMethod]
    public void Evaluate_Latch_DoesNotGrantADigitToAPartyNeverSeenCloser()
    {
        Assert.AreEqual("?", _sut.Evaluate(Snap(7, 0.30f)).CountText);
    }

    [TestMethod]
    public void Evaluate_Latch_IsPerParty()
    {
        _sut.Evaluate(Snap(7, 0.20f, id: "near"));

        Assert.AreEqual("?", _sut.Evaluate(Snap(7, 0.30f, id: "other")).CountText);
        Assert.IsFalse(_sut.Evaluate(Snap(7, 0.30f, id: "near")).Masked);
    }

    [TestMethod]
    public void Evaluate_HorizonForgetsTheLatch()
    {
        _sut.Evaluate(Snap(7, 0.20f));
        Assert.AreEqual(HiddenHostsMath.HorizonText, _sut.Evaluate(Snap(7, 0.95f)).CountText);

        Assert.AreEqual("?", _sut.Evaluate(Snap(7, 0.30f)).CountText);
    }

    [TestMethod]
    public void Evaluate_SteadyState_ReturnsTheSameStringInstance()
    {
        var first = _sut.Evaluate(Snap(123, 0.7f)).CountText;
        var second = _sut.Evaluate(Snap(123, 0.7f)).CountText;

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void Evaluate_SteadyStatePartialMask_ReturnsTheSameStringInstance()
    {
        var first = _sut.Evaluate(Snap(123, 0.3f)).CountText;
        var second = _sut.Evaluate(Snap(123, 0.31f)).CountText;

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void Evaluate_CountChangesDigits_FormatsAFreshString()
    {
        Assert.AreEqual("???", _sut.Evaluate(Snap(123, 0.7f)).CountText);
        Assert.AreEqual("????", _sut.Evaluate(Snap(1234, 0.85f)).CountText);
    }

    [TestMethod]
    public void Evaluate_KnownDigitsChange_FormatsAFreshString()
    {
        Assert.AreEqual("???", _sut.Evaluate(Snap(123, 0.7f)).CountText);
        Assert.AreEqual("1??", _sut.Evaluate(Snap(123, 0.5f)).CountText);
    }

    [TestMethod]
    public void Evaluate_NegativeHealthyCount_IsTreatedAsZero() =>
        Assert.AreEqual("?", _sut.Evaluate(Snap(-4, 0.7f)).CountText);

    [TestMethod]
    public void Evaluate_ManyParties_TrimsTheBookkeeping_AndKeepsWorking()
    {
        _sut.Evaluate(Snap(7, 0.20f, id: "keep"));
        for (int i = 0; i < HiddenHostsService.MaxTrackedParties + 10; i++)
            _sut.Evaluate(Snap(7, 0.7f, id: "p" + i));

        // The trim dropped the latch; the party is masked by its current distance alone.
        Assert.AreEqual("?", _sut.Evaluate(Snap(7, 0.30f, id: "keep")).CountText);
    }

    [TestMethod]
    public void ResetForNewSession_ClearsTheLatch()
    {
        _sut.Evaluate(Snap(7, 0.20f));
        _sut.ResetForNewSession();

        Assert.AreEqual("?", _sut.Evaluate(Snap(7, 0.30f)).CountText);
    }

    [TestMethod]
    public void ResetForNewSession_ClearsTheStringCache()
    {
        var first = _sut.Evaluate(Snap(123, 0.7f)).CountText;
        _sut.ResetForNewSession();

        var second = _sut.Evaluate(Snap(123, 0.7f)).CountText;

        Assert.AreEqual(first, second);
        Assert.AreNotSame(first, second);
    }

    [TestMethod]
    public void IsHorizon_AtTheHorizon_IsTrue() => Assert.IsTrue(_sut.IsHorizon(Snap(123, 0.95f)));

    [TestMethod]
    public void IsHorizon_InsideIt_IsFalse() => Assert.IsFalse(_sut.IsHorizon(Snap(123, 0.5f)));

    [TestMethod]
    public void IsHorizon_SettingOff_IsFalse()
    {
        _settings.Enabled.Returns(false);

        Assert.IsFalse(_sut.IsHorizon(Snap(123, 0.95f)));
    }

    [TestMethod]
    public void IsHorizon_ExemptInfoHiddenOrNoData_IsFalse()
    {
        Assert.IsFalse(_sut.IsHorizon(Snap(123, 0.95f) with { IsMainParty = true }));
        Assert.IsFalse(_sut.IsHorizon(Snap(123, 0.95f) with { InPlayerClan = true }));
        Assert.IsFalse(_sut.IsHorizon(Snap(123, 0.95f) with { InPlayerArmy = true }));
        Assert.IsFalse(_sut.IsHorizon(Snap(123, 0.95f) with { TrueSight = true }));
        Assert.IsFalse(_sut.IsHorizon(Snap(123, 0.95f, infoHidden: true)));
        Assert.IsFalse(_sut.IsHorizon(PartyIntelSnapshot.NoData));
    }

    [TestMethod]
    public void IsHorizon_NaNDistance_IsFalse()
    {
        var s = new PartyIntelSnapshot
        {
            HasData = true, StringId = "p1", HealthyCount = 5, DistanceToMain = float.NaN, MainSeeingRange = 100f,
            SpottingRatio = 1f,
        };

        Assert.IsFalse(_sut.IsHorizon(s));
    }

    [TestMethod]
    public void IsHorizon_DoesNotTouchTheLatch()
    {
        _sut.Evaluate(Snap(7, 0.20f));
        _sut.IsHorizon(Snap(7, 0.95f));

        Assert.IsFalse(_sut.Evaluate(Snap(7, 0.30f)).Masked, "an encyclopedia lookup must not erase the latch");
    }
}
