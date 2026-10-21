using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.Actions;
using TAOM.Features.GoodDeeds;
using TAOM.Features.GoodDeeds.Hooks;
using TAOM.Features.GoodDeeds.Models;
using TAOM.Tests.Infrastructure;
using static TAOM.Tests.Features.GoodDeeds.GoodDeedsFixture;

namespace TAOM.Tests.Features.GoodDeeds;

/// <summary>
/// The listeners are thin boundaries. The release listener hands the prisoner's id, whether the detail was a release by choice
/// and, only for that detail, the raw facts about the event's party (read by the adapter) to the service; the tournament
/// listener hands whether the player won and the town's id. A fault never escapes into the engine's event invoke, which has no
/// try of its own. Building a CampaignBehaviorBase runs engine code, hence the category.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class GoodDeedsBehaviorTests
{
    private static readonly ReleaseSource FromMain = new(true, false, false);

    private GoodDeedsFixture _f = null!;
    private GoodDeedsBehavior _behavior = null!;

    [TestInitialize]
    public void Setup()
    {
        _f = new GoodDeedsFixture();
        _behavior = new GoodDeedsBehavior(_f.Service, _f.Heroes, _f.Logger);
    }

    // --- the release listener ------------------------------------------------------------------------------------------

    [TestMethod]
    public void Handle_AReleaseByChoiceFromThePlayersParty_ReachesTheService()
    {
        _f.Heroes.ReadRelease("lord").Returns(Release(Free, Free));
        _f.Heroes.ReadReleaseSource(null).Returns(FromMain);

        _behavior.Handle("lord", true, null);

        Assert.AreEqual(1, _f.Grants.Count);
    }

    [TestMethod]
    public void Handle_APartyThatIsNoneOfTheThreeSources_GrantsNothing()
    {
        _f.Heroes.ReadRelease("lord").Returns(Release(Free, Free));
        _f.Heroes.ReadReleaseSource(null).Returns(ReleaseSource.None);

        _behavior.Handle("lord", true, null);

        Assert.AreEqual(0, _f.Grants.Count);
    }

    [TestMethod]
    public void Handle_NotAReleaseByChoice_NeverReadsTheParty()
    {
        _f.Heroes.ReadRelease("lord").Returns(Release(Free, Free));
        _f.Heroes.ReadReleaseSource(null).Returns(FromMain);

        _behavior.Handle("lord", false, null);

        Assert.AreEqual(0, _f.Grants.Count);
        _f.Heroes.DidNotReceive().ReadReleaseSource(Arg.Any<TaleWorlds.CampaignSystem.Party.PartyBase?>());
    }

    [TestMethod]
    public void Handle_AFaultInTheService_IsLoggedAndNeverEscapes()
    {
        _f.Settings.Get().Returns(_ => throw new InvalidOperationException("boom"));
        _f.Heroes.ReadReleaseSource(null).Returns(FromMain);

        _behavior.Handle("lord", true, null);

        _f.Logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("[GoodDeeds]") && s.Contains("boom")));
    }

    [TestMethod]
    public void Handle_AFaultReadingTheParty_IsLoggedAndNeverEscapes()
    {
        _f.Heroes.ReadReleaseSource(null).Returns(_ => throw new InvalidOperationException("no party"));

        _behavior.Handle("lord", true, null);

        _f.Logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("[GoodDeeds]") && s.Contains("no party")));
    }

    [TestMethod]
    public void OnHeroPrisonerReleased_NoPrisonerAndAReleaseByChoice_ReadsThePartyOnceAndGrantsNothing()
    {
        _behavior.OnHeroPrisonerReleased(null, null, null, EndCaptivityDetail.ReleasedByChoice, true);

        _f.Heroes.Received(1).ReadReleaseSource(null);
        _f.Heroes.DidNotReceive().ReadRelease(Arg.Any<string>());
        _f.Logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow(EndCaptivityDetail.ReleasedAfterBattle)]
    [DataRow(EndCaptivityDetail.Ransom)]
    [DataRow(EndCaptivityDetail.ReleasedAfterPeace)]
    [DataRow(EndCaptivityDetail.ReleasedAfterEscape)]
    [DataRow(EndCaptivityDetail.ReleasedByCompensation)]
    [DataRow(EndCaptivityDetail.Death)]
    public void OnHeroPrisonerReleased_AnyOtherDetail_NeverReadsTheParty(EndCaptivityDetail detail)
    {
        _behavior.OnHeroPrisonerReleased(null, null, null, detail, true);

        _f.Heroes.DidNotReceive().ReadReleaseSource(Arg.Any<TaleWorlds.CampaignSystem.Party.PartyBase?>());
    }

    // --- the tournament listener ---------------------------------------------------------------------------------------

    private void Town() =>
        _f.Heroes.ReadTournament("town_1").Returns(new TournamentFacts("Minas Tirith", Free, false, Hero("owner"), Free,
            new[] { new DeedNotable(Hero("n1"), Free) }));

    [TestMethod]
    public void HandleTournament_ThePlayerWon_ReachesTheServiceWithTheTownId()
    {
        Town();

        _behavior.HandleTournament(true, "town_1");

        Assert.AreEqual(2, _f.Grants.Count);
        _f.Heroes.Received(1).ReadTournament("town_1");
        _f.Notice.Received(1).ShowTournamentHonor("Minas Tirith");
    }

    [TestMethod]
    public void HandleTournament_ThePlayerDidNotWin_GrantsNothing()
    {
        Town();

        _behavior.HandleTournament(false, "town_1");

        Assert.AreEqual(0, _f.Grants.Count);
        _f.Heroes.DidNotReceive().ReadTournament(Arg.Any<string>());
    }

    [TestMethod]
    public void HandleTournament_AFaultInTheService_IsLoggedAndNeverEscapes()
    {
        _f.Settings.Get().Returns(_ => throw new InvalidOperationException("boom"));

        _behavior.HandleTournament(true, "town_1");

        _f.Logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("[GoodDeeds]") && s.Contains("tournament") && s.Contains("boom")));
    }

    [TestMethod]
    public void OnTournamentFinished_NoWinnerAndNoTown_GrantsNothingAndNeverFaults()
    {
        _behavior.OnTournamentFinished(null, null, null, null);

        Assert.AreEqual(0, _f.Grants.Count);
        _f.Heroes.DidNotReceive().ReadTournament(Arg.Any<string>());
        _f.Logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void OnTournamentFinished_PassesWhetherThePlayerWonAndTheTownsSettlementId()
    {
        // The listener's arguments come from engine types that cannot be built outside the game, so the expressions are pinned
        // from the source: a winner that is the player's character, and the town's settlement id.
        var src = RepoPaths.ReadSource("Main/Features/GoodDeeds/Hooks/GoodDeedsBehavior.cs", stripComments: true);

        Assert.IsTrue(Regex.IsMatch(src, @"HandleTournament\(\s*winner\s*!=\s*null\s*&&\s*winner\s*==\s*CharacterObject\.PlayerCharacter\s*,\s*town\?\.Settlement\?\.StringId\s*\)"),
            "the listener must pass (winner is the player's character, the town's settlement id)");
    }
}
