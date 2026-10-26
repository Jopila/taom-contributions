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
/// <c>{ "spawnDays": [10, 8, 6], "sizeLimits": [25, 40, 56], "securityPerDay": [1, 1.5, 2], "guardHousePriorityChance": 0.6 }</c>
/// (csharp-architecture.md "Config Providers MUST Validate").
/// Each field reverts to its default on its own when it is missing, not an array of three (the chance is one number), or holds
/// a value that is not a number, is not finite, or is outside its range: days above 0 to 60, sizes 1 to 500, security 0 to 10,
/// the chance 0 to 1. Size limits must be JSON integers: a fractional one is refused rather than rounded. One summary warning
/// names every field that reverted; a missing file or text that is not a JSON object reverts all four. The load never throws
/// (a faulted Lazy rethrows for ever) and happens once per process, so an edit needs a full restart.
/// </summary>
public sealed class CastlePatrolConfigProvider : ICastlePatrolConfigProvider
{
    private const string Tag = "CastlePatrolConfigProvider";
    private const float DaysMax = 60f;
    private const int SizeMax = 500;
    private const float SecurityMax = 10f;
    private const int Levels = 3;
    private const float DefaultChance = 0.6f;

    private static readonly float[] DefaultDays = { 10f, 8f, 6f };
    private static readonly int[] DefaultSizes = { 25, 40, 56 };
    private static readonly float[] DefaultSecurity = { 1f, 1.5f, 2f };
    private static readonly Loaded Defaults = new(DefaultDays, DefaultSizes, DefaultSecurity, DefaultChance);

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<Loaded> _loaded;

    public CastlePatrolConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _loaded = new Lazy<Loaded>(Load);
    }

    public IReadOnlyList<float> SpawnDays => _loaded.Value.Days;

    public IReadOnlyList<int> SizeLimits => _loaded.Value.Sizes;

    public IReadOnlyList<float> SecurityPerDay => _loaded.Value.Security;

    public float GuardHousePriorityChance => _loaded.Value.Chance;

    private Loaded Load()
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
            var security = ReadSecurity(json, problems) ?? DefaultSecurity;
            var chance = ReadChance(json, problems) ?? DefaultChance;
            if (problems.Count > 0)
                _logger.LogWarning($"{Tag}: reverted to the default for {string.Join("; ", problems)}");
            else
                _logger.LogInfo($"{Tag}: loaded castle_patrols.json");
            return new Loaded(days, sizes, security, chance);
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
            if (!TryReadNumber(array[i], problems, "spawnDays", out var value))
                return null;
            if (value <= 0f || value > DaysMax)
                return Reject<float>(problems, "spawnDays", "holds a value that is not above 0 and at most 60");
            values[i] = value;
        }

        return values;
    }

    private static float[]? ReadSecurity(JObject json, List<string> problems)
    {
        if (!(json.TryGetValue("securityPerDay", out var token) && token is JArray array) || array.Count != Levels)
            return Reject<float>(problems, "securityPerDay", "is not an array of three numbers");

        var values = new float[Levels];
        for (var i = 0; i < Levels; i++)
        {
            if (!TryReadNumber(array[i], problems, "securityPerDay", out var value))
                return null;
            if (!FiniteFloatValidator.IsFiniteInRange(value, 0f, SecurityMax))
                return Reject<float>(problems, "securityPerDay", "holds a value outside 0 to 10");
            values[i] = value;
        }

        return values;
    }

    private static float? ReadChance(JObject json, List<string> problems)
    {
        if (!json.TryGetValue("guardHousePriorityChance", out var token))
        {
            problems.Add("'guardHousePriorityChance' (is missing)");
            return null;
        }

        if (!TryReadNumber(token, problems, "guardHousePriorityChance", out var value))
            return null;
        if (!FiniteFloatValidator.IsFiniteInRange(value, 0f, 1f))
        {
            problems.Add("'guardHousePriorityChance' (is outside 0 to 1)");
            return null;
        }

        return value;
    }

    /// <summary>A finite JSON number as a float; on failure it records the problem and returns false.</summary>
    private static bool TryReadNumber(JToken token, List<string> problems, string field, out float result)
    {
        result = 0f;
        if (token.Type != JTokenType.Float && token.Type != JTokenType.Integer)
        {
            problems.Add($"'{field}' (holds a value that is not a number)");
            return false;
        }

        double value;
        try
        {
            value = token.Value<double>();
        }
        catch (Exception ex) when (ex is OverflowException || ex is InvalidCastException)
        {
            problems.Add($"'{field}' (holds a value out of range)");
            return false;
        }

        var asFloat = (float)value;
        if (!FiniteFloatValidator.IsFinite(value) || !FiniteFloatValidator.IsFinite(asFloat))
        {
            problems.Add($"'{field}' (holds a value that is not finite)");
            return false;
        }

        result = asFloat;
        return true;
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

    private Loaded Fallback(string reason)
    {
        _logger.LogWarning($"{Tag}: {reason}; every field reverts to its default (spawnDays, sizeLimits, securityPerDay and guardHousePriorityChance)");
        return Defaults;
    }

    private sealed record Loaded(float[] Days, int[] Sizes, float[] Security, float Chance);
}
