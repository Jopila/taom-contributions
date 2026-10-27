using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.InnRounds;

/// <summary>
/// Loads and validates <c>inn_rounds/inn_rounds.json</c>. A missing or unparseable file falls back to the compiled defaults; a
/// value that is NaN, infinite or outside its range reverts to its default with a warning, and one summary warning follows. A
/// minimum above its maximum reverts both. Cached for the process (Reuse.Singleton), so a JSON edit needs a full restart.
/// </summary>
public class InnRoundsConfigProvider : IInnRoundsConfigProvider
{
    internal const string Beer = "beer";
    internal const string Wine = "wine";

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<InnRoundsConfig> _config;

    public InnRoundsConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<InnRoundsConfig>(LoadConfig);
    }

    public InnRoundsConfig GetConfig() => _config.Value;

    private InnRoundsConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "inn_rounds", "inn_rounds.json");
        if (!File.Exists(path))
        {
            _logger.LogInfo($"InnRoundsConfigProvider: inn_rounds.json not found at {path}, using defaults");
            return new InnRoundsConfig();
        }

        InnRoundsConfig parsed;
        try
        {
            parsed = JsonConvert.DeserializeObject<InnRoundsConfig>(File.ReadAllText(path)) ?? new InnRoundsConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"InnRoundsConfigProvider: Failed to parse inn_rounds.json: {ex.Message}");
            return new InnRoundsConfig();
        }

        return Validate(parsed);
    }

    private InnRoundsConfig Validate(InnRoundsConfig parsed)
    {
        var defaults = new InnRoundsConfig();
        var rejected = false;

        var sanitized = new InnRoundsConfig
        {
            MaxRoundsPerDay = Checked("maxRoundsPerDay", parsed.MaxRoundsPerDay, 1f, 20f, defaults.MaxRoundsPerDay, ref rejected),
            MaxPricePerHead = Checked("maxPricePerHead", parsed.MaxPricePerHead, 1f, 1000f, defaults.MaxPricePerHead, ref rejected),
            FallbackPricePerHead = Checked("fallbackPricePerHead", parsed.FallbackPricePerHead, 1f, 1000f, defaults.FallbackPricePerHead, ref rejected),
            MoraleMin = Checked("moraleMin", parsed.MoraleMin, 0f, 100f, defaults.MoraleMin, ref rejected),
            MoraleMax = Checked("moraleMax", parsed.MoraleMax, 0f, 100f, defaults.MoraleMax, ref rejected),
            RenownMin = Checked("renownMin", parsed.RenownMin, 0f, 50f, defaults.RenownMin, ref rejected),
            RenownMax = Checked("renownMax", parsed.RenownMax, 0f, 50f, defaults.RenownMax, ref rejected),
            CharmXpMin = Checked("charmXpMin", parsed.CharmXpMin, 0f, 1000f, defaults.CharmXpMin, ref rejected),
            CharmXpMax = Checked("charmXpMax", parsed.CharmXpMax, 0f, 1000f, defaults.CharmXpMax, ref rejected),
            CultureDrinks = Drinks(parsed.CultureDrinks, ref rejected),
        };

        Ordered("moraleMin", "moraleMax", sanitized.MoraleMin, sanitized.MoraleMax, ref rejected,
            () => { sanitized.MoraleMin = defaults.MoraleMin; sanitized.MoraleMax = defaults.MoraleMax; });
        Ordered("renownMin", "renownMax", sanitized.RenownMin, sanitized.RenownMax, ref rejected,
            () => { sanitized.RenownMin = defaults.RenownMin; sanitized.RenownMax = defaults.RenownMax; });
        Ordered("charmXpMin", "charmXpMax", sanitized.CharmXpMin, sanitized.CharmXpMax, ref rejected,
            () => { sanitized.CharmXpMin = defaults.CharmXpMin; sanitized.CharmXpMax = defaults.CharmXpMax; });

        if (rejected)
            _logger.LogWarning("InnRoundsConfigProvider: inn_rounds.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("InnRoundsConfigProvider: Loaded inn_rounds.json");

        return sanitized;
    }

    private float Checked(string key, float value, float min, float max, float fallback, ref bool rejected)
    {
        if (FiniteFloatValidator.IsFiniteInRange(value, min, max))
            return value;
        _logger.LogWarning($"InnRoundsConfigProvider: {key}={value} not finite or outside [{min},{max}], reverting to default {fallback}");
        rejected = true;
        return fallback;
    }

    private void Ordered(string minKey, string maxKey, float min, float max, ref bool rejected, Action revert)
    {
        if (min <= max)
            return;
        _logger.LogWarning($"InnRoundsConfigProvider: {minKey}={min} is above {maxKey}={max}, reverting both to their defaults");
        rejected = true;
        revert();
    }

    /// <summary>Keeps each culture whose drink is beer or wine (any case); the key is lower-cased. Anything else is dropped with a warning.</summary>
    private Dictionary<string, string> Drinks(Dictionary<string, string>? parsed, ref bool rejected)
    {
        var kept = new Dictionary<string, string>(StringComparer.Ordinal);
        if (parsed == null)
            return kept;

        foreach (var pair in parsed)
        {
            var drink = pair.Value?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(pair.Key) || (drink != Beer && drink != Wine))
            {
                _logger.LogWarning($"InnRoundsConfigProvider: cultureDrinks['{pair.Key}']='{pair.Value}' is not beer or wine, dropped (the culture pours beer)");
                rejected = true;
                continue;
            }
            kept[pair.Key.Trim().ToLowerInvariant()] = drink!;
        }
        return kept;
    }
}
