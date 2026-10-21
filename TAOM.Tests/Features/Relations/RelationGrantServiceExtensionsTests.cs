using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Relations;

namespace TAOM.Tests.Features.Relations;

/// <summary>The shared "strongest ask once per relation owner" rule, over a fake seam that records every grant in order.</summary>
[TestClass]
public class RelationGrantServiceExtensionsTests
{
    private readonly IRelationGrantService _grants = Substitute.For<IRelationGrantService>();
    private readonly List<(string HeroId, int Delta, string Reason)> _calls = new();

    [TestInitialize]
    public void Setup()
    {
        _grants.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(call =>
        {
            _calls.Add((call.ArgAt<string>(0), call.ArgAt<int>(1), call.ArgAt<string>(2)));
            return call.ArgAt<int>(1);
        });
    }

    private static RelationTarget Target(string id, string? owner = null, bool alive = true, bool isPlayer = false) =>
        new(id, owner ?? id, alive, isPlayer);

    [TestMethod]
    public void GrantStrongestPerOwner_TwoGainsToOneOwner_GrantsTheLargerOnly()
    {
        var landed = _grants.GrantStrongestPerOwner(new[]
        {
            new RelationAsk(Target("lord", "leader"), 2),
            new RelationAsk(Target("leader"), 5),
        }, "test");

        Assert.AreEqual(1, landed);
        CollectionAssert.AreEqual(new[] { ("leader", 5, "test") }, _calls);
    }

    [TestMethod]
    public void GrantStrongestPerOwner_TwoLossesToOneOwner_GrantsTheLargerLossOnly()
    {
        var landed = _grants.GrantStrongestPerOwner(new[]
        {
            new RelationAsk(Target("notable_a", "leader"), -12),
            new RelationAsk(Target("leader"), -40),
        }, "test");

        Assert.AreEqual(1, landed);
        CollectionAssert.AreEqual(new[] { ("leader", -40, "test") }, _calls);
    }

    [TestMethod]
    public void GrantStrongestPerOwner_AGainAndALossForOneOwner_TheLargerMagnitudeWins()
    {
        _grants.GrantStrongestPerOwner(new[]
        {
            new RelationAsk(Target("a", "owner"), 3),
            new RelationAsk(Target("b", "owner"), -7),
        }, "test");

        CollectionAssert.AreEqual(new[] { ("b", -7, "test") }, _calls);
    }

    [TestMethod]
    public void GrantStrongestPerOwner_ATieOfMagnitude_TheFirstListedWins()
    {
        _grants.GrantStrongestPerOwner(new[]
        {
            new RelationAsk(Target("first", "owner"), -4),
            new RelationAsk(Target("second", "owner"), 4),
            new RelationAsk(Target("third", "owner"), -4),
        }, "test");

        CollectionAssert.AreEqual(new[] { ("first", -4, "test") }, _calls);
    }

    [TestMethod]
    public void GrantStrongestPerOwner_SeveralOwners_GrantsInTheOrderEachOwnerFirstAppears()
    {
        _grants.GrantStrongestPerOwner(new[]
        {
            new RelationAsk(Target("b1", "owner_b"), 1),
            new RelationAsk(Target("a1", "owner_a"), 1),
            new RelationAsk(Target("b2", "owner_b"), 9),
            new RelationAsk(Target("c1", "owner_c"), 1),
        }, "test");

        CollectionAssert.AreEqual(new[] { ("b2", 9, "test"), ("a1", 1, "test"), ("c1", 1, "test") }, _calls);
    }

    [TestMethod]
    public void GrantStrongestPerOwner_ABlankOwner_FallsBackToTheHeroId()
    {
        var landed = _grants.GrantStrongestPerOwner(new[]
        {
            new RelationAsk(new RelationTarget("x", "", true, false), 1),
            new RelationAsk(new RelationTarget("y", "", true, false), 2),
            new RelationAsk(new RelationTarget("x2", "x", true, false), 3),
        }, "test");

        Assert.AreEqual(2, landed, "x and x2 share the owner x; y is its own owner");
        CollectionAssert.AreEqual(new[] { ("x2", 3, "test"), ("y", 2, "test") }, _calls);
    }

    [TestMethod]
    public void GrantStrongestPerOwner_OwnerIdsAreComparedOrdinally()
    {
        var landed = _grants.GrantStrongestPerOwner(new[]
        {
            new RelationAsk(Target("a", "Owner"), 1),
            new RelationAsk(Target("b", "owner"), 1),
        }, "test");

        Assert.AreEqual(2, landed);
    }

    [TestMethod]
    public void GrantStrongestPerOwner_ANullTarget_IsSkipped()
    {
        var landed = _grants.GrantStrongestPerOwner(new[] { new RelationAsk(null, 5), new RelationAsk(Target("a"), 1) }, "test");

        Assert.AreEqual(1, landed);
        CollectionAssert.AreEqual(new[] { ("a", 1, "test") }, _calls);
    }

    [TestMethod]
    public void GrantStrongestPerOwner_ADeadHero_IsSkipped()
    {
        var landed = _grants.GrantStrongestPerOwner(new[] { new RelationAsk(Target("dead", alive: false), 5) }, "test");

        Assert.AreEqual(0, landed);
        Assert.AreEqual(0, _calls.Count);
    }

    [TestMethod]
    public void GrantStrongestPerOwner_ADeadStrongerHero_DoesNotHideALivingWeakerOne()
    {
        _grants.GrantStrongestPerOwner(new[]
        {
            new RelationAsk(Target("dead", "owner", alive: false), 9),
            new RelationAsk(Target("alive", "owner"), 2),
        }, "test");

        CollectionAssert.AreEqual(new[] { ("alive", 2, "test") }, _calls);
    }

    [TestMethod]
    public void GrantStrongestPerOwner_ThePlayer_IsSkipped()
    {
        var landed = _grants.GrantStrongestPerOwner(new[] { new RelationAsk(Target("me", isPlayer: true), 5) }, "test");

        Assert.AreEqual(0, landed);
        Assert.AreEqual(0, _calls.Count);
    }

    [TestMethod]
    public void GrantStrongestPerOwner_AZeroDelta_IsSkipped()
    {
        var landed = _grants.GrantStrongestPerOwner(new[] { new RelationAsk(Target("a"), 0) }, "test");

        Assert.AreEqual(0, landed);
        Assert.AreEqual(0, _calls.Count);
    }

    [TestMethod]
    public void GrantStrongestPerOwner_AGrantThatReturnsZero_IsNotCounted()
    {
        _grants.Grant("blocked", Arg.Any<int>(), Arg.Any<string>()).Returns(0);

        var landed = _grants.GrantStrongestPerOwner(new[]
        {
            new RelationAsk(Target("blocked"), 3),
            new RelationAsk(Target("open"), 3),
        }, "test");

        Assert.AreEqual(1, landed);
    }

    [TestMethod]
    public void GrantStrongestPerOwner_IntMinValueAgainstAnythingElse_DoesNotThrowAndWins()
    {
        _grants.GrantStrongestPerOwner(new[]
        {
            new RelationAsk(Target("small", "owner"), int.MaxValue),
            new RelationAsk(Target("huge", "owner"), int.MinValue),
        }, "test");

        CollectionAssert.AreEqual(new[] { ("huge", int.MinValue, "test") }, _calls);
    }

    [TestMethod]
    public void GrantStrongestPerOwner_NoAsks_GrantsNothing()
    {
        Assert.AreEqual(0, _grants.GrantStrongestPerOwner(new List<RelationAsk>(), "test"));
        _grants.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
    }
}
