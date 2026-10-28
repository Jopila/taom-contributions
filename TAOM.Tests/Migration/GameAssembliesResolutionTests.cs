using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Migration;

/// <summary>
/// How the binding gate finds the game. For an Install build the two environment variables win,
/// in the order Directory.Build.props uses; when neither names a usable folder in the test process
/// (the override needs Bannerlord.exe, the game dir only needs to exist), the gate falls back to
/// the GameFolder this test assembly was compiled against, so a test DLL built against the game
/// never skips the binding suite just because its runner lacks the variables. A RefAsm build loads
/// only its own refasm-game stub folder, so a variable can never mix the install into a no-game run.
/// </summary>
[TestClass]
public class GameAssembliesResolutionTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "TAOM_GameDirTest_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            try { Directory.Delete(_root, true); } catch { }
        }
    }

    private string MakeDir(string name)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    [TestMethod]
    public void ResolveGameDir_NoEnvironmentVariables_FallsBackToTheBuildFolder()
    {
        // Arrange
        var built = MakeDir("built");

        // Act
        var result = GameAssemblies.ResolveGameDir(null, null, built);

        // Assert
        Assert.AreEqual(built, result);
    }

    [TestMethod]
    public void ResolveGameDir_GameDirSet_WinsOverTheBuildFolder()
    {
        // Arrange
        var game = MakeDir("game");
        var built = MakeDir("built");

        // Act
        var result = GameAssemblies.ResolveGameDir(null, game, built);

        // Assert
        Assert.AreEqual(game, result);
    }

    [TestMethod]
    public void ResolveGameDir_OverrideHoldingBannerlordExe_WinsOverBoth()
    {
        // Arrange
        var over = MakeDir("over");
        var overBin = Path.Combine(over, "bin", "Win64_Shipping_Client");
        Directory.CreateDirectory(overBin);
        File.WriteAllText(Path.Combine(overBin, "Bannerlord.exe"), "");
        var game = MakeDir("game");
        var built = MakeDir("built");

        // Act
        var result = GameAssemblies.ResolveGameDir(over, game, built);

        // Assert
        Assert.AreEqual(over, result);
    }

    [TestMethod]
    public void ResolveGameDir_OverrideWithoutBannerlordExe_FallsThroughToTheGameDir()
    {
        // Arrange
        var over = MakeDir("over");
        var game = MakeDir("game");
        var built = MakeDir("built");

        // Act
        var result = GameAssemblies.ResolveGameDir(over, game, built);

        // Assert
        Assert.AreEqual(game, result);
    }

    [TestMethod]
    public void ResolveGameDir_GameDirNotOnDisk_FallsBackToTheBuildFolder()
    {
        // Arrange
        var gone = Path.Combine(_root, "gone");
        var built = MakeDir("built");

        // Act
        var result = GameAssemblies.ResolveGameDir(null, gone, built);

        // Assert
        Assert.AreEqual(built, result);
    }

    [TestMethod]
    public void ResolveGameDir_BuildFolderNotOnDisk_ReturnsNull()
    {
        // Arrange
        var missing = Path.Combine(_root, "missing");

        // Act
        var result = GameAssemblies.ResolveGameDir(null, null, missing);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ResolveGameDir_BuildFolderEmpty_ReturnsNull()
    {
        // Arrange (none)

        // Act
        var result = GameAssemblies.ResolveGameDir(null, null, "");

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ResolveGameDir_RefAsmBuild_ItsStubFolderWinsOverBothVariables()
    {
        // Arrange
        var over = MakeDir("over");
        var overBin = Path.Combine(over, "bin", "Win64_Shipping_Client");
        Directory.CreateDirectory(overBin);
        File.WriteAllText(Path.Combine(overBin, "Bannerlord.exe"), "");
        var game = MakeDir("game");
        var stubs = MakeDir("refasm-game");

        // Act
        var result = GameAssemblies.ResolveGameDir(over, game, stubs, builtAgainstReferenceAssemblies: true);

        // Assert
        Assert.AreEqual(stubs, result);
    }

    [TestMethod]
    public void ResolveGameDir_RefAsmBuildWithoutItsStubFolder_ReturnsNull_NeverTheInstall()
    {
        // Arrange (a variable naming the install would load real module DLLs beside the stubs)
        var game = MakeDir("game");
        var missing = Path.Combine(_root, "refasm-game");

        // Act
        var result = GameAssemblies.ResolveGameDir(null, game, missing, builtAgainstReferenceAssemblies: true);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void BuiltGameFolder_TestAssemblyAsCompiled_IsTheGameItsReferenceModeNeeds()
    {
        // Arrange (TAOM.Tests.csproj stamps both values at build time. An Install build
        // stamps its GameFolder; a RefAsm build stamps the refasm-game folder that
        // GameReferences.targets writes beside this DLL, so the no-game gate needs no variable)
        var stubFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "refasm-game");

        // Act
        var folder = GameAssemblies.BuiltGameFolder;
        var mode = GameAssemblies.BuiltGameRefs;

        // Assert
        Assert.IsTrue(mode == "Install" || mode == "RefAsm", $"TaomGameRefs metadata is '{mode ?? "missing"}', not Install or RefAsm.");
        Assert.IsFalse(string.IsNullOrEmpty(folder), "TaomGameFolder metadata is missing or empty, so GameAssemblies cannot find a game.");
        Assert.IsTrue(
            File.Exists(Path.Combine(folder!, "bin", "Win64_Shipping_Client", "Bannerlord.exe")) ||
            File.Exists(Path.Combine(folder!, "bin", "Gaming.Desktop.x64_Shipping_Client", "Bannerlord.exe")),
            $"TaomGameFolder '{folder}' holds no bin\\<platform>\\Bannerlord.exe, so GameAssemblies.EnsureLoaded would reject it.");
        Assert.AreEqual(
            mode == "RefAsm",
            string.Equals(Path.GetFullPath(folder!).TrimEnd('\\', '/'), Path.GetFullPath(stubFolder).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase),
            $"A {mode} build stamped TaomGameFolder '{folder}': only a RefAsm build stamps its own refasm-game folder.");
    }
}
