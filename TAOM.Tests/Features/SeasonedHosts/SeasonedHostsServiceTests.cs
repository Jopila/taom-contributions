using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.SeasonedHosts;

namespace TAOM.Tests.Features.SeasonedHosts;

/// <summary>The decision matrix of Seasoned Hosts (#890): who gets the bonus, the floor, and when the steward grant is skipped.</summary>
[TestClass]
public class SeasonedHostsServiceTests
{
    private static SeasonedHostsService Make(
        float trainingBonus = 0.25f,
        float skillXpMultiplier = 1.25f,
        bool learningNeverStops = true,
        bool noStewardXp = true,
        bool defer = false)
    {
        var provider = Substitute.For<ISeasonedHostsSettingsProvider>();
        provider.TrainingBonus.Returns(trainingBonus);
        provider.SkillXpMultiplier.Returns(skillXpMultiplier);
        provider.LearningNeverStops.Returns(learningNeverStops);
        provider.NoStewardXp.Returns(noStewardXp);
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.ShouldDeferToHost.Returns(defer);
        return new SeasonedHostsService(provider, coop);
    }

    private static LordXpFacts Lord(bool lord = true, bool main = false, PlayerControl clan = PlayerControl.NotPlayer) => new(lord, main, clan);

    [TestMethod]
    public void SkillXpMultiplier_AnAiLord_ReturnsTheSetting() =>
        Assert.AreEqual(1.25f, Make().SkillXpMultiplier(Lord()));

    [TestMethod]
    public void SkillXpMultiplier_TheMainHero_ReturnsOne() =>
        Assert.AreEqual(1f, Make().SkillXpMultiplier(Lord(main: true, clan: PlayerControl.Player)));

    [TestMethod]
    public void SkillXpMultiplier_TheMainHeroWhoseClanReadsAsAi_ReturnsOne() =>
        Assert.AreEqual(1f, Make().SkillXpMultiplier(Lord(main: true)));

    [TestMethod]
    public void SkillXpMultiplier_ALordOfThePlayersClan_ReturnsOne() =>
        Assert.AreEqual(1f, Make().SkillXpMultiplier(Lord(clan: PlayerControl.Player)), "a spouse or child lord of the player's clan");

    [TestMethod]
    public void SkillXpMultiplier_AnUnknownClan_ReturnsOne() =>
        Assert.AreEqual(1f, Make().SkillXpMultiplier(Lord(clan: PlayerControl.Unknown)));

    [TestMethod]
    public void SkillXpMultiplier_ANonLord_ReturnsOne() =>
        Assert.AreEqual(1f, Make().SkillXpMultiplier(Lord(lord: false)));

    [TestMethod]
    public void SkillXpMultiplier_TheSettingAtOne_ReturnsOneForAnAiLord() =>
        Assert.AreEqual(1f, Make(skillXpMultiplier: 1f).SkillXpMultiplier(Lord()));

    [TestMethod]
    public void SkillXpMultiplier_DeferringToTheHost_ReturnsOneForAnAiLord() =>
        Assert.AreEqual(1f, Make(defer: true).SkillXpMultiplier(Lord()));

    private static TrainingPartyFacts Party(bool lord = true, bool main = false, PlayerControl owner = PlayerControl.NotPlayer, PlayerControl army = PlayerControl.NotPlayer) =>
        new(lord, main, owner, army);

    [TestMethod]
    public void TrainingFactor_AnAiLordParty_ReturnsTheBonus() => Assert.AreEqual(0.25f, Make().TrainingFactor(Party()));

    [TestMethod]
    public void TrainingFactor_TheMainParty_ReturnsZero() => Assert.AreEqual(0f, Make().TrainingFactor(Party(main: true)));

    [TestMethod]
    public void TrainingFactor_APartyOwnedByThePlayersClan_ReturnsZero() =>
        Assert.AreEqual(0f, Make().TrainingFactor(Party(owner: PlayerControl.Player)));

    [TestMethod]
    public void TrainingFactor_APartyInAPlayerLedArmy_ReturnsZero() =>
        Assert.AreEqual(0f, Make().TrainingFactor(Party(army: PlayerControl.Player)));

    [TestMethod]
    public void TrainingFactor_UnknownOwnerOrUnknownArmyLeader_ReturnsZero()
    {
        Assert.AreEqual(0f, Make().TrainingFactor(Party(owner: PlayerControl.Unknown)));
        Assert.AreEqual(0f, Make().TrainingFactor(Party(army: PlayerControl.Unknown)));
    }

    [TestMethod]
    public void TrainingFactor_ACaravanOrGarrison_ReturnsZero() =>
        Assert.AreEqual(0f, Make().TrainingFactor(Party(lord: false)));

    [TestMethod]
    public void TrainingFactor_TheBonusAtZero_ReturnsZero() =>
        Assert.AreEqual(0f, Make(trainingBonus: 0f).TrainingFactor(Party()));

    [TestMethod]
    public void TrainingFactor_DeferringToTheHost_ReturnsZeroForAnAiLordParty() =>
        Assert.AreEqual(0f, Make(defer: true).TrainingFactor(Party()));

    [TestMethod]
    public void LearningRateFloor_ToggleOff_ReturnsTheCurrentMinimum() =>
        Assert.AreEqual(0f, Make(learningNeverStops: false).LearningRateFloor(0f));

    [TestMethod]
    public void LearningRateFloor_ToggleOn_RaisesZeroToTheFloor() =>
        Assert.AreEqual(0.05f, Make().LearningRateFloor(0f));

    [TestMethod]
    public void LearningRateFloor_ToggleOn_KeepsAHigherMinimum() =>
        Assert.AreEqual(0.3f, Make().LearningRateFloor(0.3f));

    [TestMethod]
    public void LearningRateFloor_ToggleOn_RaisesAnUnsetMinimum() =>
        Assert.AreEqual(0.05f, Make().LearningRateFloor(float.MinValue));

    [TestMethod]
    public void LearningRateFloor_ToggleOff_KeepsAnUnsetMinimum() =>
        Assert.AreEqual(float.MinValue, Make(learningNeverStops: false).LearningRateFloor(float.MinValue));

    [TestMethod]
    public void LearningRateFloor_DeferringToTheHost_StillRaisesTheMinimum() =>
        Assert.AreEqual(0.05f, Make(defer: true).LearningRateFloor(0f), "the floor does not defer: it has no hero to classify");

    [TestMethod]
    public void LearningRateFloorValue_IsFivePercent() => Assert.AreEqual(0.05f, SeasonedHostsService.LearningRateFloorValue);

    [TestMethod]
    public void SkipsProjectStewardXp_OnAndNotDeferring_ReturnsTrue() => Assert.IsTrue(Make().SkipsProjectStewardXp());

    [TestMethod]
    public void SkipsProjectStewardXp_Off_ReturnsFalse() => Assert.IsFalse(Make(noStewardXp: false).SkipsProjectStewardXp());

    [TestMethod]
    public void SkipsProjectStewardXp_OnButDeferringToTheHost_ReturnsFalse() => Assert.IsFalse(Make(defer: true).SkipsProjectStewardXp());

    [TestMethod]
    public void SkipsProjectStewardXp_OffAndDeferring_ReturnsFalse() => Assert.IsFalse(Make(noStewardXp: false, defer: true).SkipsProjectStewardXp());
}
