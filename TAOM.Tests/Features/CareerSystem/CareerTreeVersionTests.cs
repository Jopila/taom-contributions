using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

// #769 E12 — a hero's stored tree version is a flag on the hero, so a redesigned (v2+) tree can be
// told apart from the save that was built against the old one.
[TestClass]
public class CareerTreeVersionTests
{
    private const string Hero = "main_hero";

    private CareerDataService _dataService;

    [TestInitialize]
    public void Setup()
    {
        _dataService = new CareerDataService();
        _dataService.SetCareer(Hero, "ranger");
    }

    private static CareerDefinition Career(int treeVersion) => new CareerDefinition(
        id: "ranger", displayName: "Ranger", description: "", portraitSprite: "",
        abilityTemplateId: "ambush", minClanTier: 0, rootChoiceId: "ranger_root",
        eligibleCultureIds: new List<string>(), choiceGroupIds: new List<string>(),
        treeVersion: treeVersion);

    [TestMethod]
    public void FlagFor_Version_IsCareerTreeVN()
    {
        Assert.AreEqual("career_tree_v2", CareerTreeVersion.FlagFor(2));
        Assert.AreEqual("career_tree_v10", CareerTreeVersion.FlagFor(10));
    }

    [TestMethod]
    public void FlagFor_Version_NeverCarriesAComma()
    {
        Assert.IsFalse(CareerTreeVersion.FlagFor(3).Contains(","));
    }

    [TestMethod]
    public void StoredVersion_NoFlags_IsOne()
    {
        Assert.AreEqual(1, CareerTreeVersion.StoredVersion(_dataService.GetOrCreateData(Hero)));
    }

    [TestMethod]
    public void StoredVersion_VersionFlag_IsThatVersion()
    {
        _dataService.SetFlag(Hero, CareerTreeVersion.FlagFor(2));

        Assert.AreEqual(2, CareerTreeVersion.StoredVersion(_dataService.GetOrCreateData(Hero)));
    }

    [TestMethod]
    public void StoredVersion_SeveralVersionFlags_IsTheHighest()
    {
        _dataService.SetFlag(Hero, CareerTreeVersion.FlagFor(2));
        _dataService.SetFlag(Hero, CareerTreeVersion.FlagFor(4));
        _dataService.SetFlag(Hero, CareerTreeVersion.FlagFor(3));

        Assert.AreEqual(4, CareerTreeVersion.StoredVersion(_dataService.GetOrCreateData(Hero)));
    }

    [TestMethod]
    public void StoredVersion_UnrelatedAndMalformedFlags_AreIgnored()
    {
        _dataService.SetFlag(Hero, "quest_done");
        _dataService.SetFlag(Hero, "career_tree_v");
        _dataService.SetFlag(Hero, "career_tree_vx");
        _dataService.SetFlag(Hero, "career_tree_v-3");
        _dataService.SetFlag(Hero, "career_tree_v 2");
        _dataService.SetFlag(Hero, "career_tree_v+2");

        Assert.AreEqual(1, CareerTreeVersion.StoredVersion(_dataService.GetOrCreateData(Hero)));
    }

    [TestMethod]
    public void StoredVersion_NullData_IsOne()
    {
        Assert.AreEqual(1, CareerTreeVersion.StoredVersion(null));
    }

    [TestMethod]
    public void Stamp_VersionTwoCareer_SetsTheFlag()
    {
        CareerTreeVersion.Stamp(_dataService, Hero, Career(2));

        Assert.IsTrue(_dataService.HasFlag(Hero, "career_tree_v2"));
    }

    [TestMethod]
    public void Stamp_VersionOneCareer_SetsNothing()
    {
        CareerTreeVersion.Stamp(_dataService, Hero, Career(1));

        Assert.AreEqual(0, _dataService.GetOrCreateData(Hero).Flags.Count);
    }

    [TestMethod]
    public void Stamp_NullCareer_SetsNothing()
    {
        CareerTreeVersion.Stamp(_dataService, Hero, null);

        Assert.AreEqual(0, _dataService.GetOrCreateData(Hero).Flags.Count);
    }

    // A flag left by an earlier career must not outlive the next stamp: StoredVersion takes the
    // highest, so a stale v3 would hide a later bump to v2.

    [TestMethod]
    public void Stamp_HeroHoldingAHigherVersionFlag_VersionOneCareer_LeavesNoVersionFlag()
    {
        _dataService.SetFlag(Hero, CareerTreeVersion.FlagFor(3));
        _dataService.SetFlag(Hero, "quest_done");

        CareerTreeVersion.Stamp(_dataService, Hero, Career(1));

        var flags = _dataService.GetOrCreateData(Hero).Flags;
        CollectionAssert.AreEqual(new[] { "quest_done" }, flags);
        Assert.AreEqual(1, CareerTreeVersion.StoredVersion(_dataService.GetOrCreateData(Hero)));
    }

    [TestMethod]
    public void Stamp_HeroHoldingAHigherVersionFlag_VersionTwoCareer_LeavesExactlyV2()
    {
        _dataService.SetFlag(Hero, CareerTreeVersion.FlagFor(3));
        _dataService.SetFlag(Hero, "quest_done");

        CareerTreeVersion.Stamp(_dataService, Hero, Career(2));

        var flags = _dataService.GetOrCreateData(Hero).Flags;
        CollectionAssert.AreEquivalent(new[] { "quest_done", "career_tree_v2" }, flags);
    }

    [TestMethod]
    public void Stamp_SameVersionTwice_KeepsOneFlag()
    {
        CareerTreeVersion.Stamp(_dataService, Hero, Career(2));
        CareerTreeVersion.Stamp(_dataService, Hero, Career(2));

        CollectionAssert.AreEqual(new[] { "career_tree_v2" }, _dataService.GetOrCreateData(Hero).Flags);
    }
}
