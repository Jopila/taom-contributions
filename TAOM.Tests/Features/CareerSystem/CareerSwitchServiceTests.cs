using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class CareerSwitchServiceTests
{
    private CareerDataService _dataService;
    private ICareerRegistry _registry;
    private ICareerPassiveService _passiveService;
    private CareerCreationHandler _creationHandler;
    private ICareerPurseAdapter _purse;
    private IModLogger _logger;
    private ICareerHeroAdapter _hero;
    private CareerSwitchService _service;

    [TestInitialize]
    public void Setup()
    {
        _dataService = new CareerDataService();
        _registry = Substitute.For<ICareerRegistry>();
        _passiveService = Substitute.For<ICareerPassiveService>();
        _purse = Substitute.For<ICareerPurseAdapter>();
        _purse.ChargeGold(Arg.Any<int>()).Returns(true);
        _registry.GetRetrainCost(Arg.Any<int>()).Returns(ci => 1000 + 250 * System.Math.Max(1, ci.Arg<int>()));
        _logger = Substitute.For<IModLogger>();
        _hero = Substitute.For<ICareerHeroAdapter>();

        _hero.CultureStringId.Returns("mordor");
        _hero.ClanTier.Returns(2);
        _hero.Level.Returns(10);
        _hero.StringId.Returns("hero1");

        _registry.IsEligible("ranger", _hero).Returns(true);
        _registry.IsEligible("warboss", _hero).Returns(true);
        _registry.GetMaxChoicesForHero(Arg.Any<CareerDefinition>(), Arg.Any<int>()).Returns(11);

        _registry.GetCareer("warboss").Returns(new CareerDefinition(
            id: "warboss", displayName: "Warboss", description: "", portraitSprite: "",
            abilityTemplateId: "rally",
            minClanTier: 0, rootChoiceId: "wb_root",
            eligibleCultureIds: new List<string> { "mordor" },
            choiceGroupIds: new List<string>()));

        _registry.GetCareer("ranger").Returns(new CareerDefinition(
            id: "ranger", displayName: "Ranger", description: "", portraitSprite: "",
            abilityTemplateId: "ambush",
            minClanTier: 0, rootChoiceId: "ranger_root",
            eligibleCultureIds: new List<string> { "gondor", "mordor" },
            choiceGroupIds: new List<string>()));

        // The real creation handler over the same fakes: a switch reuses it to set, stamp, seed the
        // root and refresh the cache, so these tests prove the whole path rather than a stub.
        _creationHandler = new CareerCreationHandler(_dataService, _registry, _passiveService, _logger);
        _service = new CareerSwitchService(_dataService, _registry, _creationHandler, _purse, _logger);
    }

    [TestMethod]
    public void SwitchCareer_EligibleHero_ClearsOldAndSetsNew()
    {
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_root", 10);
        _dataService.TryAddChoice("hero1", "wb_brut_p1", 10);

        var result = _service.SwitchCareer("hero1", _hero, "ranger");

        Assert.AreEqual(CareerSwitchResult.Switched, result);
        Assert.AreEqual("ranger", _dataService.GetCareerStringId("hero1"));
        Assert.AreEqual(1, _dataService.GetChoiceCount("hero1")); // only root
        Assert.IsTrue(_dataService.GetOrCreateData("hero1").HasChoice("ranger_root"));
    }

    [TestMethod]
    public void SwitchCareer_IneligibleHero_IsNotEligible()
    {
        _registry.IsEligible("knight", _hero).Returns(false);

        _dataService.SetCareer("hero1", "warboss");
        var result = _service.SwitchCareer("hero1", _hero, "knight");

        Assert.AreEqual(CareerSwitchResult.NotEligible, result);
        Assert.AreEqual("warboss", _dataService.GetCareerStringId("hero1"));
    }

    [TestMethod]
    public void SwitchCareer_RefreshesCache()
    {
        _dataService.SetCareer("hero1", "warboss");
        _service.SwitchCareer("hero1", _hero, "ranger");
        _passiveService.Received(1).RefreshCache(_dataService, _registry);
    }

    [TestMethod]
    public void CanSwitch_NullHero_ReturnsFalse()
    {
        Assert.IsFalse(_service.CanSwitch(null, "warboss"));
    }

    [TestMethod]
    public void CanSwitch_EligibleHero_ReturnsTrue()
    {
        Assert.IsTrue(_service.CanSwitch(_hero, "warboss"));
    }

    [TestMethod]
    public void CanSwitch_SameCareer_ReturnsFalse()
    {
        // Switching to your own current career is a no-op; the service must reject it so the
        // dialogue path doesn't waste a player click + the screen doesn't list the current career.
        // Uses hero.StringId to look up the current career via the data service.
        _dataService.SetCareer("hero1", "warboss");

        Assert.IsFalse(_service.CanSwitch(_hero, "warboss"));
    }

    [TestMethod]
    public void SwitchCareer_SameCareer_IsNotEligible()
    {
        _dataService.SetCareer("hero1", "warboss");

        var result = _service.SwitchCareer("hero1", _hero, "warboss");

        Assert.AreEqual(CareerSwitchResult.NotEligible, result);
        Assert.AreEqual("warboss", _dataService.GetCareerStringId("hero1"));
        // Cache refresh must NOT fire on a rejected switch.
        _passiveService.DidNotReceive().RefreshCache(Arg.Any<ICareerDataService>(), Arg.Any<ICareerRegistry>());
    }

    [TestMethod]
    public void CanSwitch_EmptyHeroStringId_FallsThroughToEligibility()
    {
        // If a hero adapter returns empty StringId (mock state or test fixture mistake), the
        // same-career guard should silently skip and the call should fall through to IsEligible.
        // This guards the intent of the guard (defensive same-career rejection) from breaking
        // unrelated eligibility checks.
        _hero.StringId.Returns("");

        Assert.IsTrue(_service.CanSwitch(_hero, "warboss"));
    }

    // ── #769 E7: leaving a career that holds fork picks costs the retrain price ──

    private void UseForkWarboss(int newCareerTreeVersion = 1)
    {
        _registry.GetCareer("warboss").Returns(new CareerDefinition(
            id: "warboss", displayName: "Warboss", description: "", portraitSprite: "",
            abilityTemplateId: "rally", minClanTier: 0, rootChoiceId: "wb_root",
            eligibleCultureIds: new List<string> { "mordor" },
            choiceGroupIds: new List<string> { "wb_fork" }));
        _registry.GetGroup("wb_fork").Returns(new CareerChoiceGroupDefinition(
            "wb_fork", "warboss", 1, new List<string> { "wb_fork_a", "wb_fork_b" }, maxPicks: 1));
        _registry.GetCareer("ranger").Returns(new CareerDefinition(
            id: "ranger", displayName: "Ranger", description: "", portraitSprite: "",
            abilityTemplateId: "ambush", minClanTier: 0, rootChoiceId: "ranger_root",
            eligibleCultureIds: new List<string> { "gondor", "mordor" },
            choiceGroupIds: new List<string>(), treeVersion: newCareerTreeVersion));
    }

    [TestMethod]
    public void GetSwitchCost_NoForkPicks_IsZero()
    {
        UseForkWarboss();
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_root", 10);

        Assert.AreEqual(0, _service.GetSwitchCost("hero1", 10));
    }

    [TestMethod]
    public void GetSwitchCost_HoldingAForkPick_IsTheRetrainCost()
    {
        UseForkWarboss();
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_fork_a", 10);

        Assert.AreEqual(3500, _service.GetSwitchCost("hero1", 10)); // 1000 + 250 * 10
    }

    [TestMethod]
    public void GetSwitchCost_NoCareer_IsZero()
    {
        Assert.AreEqual(0, _service.GetSwitchCost("hero1", 10));
    }

    [TestMethod]
    public void SwitchCareer_NoForkPicks_ChargesNothing()
    {
        UseForkWarboss();
        _dataService.SetCareer("hero1", "warboss");

        Assert.AreEqual(CareerSwitchResult.Switched, _service.SwitchCareer("hero1", _hero, "ranger"));

        _purse.DidNotReceive().ChargeGold(Arg.Any<int>());
    }

    [TestMethod]
    public void SwitchCareer_HoldingAForkPick_ChargesTheCostOnce()
    {
        UseForkWarboss();
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_fork_a", 10);

        Assert.AreEqual(CareerSwitchResult.Switched, _service.SwitchCareer("hero1", _hero, "ranger"));

        _purse.Received(1).ChargeGold(3500);
        Assert.AreEqual("ranger", _dataService.GetCareerStringId("hero1"));
    }

    [TestMethod]
    public void SwitchCareer_ShortOfGold_IsNotEnoughGoldAndChangesNothing()
    {
        UseForkWarboss();
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_fork_a", 10);
        _dataService.UnlockTier("hero1", 2);
        _purse.ChargeGold(Arg.Any<int>()).Returns(false);

        Assert.AreEqual(CareerSwitchResult.NotEnoughGold, _service.SwitchCareer("hero1", _hero, "ranger"));

        Assert.AreEqual("warboss", _dataService.GetCareerStringId("hero1"));
        Assert.IsTrue(_dataService.GetOrCreateData("hero1").HasChoice("wb_fork_a"));
        Assert.IsTrue(_dataService.IsTierUnlocked("hero1", 2));
        _passiveService.DidNotReceive().RefreshCache(Arg.Any<ICareerDataService>(), Arg.Any<ICareerRegistry>());
    }

    [TestMethod]
    public void SwitchCareer_ToAVersionTwoCareer_StampsIt()
    {
        UseForkWarboss(newCareerTreeVersion: 2);
        _dataService.SetCareer("hero1", "warboss");

        Assert.AreEqual(CareerSwitchResult.Switched, _service.SwitchCareer("hero1", _hero, "ranger"));

        Assert.IsTrue(_dataService.HasFlag("hero1", CareerTreeVersion.FlagFor(2)));
    }

    [TestMethod]
    public void SwitchCareer_ToAVersionOneCareer_StampsNothing()
    {
        UseForkWarboss(newCareerTreeVersion: 1);
        _dataService.SetCareer("hero1", "warboss");

        Assert.AreEqual(CareerSwitchResult.Switched, _service.SwitchCareer("hero1", _hero, "ranger"));

        Assert.AreEqual(0, _dataService.GetOrCreateData("hero1").Flags.Count);
    }

    // ── review: unknown career, ineligible target, overflow-sized price ──

    [TestMethod]
    public void GetSwitchCost_CareerUnknownToTheRegistry_IsZero()
    {
        _dataService.SetCareer("hero1", "ghost");
        _registry.GetCareer("ghost").Returns((CareerDefinition)null);

        Assert.AreEqual(0, _service.GetSwitchCost("hero1", 10));
    }

    [TestMethod]
    public void SwitchCareer_IneligibleTargetFromAForkCareer_ChargesNothingAndChangesNothing()
    {
        UseForkWarboss();
        _registry.IsEligible("knight", _hero).Returns(false);
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_fork_a", 10);

        var result = _service.SwitchCareer("hero1", _hero, "knight");

        Assert.AreEqual(CareerSwitchResult.NotEligible, result);
        _purse.DidNotReceive().ChargeGold(Arg.Any<int>());
        Assert.AreEqual("warboss", _dataService.GetCareerStringId("hero1"));
        Assert.IsTrue(_dataService.GetOrCreateData("hero1").HasChoice("wb_fork_a"));
    }

    [TestMethod]
    public void SwitchCareer_HugePriceAndAPurseThatCannotPay_IsRefusedAndChangesNothing()
    {
        // A price clamped to int.MaxValue is still a price: it must reach the purse, not read as free.
        UseForkWarboss();
        _registry.GetRetrainCost(Arg.Any<int>()).Returns(int.MaxValue);
        _purse.ChargeGold(Arg.Any<int>()).Returns(false);
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_fork_a", 10);

        var result = _service.SwitchCareer("hero1", _hero, "ranger");

        Assert.AreEqual(CareerSwitchResult.NotEnoughGold, result);
        _purse.Received(1).ChargeGold(int.MaxValue);
        Assert.AreEqual("warboss", _dataService.GetCareerStringId("hero1"));
        Assert.IsTrue(_dataService.GetOrCreateData("hero1").HasChoice("wb_fork_a"));
    }
}
