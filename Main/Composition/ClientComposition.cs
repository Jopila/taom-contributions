using System;
using System.Reflection;
using DryIoc;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;

namespace TAOM.Composition;

/// <summary>
/// The hand-off slot from the client submodule to core (ADR-013). The engine constructs every submodule
/// before it calls any OnSubModuleLoad (TaleWorlds.MountAndBlade.Module.LoadSubModules, then
/// InitializeSubModuleBases), so the client constructor has set <see cref="Current"/> before
/// SubModule.OnSubModuleLoad calls IoC.Configure. A dedicated server never loads the client submodule,
/// so there the slot stays null and core's registrations (its null adapters, ADR-007) stand.
/// </summary>
public static class ClientComposition
{
    /// <summary>Set by the client submodule's constructor; null on a dedicated server.</summary>
    public static IClientComposition? Current { get; set; }

    /// <summary>
    /// The client submodule's assembly, or null on a dedicated server. Core finds client types through this and never
    /// names TAOM.Client (ADR-013); a reflection scan that must see the client's types reads it.
    /// </summary>
    internal static Assembly? Assembly => Current?.GetType().Assembly;

    /// <summary>
    /// TAOM's assemblies in load order: core, then TAOM.Client when the slot is filled (ADR-013). A dedicated server,
    /// or a rendering client whose TAOM.Client did not load, gets core only. Every runtime scan of TAOM's own types
    /// reads this (the patch categories, the map profiler's views, the dev console's patch report), so a type that
    /// moves to the client is still found.
    /// </summary>
    internal static Assembly[] LoadedTaomAssemblies()
    {
        var core = typeof(ClientComposition).Assembly;
        var client = Assembly;
        return client == null || client == core ? new[] { core } : new[] { core, client };
    }

    /// <summary>
    /// Whether this process is a dedicated server: the engine's own build constant, as
    /// DedicatedServerProvider reads it. A seam so a test can stand on either side.
    /// </summary>
    internal static Func<bool> IsDedicatedServer = ReadDedicatedServer;

    internal static bool ReadDedicatedServer() => GameNetwork.IsDedicatedServer;

    /// <summary>
    /// Runs the slot's registrations with IfAlreadyRegistered.Replace as the default. The registrator is a
    /// view sharing the container's registry and singleton scope, so what it registers is what the
    /// container resolves. Never dispose the view: it shares the registry and the singleton scope, so
    /// disposing it would also empty the container's registry and dispose its singletons. A throw
    /// propagates, because a half-applied client composition must not load.
    /// </summary>
    internal static void Apply(IContainer container)
    {
        var composition = Current;
        if (composition != null)
        {
            var replacing = container.With(
                container.Rules.WithDefaultIfAlreadyRegistered(IfAlreadyRegistered.Replace),
                container.ScopeContext, RegistrySharing.Share, container.SingletonScope);
            composition.RegisterServices(replacing);
        }

        if (composition == null)
        {
            Log(container, l => l.LogInfo(
                "[Client] no client composition: core registrations only (a dedicated server, or the client submodule did not load)"));
            if (!SafeIsDedicatedServer())
            {
                Log(container, l => l.LogError(
                    "[Client] TAOM.Client did not load on a rendering client: front-end, campaign map and mission UI will not work "
                    + "(faction map, front-end art, Custom Battle factions, realm borders, camp overlay, map visuals of camps, refuges and supply routes, War of the Ring meter, "
                    + "Fiefs button, settlement plates, Hall of Fire backdrops, battle action bar, warg rage control, "
                    + "Host Review order bar, shader notice, and the TAOM screen extensions: time controls, Telchar's Mark, "
                    + "special resources, Reeve's Ledger, fief naming, messengers, careers, Shadowfax screens). Check that the TAOM.Client submodule is installed."));
            }

            return;
        }

        string clientVer = BuildStampReport.ReadInformationalVersion(composition.GetType().Assembly);
        string coreVer = BuildStampReport.ReadInformationalVersion(typeof(ClientComposition).Assembly);
        Log(container, l => l.LogInfo(
            $"[Client] composition applied: {composition.GetType().FullName} (client={clientVer}, core={coreVer})"));
        string? mismatch = DescribeStampMismatch(clientVer, coreVer);
        if (mismatch != null) Log(container, l => l.LogWarning(mismatch));
    }

    /// <summary>A warning line when the client and core build stamps are not from one build; null when paired or unreadable.</summary>
    internal static string? DescribeStampMismatch(string clientVersion, string coreVersion)
    {
        if (!BuildStampReport.TryParseStamp(clientVersion, out var client)
            || !BuildStampReport.TryParseStamp(coreVersion, out var core)) return null;

        var verdict = BuildStampReport.Classify(client, core);
        return verdict == BuildPairVerdict.Paired
            ? null
            : "[Client] TAOM.Client and TAOM core were not built together (client="
                + clientVersion + ", core=" + coreVersion + ")"
                + BuildStampReport.DescribeVerdict(verdict, client, core);
    }

    private static bool SafeIsDedicatedServer()
    {
        // Unreadable means unknown, and unknown must not raise the rendering-client error.
        try { return IsDedicatedServer(); }
        catch { return true; }
    }

    private static void Log(IContainer container, Action<IModLogger> write)
    {
        try
        {
            var logger = container.Resolve<IModLogger>(IfUnresolved.ReturnDefault);
            if (logger != null) write(logger);
        }
        catch { /* the log line must never stop the mod loading */ }
    }
}
