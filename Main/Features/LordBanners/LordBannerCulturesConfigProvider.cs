using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;

namespace TAOM.Features.LordBanners;

/// <summary>
/// Validating boundary loader for <c>lord_banners/lord_banner_cultures.json</c> (csharp-architecture.md
/// "Config Providers MUST Validate"). A missing, empty or unparseable file, or a null section, gives the
/// built-in map (a parse failure logs an error, the rest a warning). A bad row is dropped with a warning: an
/// empty key, a null list, a value that is not one of the six vanilla banner cultures, or a key that differs
/// from an earlier one only in case or whitespace (the first row is kept; an exact duplicate key never
/// reaches here, the JSON reader keeps its last value). Repeated values inside one row
/// collapse silently. Keys and values are trimmed and lower-cased.
/// </summary>
public sealed class LordBannerCulturesConfigProvider : ILordBannerCulturesConfigProvider
{
    private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<LordBannerMap> _map;

    public LordBannerCulturesConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _map = new Lazy<LordBannerMap>(Load);
    }

    public LordBannerMap GetMap() => _map.Value;

    private LordBannerMap Load()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "lord_banners", "lord_banner_cultures.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"LordBannerCulturesConfigProvider: lord_banner_cultures.json not found at {path}, using the built-in banner map");
            return LordBannerMap.Default();
        }

        LordBannerCulturesConfig parsed;
        try
        {
            parsed = JsonConvert.DeserializeObject<LordBannerCulturesConfig>(File.ReadAllText(path), SerializerSettings)
                ?? new LordBannerCulturesConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"LordBannerCulturesConfigProvider: Failed to parse lord_banner_cultures.json: {ex.Message}");
            return LordBannerMap.Default();
        }

        if (parsed.Cultures == null)
        {
            _logger.LogWarning("LordBannerCulturesConfigProvider: the cultures section is null, using the built-in banner map");
            return LordBannerMap.Default();
        }

        return Validate(parsed.Cultures);
    }

    private LordBannerMap Validate(Dictionary<string, List<string?>?> rows)
    {
        var rejected = false;
        var kept = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var pair in rows)
        {
            var key = LordBannerMap.Normalize(pair.Key);
            if (key == null)
            {
                _logger.LogWarning("LordBannerCulturesConfigProvider: a culture row has an empty key, dropping it");
                rejected = true;
                continue;
            }
            if (kept.ContainsKey(key))
            {
                _logger.LogWarning($"LordBannerCulturesConfigProvider: cultures['{key}'] is a duplicate after trimming and lower-casing, keeping the first row");
                rejected = true;
                continue;
            }
            var values = ValidateValues(key, pair.Value);
            if (values == null)
            {
                rejected = true;
                continue;
            }
            kept[key] = values;
        }

        if (rejected)
            _logger.LogWarning("LordBannerCulturesConfigProvider: lord_banner_cultures.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo($"LordBannerCulturesConfigProvider: Loaded lord_banner_cultures.json ({kept.Count} culture row(s))");

        return new LordBannerMap(kept);
    }

    private IReadOnlyList<string>? ValidateValues(string key, List<string?>? raw)
    {
        if (raw == null)
        {
            _logger.LogWarning($"LordBannerCulturesConfigProvider: cultures['{key}'] is null, dropping the row");
            return null;
        }

        var values = new List<string>();
        foreach (var entry in raw)
        {
            var value = LordBannerMap.Normalize(entry);
            if (value == null || !LordBannerMap.IsBannerCulture(value))
            {
                _logger.LogWarning($"LordBannerCulturesConfigProvider: cultures['{key}'] lists '{entry}', which is not one of {string.Join(", ", LordBannerMap.BannerCultures)}, dropping the row");
                return null;
            }
            if (!values.Contains(value))
                values.Add(value);
        }
        return values;
    }

    private sealed class LordBannerCulturesConfig
    {
        [JsonProperty("cultures")]
        public Dictionary<string, List<string?>?>? Cultures { get; set; }
    }
}
