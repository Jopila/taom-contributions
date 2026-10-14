using System;
using System.Collections.Generic;
using System.IO;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;

namespace TAOM.Features.WarChronicle.Events;

/// <summary>
/// Loads <c>war_chronicle/war_chronicle.json</c> through <see cref="ChronicleConfigValidator"/>: each problem
/// is a warning, and one summary warning follows any. A missing or unreadable file loads no events (the
/// chronicle then does nothing; the rally and ledger are unaffected). <see cref="Lazy{T}"/> and
/// <c>Reuse.Singleton</c>, so an edit needs a full game restart, not a save and load.
/// </summary>
public class ChronicleConfigProvider : IChronicleConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<IReadOnlyList<ChronicleEventDefinition>> _events;

    public ChronicleConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _events = new Lazy<IReadOnlyList<ChronicleEventDefinition>>(Load);
    }

    public IReadOnlyList<ChronicleEventDefinition> GetEvents() => _events.Value;

    private IReadOnlyList<ChronicleEventDefinition> Load()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "war_chronicle", "war_chronicle.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"ChronicleConfigProvider: war_chronicle.json not found at {path}, so no chronicle event loads");
            return new ChronicleEventDefinition[0];
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            _logger.LogError($"ChronicleConfigProvider: could not read war_chronicle.json: {ex.Message}");
            return new ChronicleEventDefinition[0];
        }

        var warnings = new List<string>();
        var events = ChronicleConfigValidator.Parse(json, warnings);
        foreach (var warning in warnings)
            _logger.LogWarning("ChronicleConfigProvider: " + warning);

        if (warnings.Count > 0)
            _logger.LogWarning("ChronicleConfigProvider: war_chronicle.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo($"ChronicleConfigProvider: Loaded war_chronicle.json ({events.Count} events)");

        return events;
    }
}
