using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.FromTheAshes;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FromTheAshes;

/// <summary>The REAL <c>settlement_buildings/from_the_ashes.json</c> the module ships, read the way the game reads it.</summary>
[TestClass]
public class ShippedFromTheAshesConfigTests
{
    private static string ModuleData => RepoPaths.RepoPath("Main", "_Module", "ModuleData");

    [TestMethod]
    public void TheShippedFile_Exists_InTheFolderTheProviderReads()
    {
        Assert.IsTrue(File.Exists(Path.Combine(ModuleData, "settlement_buildings", "from_the_ashes.json")),
            "Main/_Module/ModuleData/settlement_buildings/from_the_ashes.json is missing, so every install falls back to the defaults with a warning");
    }

    [TestMethod]
    public void TheShippedFile_LoadsThroughTheProvider_AsTheDefaults_WithNoWarning()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(ModuleData);
        var logger = Substitute.For<IModLogger>();

        var provider = new FromTheAshesConfigProvider(paths, logger);

        AssertSame(FromTheAshesConfigProvider.DefaultRebuild(), provider.Rebuild);
        AssertSame(FromTheAshesConfigProvider.DefaultThralls(), provider.Thralls);
        AssertSame(FromTheAshesConfigProvider.DefaultProject(), provider.Project);
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void TheDefaults_AreTheBriefsNumbers()
    {
        var rebuild = FromTheAshesConfigProvider.DefaultRebuild();
        Assert.AreEqual((24f, 20, 40, 3, 5, 200, -1, 0f), (rebuild.Hours, rebuild.Labour, rebuild.Hardwood, rebuild.NotableRelation,
            rebuild.OwnerRelation, rebuild.AthleticsXp, rebuild.StopPenalty, rebuild.ConstructionPerHour));
        var thralls = FromTheAshesConfigProvider.DefaultThralls();
        Assert.AreEqual((24f, 20, 40, 0, 5, 200, 0, 0f), (thralls.Hours, thralls.Labour, thralls.Hardwood, thralls.NotableRelation,
            thralls.OwnerRelation, thralls.AthleticsXp, thralls.StopPenalty, thralls.ConstructionPerHour));
        var project = FromTheAshesConfigProvider.DefaultProject();
        Assert.AreEqual((20f, 40, 30, 10, 5, 5, 1, 150, 15f), (project.Hours, project.Labour, project.Hardwood, project.Clay,
            project.Tools, project.Iron, project.NotableRelation, project.AthleticsXp, project.ConstructionPerHour));
    }

    private static void AssertSame(AshesVariantConfig expected, AshesVariantConfig actual)
    {
        foreach (var property in typeof(AshesVariantConfig).GetProperties())
            Assert.AreEqual(property.GetValue(expected), property.GetValue(actual), property.Name);
    }
}
