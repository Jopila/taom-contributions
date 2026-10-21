using System;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// The swallow decision behind <see cref="CollectAssemblyTypesShim"/>, extracted as a pure function
/// so it can be tested without Harmony or a running game.
/// </summary>
public static class CollectAssemblyTypesPolicy
{
    /// <summary>
    /// Simple names of BannerlordCoop's assemblies, from the AssemblyName (or project name) of each
    /// shipped project in its source tree. Matched exactly, ignoring case.
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
    };

    /// <summary>
    /// Whether the shim should swallow a <see cref="System.Reflection.ReflectionTypeLoadException"/>
    /// and return the partial type list (true), or let it propagate (false).
    ///
    /// Historic behaviour is to swallow for every assembly. Under co-op that is wrong for Coop's own
    /// assemblies: a partial list means the mod registers some of its types and not others, and the
    /// host and client then disagree without any error. A loud load failure is the cheaper outcome.
    /// Every other assembly keeps the swallow, since one broken third-party mod must not cascade.
    /// </summary>
    public static bool ShouldSwallow(string? assemblySimpleName, bool coopActive)
    {
        if (!coopActive) return true;
        if (string.IsNullOrEmpty(assemblySimpleName)) return true;

        foreach (var coopName in CoopAssemblyNames)
        {
            if (string.Equals(coopName, assemblySimpleName, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }
}
