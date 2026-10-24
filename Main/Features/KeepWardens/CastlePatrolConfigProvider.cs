using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.KeepWardens;

/// <summary>
/// Validating loader for <c>configs/castle_patrols.json</c>:
/// <c>{ "spawnDays": [10, 8, 6], "sizeLimits": [15, 20, 25] }</c> (csharp-architecture.md "Config Providers MUST Validate").
/// Each field reverts to its default on its own when it is missing, not an array of three, or holds a value that is not a
/// number, is not finite, is zero or negative, or is above its maximum (60 days, 500 troops). Size limits must be JSON
/// integers: a fractional one is refused rather than rounded. One summary warning names every field that reverted; a missing
/// file or text that is not a JSON object reverts both. The load never throws (a faulted Lazy rethrows for ever) and happens
/// once per process, so an edit needs a full restart.
/// </summary>
public sealed class CastlePatrolConfigProvider : ICastlePatrolConfigProvider
{
    private const string Tag = "CastlePatrolConfigProvider";
    private const float DaysMax = 60f;
    private const int SizeMax = 500;
    private const int Levels = 3;

    private static readonly float[] DefaultDays = { 10f, 8f, 6f };
    private static readonly int[] DefaultSizes = { 15, 20, 25 };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<(float[] Days, int[] Sizes)> _loaded;

    public CastlePatrolConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _loaded = new Lazy<(float[], int[])>(Load);
    }

    public IReadOnlyList<float> SpawnDays => _loaded.Value.Days;

    public IReadOnlyList<int> SizeLimits => _loaded.Value.Sizes;

    private (float[], int[]) Load()
    {
        try
        {
            var path = Path.Combine(_pathService.ModuleDataPath, "configs", "castle_patrols.json");
            if (!File.Exists(path))
                return Fallback($"castle_patrols.json not found at {path}");

            JToken root;
            try
            {
                root = JToken.Parse(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                return Fallback($"castle_patrols.json could not be read ({ex.GetType().Name}: {ex.Message})");
            }

            if (!(root is JObject json))
                return Fallback("castle_patrols.json is not a JSON object");

            var problems = new List<string>();
            var days = ReadDays(json, problems) ?? DefaultDays;
            var sizes = ReadSizes(json, problems) ?? DefaultSizes;
            if (problems.Count > 0)
                _logger.LogWarning($"{Tag}: reverted to the default for {string.Join("; ", problems)}");
            else
                _logger.LogInfo($"{Tag}: loaded castle_patrols.json");
            return (days, sizes);
        }
        catch (Exception ex)
        {
            return Fallback($"castle_patrols.json could not be read ({ex.GetType().Name}: {ex.Message})");
        }
    }

    private static float[]? ReadDays(JObject json, List<string> problems)
    {
        if (!(json.TryGetValue("spawnDays", out var token) && token is JArray array) || array.Count != Levels)
            return Reject<float>(problems, "spawnDays", "is not an array of three numbers");

        var values = new float[Levels];
        for (var i = 0; i < Levels; i++)
        {
            if (array[i].Type != JTokenType.Float && array[i].Type != JTokenType.Integer)
                return Reject<float>(problems, "spawnDays", "holds a value that is not a number");
            double value;
            try
            {
                value = array[i].Value<double>();
            }
            catch (Exception ex) when (ex is OverflowException || ex is InvalidCastException)
            {
                return Reject<float>(problems, "spawnDays", "holds a value out of range");
            }

            if (!FiniteFloatValidator.IsFinite(value))
                return Reject<float>(problems, "spawnDays", "holds a value that is not finite");
            var asFloat = (float)value;
            if (!FiniteFloatValidator.IsFinite(asFloat) || asFloat <= 0f || asFloat > DaysMax)
                return Reject<float>(problems, "spawnDays", "holds a value that is not above 0 and at most 60");
            values[i] = asFloat;
        }

        return values;
    }

    private static int[]? ReadSizes(JObject json, List<string> problems)
    {
        if (!(json.TryGetValue("sizeLimits", out var token) && token is JArray array) || array.Count != Levels)
            return Reject<int>(problems, "sizeLimits", "is not an array of three numbers");

        var values = new int[Levels];
        for (var i = 0; i < Levels; i++)
        {
            if (array[i].Type != JTokenType.Integer)
                return Reject<int>(problems, "sizeLimits", "holds a value that is not a whole number");
            long value;
            try
            {
                value = array[i].Value<long>();
            }
            catch (Exception ex) when (ex is OverflowException || ex is InvalidCastException)
            {
                return Reject<int>(problems, "sizeLimits", "holds a value out of range");
            }

            if (value < 1 || value > SizeMax)
                return Reject<int>(problems, "sizeLimits", $"holds a value outside 1 to {SizeMax}");
            values[i] = (int)value;
        }

        return values;
    }

    private static T[]? Reject<T>(List<string> problems, string field, string why)
    {
        problems.Add($"'{field}' ({why})");
        return null;
    }

    private (float[], int[]) Fallback(string reason)
    {
        _logger.LogWarning($"{Tag}: {reason}; the spawn days and size limits revert to the defaults (spawnDays and sizeLimits)");
        return (DefaultDays, DefaultSizes);
    }
}
