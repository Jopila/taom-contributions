using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Relations;

namespace TAOM.Tests.Features.Relations;

[TestClass]
public class RelationGrantServiceTests
{
    private IHeroRelationAdapter _relations = null!;
    private IDedicatedServerProvider _server = null!;
    private IModLogger _logger = null!;
    private RelationGrantService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _relations = Substitute.For<IHeroRelationAdapter>();
        _relations.ApplyPlayerRelation(Arg.Any<string>(), Arg.Any<int>()).Returns(call => call.ArgAt<int>(1));
        _server = Substitute.For<IDedicatedServerProvider>();
        _server.IsDedicatedServer.Returns(false);
        _logger = Substitute.For<IModLogger>();
        _sut = new RelationGrantService(_relations, _server, _logger);
    }

    [TestMethod]
    public void Grant_AValidRequest_ReturnsTheChangeTheAdapterReports()
    {
        _relations.ApplyPlayerRelation("lord_1", 3).Returns(5);

        var result = _sut.Grant("lord_1", 3, "test");

        Assert.AreEqual(5, result, "what landed, not what was asked");
        _relations.Received(1).ApplyPlayerRelation("lord_1", 3);
    }

    [TestMethod]
    public void Grant_ZeroDelta_IsSkippedWithoutTouchingTheAdapter()
    {
        var result = _sut.Grant("lord_1", 0, "test");

        Assert.AreEqual(0, result);
        _relations.DidNotReceiveWithAnyArgs().ApplyPlayerRelation(default!, default);
    }

    [DataTestMethod]
    [DataRow(500, 100)]
    [DataRow(101, 100)]
    [DataRow(-101, -100)]
    [DataRow(-500, -100)]
    [DataRow(int.MaxValue, 100)]
    [DataRow(int.MinValue, -100)]
    public void Grant_OutOfRangeDelta_IsClampedToPlusOrMinus100(int delta, int expected)
    {
        _sut.Grant("lord_1", delta, "test");

        _relations.Received(1).ApplyPlayerRelation("lord_1", expected);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Grant_BlankHeroId_IsANoOp(string? heroId)
    {
        var result = _sut.Grant(heroId!, 5, "test");

        Assert.AreEqual(0, result);
        _relations.DidNotReceiveWithAnyArgs().ApplyPlayerRelation(default!, default);
    }

    [TestMethod]
    public void Grant_UnknownOrDeadHero_ReturnsZero()
    {
        _relations.ApplyPlayerRelation("ghost", Arg.Any<int>()).Returns(0);

        Assert.AreEqual(0, _sut.Grant("ghost", 5, "test"));
    }

    [TestMethod]
    public void Grant_OnADedicatedServer_DoesNothing()
    {
        _server.IsDedicatedServer.Returns(true);

        var result = _sut.Grant("lord_1", 3, "test");

        Assert.AreEqual(0, result);
        _relations.DidNotReceiveWithAnyArgs().ApplyPlayerRelation(default!, default);
        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
    }

    [TestMethod]
    public void Grant_Applied_LogsTheReasonTheAskedAndTheLandedAmount()
    {
        _relations.ApplyPlayerRelation("lord_1", 4).Returns(6);

        _sut.Grant("lord_1", 4, "fought together");

        _logger.Received(1).LogInfo(Arg.Is<string>(s =>
            s.Contains("lord_1") && s.Contains("asked +4") && s.Contains("landed +6") && s.Contains("fought together")));
    }

    [TestMethod]
    public void Grant_AdapterAppliedNothing_ReturnsZeroAndLogsNothing()
    {
        _relations.ApplyPlayerRelation("ghost", Arg.Any<int>()).Returns(0);

        var result = _sut.Grant("ghost", 4, "fought together");

        Assert.AreEqual(0, result);
        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
    }
}
