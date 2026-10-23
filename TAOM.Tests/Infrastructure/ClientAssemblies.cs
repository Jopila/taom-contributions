using System;
using System.Linq;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Simple names of the assemblies that do not resolve when TAOM.dll loads on the Bannerlord
/// dedicated server (v1.5.5 <c>Win64_Shipping_Server</c> bin plus the server module bins). A TAOM type
/// whose base type or interface comes from one of these fails <c>Assembly.GetTypes()</c> there.
///
/// Source: the TAOM.dll references that were absent from the server's app bin and module bins when
/// the 40 failing types in <c>rgl_log_46908.txt</c> were traced (the headless plan, 2026-10-21). The
/// view layer (Gauntlet, SandBox.View, the ViewModelCollection assemblies, TwoDimension) is staged by
/// the Coop module only after TAOM loads, so it is unresolvable at TAOM's GetTypes; CustomBattle and
/// System.Windows.Forms exist nowhere on the server.
///
/// <c>Bannerlord.UIExtenderEx</c> is not here: it resolves on the staged server, and the staged-server check that
/// reads <see cref="Names"/> must never see it. That no core type carries a UIExtenderEx attribute is pinned by
/// <c>ClientIsolationGateTests.TaomCore_DeclaresNoUiExtension</c> (ADR-013).
/// </summary>
internal static class ClientAssemblies
{
    public static readonly string[] Names =
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

    public static bool IsClientOnly(string assemblySimpleName) =>
        Names.Contains(assemblySimpleName, StringComparer.OrdinalIgnoreCase);
}
