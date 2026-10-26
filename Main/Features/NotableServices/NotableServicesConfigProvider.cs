using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.NotableServices;

/// <summary>
/// Loads and validates <c>notable_services/notable_services.json</c>. A missing or unparseable file falls back to the
/// compiled defaults; a value that is NaN, infinite or outside its range reverts to its default with a warning, and one
/// summary warning follows. Cached for the process (Reuse.Singleton), so a JSON edit needs a full restart.
/// </summary>
public class NotableServicesConfigProvider : INotableServicesConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<NotableServicesConfig> _config;

    public NotableServicesConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<NotableServicesConfig>(LoadConfig);
    }

    public NotableServicesConfig GetConfig() => _config.Value;

    private NotableServicesConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "notable_services", "notable_services.json");
        if (!File.Exists(path))
        {
            _logger.LogInfo($"NotableServicesConfigProvider: notable_services.json not found at {path}, using defaults");
            return new NotableServicesConfig();
        }

        NotableServicesConfig parsed;
        try
        {
            parsed = JsonConvert.DeserializeObject<NotableServicesConfig>(File.ReadAllText(path)) ?? new NotableServicesConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"NotableServicesConfigProvider: Failed to parse notable_services.json: {ex.Message}");
            return new NotableServicesConfig();
        }

        return Validate(parsed);
    }

    private NotableServicesConfig Validate(NotableServicesConfig parsed)
    {
        var defaults = new NotableServicesConfig();
        var rejected = false;

        // Checked rejects non-finite values first: NaN passes a bare "< min || > max" test and would poison every price and gain.
        var sanitized = new NotableServicesConfig
        {
            IntroducePrice = Checked("introducePrice", parsed.IntroducePrice, 0f, 100000f, defaults.IntroducePrice, ref rejected),
            RevealPrice = Checked("revealPrice", parsed.RevealPrice, 0f, 100000f, defaults.RevealPrice, ref rejected),
            RevealCount = Checked("revealCount", parsed.RevealCount, 1f, 12f, defaults.RevealCount, ref rejected),
            RevealMaxDiagonalFraction = Checked("revealMaxDiagonalFraction", parsed.RevealMaxDiagonalFraction, 0f, 1f, defaults.RevealMaxDiagonalFraction, ref rejected, minExclusive: true),
            RevealMinRelation = Checked("revealMinRelation", parsed.RevealMinRelation, -100f, 100f, defaults.RevealMinRelation, ref rejected),
            GainPerMan = Checked("gainPerMan", parsed.GainPerMan, 0f, 10f, defaults.GainPerMan, ref rejected),
            MaxGainPerGift = Checked("maxGainPerGift", parsed.MaxGainPerGift, 0f, 100f, defaults.MaxGainPerGift, ref rejected),
            MilitiaPerTroop = Checked("militiaPerTroop", parsed.MilitiaPerTroop, 0f, 10f, defaults.MilitiaPerTroop, ref rejected),
            HearthPerPrisoner = Checked("hearthPerPrisoner", parsed.HearthPerPrisoner, 0f, 10f, defaults.HearthPerPrisoner, ref rejected),
            ProsperityPerPrisoner = Checked("prosperityPerPrisoner", parsed.ProsperityPerPrisoner, 0f, 100f, defaults.ProsperityPerPrisoner, ref rejected),
            RogueryXpPerPrisoner = Checked("rogueryXpPerPrisoner", parsed.RogueryXpPerPrisoner, 0f, 1000f, defaults.RogueryXpPerPrisoner, ref rejected),
            MaxMenPerGift = Checked("maxMenPerGift", parsed.MaxMenPerGift, 1f, 1000f, defaults.MaxMenPerGift, ref rejected),
        };

        if (rejected)
            _logger.LogWarning("NotableServicesConfigProvider: notable_services.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("NotableServicesConfigProvider: Loaded notable_services.json");

        return sanitized;
    }

    private float Checked(string key, float value, float min, float max, float fallback, ref bool rejected, bool minExclusive = false)
    {
        if (FiniteFloatValidator.IsFiniteInRange(value, min, max) && !(minExclusive && value <= min))
            return value;
        _logger.LogWarning($"NotableServicesConfigProvider: {key}={value} not finite or outside {(minExclusive ? "(" : "[")}{min},{max}], reverting to default {fallback}");
        rejected = true;
        return fallback;
    }
}
