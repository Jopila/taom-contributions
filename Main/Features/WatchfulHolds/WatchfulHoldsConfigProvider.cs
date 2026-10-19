using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.WatchfulHolds;

/// <summary>
/// Loads and validates <c>watchful_holds/watchful_holds.json</c> (AlignmentDesertionConfigProvider for the flat
/// fields, CultureDoctrineConfigProvider for the culture table). A missing file gives the compiled defaults with a
/// warning, a parse failure the same with an error, and a parseable but invalid field reverts alone to its default with
/// a warning (csharp-architecture.md "Config Providers MUST Validate"). Every float goes through
/// <see cref="FiniteFloatValidator"/> before any range check, because NaN passes a bare comparison. Cached for the
/// process (Reuse.Singleton): an edit needs a full restart.
/// </summary>
public sealed class WatchfulHoldsConfigProvider : IWatchfulHoldsConfigProvider
{
    private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<WatchfulHoldsConfig> _config;
    private bool _rejected;

    public WatchfulHoldsConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<WatchfulHoldsConfig>(Load);
    }

    public WatchfulHoldsConfig GetConfig() => _config.Value;

    private WatchfulHoldsConfig Load()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "watchful_holds", "watchful_holds.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"WatchfulHoldsConfigProvider: watchful_holds.json not found at {path}, using the compiled defaults");
            return new WatchfulHoldsConfig();
        }

        WatchfulHoldsConfig parsed;
        try
        {
            parsed = JsonConvert.DeserializeObject<WatchfulHoldsConfig>(File.ReadAllText(path), SerializerSettings)
                ?? new WatchfulHoldsConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"WatchfulHoldsConfigProvider: Failed to parse watchful_holds.json: {ex.Message}");
            return new WatchfulHoldsConfig();
        }

        return Validate(parsed);
    }

    private WatchfulHoldsConfig Validate(WatchfulHoldsConfig parsed)
    {
        _rejected = false;
        var d = new WatchfulHoldsConfig();
        var sanitized = new WatchfulHoldsConfig
        {
            Radius = Radius(parsed.Radius, d.Radius),
            MinGarrison = Count("minGarrison", parsed.MinGarrison, WatchfulHoldsConfig.MaxCount, d.MinGarrison),
            MinPartySize = Count("minPartySize", parsed.MinPartySize, WatchfulHoldsConfig.MaxCount, d.MinPartySize),
            PlayerChancePerHour = Float("playerChancePerHour", parsed.PlayerChancePerHour, 0f, 1f, d.PlayerChancePerHour),
            AiChancePerHour = Float("aiChancePerHour", parsed.AiChancePerHour, 0f, 1f, d.AiChancePerHour),
            WoundFraction = Float("woundFraction", parsed.WoundFraction, 0f, 1f, d.WoundFraction),
            WoundExtraMax = Count("woundExtraMax", parsed.WoundExtraMax, WatchfulHoldsConfig.MaxExtra, d.WoundExtraMax),
            KillFraction = Float("killFraction", parsed.KillFraction, 0f, 1f, d.KillFraction),
            KillExtraMax = Count("killExtraMax", parsed.KillExtraMax, WatchfulHoldsConfig.MaxExtra, d.KillExtraMax),
            FoodLossFraction = Float("foodLossFraction", parsed.FoodLossFraction, 0f, 1f, d.FoodLossFraction),
            MoraleLoss = Float("moraleLoss", parsed.MoraleLoss, 0f, WatchfulHoldsConfig.MaxMeter, d.MoraleLoss),
            CohesionLoss = Float("cohesionLoss", parsed.CohesionLoss, 0f, WatchfulHoldsConfig.MaxMeter, d.CohesionLoss),
            CohesionFloor = Float("cohesionFloor", parsed.CohesionFloor, 0f, WatchfulHoldsConfig.MaxMeter, d.CohesionFloor),
            HornCooldownHours = HornCooldown(parsed.HornCooldownHours, d.HornCooldownHours),
            Cultures = Cultures(parsed.Cultures),
        };

        if (_rejected)
            _logger.LogWarning("WatchfulHoldsConfigProvider: watchful_holds.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo($"WatchfulHoldsConfigProvider: Loaded watchful_holds.json ({sanitized.Cultures.Count} culture row(s) including default)");
        return sanitized;
    }

    private float Radius(float value, float fallback)
    {
        // Finite first, then strictly above zero and at most the cap: a zero radius is a feature that never fires.
        if (FiniteFloatValidator.IsFiniteInRange(value, 0f, WatchfulHoldsConfig.MaxRadius) && value > 0f)
            return value;
        return Reject("radius", value, $"a finite value above 0 and at most {WatchfulHoldsConfig.MaxRadius}", fallback);
    }

    private float HornCooldown(float value, float fallback)
    {
        if (FiniteFloatValidator.IsFiniteAtLeast(value, 0f))
            return value;
        return Reject("hornCooldownHours", value, "a finite value of at least 0", fallback);
    }

    private float Float(string name, float value, float min, float max, float fallback)
    {
        if (FiniteFloatValidator.IsFiniteInRange(value, min, max))
            return value;
        return Reject(name, value, $"a finite value in [{min}, {max}]", fallback);
    }

    private float Reject(string name, float value, string expected, float fallback)
    {
        _logger.LogWarning($"WatchfulHoldsConfigProvider: {name} = {value} is not {expected}, reverting to {fallback}");
        _rejected = true;
        return fallback;
    }

    private int Count(string name, int value, int max, int fallback)
    {
        if (value >= 0 && value <= max)
            return value;
        _logger.LogWarning($"WatchfulHoldsConfigProvider: {name} = {value} is not in [0, {max}], reverting to {fallback}");
        _rejected = true;
        return fallback;
    }

    private Dictionary<string, WatchfulCultureRow> Cultures(Dictionary<string, WatchfulCultureRow>? rows)
    {
        if (rows == null)
        {
            _logger.LogWarning("WatchfulHoldsConfigProvider: cultures is null, using the compiled culture table");
            _rejected = true;
            return WatchfulHoldsConfig.DefaultCultures();
        }

        var compiled = WatchfulHoldsConfig.DefaultCultures();
        var kept = new Dictionary<string, WatchfulCultureRow>(StringComparer.Ordinal);
        foreach (var pair in rows)
        {
            var key = pair.Key?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(key))
            {
                _logger.LogWarning("WatchfulHoldsConfigProvider: a culture row has an empty culture key, dropping it");
                _rejected = true;
                continue;
            }
            if (pair.Value == null)
            {
                _logger.LogWarning($"WatchfulHoldsConfigProvider: cultures['{key}'] is null, dropping the row");
                _rejected = true;
                continue;
            }

            compiled.TryGetValue(key!, out var compiledRow);
            kept[key!] = new WatchfulCultureRow
            {
                ChanceMultiplier = Multiplier(key!, "chanceMultiplier", pair.Value.ChanceMultiplier, compiledRow?.ChanceMultiplier ?? 1f),
                LossMultiplier = Multiplier(key!, "lossMultiplier", pair.Value.LossMultiplier, compiledRow?.LossMultiplier ?? 1f),
            };
        }

        if (!kept.ContainsKey(WatchfulHoldsConfig.DefaultCultureKey))
        {
            _logger.LogWarning("WatchfulHoldsConfigProvider: cultures has no 'default' row, adding one at 1 and 1");
            _rejected = true;
            kept[WatchfulHoldsConfig.DefaultCultureKey] = new WatchfulCultureRow();
        }
        return kept;
    }

    private float Multiplier(string culture, string name, float value, float fallback)
    {
        if (FiniteFloatValidator.IsFiniteInRange(value, 0f, WatchfulHoldsConfig.MaxMultiplier))
            return value;
        return Reject($"cultures['{culture}'].{name}", value, $"a finite value in [0, {WatchfulHoldsConfig.MaxMultiplier}]", fallback);
    }
}
