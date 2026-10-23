using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CoopInterop;
using TAOM.Tests.Features.LoadTimeStamps;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CoopInterop;

/// <summary>
/// ADR-013 B6c (D3): a dedicated server logs which client-only assemblies it has loaded by game init, so a UI
/// path that runs there is explained by the log. The runtime list must stay equal to the test-side one.
/// </summary>
[TestClass]
public class ServerAssemblyReportTests
{
    [TestMethod]
    public void Names_MatchClientAssemblies()
    {
        CollectionAssert.AreEquivalent(ClientAssemblies.Names, ServerAssemblyReport.ClientOnlyAssemblyNames);
    }

    [TestMethod]
    public void Describe_SomeLoaded_NamesLoadedAndNotLoaded()
    {
        var loaded = new[]
        {
            new AssemblyName("TaleWorlds.Core"),
            new AssemblyName("TaleWorlds.GauntletUI"),
            new AssemblyName("sandbox.view"),
        };

        var line = ServerAssemblyReport.Describe(loaded);

        Assert.AreEqual(
            "[Server] client-only assemblies loaded at game init: SandBox.View, TaleWorlds.GauntletUI; not loaded: "
            + "SandBox.GauntletUI, SandBox.ViewModelCollection, TaleWorlds.CampaignSystem.ViewModelCollection, "
            + "TaleWorlds.Core.ViewModelCollection, TaleWorlds.Engine.GauntletUI, TaleWorlds.GauntletUI.Data, "
            + "TaleWorlds.GauntletUI.PrefabSystem, TaleWorlds.MountAndBlade.GauntletUI, "
            + "TaleWorlds.MountAndBlade.GauntletUI.Widgets, TaleWorlds.MountAndBlade.View, "
            + "TaleWorlds.MountAndBlade.ViewModelCollection, TaleWorlds.TwoDimension, "
            + "TaleWorlds.MountAndBlade.CustomBattle, System.Windows.Forms",
            line);
    }

    [TestMethod]
    public void Describe_NoneLoaded_SaysNone()
    {
        StringAssert.StartsWith(ServerAssemblyReport.Describe(new[] { new AssemblyName("TaleWorlds.Core") }),
            "[Server] client-only assemblies loaded at game init: none; not loaded: SandBox.View, ");
    }

    // The report runs first in the server's game-init block; a logger that throws on every level must not take
    // the rest of that block down (both catches in Log are load-bearing).
    [TestMethod]
    public void Log_LoggerThrowsOnEveryLevel_Returns()
    {
        var logger = new RecordingLogger { OnLog = _ => throw new InvalidOperationException("logger down") };

        ServerAssemblyReport.Log(logger);

        Assert.AreEqual(0, logger.Lines.Count);
    }

    [TestMethod]
    public void Log_HealthyLogger_WritesOneInfoLine()
    {
        var logger = new RecordingLogger();

        ServerAssemblyReport.Log(logger);

        Assert.AreEqual(1, logger.Lines.Count);
        StringAssert.StartsWith(logger.Lines[0], "INFO [Server] client-only assemblies loaded at game init: ");
    }
}
