using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.Messengers;

public class MessengerConfigProvider : IMessengerConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<MessengerConfig> _config;

    public MessengerConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<MessengerConfig>(LoadConfig);
    }

    public MessengerConfig GetConfig() => _config.Value;

    private MessengerConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "messengers", "messenger_config.json");

        if (!File.Exists(path))
        {
            _logger.LogInfo($"MessengerConfigProvider: messenger_config.json not found at {path}, using defaults");
            return new MessengerConfig();
        }

        MessengerConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<MessengerConfig>(json) ?? new MessengerConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"MessengerConfigProvider: Failed to parse messenger_config.json: {ex.Message}");
            return new MessengerConfig();
        }

        return Validate(parsed);
    }

    private MessengerConfig Validate(MessengerConfig parsed)
    {
        var defaults = new MessengerConfig();
        var rejected = false;

        // Codex review #34: '<' and '>' both fail for NaN, so Checked rejects non-finite values up front; a NaN chance or
        // speed would otherwise poison RollAccident and the travel positions.
        var sanitized = new MessengerConfig
        {
            AccidentChancePerHour = Checked("accidentChancePerHour", parsed.AccidentChancePerHour, 0f, 1f, defaults.AccidentChancePerHour, ref rejected),
            TravelSpeedMultiplier = Checked("travelSpeedMultiplier", parsed.TravelSpeedMultiplier, 0.1f, 10f, defaults.TravelSpeedMultiplier, ref rejected),
            GoldPerTravelDay = Checked("goldPerTravelDay", parsed.GoldPerTravelDay, 0f, 1000f, defaults.GoldPerTravelDay, ref rejected),
            AtWarMultiplier = Checked("atWarMultiplier", parsed.AtWarMultiplier, 1f, 10f, defaults.AtWarMultiplier, ref rejected),
            NeverMetMultiplier = Checked("neverMetMultiplier", parsed.NeverMetMultiplier, 1f, 10f, defaults.NeverMetMultiplier, ref rejected),
            BesiegedMultiplier = Checked("besiegedMultiplier", parsed.BesiegedMultiplier, 1f, 10f, defaults.BesiegedMultiplier, ref rejected),
        };

        if (rejected)
            _logger.LogWarning("MessengerConfigProvider: messenger_config.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("MessengerConfigProvider: Loaded messenger_config.json");

        return sanitized;
    }

    private float Checked(string key, float value, float min, float max, float fallback, ref bool rejected)
    {
        if (FiniteFloatValidator.IsFiniteInRange(value, min, max))
            return value;
        _logger.LogWarning($"MessengerConfigProvider: {key}={value} not finite or outside [{min},{max}], reverting to default {fallback}");
        rejected = true;
        return fallback;
    }
}
