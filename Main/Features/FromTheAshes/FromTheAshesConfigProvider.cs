using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.FromTheAshes;

/// <summary>One job's numbers. The materials are item ids: hardwood, clay, tools and iron (Iron Ore).</summary>
public sealed class AshesVariantConfig
{
    /// <summary>Whole job length in campaign hours. Finite, 1 to 168.</summary>
    public float Hours { get; set; }

    /// <summary>Healthy troops (prisoners for the thrall variant) that must be in the party. Never consumed. 0 to 500.</summary>
    public int Labour { get; set; }

    public int Hardwood { get; set; }

    public int Clay { get; set; }

    public int Tools { get; set; }

    public int Iron { get; set; }

    /// <summary>Relation gained with each notable present at the end. 0 to 20.</summary>
    public int NotableRelation { get; set; }

    /// <summary>Relation gained with the village's owner at the end. Rebuild and thralls only. 0 to 20.</summary>
    public int OwnerRelation { get; set; }

    /// <summary>Athletics XP for each hero in the party at the end. 0 to 5000.</summary>
    public int AthleticsXp { get; set; }

    /// <summary>Relation change with each notable when the player stops a rebuild early. Rebuild and thralls only. -20 to 0.</summary>
    public int StopPenalty { get; set; }

    /// <summary>Construction points added to the fief's project each hour. Project only. Finite, 0 to 1000.</summary>
    public float ConstructionPerHour { get; set; }

    public AshesVariantConfig Clone() => (AshesVariantConfig)MemberwiseClone();
}

public interface IFromTheAshesConfigProvider
{
    /// <summary>The free player's village rebuild.</summary>
    AshesVariantConfig Rebuild { get; }

    /// <summary>The evil player's village rebuild with captive labour.</summary>
    AshesVariantConfig Thralls { get; }

    /// <summary>Help with a fief's construction project.</summary>
    AshesVariantConfig Project { get; }
}

/// <summary>
/// Validating loader for <c>settlement_buildings/from_the_ashes.json</c>: three objects (<c>rebuild</c>, <c>thralls</c>,
/// <c>project</c>). Eight fields are shared; <c>ownerRelation</c> and <c>stopPenalty</c> belong to the two rebuild objects and
/// <c>constructionPerHour</c> to <c>project</c>, and a field outside its variant is neither read nor required
/// (csharp-architecture.md "Config Providers MUST Validate"). Each field reverts to its default on its own when it is missing, the wrong JSON type, not
/// finite or outside its range; whole-number fields refuse a fractional value rather than round it. One summary warning
/// names every field that reverted. A missing file or text that is not a JSON object reverts everything. The load never
/// throws (a faulted Lazy rethrows for ever) and happens once per process.
/// </summary>
public sealed class FromTheAshesConfigProvider : IFromTheAshesConfigProvider
{
    private const string Tag = "FromTheAshesConfigProvider";

    /// <summary>Which objects of the file carry a field.</summary>
    private enum Scope
    {
        Shared,
        RebuildOnly,
        ProjectOnly,
    }

    private sealed class Field
    {
        public Field(string name, bool whole, double min, double max, Action<AshesVariantConfig, double> set, Scope scope = Scope.Shared)
        {
            Name = name;
            Whole = whole;
            Min = min;
            Max = max;
            Set = set;
            Scope = scope;
        }

        public Scope Scope { get; }

        public string Name { get; }
        public bool Whole { get; }
        public double Min { get; }
        public double Max { get; }
        public Action<AshesVariantConfig, double> Set { get; }
    }

    private static readonly Field[] Fields =
    {
        new("hours", false, 1, AshesJob.MaxHours, (c, v) => c.Hours = (float)v),
        new("labour", true, 0, 500, (c, v) => c.Labour = (int)v),
        new("hardwood", true, 0, 1000, (c, v) => c.Hardwood = (int)v),
        new("clay", true, 0, 1000, (c, v) => c.Clay = (int)v),
        new("tools", true, 0, 1000, (c, v) => c.Tools = (int)v),
        new("iron", true, 0, 1000, (c, v) => c.Iron = (int)v),
        new("notableRelation", true, 0, 20, (c, v) => c.NotableRelation = (int)v),
        new("ownerRelation", true, 0, 20, (c, v) => c.OwnerRelation = (int)v, Scope.RebuildOnly),
        new("athleticsXp", true, 0, 5000, (c, v) => c.AthleticsXp = (int)v),
        new("stopPenalty", true, -20, 0, (c, v) => c.StopPenalty = (int)v, Scope.RebuildOnly),
        new("constructionPerHour", false, 0, 1000, (c, v) => c.ConstructionPerHour = (float)v, Scope.ProjectOnly),
    };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<(AshesVariantConfig Rebuild, AshesVariantConfig Thralls, AshesVariantConfig Project)> _loaded;

