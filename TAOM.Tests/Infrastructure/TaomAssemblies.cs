using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TAOM.Client;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// The compiled TAOM assemblies an IL or type-scan safety gate reads: core (TAOM.dll) and the client
/// submodule (TAOM.Client.dll, ADR-013). A gate that passed only <c>typeof(IoC).Assembly</c> would miss
/// a banned call in a moved MapView or widget, so every such gate goes through here.
/// </summary>
public static class TaomAssemblies
{
    /// <summary>Core first, then the client. Naming a client type is what makes the runtime load its assembly.</summary>
    public static readonly Assembly[] All =
    {
        typeof(IoC).Assembly,
        typeof(ClientSubModule).Assembly,
    };

    /// <summary>
    /// <see cref="IlCallScanner.FindCallers"/> over every TAOM assembly, aggregated: callers and unreadable
    /// bodies are concatenated and the scanned counts summed.
    /// </summary>
    public static List<string> FindCallers(
        Func<MethodBase, bool> isBanned,
        out List<string> unreadable,
        out int scanned)
    {
        var violations = new List<string>();
        unreadable = new List<string>();
        scanned = 0;
        foreach (var assembly in All)
        {
            violations.AddRange(IlCallScanner.FindCallers(assembly, isBanned, out var bad, out var count));
            unreadable.AddRange(bad);
            scanned += count;
        }

        return violations;
    }
}
