using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;

namespace TAOM.Features.CrownGuard;

/// <summary>
/// Validating boundary loader for <c>crown_guard/crown_guard_cultures.json</c> (csharp-architecture.md
/// "Config Providers MUST Validate"). A missing, empty or unparseable file, or a null section, gives the
/// built-in map (a parse failure logs an error, the rest a warning). A bad row is dropped with a warning: an
/// empty key, a null or blank troop id, or a key that differs from an earlier one only in case or whitespace
/// (the first row is kept; an exact duplicate key never reaches here, the JSON reader keeps its last value).
/// Keys are trimmed and lower-cased; troop ids are trimmed and keep their case.
/// Loaded once per process (<c>Reuse.Singleton</c>): a JSON edit needs a full game restart.
/// </summary>
public sealed class CrownGuardConfigProvider
{
    private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<CrownGuardMap> _map;

    public CrownGuardConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _map = new Lazy<CrownGuardMap>(Load);
    }

    public CrownGuardMap GetMap() => _map.Value;

    private CrownGuardMap Load()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "crown_guard", "crown_guard_cultures.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"CrownGuardConfigProvider: crown_guard_cultures.json not found at {path}, using the built-in guard map");
            return CrownGuardMap.Default();
        }

        CrownGuardCulturesConfig parsed;
        try
        {
            parsed = JsonConvert.DeserializeObject<CrownGuardCulturesConfig>(File.ReadAllText(path), SerializerSettings)
                ?? new CrownGuardCulturesConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"CrownGuardConfigProvider: Failed to parse crown_guard_cultures.json: {ex.Message}");
            return CrownGuardMap.Default();
        }

        if (parsed.Cultures == null)
        {
            _logger.LogWarning("CrownGuardConfigProvider: the cultures section is null, using the built-in guard map");
            return CrownGuardMap.Default();
        }

        return Validate(parsed.Cultures);
    }

    private CrownGuardMap Validate(Dictionary<string, string?> rows)
    {
        var rejected = false;
        var kept = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in rows)
        {
            var key = CrownGuardMap.NormalizeKey(pair.Key);
            if (key == null)
            {
                _logger.LogWarning("CrownGuardConfigProvider: a culture row has an empty key, dropping it");
                rejected = true;
                continue;
            }
            if (kept.ContainsKey(key))
            {
                _logger.LogWarning($"CrownGuardConfigProvider: cultures['{key}'] is a duplicate after trimming and lower-casing, keeping the first row");
                rejected = true;
                continue;
            }
            var troopId = pair.Value?.Trim();
            if (string.IsNullOrEmpty(troopId))
            {
                _logger.LogWarning($"CrownGuardConfigProvider: cultures['{key}'] has an empty or null troop id, dropping the row");
                rejected = true;
                continue;
            }
            kept[key] = troopId!;
        }

        if (rejected)
            _logger.LogWarning("CrownGuardConfigProvider: crown_guard_cultures.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo($"CrownGuardConfigProvider: Loaded crown_guard_cultures.json ({kept.Count} culture row(s))");

        return new CrownGuardMap(kept);
    }

    private sealed class CrownGuardCulturesConfig
    {
        [JsonProperty("cultures")]
        public Dictionary<string, string?>? Cultures { get; set; }
    }
}
