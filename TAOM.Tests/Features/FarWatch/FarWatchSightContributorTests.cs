using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.FarWatch;
using TAOM.Features.FieldCamp;
using TAOM.Features.HiddenHosts;

namespace TAOM.Tests.Features.FarWatch;

/// <summary>
/// The contributor is the boundary of the sight seam: the toggle and the party test, then the service's arithmetic on plain
/// fields (all of it in <see cref="FarWatchServiceTests"/>). The main-party branch needs a campaign (<c>IsMainParty</c> reads
/// <c>Campaign.Current</c>), so what a unit test can pin is every way the answer is zero.
/// </summary>
[TestClass]
public class FarWatchSightContributorTests
{
    private ICampSettingsProvider _camps = null!;
    private IFarWatchWorldAdapter _world = null!;
    private FarWatchStore _store = null!;
    private FarWatchSightContributor _sut = null!;

    [TestInitialize]
    public void SetUp()
    {
        var logger = Substitute.For<IModLogger>();
        _store = new FarWatchStore(logger);
        _store.StartRide(ScoutKind.Settlement, "scout_1", "Mirien", false, "town_9", "Edoras", 100d, 4d, 100);
        _world = Substitute.For<IFarWatchWorldAdapter>();
        _world.NowHours.Returns(101d);
        _camps = Substitute.For<ICampSettingsProvider>();
        _camps.Enabled.Returns(true);
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsAuthority.Returns(true);
        var hidden = new HiddenHostsService(Substitute.For<IHiddenHostsSettings>(), _store);
        var service = new FarWatchService(_store, _world, hidden, coop, Substitute.For<IDedicatedServerProvider>(), _camps, logger);
        _sut = new FarWatchSightContributor(service, _world, _camps);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheFieldCampToggleOff_GivesZero_ForBothMembers_AndReadsNeitherTheClockNorTheParty()
    {
        _camps.Enabled.Returns(false);
        // A bare party: its IsMainParty would throw without a campaign, so only the toggle's short circuit can answer.
        var party = (TaleWorlds.CampaignSystem.Party.MobileParty)System.Runtime.Serialization.FormatterServices
            .GetUninitializedObject(typeof(TaleWorlds.CampaignSystem.Party.MobileParty));

        Assert.AreEqual(0f, _sut.GetSpottingRangeBonusFactor(party));
        Assert.AreEqual(0f, _sut.GetSpottingRangeCapBonus(party));
        _ = _world.DidNotReceive().NowHours;
    }

    [TestMethod]
    public void ANullParty_GivesZero_ForBothMembers()
    {
        Assert.AreEqual(0f, _sut.GetSpottingRangeBonusFactor(null!));
        Assert.AreEqual(0f, _sut.GetSpottingRangeCapBonus(null!));
    }

    [TestMethod]
    public void TheContributor_IsAnIPartySpottingContributor() =>
        Assert.IsInstanceOfType(_sut, typeof(IPartySpottingContributor));
}
