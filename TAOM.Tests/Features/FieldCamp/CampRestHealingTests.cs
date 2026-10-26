using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.BattleBalance.Models;
using TAOM.Features.CoopInterop;
using TAOM.Features.FieldCamp;
using TAOM.Features.FieldCamp.Activities;
using TAOM.Features.FieldCamp.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FieldCamp;

/// <summary>
/// Rest healing in camp (#860): when a party rests (a raised camp, the camp panel open, the feature on, single-player, at least one
/// rate above 0), the finite checks on the two rates, the two rates the panel shows, and the one decision the healing model asks
/// for: how much to add, and whether the two lodging perks follow. The model itself only reads the engine and applies the answer.
/// No test here builds a game text.
/// </summary>
[TestClass]
public class CampRestHealingTests
{
    private ICampSettingsProvider _settings = null!;
    private ICampWorkAdapter _work = null!;
    private IGameMenuAdapter _menus = null!;
    private ICoopSessionProvider _coop = null!;
    private IDedicatedServerProvider _dedicated = null!;
    private CampRestHealing _sut = null!;

    [TestInitialize]
    public void SetUp()
    {
        _settings = Substitute.For<ICampSettingsProvider>();
        _settings.Enabled.Returns(true);
        _settings.CampRestHealRegularsPerDay.Returns(15f);
        _settings.CampRestHealHeroesPerDay.Returns(19f);
        _work = Substitute.For<ICampWorkAdapter>();
        _work.CampReady().Returns(true);
        _menus = Substitute.For<IGameMenuAdapter>();
        _menus.CurrentMenuId.Returns(FieldCampCampaignBehavior.CampSubMenuId);
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        _dedicated = Substitute.For<IDedicatedServerProvider>();
        _sut = new CampRestHealing(_settings, _work, _menus, _coop, _dedicated);
    }

    // ---- When a party rests

    [TestMethod]
    public void ARestingCamp_AddsFifteenForRegulars_AndNineteenForHeroes()
    {
        Assert.IsTrue(_sut.IsResting());
        Assert.AreEqual(15f, _sut.DailyBonus(forHeroes: false));
        Assert.AreEqual(19f, _sut.DailyBonus(forHeroes: true));
    }

    [TestMethod]
    public void TheFeatureOff_AddsNothing()
    {
        _settings.Enabled.Returns(false);

        Assert.IsFalse(_sut.IsResting());
        Assert.AreEqual(0f, _sut.DailyBonus(false));
    }

    [TestMethod]
    public void NoCampOrACampStillBeingRaised_AddsNothing()
    {
        // CampReady() is false for both: it is the adapter's "a camp stands and its raise is done".
        _work.CampReady().Returns(false);

        Assert.IsFalse(_sut.IsResting());
        Assert.AreEqual(0f, _sut.DailyBonus(true));
        Assert.IsNull(_sut.StatusRates());
    }

    [DataTestMethod]
    [DataRow("taom_fc_work_wait")]
    [DataRow("taom_fc_work")]
    [DataRow("taom_fc_scout_wait")]
    [DataRow("encounter")]
    [DataRow("town")]
    [DataRow("")]
    [DataRow(null)]
    public void OnlyTheCampPanel_Counts_NotTheWorkMenus_NorTheMap(string? menu)
    {
        _menus.CurrentMenuId.Returns(menu!);

        Assert.IsFalse(_sut.IsResting(), "no healing during a work: the work wait menu is not the camp panel");
        Assert.AreEqual(0f, _sut.DailyBonus(false));
        Assert.AreEqual(0f, _sut.DailyBonus(true));
    }

    [TestMethod]
    public void TheCoopGate_HasAllFourTerms()
    {
        _coop.IsSessionActive.Returns(true);
        Assert.AreEqual(0f, _sut.DailyBonus(false), "a live co-op session");

        _coop.IsSessionActive.Returns(false);
        _coop.ShouldDeferToHost.Returns(true);
        Assert.AreEqual(0f, _sut.DailyBonus(false), "a co-op mod TAOM cannot probe");

        _coop.ShouldDeferToHost.Returns(false);
        _dedicated.IsDedicatedServer.Returns(true);
        Assert.AreEqual(0f, _sut.DailyBonus(false), "a dedicated server");

        _dedicated.IsDedicatedServer.Returns(false);
        _coop.IsAuthority.Returns(false);
        Assert.AreEqual(0f, _sut.DailyBonus(false), "a peer that may not write the world");

        _coop.IsAuthority.Returns(true);
        Assert.AreEqual(15f, _sut.DailyBonus(false));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(-3f)]
    [DataRow(0f)]
    public void ABadRate_AddsNothing(float rate)
    {
        _settings.CampRestHealRegularsPerDay.Returns(rate);
        _settings.CampRestHealHeroesPerDay.Returns(rate);

        Assert.AreEqual(0f, _sut.DailyBonus(false));
        Assert.AreEqual(0f, _sut.DailyBonus(true));
    }

    [TestMethod]
    public void BothRatesZero_TurnsRestHealingFullyOff_NoRest_NoPerks_NoRates()
    {
        _settings.CampRestHealRegularsPerDay.Returns(0f);
        _settings.CampRestHealHeroesPerDay.Returns(0f);

        Assert.IsFalse(_sut.IsResting());
        Assert.AreEqual(0f, _sut.DailyBonus(false));
        Assert.AreEqual(0f, _sut.DailyBonus(true));
        Assert.IsNull(_sut.StatusRates());
        Assert.IsFalse(_sut.Grant(true, false, false, false, false).LodgingPerks, "the perks follow resting, which is off");
    }

