using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.PosternGate;

public interface IPosternGateConfigProvider
{
    /// <summary>The gold cost of building level 1, 2 and 3. Three whole numbers from 1 to 20000, not decreasing.</summary>
    IReadOnlyList<int> Costs { get; }

    /// <summary>The share of troop losses each level removes. Three finite numbers from 0 to 0.85, not decreasing.</summary>
    IReadOnlyList<float> Reductions { get; }
}

/// <summary>
/// Validating loader for <c>settlement_buildings/postern_gate.json</c>:
/// <c>{ "costs": [400, 700, 1000], "reductions": [0.50, 0.67, 0.75] }</c> (csharp-architecture.md "Config Providers MUST
/// Validate"). Each field reverts to its default on its own when it is missing, not an array of three, holds a value
/// outside its range or an array that decreases; one summary warning names every field that reverted. A missing file or
/// text that is not a JSON object reverts both. Costs must be JSON integers: a fractional cost is refused rather than
/// rounded. The load never throws (a faulted Lazy rethrows for ever) and happens once per process.
/// </summary>
public sealed class PosternGateConfigProvider : IPosternGateConfigProvider
{
    private const string Tag = "PosternGateConfigProvider";
    private const int CostMax = 20000;
    private const float ReductionMax = 0.85f;
    private const int Levels = 3;

    private static readonly int[] DefaultCosts = { 400, 700, 1000 };
    private static readonly float[] DefaultReductions = { 0.50f, 0.67f, 0.75f };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<(int[] Costs, float[] Reductions)> _loaded;

    public PosternGateConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _loaded = new Lazy<(int[], float[])>(Load);
    }

    public IReadOnlyList<int> Costs => _loaded.Value.Costs;

    public IReadOnlyList<float> Reductions => _loaded.Value.Reductions;

    private (int[], float[]) Load()
    {
        try
        {
            var path = Path.Combine(_pathService.ModuleDataPath, "settlement_buildings", "postern_gate.json");
            if (!File.Exists(path))
                return Fallback($"postern_gate.json not found at {path}");

            JToken root;
            try
            {
                root = JToken.Parse(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                return Fallback($"postern_gate.json could not be read ({ex.GetType().Name}: {ex.Message})");
            }

            if (!(root is JObject json))
                return Fallback("postern_gate.json is not a JSON object");

            var problems = new List<string>();
            var costs = ReadCosts(json, problems) ?? DefaultCosts;
            var reductions = ReadReductions(json, problems) ?? DefaultReductions;
            if (problems.Count > 0)
                _logger.LogWarning($"{Tag}: reverted to the default for {string.Join("; ", problems)}");
            else
                _logger.LogInfo($"{Tag}: loaded postern_gate.json");
            return (costs, reductions);
        }
        catch (Exception ex)
        {
            return Fallback($"postern_gate.json could not be read ({ex.GetType().Name}: {ex.Message})");
        }
    }

    private static int[]? ReadCosts(JObject json, List<string> problems)
    {
        if (!(json.TryGetValue("costs", out var token) && token is JArray array) || array.Count != Levels)
            return Reject<int>(problems, "costs", "is not an array of three numbers");

        var values = new int[Levels];
        for (var i = 0; i < Levels; i++)
        {
            if (array[i].Type != JTokenType.Integer)
                return Reject<int>(problems, "costs", "holds a value that is not a whole number");
            long value;
            try
            {
                value = array[i].Value<long>();
            }
            catch (Exception ex) when (ex is OverflowException || ex is InvalidCastException)
            {
                return Reject<int>(problems, "costs", "holds a value out of range");
            }

            if (value < 1 || value > CostMax)
                return Reject<int>(problems, "costs", $"holds a value outside 1 to {CostMax}");
            if (i > 0 && value < values[i - 1])
                return Reject<int>(problems, "costs", "decreases");
            values[i] = (int)value;
        }

        return values;
    }

    private static float[]? ReadReductions(JObject json, List<string> problems)
    {
        if (!(json.TryGetValue("reductions", out var token) && token is JArray array) || array.Count != Levels)
            return Reject<float>(problems, "reductions", "is not an array of three numbers");

        var values = new float[Levels];
        for (var i = 0; i < Levels; i++)
        {
            if (array[i].Type != JTokenType.Float && array[i].Type != JTokenType.Integer)
                return Reject<float>(problems, "reductions", "holds a value that is not a number");
            double value;
            try
            {
                value = array[i].Value<double>();
            }
            catch (Exception ex) when (ex is OverflowException || ex is InvalidCastException)
            {
                return Reject<float>(problems, "reductions", "holds a value out of range");
            }

            if (!FiniteFloatValidator.IsFinite(value))
                return Reject<float>(problems, "reductions", "holds a value that is not finite");
            var asFloat = (float)value;
            if (!FiniteFloatValidator.IsFiniteInRange(asFloat, 0f, ReductionMax))
                return Reject<float>(problems, "reductions", $"holds a value outside 0 to {ReductionMax.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            if (i > 0 && asFloat < values[i - 1])
                return Reject<float>(problems, "reductions", "decreases");
            values[i] = asFloat;
        }

        return values;
    }

    private static T[]? Reject<T>(List<string> problems, string field, string why)
    {
        problems.Add($"'{field}' ({why})");
        return null;
    }

    private (int[], float[]) Fallback(string reason)
    {
        _logger.LogWarning($"{Tag}: {reason}; the costs and reductions revert to the defaults (costs and reductions)");
        return (DefaultCosts, DefaultReductions);
    }
}
