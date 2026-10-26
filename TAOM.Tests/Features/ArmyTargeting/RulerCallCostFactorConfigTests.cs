using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.ArmyTargeting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.ArmyTargeting;

/// <summary>
/// The King's Muster factor (#874) in army_targeting.json: finite and from 0.25 to 1 inclusive, in
/// <c>ArmyTargetingConfigProvider.Validate</c> (csharp-architecture.md "Config Providers MUST Validate"). A rejected value reverts
/// to 0.5 alone, with a warning and the summary warning, and leaves the sibling fields alone.
/// </summary>
[TestClass]
public class RulerCallCostFactorConfigTests
{
    private IModLogger _logger = null!;
    private ArmyTargetingConfigProvider _sut = null!;

    [TestInitialize]
    public void SetUp()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = new ArmyTargetingConfigProvider(Substitute.For<IPathService>(), _logger);
    }

    [TestMethod]
    public void TheDefault_IsHalf()
    {
        Assert.AreEqual(0.5f, new ArmyTargetingConfig().RulerCallCostFactor, 0f);
    }

    [DataTestMethod]
    [DataRow(0.25f)]
    [DataRow(0.5f)]
    [DataRow(1f)]
    public void Validate_AFactorInRange_IsKept_WithNoWarning(float factor)
    {
        var result = _sut.Validate(new ArmyTargetingConfig { RulerCallCostFactor = factor });

        Assert.AreEqual(factor, result.RulerCallCostFactor, 0f);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(0f)]
    [DataRow(0.2499f)]
    [DataRow(-0.5f)]
    [DataRow(1.0001f)]
    [DataRow(1.5f)]
    public void Validate_AFactorOutOfRange_RevertsToTheDefault_WithWarnings(float bad)
    {
        var result = _sut.Validate(new ArmyTargetingConfig { RulerCallCostFactor = bad });

        Assert.AreEqual(0.5f, result.RulerCallCostFactor, 0f);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("RulerCallCostFactor")));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("contained invalid values")));
    }

    [TestMethod]
    public void Validate_ABadFactor_LeavesTheSiblingFieldsAlone()
    {
        var result = _sut.Validate(new ArmyTargetingConfig { RulerCallCostFactor = float.NaN, PrimaryTheaterWeight = 1.4f });

        Assert.AreEqual(1.4f, result.PrimaryTheaterWeight, 0f);
    }

    [TestMethod]
    public void TheShippedFile_CarriesTheFactor_AndLoadsItWithNoWarning()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(RepoPaths.RepoPath("Main", "_Module", "ModuleData"));
        var logger = Substitute.For<IModLogger>();

        var config = new ArmyTargetingConfigProvider(paths, logger).GetConfig();

        StringAssert.Contains(System.IO.File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "configs", "army_targeting.json")), "\"RulerCallCostFactor\": 0.5");
        Assert.AreEqual(0.5f, config.RulerCallCostFactor, 0f);
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        logger.DidNotReceive().LogError(Arg.Any<string>());
    }
}
