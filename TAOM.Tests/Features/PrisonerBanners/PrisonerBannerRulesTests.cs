using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.PrisonerBanners;

namespace TAOM.Tests.Features.PrisonerBanners;

[TestClass]
public class PrisonerBannerRulesTests
{
    private static PrisonerBannerView Decide(bool enabled = true, bool prisoner = true, bool clan = true, bool kingdom = true, bool leader = false)
        => PrisonerBannerRules.Decide(enabled, prisoner, clan, kingdom, leader);

    [TestMethod]
    public void AKingdomLordPrisoner_ShowsBothBanners_ButNoIconWhenNotTheLeader()
    {
        var view = Decide();

        Assert.IsTrue(view.ShowClanBanner);
        Assert.IsTrue(view.ShowKingdomBanner);
        Assert.IsFalse(view.ShowLeaderIcon);
    }

    [TestMethod]
    public void TheClanLeader_AlsoShowsTheNobleIcon()
    {
        var view = Decide(leader: true);

        Assert.IsTrue(view.ShowClanBanner && view.ShowKingdomBanner && view.ShowLeaderIcon);
    }

    [TestMethod]
    public void AClanWithoutAKingdom_ShowsOnlyItsOwnBanner()
    {
        var view = Decide(kingdom: false, leader: true);

        Assert.IsTrue(view.ShowClanBanner);
        Assert.IsFalse(view.ShowKingdomBanner);
        Assert.IsTrue(view.ShowLeaderIcon);
    }

    [TestMethod]
    public void AHeroWithoutAClan_ShowsNothing()
        => AssertNothing(Decide(clan: false, kingdom: true, leader: true));

    [TestMethod]
    public void ARowThatIsNotAPrisoner_ShowsNothing()
        => AssertNothing(Decide(prisoner: false, leader: true));

    [TestMethod]
    public void WithTheToggleOff_ShowsNothing()
        => AssertNothing(Decide(enabled: false, leader: true));

    private static void AssertNothing(PrisonerBannerView view)
    {
        Assert.IsFalse(view.ShowClanBanner);
        Assert.IsFalse(view.ShowKingdomBanner);
        Assert.IsFalse(view.ShowLeaderIcon);
    }
}
