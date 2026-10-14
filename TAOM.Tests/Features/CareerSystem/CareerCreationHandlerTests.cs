using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class CareerCreationHandlerTests
{
    private CareerDataService _dataService;
    private ICareerRegistry _registry;
    private ICareerPassiveService _passiveService;
    private IModLogger _logger;
    private CareerCreationHandler _handler;

    [TestInitialize]
    public void Setup()
    {
        _dataService = new CareerDataService();
        _registry = Substitute.For<ICareerRegistry>();
        _passiveService = Substitute.For<ICareerPassiveService>();
        _logger = Substitute.For<IModLogger>();
        _handler = new CareerCreationHandler(_dataService, _registry, _passiveService, _logger);

        _registry.GetCareer("warboss").Returns(new CareerDefinition(
            id: "warboss", displayName: "Warboss", description: "", portraitSprite: "",
            abilityTemplateId: "rally",
            minClanTier: 0, rootChoiceId: "wb_root",
            eligibleCultureIds: new List<string> { "mordor" },
            choiceGroupIds: new List<string>()));

        _registry.GetMaxChoicesForHero(Arg.Any<CareerDefinition>(), 1).Returns(2);
    }

    [TestMethod]
    public void OnCareerSelected_ValidCareer_SetsCareerAndAddsRoot()
    {
        _handler.OnCareerSelected("hero1", "warboss");

        Assert.AreEqual("warboss", _dataService.GetCareerStringId("hero1"));
        Assert.AreEqual(1, _dataService.GetChoiceCount("hero1"));
        Assert.IsTrue(_dataService.GetOrCreateData("hero1").HasChoice("wb_root"));
    }

    [TestMethod]
    public void OnCareerSelected_ValidCareer_RefreshesCache()
    {
        _handler.OnCareerSelected("hero1", "warboss");
        _passiveService.Received(1).RefreshCache(_dataService, _registry);
    }

    [TestMethod]
    public void OnCareerSelected_InvalidCareer_DoesNotSetCareer()
    {
        _registry.GetCareer("nonexistent").Returns((CareerDefinition)null);
        _handler.OnCareerSelected("hero1", "nonexistent");

        Assert.IsFalse(_dataService.HasCareer("hero1"));
    }

    [TestMethod]
    public void OnCareerSelected_NullHeroId_DoesNothing()
    {
        _handler.OnCareerSelected(null, "warboss");
        Assert.AreEqual(0, _dataService.GetAllData().Count);
    }

    // ── #769 E12: the tree-version stamp ──

    private void RegisterCareer(string id, int treeVersion)
    {
        _registry.GetCareer(id).Returns(new CareerDefinition(
            id: id, displayName: id, description: "", portraitSprite: "",
            abilityTemplateId: "rally", minClanTier: 0, rootChoiceId: id + "_root",
            eligibleCultureIds: new List<string> { "mordor" },
            choiceGroupIds: new List<string>(), treeVersion: treeVersion));
    }

    [TestMethod]
    public void OnCareerSelected_VersionTwoCareer_StampsTheTreeVersion()
    {
        RegisterCareer("redesigned", treeVersion: 2);

        _handler.OnCareerSelected("hero1", "redesigned");

        Assert.IsTrue(_dataService.HasFlag("hero1", CareerTreeVersion.FlagFor(2)));
    }

    [TestMethod]
    public void OnCareerSelected_VersionOneCareer_StampsNothing()
    {
        RegisterCareer("classic", treeVersion: 1);

        _handler.OnCareerSelected("hero1", "classic");

        Assert.AreEqual(0, _dataService.GetOrCreateData("hero1").Flags.Count);
    }
}
