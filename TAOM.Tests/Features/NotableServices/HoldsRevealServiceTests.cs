using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.NotableServices;
using TAOM.Features.NotableServices.Domain;

namespace TAOM.Tests.Features.NotableServices;

[TestClass]
public sealed class HoldsRevealServiceTests
{
    private const string Settlement = "town_1";

    private readonly NotableServicesConfig _config = new();
    private readonly INotableServicesConfigProvider _configProvider = Substitute.For<INotableServicesConfigProvider>();
    private readonly IMapRevealAdapter _reveal = Substitute.For<IMapRevealAdapter>();
    private readonly IModLogger _logger = Substitute.For<IModLogger>();
    private readonly HoldsRevealService _sut;

    public HoldsRevealServiceTests()
    {
        _configProvider.GetConfig().Returns(_config);
        _reveal.MapDiagonal.Returns(1000f);
        _reveal.Track(Arg.Any<IReadOnlyList<string>>()).Returns(call => call.Arg<IReadOnlyList<string>>().Count);
        _sut = new HoldsRevealService(_reveal, _configProvider, _logger);
    }

    private void Around(params RevealCandidate[] candidates) =>
        _reveal.FortificationsAround(Settlement).Returns(candidates);

    private static RevealAsk Ask(bool atWar = false, float relation = 0f) => new(Settlement, atWar, relation);

    [TestMethod]
    public void Reveal_AtWar_IsAtWar_WithNoPaymentAndNoTrack()
    {
        Around(new RevealCandidate("a", 10f, false));
        var paid = 0;

        var outcome = _sut.Reveal(Ask(atWar: true), _ => { paid++; return true; });

        Assert.AreEqual(RevealOutcome.AtWar, outcome);
        Assert.AreEqual(0, paid);
        _reveal.DidNotReceiveWithAnyArgs().Track(default!);
    }

    [TestMethod]
    public void Reveal_WarWinsOverACoolRelation()
    {
        _config.RevealMinRelation = 50f;

        Assert.AreEqual(RevealOutcome.AtWar, _sut.Reveal(Ask(atWar: true, relation: -10f), _ => true));
    }

    [TestMethod]
    public void Reveal_BelowTheMinimumRelation_IsCold_WithNoPaymentAndNoTrack()
    {
        Around(new RevealCandidate("a", 10f, false));
        _config.RevealMinRelation = 5f;
        var paid = 0;

        var outcome = _sut.Reveal(Ask(relation: 4f), _ => { paid++; return true; });

        Assert.AreEqual(RevealOutcome.Cold, outcome);
        Assert.AreEqual(0, paid);
        _reveal.DidNotReceiveWithAnyArgs().Track(default!);
    }

    [TestMethod]
    public void Reveal_ARelationThatIsNotANumber_PassesTheColdCheck_ByDesign()
    {
        // NaN compares false against the minimum, so it is never cold. The engine reports whole-number relations, so this is
        // unreachable in play; the test pins the outcome so a change to it is a decision, not a drift.
        Around(new RevealCandidate("a", 10f, false));
        _config.RevealMinRelation = 5f;

        Assert.AreEqual(RevealOutcome.Revealed, _sut.Reveal(Ask(relation: float.NaN), _ => true));
    }

