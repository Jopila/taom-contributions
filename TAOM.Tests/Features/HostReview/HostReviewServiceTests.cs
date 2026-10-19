using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.HostReview;
using TAOM.Features.HostReview.Domain;

namespace TAOM.Tests.Features.HostReview;

/// <summary>
/// Host Review (#891): which block applies to the camp menu option, the order and size of the lineup,
/// and when the launcher is called at all.
/// </summary>
[TestClass]
public class HostReviewServiceTests
{
    private IHostReviewPartyAdapter _party = null!;
    private IEnlistmentStateQuery _enlistment = null!;
    private IEncounterAdapter _encounter = null!;
    private ICoopSessionProvider _coop = null!;
    private IHostReviewMissionLauncher _launcher = null!;
    private HostReviewService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _party = Substitute.For<IHostReviewPartyAdapter>();
        _enlistment = Substitute.For<IEnlistmentStateQuery>();
        _encounter = Substitute.For<IEncounterAdapter>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _launcher = Substitute.For<IHostReviewMissionLauncher>();
        _party.GetRoster().Returns(Roster(Hero(0, main: true), Troop(1, 5)));
        _party.RealBattleSize.Returns(200);
        _launcher.TryOpen(Arg.Any<IReadOnlyList<HostReviewSlot>>()).Returns(true);
        _service = new HostReviewService(_party, _enlistment, _encounter, _coop, _launcher, Substitute.For<IModLogger>());
    }

    private static HostRosterEntry Hero(int index, bool main = false, int healthy = 1) =>
        new HostRosterEntry(index, isHero: true, isMainHero: main, healthyCount: healthy);

    private static HostRosterEntry Troop(int index, int healthy) =>
        new HostRosterEntry(index, isHero: false, isMainHero: false, healthyCount: healthy);

    private static IReadOnlyList<HostRosterEntry> Roster(params HostRosterEntry[] entries) => entries;

    private static int Total(IReadOnlyList<HostReviewSlot> lineup) => lineup.Sum(s => s.Count);

    [TestMethod]
    public void GetBlock_NothingWrong_IsNone()
    {
        Assert.AreEqual(HostReviewBlock.None, _service.GetBlock());
    }

    [TestMethod]
    public void GetBlock_Enlisted_IsEnlisted()
    {
        _enlistment.IsEnlisted.Returns(true);
        Assert.AreEqual(HostReviewBlock.Enlisted, _service.GetBlock());
    }

    [TestMethod]
    public void GetBlock_PlayerEncounterCurrent_IsInEncounter()
    {
        _encounter.HasCurrent.Returns(true);
        Assert.AreEqual(HostReviewBlock.InEncounter, _service.GetBlock());
    }

    [TestMethod]
    public void GetBlock_MainPartyInMapEvent_IsInEncounter()
    {
        _encounter.IsMainPartyInMapEvent.Returns(true);
        Assert.AreEqual(HostReviewBlock.InEncounter, _service.GetBlock());
    }

    [TestMethod]
    public void GetBlock_CoopSessionLive_IsCoopSession()
    {
        _coop.IsSessionActive.Returns(true);
        Assert.AreEqual(HostReviewBlock.CoopSession, _service.GetBlock());
    }

    [TestMethod]
    public void GetBlock_MainHeroWounded_IsHeroWounded()
    {
        _party.GetRoster().Returns(Roster(Hero(0, main: true, healthy: 0), Troop(1, 5)));
        Assert.AreEqual(HostReviewBlock.HeroWounded, _service.GetBlock());
    }

    [TestMethod]
    public void GetBlock_MainHeroAbsentFromTheRoster_IsHeroWounded()
    {
        _party.GetRoster().Returns(Roster(Hero(0), Troop(1, 5)));
        Assert.AreEqual(HostReviewBlock.HeroWounded, _service.GetBlock());
    }

    [TestMethod]
    public void GetBlock_EmptyRoster_IsHeroWounded()
    {
        // The adapter returns an empty roster when reading the party throws.
        _party.GetRoster().Returns(Roster());
        Assert.AreEqual(HostReviewBlock.HeroWounded, _service.GetBlock());
    }

    [TestMethod]
    public void GetBlock_OneHealthyMan_IsTooFewHealthy()
    {
        _party.GetRoster().Returns(Roster(Hero(0, main: true), Troop(1, 0)));
        Assert.AreEqual(HostReviewBlock.TooFewHealthy, _service.GetBlock());
    }

    [TestMethod]
    public void GetBlock_TwoHealthyMen_IsNone()
    {
        _party.GetRoster().Returns(Roster(Hero(0, main: true), Troop(1, 1)));
        Assert.AreEqual(HostReviewBlock.None, _service.GetBlock());
    }

    [TestMethod]
    public void GetBlock_EverythingWrong_ReportsEnlistedFirst()
    {
        _enlistment.IsEnlisted.Returns(true);
        _encounter.HasCurrent.Returns(true);
        _coop.IsSessionActive.Returns(true);
        _party.GetRoster().Returns(Roster(Hero(0, main: true, healthy: 0)));
        Assert.AreEqual(HostReviewBlock.Enlisted, _service.GetBlock());
    }

    [TestMethod]
    public void GetBlock_EncounterAndLaterBlocks_ReportsEncounterBeforeCoop()
    {
        _encounter.HasCurrent.Returns(true);
        _coop.IsSessionActive.Returns(true);
        _party.GetRoster().Returns(Roster(Hero(0, main: true, healthy: 0)));
        Assert.AreEqual(HostReviewBlock.InEncounter, _service.GetBlock());
    }

    [TestMethod]
    public void GetBlock_CoopAndLaterBlocks_ReportsCoopBeforeWound()
    {
        _coop.IsSessionActive.Returns(true);
        _party.GetRoster().Returns(Roster(Hero(0, main: true, healthy: 0)));
        Assert.AreEqual(HostReviewBlock.CoopSession, _service.GetBlock());
    }

    [TestMethod]
    public void GetBlock_WoundAndTooFew_ReportsWoundBeforeTooFew()
    {
        _party.GetRoster().Returns(Roster(Hero(0, main: true, healthy: 0)));
        Assert.AreEqual(HostReviewBlock.HeroWounded, _service.GetBlock());
    }

    [TestMethod]
    public void BuildLineup_MainHeroListedLate_StandsFirst()
    {
        _party.GetRoster().Returns(Roster(Troop(0, 3), Hero(1), Hero(2, main: true)));

        var lineup = _service.BuildLineup(100);

        CollectionAssert.AreEqual(new[] { 2, 1, 0 }, lineup.Select(s => s.RosterIndex).ToArray());
    }

    [TestMethod]
    public void BuildLineup_HeroesBeforeTroops_EvenWhenTroopsComeFirstInTheRoster()
    {
        _party.GetRoster().Returns(Roster(Troop(0, 4), Hero(1, main: true), Troop(2, 2), Hero(3)));

        var lineup = _service.BuildLineup(100);

        CollectionAssert.AreEqual(new[] { 1, 3, 0, 2 }, lineup.Select(s => s.RosterIndex).ToArray());
    }

    [TestMethod]
    public void BuildLineup_WoundedCompanion_IsSkipped()
    {
        _party.GetRoster().Returns(Roster(Hero(0, main: true), Hero(1, healthy: 0), Troop(2, 3)));

        var lineup = _service.BuildLineup(100);

        CollectionAssert.AreEqual(new[] { 0, 2 }, lineup.Select(s => s.RosterIndex).ToArray());
    }

    [TestMethod]
    public void BuildLineup_Troops_CountOnlyTheHealthy()
    {
        _party.GetRoster().Returns(Roster(Hero(0, main: true), Troop(1, 7), Troop(2, 0)));

        var lineup = _service.BuildLineup(100);

        Assert.AreEqual(2, lineup.Count, "a troop with no healthy men gets no slot");
        Assert.AreEqual(7, lineup.Single(s => s.RosterIndex == 1).Count);
    }

    [TestMethod]
    public void BuildLineup_BattleSizeZero_IsEmpty()
    {
        Assert.AreEqual(0, _service.BuildLineup(0).Count);
    }

    [TestMethod]
    public void BuildLineup_BattleSizeBelowTheHeroCount_KeepsTheFirstHeroesOnly()
    {
        _party.GetRoster().Returns(Roster(Hero(0), Hero(1, main: true), Hero(2), Troop(3, 10)));

        var lineup = _service.BuildLineup(2);

        CollectionAssert.AreEqual(new[] { 1, 0 }, lineup.Select(s => s.RosterIndex).ToArray());
        Assert.AreEqual(2, Total(lineup));
    }

    [TestMethod]
    public void BuildLineup_BattleSizeInsideATroopStack_TrimsThatStack()
    {
        _party.GetRoster().Returns(Roster(Hero(0, main: true), Troop(1, 10), Troop(2, 10)));

        var lineup = _service.BuildLineup(6);

        Assert.AreEqual(6, Total(lineup));
        Assert.AreEqual(5, lineup.Single(s => s.RosterIndex == 1).Count);
        Assert.IsFalse(lineup.Any(s => s.RosterIndex == 2), "the second stack no longer fits");
    }

    [TestMethod]
    public void BuildLineup_BattleSizeAboveTheTotal_TakesEveryHealthyMan()
    {
        _party.GetRoster().Returns(Roster(Hero(0, main: true), Troop(1, 3)));

        Assert.AreEqual(4, Total(_service.BuildLineup(500)));
    }

    [TestMethod]
    public void Launch_Blocked_ReturnsTheBlockAndDoesNotCallTheLauncher()
    {
        _enlistment.IsEnlisted.Returns(true);

        var result = _service.Launch();

        Assert.AreEqual(HostReviewBlock.Enlisted, result);
        _launcher.DidNotReceiveWithAnyArgs().TryOpen(null!);
    }

    [TestMethod]
    public void Launch_EmptyLineup_ReturnsOpenFailedAndDoesNotCallTheLauncher()
    {
        _party.RealBattleSize.Returns(0);

        var result = _service.Launch();

        Assert.AreEqual(HostReviewBlock.OpenFailed, result);
        _launcher.DidNotReceiveWithAnyArgs().TryOpen(null!);
    }

    [TestMethod]
    public void Launch_LauncherRefuses_ReturnsOpenFailed()
    {
        _launcher.TryOpen(Arg.Any<IReadOnlyList<HostReviewSlot>>()).Returns(false);

        Assert.AreEqual(HostReviewBlock.OpenFailed, _service.Launch());
    }

    [TestMethod]
    public void Launch_LauncherAccepts_ReturnsNoneAndPassesTheLineup()
    {
        var result = _service.Launch();

        Assert.AreEqual(HostReviewBlock.None, result);
        _launcher.Received(1).TryOpen(Arg.Is<IReadOnlyList<HostReviewSlot>>(l => Total(l) == 6 && l[0].RosterIndex == 0));
    }

    [TestMethod]
    public void Launch_EmptyRoster_ReturnsHeroWoundedAndNeverCallsTheLauncher()
    {
        _party.GetRoster().Returns(Roster());

        var result = _service.Launch();

        Assert.AreEqual(HostReviewBlock.HeroWounded, result);
        _launcher.DidNotReceiveWithAnyArgs().TryOpen(null!);
    }
}
