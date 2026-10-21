using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.NamedCompanions;
using TAOM.Features.NamedCompanions.Domain;
using TAOM.Features.NamedCompanions.Oaths;

namespace TAOM.Tests.Features.NamedCompanions.Oaths;

/// <summary>Entity State Matrix for the daily home anchor: one row per state that must NOT be moved, and the one that is.</summary>
[TestClass]
public class HomeAnchorServiceTests
{
    private const string Id = "named_companion_gimli";
    private const string Home = "town_E1";
    private const string Elsewhere = "town_X9";

    private INamedCompanionConfigProvider _config = null!;
    private INamedCompanionAdapter _adapter = null!;
    private ICoopSessionProvider _coop = null!;
    private IModLogger _logger = null!;
    private NamedCompanionDefinition _companion = null!;
    private HomeAnchorService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _config = Substitute.For<INamedCompanionConfigProvider>();
        _adapter = Substitute.For<INamedCompanionAdapter>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _logger = Substitute.For<IModLogger>();
        _companion = new NamedCompanionDefinition
        {
            CharacterId = Id, SpawnSettlement = Home, Enabled = true, StayAtHome = true,
        };
        _config.GetCompanions().Returns(new List<NamedCompanionDefinition> { _companion });
        _coop.IsAuthority.Returns(true);
        // The movable state: Active, free, wandering in a settlement that is not home.
        _adapter.IsHeroActive(Id).Returns(true);
        _adapter.GetCurrentSettlementId(Id).Returns(Elsewhere);
        _sut = new HomeAnchorService(_config, _adapter, _coop, _logger);
    }

    [TestMethod]
    public void AnchorAll_MovableCompanion_ReturnsHome()
    {
        Assert.AreEqual(1, _sut.AnchorAll());

        _adapter.Received(1).ReturnHome(Id, Home);
    }

    [TestMethod]
    public void AnchorAll_NoCurrentSettlement_ReturnsHome()
    {
        _adapter.GetCurrentSettlementId(Id).Returns((string)null);

        Assert.AreEqual(1, _sut.AnchorAll());

        _adapter.Received(1).ReturnHome(Id, Home);
    }

    [TestMethod]
    public void AnchorAll_NotAuthority_DoesNothing()
    {
        _coop.IsAuthority.Returns(false);

        Assert.AreEqual(0, _sut.AnchorAll());

        _adapter.DidNotReceive().ReturnHome(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void AnchorAll_NotStayAtHome_Skipped()
    {
        _companion.StayAtHome = false;

        AssertSkipped();
    }

    [TestMethod]
    public void AnchorAll_Disabled_Skipped()
    {
        _companion.Enabled = false;

        AssertSkipped();
    }

    [TestMethod]
    public void AnchorAll_NotActive_CoversMissingDeadPrisonerAndFugitive_Skipped()
    {
        _adapter.IsHeroActive(Id).Returns(false);

        AssertSkipped();
    }

    [TestMethod]
    public void AnchorAll_RecruitedOrInParty_Skipped()
    {
        _adapter.IsRecruitedOrInParty(Id).Returns(true);

        AssertSkipped();
    }

    [TestMethod]
    public void AnchorAll_AlreadyHome_ReadsNothingElse()
    {
        _adapter.GetCurrentSettlementId(Id).Returns(Home);

        AssertSkipped();
        _adapter.DidNotReceive().IsHeroActive(Arg.Any<string>());
        _adapter.DidNotReceive().IsPlayerInSettlement(Arg.Any<string>());
    }

    [TestMethod]
    public void AnchorAll_AlreadyHome_Skipped()
    {
        _adapter.GetCurrentSettlementId(Id).Returns(Home);

        AssertSkipped();
    }

    [TestMethod]
    public void AnchorAll_PlayerInTheCompanionsCurrentSettlement_Skipped()
    {
        _adapter.IsPlayerInSettlement(Elsewhere).Returns(true);

        AssertSkipped();
    }

    [TestMethod]
    public void AnchorAll_PlayerInTheHomeSettlement_Skipped()
    {
        _adapter.IsPlayerInSettlement(Home).Returns(true);

        AssertSkipped();
    }

    [TestMethod]
    public void AnchorAll_MoveThrows_LogsAndKeepsGoing()
    {
        var second = new NamedCompanionDefinition { CharacterId = "other", SpawnSettlement = Home, Enabled = true, StayAtHome = true };
        _config.GetCompanions().Returns(new List<NamedCompanionDefinition> { _companion, second });
        _adapter.IsHeroActive("other").Returns(true);
        _adapter.When(a => a.ReturnHome(Id, Home)).Do(_ => throw new InvalidOperationException("boom"));

        Assert.AreEqual(1, _sut.AnchorAll());

        _logger.Received().LogError(Arg.Any<string>());
        _adapter.Received(1).ReturnHome("other", Home);
    }

    [TestMethod]
    public void AnchorAll_ReadThrows_SkipsOnlyThatCompanion()
    {
        var second = new NamedCompanionDefinition { CharacterId = "other", SpawnSettlement = Home, Enabled = true, StayAtHome = true };
        _config.GetCompanions().Returns(new List<NamedCompanionDefinition> { _companion, second });
        _adapter.IsHeroActive("other").Returns(true);
        _adapter.GetCurrentSettlementId(Id).Returns(_ => throw new InvalidOperationException("boom"));

        Assert.AreEqual(1, _sut.AnchorAll());

        _logger.Received().LogError(Arg.Any<string>());
        _adapter.Received(1).ReturnHome("other", Home);
    }

    [TestMethod]
    public void AnchorAll_HomeSettlementSet_TargetsItInsteadOfSpawn()
    {
        _companion.HomeSettlement = "town_HOME2";

        Assert.AreEqual(1, _sut.AnchorAll());

        _adapter.Received(1).ReturnHome(Id, "town_HOME2");
        _adapter.DidNotReceive().ReturnHome(Id, Home);
    }

    [TestMethod]
    public void AnchorAll_HomeSettlementSet_AlreadyThere_Skipped()
    {
        _companion.HomeSettlement = "town_HOME2";
        _adapter.GetCurrentSettlementId(Id).Returns("town_HOME2");

        AssertSkipped();
    }

    [TestMethod]
    public void AnchorAll_HomeSettlementSet_PlayerInThatHome_Skipped()
    {
        _companion.HomeSettlement = "town_HOME2";
        _adapter.IsPlayerInSettlement("town_HOME2").Returns(true);

        AssertSkipped();
    }

    [TestMethod]
    public void AnchorAll_BlankHomeSettlement_FallsBackToSpawn()
    {
        _companion.HomeSettlement = "";

        Assert.AreEqual(1, _sut.AnchorAll());

        _adapter.Received(1).ReturnHome(Id, Home);
    }

    private void AssertSkipped()
    {
        Assert.AreEqual(0, _sut.AnchorAll());
        _adapter.DidNotReceive().ReturnHome(Arg.Any<string>(), Arg.Any<string>());
    }
}
