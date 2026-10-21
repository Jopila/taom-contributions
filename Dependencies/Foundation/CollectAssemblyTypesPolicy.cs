using System;
using System.Collections.Generic;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// The swallow decision behind <see cref="CollectAssemblyTypesShim"/>, extracted as a pure function
/// so it can be tested without Harmony or a running game.
/// </summary>
public static class CollectAssemblyTypesPolicy
{
    /// <summary>
    /// The launcher module id of BannerlordCoop (its SubModule.xml <c>Id</c>). Only this module's
    /// assemblies are protected: BannerlordTogether and the force flag never count. The match is by
    /// simple name, so while this module is enabled another mod's assembly named <c>Common</c>,
    /// <c>Missions</c> or <c>GameInterface</c> also loses the swallow.
    /// </summary>
    internal const string CoopModuleId = "Coop";

    /// <summary>
    /// Simple names of BannerlordCoop's assemblies, from the AssemblyName (or project name) of each
    /// shipped project in its source tree, plus "AutoSync", which Coop compiles at runtime
    /// (GameInterface AutoSyncBuilder, <c>CSharpCompilation.Create("AutoSync")</c>). Matched exactly,
    /// ignoring case.
    /// </summary>
    internal static readonly string[] CoopAssemblyNames =
    {
        "Coop",
        "Coop.Core",
        "Coop.CrashReporter",
        "Coop.Steam",
        "Common",
        "GameInterface",
        "Missions",
        "AutoSync",
    };

    /// <summary>
    /// Whether the shim should swallow a <see cref="System.Reflection.ReflectionTypeLoadException"/>
    /// and return the partial type list (true), or let it propagate (false).
    ///
    /// Historic behaviour is to swallow for every assembly. It changes only when the BannerlordCoop
    /// module (id "Coop") is among <paramref name="activeCoopModuleIds"/> and the assembly is one of
    /// Coop's own: a partial list means the mod registers some of its types and not others, and the
    /// host and client then disagree without any error. A loud load failure is the cheaper outcome.
    /// Every other assembly keeps the swallow, since one broken third-party mod must not cascade, and
    /// so does every other co-op module: BannerlordTogether ships no assembly of these names, and
    /// the force flag's marker id is not a module.
    /// </summary>
    public static bool ShouldSwallow(string? assemblySimpleName, IEnumerable<string> activeCoopModuleIds)
    {
        if (!IsCoopModuleActive(activeCoopModuleIds)) return true;
        if (string.IsNullOrEmpty(assemblySimpleName)) return true;

        foreach (var coopName in CoopAssemblyNames)
        {
            if (string.Equals(coopName, assemblySimpleName, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    private static bool IsCoopModuleActive(IEnumerable<string>? activeCoopModuleIds)
    {
        if (activeCoopModuleIds == null) return false;
        foreach (var id in activeCoopModuleIds)
        {
            if (string.Equals(id, CoopModuleId, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
