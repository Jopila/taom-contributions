using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;

namespace TAOM.Features.VoiceAccents;

/// <summary>
/// Validating boundary loader for <c>voice_accents/voice_accents.json</c> (csharp-architecture.md
/// "Config Providers MUST Validate"). A missing file or a null section gives the built-in map with a
/// warning, a parse failure the same with an error, and a bad row is dropped with a warning: an
/// empty key, an unknown accent value in either section, or a key that differs from an earlier one only
/// in case or whitespace (the first row is kept). Keys and values are trimmed and lower-cased. An exact
/// duplicate key never reaches here: the JSON reader keeps its last value.
/// </summary>
public sealed class VoiceAccentsConfigProvider : IVoiceAccentsConfigProvider
{
    private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<VoiceAccentMap> _map;

    public VoiceAccentsConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _map = new Lazy<VoiceAccentMap>(Load);
    }

    public VoiceAccentMap GetMap() => _map.Value;

    private VoiceAccentMap Load()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "voice_accents", "voice_accents.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"VoiceAccentsConfigProvider: voice_accents.json not found at {path}, using the built-in voice map");
            return VoiceAccentMap.Default();
        }

        VoiceAccentsConfig parsed;
        try
        {
            parsed = JsonConvert.DeserializeObject<VoiceAccentsConfig>(File.ReadAllText(path), SerializerSettings)
                ?? new VoiceAccentsConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"VoiceAccentsConfigProvider: Failed to parse voice_accents.json: {ex.Message}");
            return VoiceAccentMap.Default();
        }

        if (parsed.Cultures == null || parsed.Races == null)
        {
            _logger.LogWarning("VoiceAccentsConfigProvider: the cultures or races section is null, using the built-in voice map");
            return VoiceAccentMap.Default();
        }

        return Validate(parsed.Cultures, parsed.Races);
    }

    private VoiceAccentMap Validate(Dictionary<string, string?> parsedCultures, Dictionary<string, string?> parsedRaces)
    {
        var rejected = false;
        var cultures = ValidateRows("cultures", "culture", parsedCultures, ref rejected);
        var races = ValidateRows("races", "race", parsedRaces, ref rejected);

        if (rejected)
            _logger.LogWarning("VoiceAccentsConfigProvider: voice_accents.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo($"VoiceAccentsConfigProvider: Loaded voice_accents.json ({cultures.Count} culture row(s), {races.Count} race row(s))");

        return new VoiceAccentMap(cultures, races);
    }

    private Dictionary<string, string> ValidateRows(string section, string noun, Dictionary<string, string?> rows, ref bool rejected)
    {
        var kept = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in rows)
        {
            var key = VoiceAccentMap.Normalize(pair.Key);
            if (string.IsNullOrEmpty(key))
            {
                _logger.LogWarning($"VoiceAccentsConfigProvider: a {noun} row has an empty key, dropping it");
                rejected = true;
                continue;
            }
            var value = VoiceAccentMap.Normalize(pair.Value);
            if (value == null || !VoiceAccentMap.IsAllowedValue(value))
            {
                _logger.LogWarning($"VoiceAccentsConfigProvider: {section}['{key}'] = '{pair.Value}' is not silent or one of {string.Join(", ", VoiceAccentMap.Accents)}, dropping the row");
                rejected = true;
                continue;
            }
            if (kept.ContainsKey(key))
            {
                _logger.LogWarning($"VoiceAccentsConfigProvider: {section}['{key}'] is a duplicate after trimming and lower-casing, keeping the first row");
                rejected = true;
                continue;
            }
            kept[key] = value;
        }
        return kept;
    }

    private sealed class VoiceAccentsConfig
    {
        [JsonProperty("cultures")]
        public Dictionary<string, string?>? Cultures { get; set; }

        [JsonProperty("races")]
        public Dictionary<string, string?>? Races { get; set; }
    }
}
