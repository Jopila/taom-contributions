using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.NamedCompanions;
using TAOM.Features.NamedCompanions.Domain;
using TAOM.Features.NamedCompanions.Oaths;

namespace TAOM.Tests.Features.NamedCompanions.Oaths;

[TestClass]
public class HireTermsServiceTests
{
    private const string Id = "named_companion_gimli";

    private INamedCompanionConfigProvider _config = null!;
    private IOathFlagStore _flags = null!;
    private INamedCompanionAdapter _adapter = null!;
    private IPartyItemRosterAdapter _roster = null!;
    private ICareerPurseAdapter _purse = null!;
    private IModLogger _logger = null!;
    private NamedCompanionDefinition _companion = null!;
    private HireTermsService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _config = Substitute.For<INamedCompanionConfigProvider>();
        _flags = Substitute.For<IOathFlagStore>();
        _adapter = Substitute.For<INamedCompanionAdapter>();
        _roster = Substitute.For<IPartyItemRosterAdapter>();
        _purse = Substitute.For<ICareerPurseAdapter>();
        _logger = Substitute.For<IModLogger>();
        _companion = new NamedCompanionDefinition { CharacterId = Id, Enabled = true };
        _config.GetCompanions().Returns(new List<NamedCompanionDefinition> { _companion });
        _purse.Gold.Returns(10_000);
        _adapter.HireIntoPlayerParty(Id, Arg.Any<int>()).Returns(true);
        _sut = new HireTermsService(_config, _flags, _adapter, _roster, _purse, _logger);
    }

    private static HireTermsDefinition Terms(int? gold = 3000, params (string item, int count)[] items) => new()
    {
        Gold = gold,
        Items = items.Select(i => new ItemCostDefinition { ItemId = i.item, Count = i.count }).ToList(),
        RequiresFlags = new List<string>(),
    };

    [TestMethod]
    public void Evaluate_NoTerms_HasNoTermsAndStatusNone()
    {
        Assert.IsNull(_sut.Evaluate(Id).Terms);
        Assert.AreEqual(HireStatus.None, _sut.Evaluate(Id).Status);
    }

    [TestMethod]
    public void Evaluate_UnknownCompanion_HasNoTerms()
    {
        _companion.HireTerms = Terms();

        Assert.IsNull(_sut.Evaluate("someone_else").Terms);
        Assert.AreEqual(HireStatus.None, _sut.Evaluate("someone_else").Status);
    }

    [TestMethod]
    public void Evaluate_DisabledCompanion_HasNoTerms()
    {
        _companion.HireTerms = Terms();
        _companion.Enabled = false;

        Assert.IsNull(_sut.Evaluate(Id).Terms);
        Assert.AreEqual(HireStatus.None, _sut.Evaluate(Id).Status);
    }

    [TestMethod]
    public void Evaluate_CarriesTheDefinition()
    {
        _companion.HireTerms = Terms();

        Assert.AreSame(_companion.HireTerms, _sut.Evaluate(Id).Terms);
    }

    [TestMethod]
    public void NoTerms_IsAFreshInstanceEachCall()
    {
        var first = HireEvaluation.NoTerms;
        first.Missing.Add(new MissingTerm { Kind = MissingTermKind.Gold, Count = 1 });

        Assert.AreEqual(0, HireEvaluation.NoTerms.Missing.Count);
        Assert.AreNotSame(HireEvaluation.NoTerms, HireEvaluation.NoTerms);
    }

    [TestMethod]
    public void Evaluate_UnsetRequiredFlag_IsLocked()
    {
        _companion.HireTerms = Terms();
        _companion.HireTerms.RequiresFlags.Add("oath.sworn");
        _flags.Has("oath.sworn").Returns(false);

        Assert.AreEqual(HireStatus.Locked, _sut.Evaluate(Id).Status);
    }

    [TestMethod]
    public void Evaluate_OneOfTwoFlagsUnset_IsLocked()
    {
        _companion.HireTerms = Terms();
        _companion.HireTerms.RequiresFlags.AddRange(new[] { "a", "b" });
        _flags.Has("a").Returns(true);
        _flags.Has("b").Returns(false);

        Assert.AreEqual(HireStatus.Locked, _sut.Evaluate(Id).Status);
    }

    [TestMethod]
    public void Evaluate_GoldShort_ReportsTheShortfall()
    {
        _companion.HireTerms = Terms(3000);
        _purse.Gold.Returns(2500);

        var result = _sut.Evaluate(Id);

        Assert.AreEqual(HireStatus.Missing, result.Status);
        Assert.AreEqual(3000, result.Price);
        Assert.AreEqual(1, result.Missing.Count);
        Assert.AreEqual(MissingTermKind.Gold, result.Missing[0].Kind);
        Assert.AreEqual(500, result.Missing[0].Count);
    }

    [TestMethod]
    public void Evaluate_ExactGold_IsMet()
    {
        _companion.HireTerms = Terms(3000);
        _purse.Gold.Returns(3000);

        Assert.AreEqual(HireStatus.Met, _sut.Evaluate(Id).Status);
    }

    [TestMethod]
    public void Evaluate_EachItemShort_IsListed()
    {
        _companion.HireTerms = Terms(0, ("mithril", 3), ("ale", 10));
        _roster.GetItemCount("mithril").Returns(1);
        _roster.GetItemCount("ale").Returns(4);

        var result = _sut.Evaluate(Id);

        Assert.AreEqual(HireStatus.Missing, result.Status);
        Assert.AreEqual(2, result.Missing.Count);
        Assert.IsTrue(result.Missing.Any(m => m.Kind == MissingTermKind.Item && m.ItemId == "mithril" && m.Count == 2));
        Assert.IsTrue(result.Missing.Any(m => m.Kind == MissingTermKind.Item && m.ItemId == "ale" && m.Count == 6));
    }

    [TestMethod]
    public void Evaluate_OneItemShortOneEnough_ListsOnlyTheShortOne()
    {
        _companion.HireTerms = Terms(0, ("mithril", 3), ("ale", 10));
        _roster.GetItemCount("mithril").Returns(3);
        _roster.GetItemCount("ale").Returns(9);

        var result = _sut.Evaluate(Id);

        Assert.AreEqual(1, result.Missing.Count);
        Assert.AreEqual("ale", result.Missing[0].ItemId);
        Assert.AreEqual(1, result.Missing[0].Count);
    }

    [TestMethod]
    public void Evaluate_GoldAndItemShort_ListsBoth()
    {
        _companion.HireTerms = Terms(3000, ("mithril", 1));
        _purse.Gold.Returns(0);

        Assert.AreEqual(2, _sut.Evaluate(Id).Missing.Count);
    }

    [TestMethod]
    public void Evaluate_AllMet_IsMetWithNoMissing()
    {
        _companion.HireTerms = Terms(3000, ("mithril", 3));
        _roster.GetItemCount("mithril").Returns(3);

        var result = _sut.Evaluate(Id);

        Assert.AreEqual(HireStatus.Met, result.Status);
        Assert.AreEqual(0, result.Missing.Count);
    }

    [TestMethod]
    public void Evaluate_NullGold_UsesTheVanillaPrice()
    {
        _companion.HireTerms = Terms(null, ("mithril", 1));
        _adapter.GetVanillaHiringPrice(Id).Returns(1234);
        _roster.GetItemCount("mithril").Returns(1);
        _purse.Gold.Returns(1000);

        var result = _sut.Evaluate(Id);

        Assert.AreEqual(1234, result.Price);
        Assert.AreEqual(HireStatus.Missing, result.Status);
        Assert.AreEqual(234, result.Missing.Single().Count);
    }

    [TestMethod]
    public void Evaluate_ExplicitGold_NeverAsksVanilla()
    {
        _companion.HireTerms = Terms(3000);

        _sut.Evaluate(Id);

        _adapter.DidNotReceive().GetVanillaHiringPrice(Arg.Any<string>());
    }

    [TestMethod]
    public void TryPayAndHire_Met_RemovesItemsThenHires()
    {
        _companion.HireTerms = Terms(3000, ("mithril", 3), ("ale", 2));
        _roster.GetItemCount(Arg.Any<string>()).Returns(50);
        _roster.RemoveItem("mithril", 3).Returns(3);
        _roster.RemoveItem("ale", 2).Returns(2);

        Assert.IsTrue(_sut.TryPayAndHire(Id, isCoopClient: false));

        Received.InOrder(() =>
        {
            _roster.RemoveItem("mithril", 3);
            _roster.RemoveItem("ale", 2);
            _adapter.HireIntoPlayerParty(Id, 3000);
        });
        _roster.DidNotReceive().AddItem(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void TryPayAndHire_NotMet_ChangesNothing()
    {
        _companion.HireTerms = Terms(3000, ("mithril", 3));
        _roster.GetItemCount("mithril").Returns(1);

        Assert.IsFalse(_sut.TryPayAndHire(Id, isCoopClient: false));

        _roster.DidNotReceive().RemoveItem(Arg.Any<string>(), Arg.Any<int>());
        _adapter.DidNotReceive().HireIntoPlayerParty(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void TryPayAndHire_Locked_ChangesNothing()
    {
        _companion.HireTerms = Terms();
        _companion.HireTerms.RequiresFlags.Add("oath.sworn");

        Assert.IsFalse(_sut.TryPayAndHire(Id, isCoopClient: false));

        _adapter.DidNotReceive().HireIntoPlayerParty(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void TryPayAndHire_HireReturnsFalse_RefundsTheItems()
    {
        _companion.HireTerms = Terms(3000, ("mithril", 3), ("ale", 2));
        _roster.GetItemCount(Arg.Any<string>()).Returns(50);
        _roster.RemoveItem("mithril", 3).Returns(3);
        _roster.RemoveItem("ale", 2).Returns(2);
        _adapter.HireIntoPlayerParty(Id, 3000).Returns(false);

        Assert.IsFalse(_sut.TryPayAndHire(Id, isCoopClient: false));

        _roster.Received(1).AddItem("mithril", 3);
        _roster.Received(1).AddItem("ale", 2);
    }

    [TestMethod]
    public void TryPayAndHire_HireThrows_RefundsLogsAndReturnsFalse()
    {
        _companion.HireTerms = Terms(3000, ("mithril", 3));
        _roster.GetItemCount("mithril").Returns(3);
        _roster.RemoveItem("mithril", 3).Returns(3);
        _adapter.HireIntoPlayerParty(Id, 3000).Returns(_ => throw new InvalidOperationException("boom"));

        Assert.IsFalse(_sut.TryPayAndHire(Id, isCoopClient: false));

        _roster.Received(1).AddItem("mithril", 3);
        _logger.Received().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void TryPayAndHire_HireThrowsAfterTheCompanionJoined_KeepsTheItems()
    {
        _companion.HireTerms = Terms(3000, ("mithril", 3));
        _roster.GetItemCount("mithril").Returns(3);
        _roster.RemoveItem("mithril", 3).Returns(3);
        _adapter.HireIntoPlayerParty(Id, 3000).Returns(_ => throw new InvalidOperationException("boom"));
        _adapter.IsRecruitedOrInParty(Id).Returns(true);

        Assert.IsTrue(_sut.TryPayAndHire(Id, isCoopClient: false), "the companion is in the party, so the hire stands");

        _roster.DidNotReceive().AddItem(Arg.Any<string>(), Arg.Any<int>());
        _logger.Received().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void TryPayAndHire_RemoveFallsShort_RefundsWhatWasTakenAndDoesNotHire()
    {
        _companion.HireTerms = Terms(3000, ("mithril", 3), ("ale", 2));
        _roster.GetItemCount(Arg.Any<string>()).Returns(50);
        _roster.RemoveItem("mithril", 3).Returns(3);
        _roster.RemoveItem("ale", 2).Returns(1);

        Assert.IsFalse(_sut.TryPayAndHire(Id, isCoopClient: false));

        _roster.Received(1).AddItem("mithril", 3);
        _roster.Received(1).AddItem("ale", 1);
        _adapter.DidNotReceive().HireIntoPlayerParty(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void TryPayAndHire_OnCoopClient_NeverPays()
    {
        _companion.HireTerms = Terms(3000, ("mithril", 3));
        _roster.GetItemCount("mithril").Returns(3);

        Assert.IsFalse(_sut.TryPayAndHire(Id, isCoopClient: true));

        _roster.DidNotReceive().RemoveItem(Arg.Any<string>(), Arg.Any<int>());
        _adapter.DidNotReceive().HireIntoPlayerParty(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void TryPayAndHire_GoldOnly_HiresWithoutTouchingTheRoster()
    {
        _companion.HireTerms = Terms(3000);

        Assert.IsTrue(_sut.TryPayAndHire(Id, isCoopClient: false));

        _roster.DidNotReceive().RemoveItem(Arg.Any<string>(), Arg.Any<int>());
        _adapter.Received(1).HireIntoPlayerParty(Id, 3000);
    }
}
