using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TAOM.Core.Logging;

namespace TAOM.Features.CoopInterop;

/// <summary>
/// ADR-013: on a dedicated server, one Info line at game init naming which client-only assemblies are loaded and
/// which are not loaded yet. The Coop stage supplies copies of the view layer after TAOM boots, so this line shows
/// which of them a server had loaded by then. "Not loaded" is not "unavailable": an assembly nothing has touched yet
/// can still resolve later. Called from <c>SubModule.OnGameInitializationFinished</c> behind the server check.
/// </summary>
internal static class ServerAssemblyReport
{
    /// <summary>
    /// The assemblies that do not resolve when TAOM.dll loads on a dedicated server. Kept equal to
    /// <c>TAOM.Tests/Infrastructure/ClientAssemblies.cs</c> by <c>ServerAssemblyReportTests</c>.
    /// </summary>
    internal static readonly string[] ClientOnlyAssemblyNames =
    {
        "SandBox.View",
        "SandBox.GauntletUI",
        "SandBox.ViewModelCollection",
        "TaleWorlds.CampaignSystem.ViewModelCollection",
        "TaleWorlds.Core.ViewModelCollection",
        "TaleWorlds.Engine.GauntletUI",
        "TaleWorlds.GauntletUI",
        "TaleWorlds.GauntletUI.Data",
        "TaleWorlds.GauntletUI.PrefabSystem",
        "TaleWorlds.MountAndBlade.GauntletUI",
        "TaleWorlds.MountAndBlade.GauntletUI.Widgets",
        "TaleWorlds.MountAndBlade.View",
        "TaleWorlds.MountAndBlade.ViewModelCollection",
        "TaleWorlds.TwoDimension",
        "TaleWorlds.MountAndBlade.CustomBattle",
        "System.Windows.Forms",
    };

    /// <summary>The report line for the given loaded assemblies, names in <see cref="ClientOnlyAssemblyNames"/> order.</summary>
    internal static string Describe(IEnumerable<AssemblyName> loaded)
    {
        var loadedNames = new HashSet<string>(
            loaded.Select(a => a.Name ?? ""), StringComparer.OrdinalIgnoreCase);
        var present = ClientOnlyAssemblyNames.Where(loadedNames.Contains).ToList();
        var notLoaded = ClientOnlyAssemblyNames.Where(n => !loadedNames.Contains(n)).ToList();
        return "[Server] client-only assemblies loaded at game init: " + Join(present) + "; not loaded: " + Join(notLoaded);
    }

    /// <summary>Logs <see cref="Describe"/> over the current AppDomain. Never throws: it is a diagnostic.</summary>
    internal static void Log(IModLogger logger)
    {
        try
        {
            logger.LogInfo(Describe(AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName())));
        }
        catch (Exception ex)
        {
            try { logger.LogWarning($"[Server] client-only assembly report failed: {ex.GetType().Name}: {ex.Message}"); }
            catch (Exception) { }
        }
    }

    private static string Join(List<string> names) => names.Count == 0 ? "none" : string.Join(", ", names);
}
