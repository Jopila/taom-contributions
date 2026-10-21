using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>#833: what deserting during the minimum term costs.</summary>
[TestClass]
public class DesertionPenaltyServiceTests
{
    private ICommanderLordAdapter _commander = null!;
    private IPlayerTraitAdapter _traits = null!;
    private IModLogger _logger = null!;
    private EnlistmentContentConfig _config = null!;
    private DesertionPenaltyService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _commander = Substitute.For<ICommanderLordAdapter>();
        _commander.ApplyPlayerRelation(Arg.Any<string>(), Arg.Any<int>()).Returns(true);
        _traits = Substitute.For<IPlayerTraitAdapter>();
        _traits.AddTraitXp(Arg.Any<string>(), Arg.Any<int>()).Returns(true);
        _logger = Substitute.For<IModLogger>();
        _config = EnlistmentContentConfigProvider.BuildDefaults();
        var provider = Substitute.For<IEnlistmentContentConfigProvider>();
        provider.GetConfig().Returns(_ => _config);
        _sut = new DesertionPenaltyService(provider, _commander, _traits, _logger);
    }

    [TestMethod]
    public void Apply_LivingCommander_CostsRelationAndHonor()
    {
        _sut.Apply("lord_1_1", commanderAlive: true);

        _commander.Received(1).ApplyPlayerRelation("lord_1_1", -15);
        _traits.Received(1).AddTraitXp("Honor", -100);
    }

    [TestMethod]
    public void Apply_DeadCommander_SkipsBoth()
    {
        _sut.Apply("lord_1_1", commanderAlive: false);

        _commander.DidNotReceiveWithAnyArgs().ApplyPlayerRelation(default!, default);
        _traits.DidNotReceiveWithAnyArgs().AddTraitXp(default!, default);
    }

    [TestMethod]
    public void Apply_NoCommanderId_SkipsBoth()
    {
        _sut.Apply("", commanderAlive: true);

        _commander.DidNotReceiveWithAnyArgs().ApplyPlayerRelation(default!, default);
        _traits.DidNotReceiveWithAnyArgs().AddTraitXp(default!, default);
    }

    [TestMethod]
    public void Apply_ZeroConfigValue_SkipsThatPartOnly()
    {
        _config.DesertionRelationPenalty = 0;

        _sut.Apply("lord_1_1", commanderAlive: true);

        _commander.DidNotReceiveWithAnyArgs().ApplyPlayerRelation(default!, default);
        _traits.Received(1).AddTraitXp("Honor", -100);
    }

    [TestMethod]
    public void Apply_ZeroHonorValue_StillCostsRelation()
    {
        _config.DesertionHonorXpPenalty = 0;

        _sut.Apply("lord_1_1", commanderAlive: true);

        _commander.Received(1).ApplyPlayerRelation("lord_1_1", -15);
        _traits.DidNotReceiveWithAnyArgs().AddTraitXp(default!, default);
    }

    [TestMethod]
    public void Apply_RelationRefused_StillCostsHonorAndLogsQuietly()
    {
        _commander.ApplyPlayerRelation(Arg.Any<string>(), Arg.Any<int>()).Returns(false);

        _sut.Apply("lord_1_1", commanderAlive: true);

        _traits.Received(1).AddTraitXp("Honor", -100);
        _logger.DidNotReceiveWithAnyArgs().LogError(default!);
    }
}