    public FromTheAshesConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _loaded = new Lazy<(AshesVariantConfig, AshesVariantConfig, AshesVariantConfig)>(Load);
    }

    public AshesVariantConfig Rebuild => _loaded.Value.Rebuild;

    public AshesVariantConfig Thralls => _loaded.Value.Thralls;

    public AshesVariantConfig Project => _loaded.Value.Project;

    /// <summary>The numbers the shipped file carries. Fresh objects each call, so a revert never aliases a loaded one.</summary>
    internal static AshesVariantConfig DefaultRebuild() => new()
    {
        Hours = 24, Labour = 20, Hardwood = 40, NotableRelation = 3, OwnerRelation = 5, AthleticsXp = 200, StopPenalty = -1,
    };

    internal static AshesVariantConfig DefaultThralls() => new()
    {
        Hours = 24, Labour = 20, Hardwood = 40, NotableRelation = 0, OwnerRelation = 5, AthleticsXp = 200, StopPenalty = 0,
    };

    internal static AshesVariantConfig DefaultProject() => new()
    {
        Hours = 20, Labour = 40, Hardwood = 30, Clay = 10, Tools = 5, Iron = 5, NotableRelation = 1, OwnerRelation = 0,
        AthleticsXp = 150, ConstructionPerHour = 15,
    };

    private (AshesVariantConfig, AshesVariantConfig, AshesVariantConfig) Load()
    {
        try
        {
            var path = Path.Combine(_pathService.ModuleDataPath, "settlement_buildings", "from_the_ashes.json");
            if (!File.Exists(path))
                return Fallback($"from_the_ashes.json not found at {path}");

            if (!(JToken.Parse(File.ReadAllText(path)) is JObject json))
                return Fallback("from_the_ashes.json is not a JSON object");

            var problems = new List<string>();
            var rebuild = ReadVariant(json, "rebuild", DefaultRebuild(), Scope.RebuildOnly, problems);
            var thralls = ReadVariant(json, "thralls", DefaultThralls(), Scope.RebuildOnly, problems);
            var project = ReadVariant(json, "project", DefaultProject(), Scope.ProjectOnly, problems);
            if (problems.Count > 0)
                _logger.LogWarning($"{Tag}: reverted to the default for {string.Join("; ", problems)}");
            else
                _logger.LogInfo($"{Tag}: loaded from_the_ashes.json");
            return (rebuild, thralls, project);
        }
        catch (Exception ex)
        {
            return Fallback($"from_the_ashes.json could not be read ({ex.GetType().Name}: {ex.Message})");
        }
    }

    private static AshesVariantConfig ReadVariant(JObject json, string name, AshesVariantConfig defaults, Scope own, List<string> problems)
    {
        if (!(json.TryGetValue(name, out var token) && token is JObject obj))
        {
            problems.Add($"'{name}' (missing or not an object)");
            return defaults;
        }

        var result = defaults.Clone();
        foreach (var field in Fields)
        {
            if (field.Scope != Scope.Shared && field.Scope != own)
                continue;
            var why = TryRead(obj, field, out var value);
            if (why != null)
                problems.Add($"'{name}.{field.Name}' ({why})");
            else
                field.Set(result, value);
        }

        return result;
    }

    /// <summary>Null when the field read cleanly into <paramref name="value"/>, otherwise why it did not.</summary>
    private static string? TryRead(JObject obj, Field field, out double value)
    {
        value = 0;
        if (!obj.TryGetValue(field.Name, out var token))
            return "missing";
        if (field.Whole ? token.Type != JTokenType.Integer : token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
            return field.Whole ? "is not a whole number" : "is not a number";
        try
        {
            value = field.Whole ? token.Value<long>() : token.Value<double>();
        }
        catch (Exception ex) when (ex is OverflowException || ex is InvalidCastException)
        {
            return "out of range";
        }

        if (!FiniteFloatValidator.IsFinite(value))
            return "is not finite";
        if (value < field.Min || value > field.Max)
            return $"outside {field.Min.ToString(CultureInfo.InvariantCulture)} to {field.Max.ToString(CultureInfo.InvariantCulture)}";
        return null;
    }

    private (AshesVariantConfig, AshesVariantConfig, AshesVariantConfig) Fallback(string reason)
    {
        _logger.LogWarning($"{Tag}: {reason}; every number reverts to the defaults");
        return (DefaultRebuild(), DefaultThralls(), DefaultProject());
    }
}
