using DryIoc;
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

        Log(container, composition == null
            ? "[Client] no client composition: core registrations only (a dedicated server, or the client submodule did not load)"
            : $"[Client] composition applied: {composition.GetType().FullName} "
                + $"(client={BuildStampReport.ReadInformationalVersion(composition.GetType().Assembly)}, "
                + $"core={BuildStampReport.ReadInformationalVersion(typeof(ClientComposition).Assembly)})");
    }

    private static void Log(IContainer container, string line)
    {
        try { container.Resolve<IModLogger>(IfUnresolved.ReturnDefault)?.LogInfo(line); }
        catch { /* the log line must never stop the mod loading */ }
    }
}
