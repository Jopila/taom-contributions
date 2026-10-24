using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.UnlookedForAid;

/// <summary>
/// Validating loader for <c>unlooked_for_aid/unlooked_for_aid.json</c>: <c>{ "MinStrengthRatio": 0.5 }</c>
/// (csharp-architecture.md "Config Providers MUST Validate"). The ratio must be finite, at least 0 and below 1 (the
/// finite check comes first: NaN passes a plain range test). A missing key or a bad value reverts to the default with a
/// warning, then one summary warning. A missing file gives the default and a warning, malformed JSON the default and an
/// error. Loaded once per process (Reuse.Singleton), on first read; the service reads it in its constructor on the main
/// thread. An edit needs a full game restart. The load never throws.
/// </summary>
public sealed class UnlookedForAidConfigProvider : IUnlookedForAidConfigProvider
{
    internal const float DefaultMinStrengthRatio = 0.5f;
    private const string Tag = "UnlookedForAidConfigProvider";

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<float> _ratio;

    public UnlookedForAidConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _ratio = new Lazy<float>(Load);
    }

    public float MinStrengthRatio => _ratio.Value;

    private float Load()
    {
        try
        {
            var path = Path.Combine(_pathService.ModuleDataPath, "unlooked_for_aid", "unlooked_for_aid.json");
            if (!File.Exists(path))
            {
                _logger.LogWarning($"{Tag}: unlooked_for_aid.json not found at {path}, using the default ratio {Format(DefaultMinStrengthRatio)}");
                return DefaultMinStrengthRatio;
            }

            RawFile? raw;
            try
            {
                raw = JsonConvert.DeserializeObject<RawFile>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                _logger.LogError($"{Tag}: Failed to parse unlooked_for_aid.json: {ex.Message}");
                return DefaultMinStrengthRatio;
            }

            return Validate(raw?.MinStrengthRatio);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag}: unlooked_for_aid.json could not be read ({ex.GetType().Name}: {ex.Message})");
            return DefaultMinStrengthRatio;
        }
    }

    private float Validate(double? value)
    {
        if (value == null)
            return Revert("'MinStrengthRatio' is missing");
        if (!FiniteFloatValidator.IsFinite(value.Value))
            return Revert("'MinStrengthRatio' is not a finite number");
        var asFloat = (float)value.Value;
        if (!FiniteFloatValidator.IsFinite(asFloat) || asFloat < 0f || asFloat >= 1f)
            return Revert("'MinStrengthRatio' must be at least 0 and below 1");

        _logger.LogInfo($"{Tag}: loaded unlooked_for_aid.json, MinStrengthRatio {Format(asFloat)}");
        return asFloat;
    }

    private float Revert(string why)
    {
        _logger.LogWarning($"{Tag}: {why}, so the default ratio {Format(DefaultMinStrengthRatio)} applies");
        _logger.LogWarning($"{Tag}: unlooked_for_aid.json held an invalid value; the default was used (see the warning above)");
        return DefaultMinStrengthRatio;
    }

    private static string Format(float value) => value.ToString(CultureInfo.InvariantCulture);

    private sealed class RawFile
    {
        public double? MinStrengthRatio { get; set; }
    }
}
