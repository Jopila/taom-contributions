using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FarWatch.Hooks;
using TAOM.Features.FieldCamp.Hooks;

namespace TAOM.Tests.Features.FarWatch;

/// <summary>
/// Where the player lands when a ride's wait ends (docs/features/far-watch.md "What ends the wait"): the engine's own choice
/// first, then the camp panel while the camp stands, else the map. It never lands on the scout's own two menus, and inside a
/// settlement (which a camp never is) it leaves by the map with a warning.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FarWatchLandingTests
{
    private IGameMenuAdapter _menus = null!;
    private IFarWatchWorldAdapter _world = null!;
    private IModLogger _logger = null!;
    private FarWatchLanding _sut = null!;

    [TestInitialize]
    public void SetUp()
    {
        _menus = Substitute.For<IGameMenuAdapter>();
        _menus.EnsureMenuOpen(Arg.Any<string>()).Returns(true);
        _menus.SwitchTo(Arg.Any<string>()).Returns(true);
        _world = Substitute.For<IFarWatchWorldAdapter>();
        _world.ReadCamp().Returns(new ScoutCampFacts { CampExists = true, CampReady = true });
        _logger = Substitute.For<IModLogger>();
        _sut = new FarWatchLanding(_menus, _world, _logger);
    }

    [TestMethod]
    public void Land_TheEngineNamesAMenu_GoesThere_AndNowhereElse()
    {
        _menus.GenericStateMenuId.Returns("encounter");

        _sut.Land();

        _menus.Received(1).SwitchTo("encounter");
        _menus.DidNotReceiveWithAnyArgs().EnsureMenuOpen(default!);
        _menus.DidNotReceive().ExitToLast();
    }

    [DataTestMethod]
    [DataRow(FarWatchWaitMenu.WaitMenuId)]
    [DataRow(FarWatchPickMenu.PickMenuId)]
    public void Land_TheEngineNamesTheScoutsOwnMenu_IsNotALanding(string own)
    {
        _menus.GenericStateMenuId.Returns(own);

        _sut.Land();

        _menus.DidNotReceive().SwitchTo(Arg.Any<string>());
        _menus.Received(1).EnsureMenuOpen(FieldCampCampaignBehavior.CampSubMenuId);
    }

    [TestMethod]
    public void Land_NoEngineOpinion_TheCampStands_OpensTheCampPanel()
    {
        _sut.Land();

        _menus.Received(1).EnsureMenuOpen(FieldCampCampaignBehavior.CampSubMenuId);
        _menus.DidNotReceive().ExitToLast();
    }

    [TestMethod]
    public void Land_TheEngineMenuWillNotOpen_FallsToTheCampPanel()
    {
        _menus.GenericStateMenuId.Returns("encounter");
        _menus.SwitchTo("encounter").Returns(false);

        _sut.Land();

        _menus.Received(1).EnsureMenuOpen(FieldCampCampaignBehavior.CampSubMenuId);
    }

    [TestMethod]
    public void Land_TheCampIsGone_ExitsToTheMap()
    {
        _world.ReadCamp().Returns(new ScoutCampFacts());

        _sut.Land();

        _menus.Received(1).ExitToLast();
        _menus.DidNotReceiveWithAnyArgs().EnsureMenuOpen(default!);
    }

    [TestMethod]
    public void Land_TheCampPanelWillNotOpen_ExitsToTheMap()
    {
        _menus.EnsureMenuOpen(FieldCampCampaignBehavior.CampSubMenuId).Returns(false);

        _sut.Land();

        _menus.Received(1).ExitToLast();
    }

    [TestMethod]
    public void Land_InsideASettlement_ExitsToTheMapWithAWarning_NeverTheCampPanel()
    {
        _world.ReadCamp().Returns(new ScoutCampFacts { CampExists = true, InSettlement = true });

        _sut.Land();

        _menus.Received(1).ExitToLast();
        _menus.DidNotReceiveWithAnyArgs().EnsureMenuOpen(default!);
        _logger.ReceivedWithAnyArgs(1).LogWarning(default!);
    }

    [TestMethod]
    public void LeaveWaitMenu_NoMenu_JustLands()
    {
        _sut.LeaveWaitMenu(null);

        _menus.Received(1).EnsureMenuOpen(FieldCampCampaignBehavior.CampSubMenuId);
    }
}
