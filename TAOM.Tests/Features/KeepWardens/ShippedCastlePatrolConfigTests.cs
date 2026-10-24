using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.KeepWardens;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.KeepWardens;

/// <summary>The REAL <c>configs/castle_patrols.json</c> the module ships, read the way the game reads it.</summary>
[TestClass]
public class ShippedCastlePatrolConfigTests
{
    private static string ModuleData => RepoPaths.RepoPath("Main", "_Module", "ModuleData");

    [TestMethod]
    public void TheShippedFile_Exists_InTheFolderTheProviderReads()
    {
        Assert.IsTrue(File.Exists(Path.Combine(ModuleData, "configs", "castle_patrols.json")),
            "Main/_Module/ModuleData/configs/castle_patrols.json is missing, so every install falls back to the defaults with a warning");
    }

    [TestMethod]
    public void TheShippedFile_LoadsThroughTheProvider_AsVanillasTownValues_WithNoWarning()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(ModuleData);
        var logger = Substitute.For<IModLogger>();

        var provider = new CastlePatrolConfigProvider(paths, logger);

        CollectionAssert.AreEqual(new[] { 10f, 8f, 6f }, provider.SpawnDays.ToArray());
        CollectionAssert.AreEqual(new[] { 15, 20, 25 }, provider.SizeLimits.ToArray());
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        logger.DidNotReceive().LogError(Arg.Any<string>());
    }
}
