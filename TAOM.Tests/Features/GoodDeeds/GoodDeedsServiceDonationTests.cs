using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Execution;
using TAOM.Features.GoodDeeds;
using TAOM.Features.GoodDeeds.Models;
using TAOM.Features.Relations;
using static TAOM.Tests.Features.GoodDeeds.GoodDeedsFixture;

namespace TAOM.Tests.Features.GoodDeeds;

/// <summary>
/// Donating a hero prisoner to a settlement: the model value is vanilla's gain plus the bonus, both scaled by the captive's side
/// against the settlement owner's, and vanilla's own value comes back untouched whenever Good Deeds has nothing to say.
/// </summary>
[TestClass]
public class GoodDeedsServiceDonationTests
{
    private GoodDeedsFixture _f = null!;

    [TestInitialize]
    public void Setup() => _f = new GoodDeedsFixture();

    private void Facts(SideIds captive, SideIds owner) =>
        _f.Heroes.ReadDonation("captive", "town").Returns(new DonationFacts(captive, owner));

    private float Donated(float vanilla = 17.3f) => _f.Service.DonationRelation(vanilla, "captive", "town");

    [TestMethod]
    public void DonationRelation_ACaptiveOfTheOppositeSide_IsVanillaPlusFiveDoubled()
    {
        Facts(Evil, Free);

        Assert.AreEqual(45f, Donated(17.3f), "(17.3 + 5) * 2 = 44.6 rounds to 45");
    }

    [TestMethod]
    public void DonationRelation_ANeutralPairing_IsVanillaPlusFiveOnce()
    {
        Facts(Unaligned, Free);

        Assert.AreEqual(22f, Donated(17.3f));
    }

    [TestMethod]
    public void DonationRelation_ACaptiveOfTheOwnersSide_IsZero()
    {
        Facts(Free, Free);

        Assert.AreEqual(0f, Donated(17.3f), "no reward, and no vanilla relation either, for jailing a friend of the owner");
    }

    [TestMethod]
    public void DonationRelation_VanillaPaysNothing_TheBonusStillGoesThroughTheMultiplier()
    {
        // Vanilla pays only while the captive's relation with the owner is 0 or below; a liked captive still earns the bonus.
        Facts(Evil, Free);

        Assert.AreEqual(10f, Donated(0f));
    }

    [TestMethod]
    public void DonationRelation_HalfAPointRoundsAwayFromZero()
    {
        _f.Configure(Defaults with { Donation = new SideMultipliers(0f, 0.5f, 2f) });
        Facts(Unaligned, Free);

        Assert.AreEqual(3f, Donated(0f), "5 * 0.5 = 2.5 rounds to 3");
    }

    [TestMethod]
    public void DonationRelation_TheSettingsBonusAndMultipliers_AreUsed()
    {
        _f.Configure(Defaults with { DonationAmount = 8, Donation = new SideMultipliers(1f, 1f, 3f) });
        Facts(Evil, Free);

        Assert.AreEqual(39f, Donated(5f), "(5 + 8) * 3");
    }

    [DataTestMethod]
    [DataRow("k_free", "k_free", 0f)]
    [DataRow("k_evil", "k_evil", 0f)]
    [DataRow("k_free", "k_evil", 45f)]
    [DataRow("k_evil", "k_free", 45f)]
    [DataRow("k_other", "k_free", 22f)]
    [DataRow("k_evil", "k_other", 22f)]
    [DataRow("", "k_free", 22f)]
    [DataRow("k_evil", "", 22f)]
    [DataRow("", "", 22f)]
    public void DonationRelation_FollowsThePairingOfCaptiveAndOwner(string captiveKingdom, string ownerKingdom, float expected)
    {
        Facts(new SideIds(captiveKingdom, string.Empty), new SideIds(ownerKingdom, string.Empty));

        Assert.AreEqual(expected, Donated(17.3f));
    }

    [TestMethod]
    public void DonationRelation_TheSettingsAreOff_ReturnsVanillaExactly()
    {
        _f.Configure(Defaults with { DonationEnabled = false });
        Facts(Evil, Free);

        Assert.AreEqual(17.3f, Donated(17.3f));
        _f.Heroes.DidNotReceive().ReadDonation(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void DonationRelation_TheMasterSwitchIsOff_ReturnsVanillaExactly()
    {
        _f.Configure(Defaults with { Enabled = false });
        Facts(Evil, Free);

        Assert.AreEqual(17.3f, Donated(17.3f));
    }

    [TestMethod]
    public void DonationRelation_OtherSwitchesOff_DoNotStopADonation()
    {
        _f.Configure(Defaults with { ReleaseEnabled = false, TournamentEnabled = false });
        Facts(Evil, Free);

        Assert.AreEqual(45f, Donated(17.3f));
    }

    [TestMethod]
    public void DonationRelation_TheFactsCannotBeRead_ReturnsVanilla()
    {
        _f.Heroes.ReadDonation("captive", "town").Returns((DonationFacts?)null);

        Assert.AreEqual(17.3f, Donated(17.3f));
    }

    [DataTestMethod]
    [DataRow(null, "town")]
    [DataRow("captive", null)]
    [DataRow("", "town")]
    [DataRow("captive", " ")]
    public void DonationRelation_ABlankId_ReturnsVanilla(string? heroId, string? settlementId)
    {
        Facts(Evil, Free);

        Assert.AreEqual(17.3f, _f.Service.DonationRelation(17.3f, heroId, settlementId));
        _f.Heroes.DidNotReceive().ReadDonation(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void DonationRelation_ACoopPeerThatDefersToTheHost_ReturnsVanilla()
    {
        _f.Coop.ShouldDeferToHost.Returns(true);
        Facts(Evil, Free);

        Assert.AreEqual(17.3f, Donated(17.3f));
        _f.Heroes.DidNotReceive().ReadDonation(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void DonationRelation_ADedicatedServer_ReturnsVanilla()
    {
        _f.Server.IsDedicatedServer.Returns(true);
        Facts(Evil, Free);

        Assert.AreEqual(17.3f, Donated(17.3f));
        _f.Heroes.DidNotReceive().ReadDonation(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void DonationRelation_NeverGrantsThroughTheRelationSeam()
    {
        // Vanilla applies the returned value itself (CharacterRelationCampaignBehavior), so the seam is not touched here.
        Facts(Evil, Free);

        Donated(17.3f);

        _f.GrantService.DidNotReceive().Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>());
    }
}
