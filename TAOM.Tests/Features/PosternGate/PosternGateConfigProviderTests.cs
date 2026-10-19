using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.PosternGate;

namespace TAOM.Tests.Features.PosternGate;

/// <summary>
/// One test per validation rule (csharp-architecture.md "Config Providers MUST Validate"). Each of the two arrays
/// reverts to its default on its own; one summary warning names every field that reverted.
/// </summary>
[TestClass]
public class PosternGateConfigProviderTests
{
    private static readonly int[] DefaultCosts = { 400, 700, 1000 };
    private static readonly float[] DefaultReductions = { 0.50f, 0.67f, 0.75f };

    private string _tempDir = null!;
    private IModLogger _logger = null!;
    private PosternGateConfigProvider _sut = null!;

    private string ConfigPath => Path.Combine(_tempDir, "settlement_buildings", "postern_gate.json");

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_Postern_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "settlement_buildings"));
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _sut = new PosternGateConfigProvider(pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void Write(string json) => File.WriteAllText(ConfigPath, json);

    private static string Json(string costs, string reductions) =>
        "{ \"costs\": " + costs + ", \"reductions\": " + reductions + " }";

    private void AssertDefaults()
    {
        CollectionAssert.AreEqual(DefaultCosts, _sut.Costs.ToArray());
        CollectionAssert.AreEqual(DefaultReductions, _sut.Reductions.ToArray());
    }

    [TestMethod]
    public void MissingFile_Defaults()
    {
        AssertDefaults();
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void ValidFile_IsUsed_WithNoWarning()
    {
        Write(Json("[100, 200, 300]", "[0.1, 0.2, 0.8]"));

        CollectionAssert.AreEqual(new[] { 100, 200, 300 }, _sut.Costs.ToArray());
        CollectionAssert.AreEqual(new[] { 0.1f, 0.2f, 0.8f }, _sut.Reductions.ToArray());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow("[NaN, 0.2, 0.3]")]
    [DataRow("[0.1, Infinity, 0.3]")]
    [DataRow("[0.1, 0.2, -Infinity]")]
    public void NaN_Or_Infinity_Reverts(string reductions)
    {
        Write(Json("[100, 200, 300]", reductions));

        CollectionAssert.AreEqual(DefaultReductions, _sut.Reductions.ToArray());
        CollectionAssert.AreEqual(new[] { 100, 200, 300 }, _sut.Costs.ToArray(), "a valid field is kept");
    }

    [DataTestMethod]
    [DataRow("[-0.1, 0.2, 0.3]")]
    [DataRow("[0.1, 0.2, 0.86]")]
    [DataRow("[0.1, 0.2, 1.5]")]
    public void Reduction_Negative_Or_Above0_85_Reverts(string reductions)
    {
        Write(Json("[100, 200, 300]", reductions));

        CollectionAssert.AreEqual(DefaultReductions, _sut.Reductions.ToArray());
    }

    [TestMethod]
    public void Reductions_NotNonDecreasing_Revert()
    {
        Write(Json("[100, 200, 300]", "[0.5, 0.4, 0.6]"));

        CollectionAssert.AreEqual(DefaultReductions, _sut.Reductions.ToArray());
    }

    [DataTestMethod]
    [DataRow("[0, 200, 300]")]
    [DataRow("[-5, 200, 300]")]
    [DataRow("[100, 200, 20001]")]
    public void Cost_NonPositive_Or_Above20000_Reverts(string costs)
    {
        Write(Json(costs, "[0.1, 0.2, 0.3]"));

        CollectionAssert.AreEqual(DefaultCosts, _sut.Costs.ToArray());
        CollectionAssert.AreEqual(new[] { 0.1f, 0.2f, 0.3f }, _sut.Reductions.ToArray(), "a valid field is kept");
    }

    [TestMethod]
    public void Costs_Decreasing_Revert()
    {
        Write(Json("[300, 200, 100]", "[0.1, 0.2, 0.3]"));

        CollectionAssert.AreEqual(DefaultCosts, _sut.Costs.ToArray());
    }

    [TestMethod]
    public void Cost_Fractional_Reverts()
    {
        Write(Json("[100.5, 200, 300]", "[0.1, 0.2, 0.3]"));

        CollectionAssert.AreEqual(DefaultCosts, _sut.Costs.ToArray());
    }

    [TestMethod]
    public void Cost_BeyondLongRange_Reverts_NeverThrows()
    {
        // Parsed as a big integer; reading it as a long overflows, which the provider turns into a revert.
        Write(Json("[100, 200, 99999999999999999999]", "[0.1, 0.2, 0.3]"));

        CollectionAssert.AreEqual(DefaultCosts, _sut.Costs.ToArray());
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("out of range")));
    }

    [TestMethod]
    public void Reduction_BeyondLongRange_RevertsOnlyReductions_NeverThrows()
    {
        // Parsed as a big integer, which cannot convert to a double; only the reductions revert.
        Write(Json("[100, 200, 300]", "[0.1, 0.2, 99999999999999999999]"));

        CollectionAssert.AreEqual(new[] { 100, 200, 300 }, _sut.Costs.ToArray());
        CollectionAssert.AreEqual(DefaultReductions, _sut.Reductions.ToArray());
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("out of range")));
    }

    [DataTestMethod]
    [DataRow("[1, 1, 20000]", "[0, 0, 0.85]")]
    [DataRow("[20000, 20000, 20000]", "[0.85, 0.85, 0.85]")]
    public void AcceptedEdges_AreKept_WithNoWarning(string costs, string reductions)
    {
        Write(Json(costs, reductions));

        var expectedCosts = costs.Trim('[', ']').Split(',').Select(c => int.Parse(c.Trim())).ToArray();
        var expectedReductions = reductions.Trim('[', ']').Split(',')
            .Select(r => float.Parse(r.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        CollectionAssert.AreEqual(expectedCosts, _sut.Costs.ToArray());
        CollectionAssert.AreEqual(expectedReductions, _sut.Reductions.ToArray());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow("[0.5, 0.6]")]
    [DataRow("[0.5, 0.6, 0.7, 0.8]")]
    [DataRow("[]")]
    public void ReductionArray_NotLength3_Reverts(string reductions)
    {
        Write(Json("[100, 200, 300]", reductions));

        CollectionAssert.AreEqual(DefaultReductions, _sut.Reductions.ToArray());
        CollectionAssert.AreEqual(new[] { 100, 200, 300 }, _sut.Costs.ToArray(), "a valid field is kept");
    }

    [DataTestMethod]
    [DataRow("[100, 200]")]
    [DataRow("[100, 200, 300, 400]")]
    [DataRow("[]")]
    public void CostArray_NotLength3_Reverts(string costs)
    {
        Write(Json(costs, "[0.1, 0.2, 0.3]"));

        CollectionAssert.AreEqual(DefaultCosts, _sut.Costs.ToArray());
    }

    [DataTestMethod]
    [DataRow("{ \"costs\": \"400\", \"reductions\": 0.5 }")]
    [DataRow("{ \"costs\": [\"a\", \"b\", \"c\"], \"reductions\": [true, false, null] }")]
    [DataRow("[]")]
    [DataRow("not json")]
    [DataRow("{}")]
    public void WrongJsonType_Reverts(string text)
    {
        Write(text);

        AssertDefaults();
    }

    [TestMethod]
    public void SummaryWarning_OnAnyRevert()
    {
        Write(Json("[0, 200, 300]", "[0.5, 0.4, 0.6]"));

        AssertDefaults();
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("costs") && s.Contains("reductions")));
    }

    [TestMethod]
    public void ThePathServiceThrows_Defaults_NeverThrows_WarnsOnce()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_ => throw new System.InvalidOperationException("no module root"));
        var sut = new PosternGateConfigProvider(pathService, _logger);

        CollectionAssert.AreEqual(DefaultCosts, sut.Costs.ToArray());
        CollectionAssert.AreEqual(DefaultReductions, sut.Reductions.ToArray());
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }
}
