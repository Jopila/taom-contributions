using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.LordCourtesies;

/// <summary>
/// Loads and validates <c>lord_courtesies/lord_courtesies.json</c>. A missing or unparseable file falls back to
/// the compiled defaults; each value failing its rule (NaN or infinity first, then range) reverts to its default
/// with a warning, and one summary warning follows. Cached for the process (Reuse.Singleton), so a JSON edit needs
/// a full restart.
/// </summary>
public class LordCourtesiesConfigProvider : ILordCourtesiesConfigProvider
{
    internal const float MaxMagnitude = 1_000_000f;
    internal const float MaxRelation = 100f;

    private static LordCourtesiesField F(string name, LordCourtesiesFieldKind kind,
        Func<LordCourtesiesConfig, float> get, Action<LordCourtesiesConfig, float> set) => new(name, kind, get, set);

    internal static readonly IReadOnlyList<LordCourtesiesField> Fields = new[]
    {
        F("revealMinRelation", LordCourtesiesFieldKind.RelationLevel, c => c.RevealMinRelation, (c, v) => c.RevealMinRelation = v),
        F("refusalCost", LordCourtesiesFieldKind.Amount, c => c.RefusalCost, (c, v) => c.RefusalCost = v),
        F("landlessCost", LordCourtesiesFieldKind.Amount, c => c.LandlessCost, (c, v) => c.LandlessCost = v),
    };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<LordCourtesiesConfig> _config;

    public LordCourtesiesConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<LordCourtesiesConfig>(LoadConfig);
    }

    public LordCourtesiesConfig GetConfig() => _config.Value;

    private LordCourtesiesConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "lord_courtesies", "lord_courtesies.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"LordCourtesiesConfigProvider: lord_courtesies.json not found at {path}, using defaults");
            return new LordCourtesiesConfig();
        }

        LordCourtesiesConfig parsed;
        try
        {
            parsed = JsonConvert.DeserializeObject<LordCourtesiesConfig>(File.ReadAllText(path)) ?? new LordCourtesiesConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"LordCourtesiesConfigProvider: Failed to parse lord_courtesies.json: {ex.Message}");
            return new LordCourtesiesConfig();
        }

        return Validate(parsed);
    }

    private LordCourtesiesConfig Validate(LordCourtesiesConfig parsed)
    {
        var defaults = new LordCourtesiesConfig();
        var rejected = false;
        foreach (var field in Fields)
        {
            var value = field.Get(parsed);
            if (IsValid(field.Kind, value))
                continue;
            var fallback = field.Get(defaults);
            _logger.LogWarning($"LordCourtesiesConfigProvider: {field.Name}={value} breaks its {field.Kind} rule, reverting to default {fallback}");
            field.Set(parsed, fallback);
            rejected = true;
        }

        if (rejected)
            _logger.LogWarning("LordCourtesiesConfigProvider: lord_courtesies.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("LordCourtesiesConfigProvider: Loaded lord_courtesies.json");
        return parsed;
    }

    private static bool IsValid(LordCourtesiesFieldKind kind, float value) => kind switch
    {
        LordCourtesiesFieldKind.RelationLevel => FiniteFloatValidator.IsFiniteInRange(value, -MaxRelation, MaxRelation),
        _ => FiniteFloatValidator.IsFiniteInRange(value, 0f, MaxMagnitude),
    };
}
