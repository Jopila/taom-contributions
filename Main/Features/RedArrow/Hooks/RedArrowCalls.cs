using System.Collections.Generic;
using TAOM.Core.Logging;

namespace TAOM.Features.RedArrow.Hooks;

/// <summary>
/// The statics the two Red Arrow view-model patches share (#873): the boundary the module hands over in
/// <c>InitializeStatics</c>, and a log that names each patch problem once. It names no client-only type, so a dedicated
/// server can load it. Main thread only.
/// </summary>
internal static class RedArrowCalls
{
    private static readonly HashSet<string> Reported = new HashSet<string>();
    private static IModLogger? _logger;

    internal static RedArrowArmyBoundary? Boundary { get; private set; }

    internal static void Initialize(RedArrowArmyBoundary boundary, IModLogger logger)
    {
        Boundary = boundary;
        _logger = logger;
    }

    /// <summary>Logs <paramref name="message"/> at error level the first time <paramref name="key"/> is seen.</summary>
    internal static void ReportOnce(string key, string message)
    {
        if (Reported.Add(key))
            _logger?.LogError("[RedArrow] " + message);
    }
}
