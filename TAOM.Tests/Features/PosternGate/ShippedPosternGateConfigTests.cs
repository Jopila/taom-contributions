using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.PosternGate;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.PosternGate;

/// <summary>The REAL <c>settlement_buildings/postern_gate.json</c> the module ships, read the way the game reads it.</summary>
[TestClass]
public class ShippedPosternGateConfigTests
{
    private static string ModuleData => RepoPaths.RepoPath("Main", "_Module", "ModuleData");

    [TestMethod]
    public void TheShippedFile_Exists_InTheFolderTheProviderReads()
    {
        Assert.IsTrue(File.Exists(Path.Combine(ModuleData, "settlement_buildings", "postern_gate.json")),
            "Main/_Module/ModuleData/settlement_buildings/postern_gate.json is missing, so every install falls back to the defaults with a warning");
    }

    [TestMethod]
    public void TheShippedFile_LoadsThroughTheProvider_AsTheDefaults_WithNoWarning()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(ModuleData);
        var logger = Substitute.For<IModLogger>();

        var provider = new PosternGateConfigProvider(paths, logger);

        CollectionAssert.AreEqual(new[] { 400, 700, 1000 }, provider.Costs.ToArray());
        CollectionAssert.AreEqual(new[] { 0.50f, 0.67f, 0.75f }, provider.Reductions.ToArray());
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        logger.DidNotReceive().LogError(Arg.Any<string>());
    }
}
