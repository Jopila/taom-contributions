using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;

namespace TAOM.Features.HallOfFire;

/// <summary>
/// Validating boundary loader for <c>hall_of_fire/hall_of_fire.json</c> (csharp-architecture.md
/// "Config Providers MUST Validate"). A missing file or a null section gives the built-in map with a
/// warning, a parse failure the same with an error, and a bad row is dropped with a warning: an empty
/// key, a value that is not one of the six vanilla sets, a key equal to its value (a no-op), or a key
/// that differs from an earlier one only in case or whitespace (the first row is kept). Keys and values
/// are trimmed and lower-cased. An exact duplicate key never reaches here: the JSON reader keeps its last value.
/// </summary>
public sealed class HallOfFireConfigProvider : IHallOfFireConfigProvider
{
    private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<HallOfFireMap> _map;

    public HallOfFireConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _map = new Lazy<HallOfFireMap>(Load);
    }

    public HallOfFireMap GetMap() => _map.Value;

    private HallOfFireMap Load()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "hall_of_fire", "hall_of_fire.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"HallOfFireConfigProvider: hall_of_fire.json not found at {path}, using the built-in map");
            return HallOfFireMap.Default();
        }

        HallOfFireConfig parsed;
        try
        {
            parsed = JsonConvert.DeserializeObject<HallOfFireConfig>(File.ReadAllText(path), SerializerSettings)
                ?? new HallOfFireConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"HallOfFireConfigProvider: Failed to parse hall_of_fire.json: {ex.Message}");
            return HallOfFireMap.Default();
        }

        if (parsed.Cultures == null)
        {
            _logger.LogWarning("HallOfFireConfigProvider: the cultures section is null, using the built-in map");
            return HallOfFireMap.Default();
        }

        return Validate(parsed.Cultures);
    }

    private HallOfFireMap Validate(Dictionary<string, string?> rows)
    {
        var rejected = false;
        var kept = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in rows)
        {
            var key = HallOfFireMap.NormalizeKey(pair.Key);
            if (key == null)
            {
                _logger.LogWarning("HallOfFireConfigProvider: a culture row has an empty key, dropping it");
                rejected = true;
                continue;
            }
            var value = HallOfFireMap.NormalizeKey(pair.Value);
            if (value == null || !HallOfFireMap.AllowedSets.Contains(value))
            {
                _logger.LogWarning($"HallOfFireConfigProvider: cultures['{key}'] = '{pair.Value}' is not one of {string.Join(", ", HallOfFireMap.AllowedSets)}, dropping the row");
                rejected = true;
                continue;
            }
            if (key == value)
            {
                _logger.LogWarning($"HallOfFireConfigProvider: cultures['{key}'] maps to itself, a no-op, dropping the row");
                rejected = true;
                continue;
            }
            if (kept.ContainsKey(key))
            {
                _logger.LogWarning($"HallOfFireConfigProvider: cultures['{key}'] is a duplicate after trimming and lower-casing, keeping the first row");
                rejected = true;
                continue;
            }
            kept[key] = value;
        }

        if (rejected)
            _logger.LogWarning("HallOfFireConfigProvider: hall_of_fire.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo($"HallOfFireConfigProvider: Loaded hall_of_fire.json ({kept.Count} culture row(s))");

        return new HallOfFireMap(kept);
    }

    private sealed class HallOfFireConfig
    {
        [JsonProperty("cultures")]
        public Dictionary<string, string?>? Cultures { get; set; }
    }
}