    [TestMethod]
    public void OneRateZero_StillRests_AndTheOtherRateStands()
    {
        _settings.CampRestHealRegularsPerDay.Returns(0f);

        Assert.IsTrue(_sut.IsResting());
        Assert.AreEqual(0f, _sut.DailyBonus(false));
        Assert.AreEqual(19f, _sut.DailyBonus(true));
        Assert.IsNotNull(_sut.StatusRates());
    }

    // ---- The rates the panel shows

    [TestMethod]
    public void TheStatusRates_ShowWhileACampStands_WhateverMenuIsOpen_AndAreBothRates()
    {
        _menus.CurrentMenuId.Returns("elsewhere");

        var rates = _sut.StatusRates();

        Assert.IsNotNull(rates);
        Assert.AreEqual(15f, rates!.Value.Regulars);
        Assert.AreEqual(19f, rates.Value.Heroes);
    }

    [TestMethod]
    public void TheStatusRates_AreNull_WhenNothingWouldApply()
    {
        _settings.Enabled.Returns(false);
        Assert.IsNull(_sut.StatusRates());

        _settings.Enabled.Returns(true);
        _coop.IsSessionActive.Returns(true);
        Assert.IsNull(_sut.StatusRates(), "a shared campaign gets no bonus, so the panel must not promise one");
    }

    // ---- The model's one question

    [TestMethod]
    public void Grant_ARestingMainParty_GetsTheBonusAndTheLodgingPerks()
    {
        var grant = _sut.Grant(isMainParty: true, isPrisoners: false, isStarving: false, inSettlement: false, forHeroes: false);

        Assert.AreEqual(15f, grant.Bonus);
        Assert.IsTrue(grant.LodgingPerks);
        Assert.AreEqual(19f, _sut.Grant(true, false, false, false, forHeroes: true).Bonus);
    }

    [TestMethod]
    public void Grant_NotTheMainParty_GetsNothing()
    {
        var grant = _sut.Grant(isMainParty: false, isPrisoners: false, isStarving: false, inSettlement: false, forHeroes: false);

        Assert.AreEqual(0f, grant.Bonus);
        Assert.IsFalse(grant.LodgingPerks);
    }

    [TestMethod]
    public void Grant_Prisoners_GetNothing()
    {
        var grant = _sut.Grant(isMainParty: true, isPrisoners: true, isStarving: false, inSettlement: false, forHeroes: true);

        Assert.AreEqual(0f, grant.Bonus);
        Assert.IsFalse(grant.LodgingPerks);
    }

    [TestMethod]
    public void Grant_AStarvingParty_GetsNothing_VanillasStarvingCaseStands()
    {
        var grant = _sut.Grant(isMainParty: true, isPrisoners: false, isStarving: true, inSettlement: false, forHeroes: false);

        Assert.AreEqual(0f, grant.Bonus);
        Assert.IsFalse(grant.LodgingPerks);
    }

    [TestMethod]
    public void Grant_InASettlement_KeepsTheBonus_ButNoLodgingPerks_BecauseVanillaAppliedThem()
    {
        var grant = _sut.Grant(isMainParty: true, isPrisoners: false, isStarving: false, inSettlement: true, forHeroes: false);

        Assert.AreEqual(15f, grant.Bonus);
        Assert.IsFalse(grant.LodgingPerks, "a perk is never counted twice");
    }

    [TestMethod]
    public void Grant_NotResting_GetsNothing()
    {
        _menus.CurrentMenuId.Returns("taom_fc_work_wait");

        var grant = _sut.Grant(isMainParty: true, isPrisoners: false, isStarving: false, inSettlement: false, forHeroes: false);

        Assert.AreEqual(0f, grant.Bonus);
        Assert.IsFalse(grant.LodgingPerks);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(-5f)]
    public void Grant_ANonFiniteOrNegativeRate_AddsNoBonus_AndTheOtherRateStillRests(float rate)
    {
        _settings.CampRestHealRegularsPerDay.Returns(rate);

        var grant = _sut.Grant(isMainParty: true, isPrisoners: false, isStarving: false, inSettlement: false, forHeroes: false);

        Assert.AreEqual(0f, grant.Bonus);
        Assert.IsTrue(grant.LodgingPerks, "the heroes' rate is fine, so the party still rests");
    }

    [TestMethod]
    public void Grant_NeverCarriesANonFiniteBonus()
    {
        foreach (var rate in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            _settings.CampRestHealRegularsPerDay.Returns(rate);
            _settings.CampRestHealHeroesPerDay.Returns(rate);
            var grant = _sut.Grant(true, false, false, false, false);
            Assert.IsTrue(grant.Bonus == 0f, rate.ToString());
        }
    }

    // ---- The model's constructor

    [TestMethod]
    public void TheModelsCampRestParameter_IsRequired()
    {
        var last = typeof(TaomPartyHealingModel).GetConstructors().Single().GetParameters().Last();

        Assert.AreEqual(typeof(CampRestHealing), last.ParameterType);
        Assert.IsFalse(last.IsOptional, "a build that forgets to pass it must not compile, not silently heal nothing");
    }

    [TestMethod]
    public void SubModule_PassesTheCampRestHealingToTheModel()
    {
        var sub = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        var at = sub.IndexOf("new TaomPartyHealingModel(", StringComparison.Ordinal);

        Assert.IsTrue(at >= 0);
        var call = sub.Substring(at, sub.IndexOf(");", at, StringComparison.Ordinal) - at);
        StringAssert.Contains(call, "IoC.Resolve<Features.FieldCamp.Activities.CampRestHealing>()");
    }
}