    [TestMethod]
    public void Reveal_ACandidateWithADistanceThatIsNotANumber_IsLeftOut()
    {
        Around(new RevealCandidate("nan", float.NaN, false), new RevealCandidate("a", 40f, false));

        _sut.Reveal(Ask(), _ => true);

        _reveal.Received(1).Track(Arg.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "a" })));
    }

    [TestMethod]
    public void Reveal_WhenEveryDistanceIsNotANumber_IsNothingNew()
    {
        Around(new RevealCandidate("nan", float.NaN, false));

        Assert.AreEqual(RevealOutcome.NothingNew, _sut.Reveal(Ask(), _ => true));
    }

    [TestMethod]
    public void Reveal_AtExactlyTheMinimumRelation_IsRevealed()
    {
        Around(new RevealCandidate("a", 10f, false));
        _config.RevealMinRelation = 5f;

        Assert.AreEqual(RevealOutcome.Revealed, _sut.Reveal(Ask(relation: 5f), _ => true));
    }

    [TestMethod]
    public void Reveal_NothingNew_IsNothingNew_WithNoPayment()
    {
        Around(new RevealCandidate("a", 10f, true), new RevealCandidate("b", 900f, false));
        var paid = 0;

        var outcome = _sut.Reveal(Ask(), _ => { paid++; return true; });

        Assert.AreEqual(RevealOutcome.NothingNew, outcome);
        Assert.AreEqual(0, paid);
        _reveal.DidNotReceiveWithAnyArgs().Track(default!);
    }

    [TestMethod]
    public void Reveal_NoCandidates_IsNothingNew()
    {
        Around();

        Assert.AreEqual(RevealOutcome.NothingNew, _sut.Reveal(Ask(), _ => true));
    }

    [TestMethod]
    public void Reveal_PicksTheNearestUntrackedWithinTheBound_NearestFirst()
    {
        // Bound: 0.2 x 1000 = 200. Count: 3.
        Around(
            new RevealCandidate("far", 250f, false),
            new RevealCandidate("tracked", 5f, true),
            new RevealCandidate("c", 150f, false),
            new RevealCandidate("a", 40f, false),
            new RevealCandidate("edge", 200f, false),
            new RevealCandidate("b", 90f, false));

        Assert.AreEqual(RevealOutcome.Revealed, _sut.Reveal(Ask(), _ => true));

        _reveal.Received(1).Track(Arg.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "a", "b", "c" })));
    }

    [TestMethod]
    public void Reveal_TheCountAndTheBound_FollowTheConfig()
    {
        Around(new RevealCandidate("a", 40f, false), new RevealCandidate("b", 90f, false), new RevealCandidate("c", 150f, false));
        _config.RevealCount = 1.6f;
        _config.RevealMaxDiagonalFraction = 0.1f;

        _sut.Reveal(Ask(), _ => true);

        _reveal.Received(1).Track(Arg.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "a", "b" })));
    }

    [TestMethod]
    public void Reveal_AsksThePriceFromTheConfig_RoundedAwayFromZero()
    {
        Around(new RevealCandidate("a", 40f, false));
        _config.RevealPrice = 99.5f;
        var asked = new List<int>();

        _sut.Reveal(Ask(), price => { asked.Add(price); return true; });

        CollectionAssert.AreEqual(new[] { 100 }, asked);
    }

    [TestMethod]
    public void Reveal_PaysBeforeItTracks_ThenShowsTheCount()
    {
        Around(new RevealCandidate("a", 40f, false), new RevealCandidate("b", 90f, false));
        var order = new List<string>();
        _reveal.When(r => r.Track(Arg.Any<IReadOnlyList<string>>())).Do(_ => order.Add("track"));
        _reveal.When(r => r.ShowHoldsMarkedNotice(Arg.Any<int>())).Do(_ => order.Add("notice"));

        _sut.Reveal(Ask(), _ => { order.Add("pay"); return true; });

        CollectionAssert.AreEqual(new[] { "pay", "track", "notice" }, order);
        _reveal.Received(1).ShowHoldsMarkedNotice(2);
    }

    [TestMethod]
    public void Reveal_AnUnpaidPrice_IsUnavailable_AndTracksNothing()
    {
        Around(new RevealCandidate("a", 40f, false));

        Assert.AreEqual(RevealOutcome.Unavailable, _sut.Reveal(Ask(), _ => false));

        _reveal.DidNotReceiveWithAnyArgs().Track(default!);
        _reveal.DidNotReceiveWithAnyArgs().ShowHoldsMarkedNotice(default);
    }

    [TestMethod]
    public void Reveal_AFreeReveal_NeverCallsThePayment_AndTracks()
    {
        Around(new RevealCandidate("a", 40f, false));
        _config.RevealPrice = 0f;
        var paid = 0;

        var outcome = _sut.Reveal(Ask(), _ => { paid++; return false; });

        Assert.AreEqual(RevealOutcome.Revealed, outcome);
        Assert.AreEqual(0, paid);
        _reveal.Received(1).Track(Arg.Any<IReadOnlyList<string>>());
    }

    [TestMethod]
    public void Reveal_NothingMarkedByTheEngine_ShowsNoNotice()
    {
        Around(new RevealCandidate("a", 40f, false));
        _reveal.Track(Arg.Any<IReadOnlyList<string>>()).Returns(0);

        Assert.AreEqual(RevealOutcome.Revealed, _sut.Reveal(Ask(), _ => true));

        _reveal.DidNotReceiveWithAnyArgs().ShowHoldsMarkedNotice(default);
    }

    [TestMethod]
    public void Reveal_AnEngineFault_IsLogged_AndAnswersUnavailable()
    {
        Around(new RevealCandidate("a", 40f, false));
        _reveal.Track(Arg.Any<IReadOnlyList<string>>()).Returns(_ => throw new InvalidOperationException("boom"));

        Assert.AreEqual(RevealOutcome.Unavailable, _sut.Reveal(Ask(), _ => true));

        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("boom") && s.Contains(Settlement)));
    }

    [TestMethod]
    public void Price_IsTheRoundedConfigValue()
    {
        _config.RevealPrice = 99.6f;

        Assert.AreEqual(100, _sut.Price);
    }
}
