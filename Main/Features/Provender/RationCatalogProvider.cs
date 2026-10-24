using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Provender.Models;

namespace TAOM.Features.Provender;

/// <summary>
/// Loads and validates <c>provender/rations.json</c> (docs/features/provender.md). Every item, race and culture it names is
/// checked against the game once a campaign is up: an item the game does not know drops its ration, a race or culture it does
/// not know drops that id (and the ration too when nothing is left to prefer it and it is not the default). Each number is read
/// as an integer and must sit in range (troops per item 1 to 1000, morale 1 to 30) or its ration is dropped: no number has a
/// sensible default. Only one ration may be the default; a second is dropped. Every drop logs its own line and one summary
/// warning follows when any happened. Built once, then kept for the process (Reuse.Singleton), so an edit needs a full restart.
/// </summary>
public sealed class RationCatalogProvider : IRationCatalogProvider
{
    internal const int MinTroopsPerItem = 1;
    internal const int MaxTroopsPerItem = 1000;
    internal const int MinMorale = 1;
    internal const int MaxMorale = 30;
    private const string FileName = "rations.json";

    private readonly IPathService _paths;
    private readonly IProvenderWorldAdapter _world;
    private readonly IRaceManager _races;
    private readonly IModLogger _logger;
    private RationCatalog? _catalog;
    private int _drops;

    public RationCatalogProvider(IPathService paths, IProvenderWorldAdapter world, IRaceManager races, IModLogger logger)
    {
        _paths = paths;
        _world = world;
        _races = races;
        _logger = logger;
    }

    public RationCatalog Get()
    {
        if (_catalog != null)
            return _catalog;

        try
        {
            // The items and cultures exist only once a campaign is up. Not ready: no catalog yet, and none pinned.
            if (!_world.IsRegistryReady)
                return RationCatalog.Empty;

            var path = Path.Combine(_paths.ModuleDataPath, "provender", FileName);
            if (!File.Exists(path))
            {
                _logger.LogWarning($"[Provender] {FileName} not found at {path}: no ration is offered.");
                return _catalog = RationCatalog.Empty;
            }
            return _catalog = Load(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            // Cached empty: a fault is logged once, not on every conversation line. A restart retries.
            _logger.LogError($"[Provender] loading {FileName} failed: {ex.Message}");
            return _catalog = RationCatalog.Empty;
        }
    }

    internal RationCatalog Load(string json)
    {
        JToken root;
        try
        {
            root = JToken.Parse(json);
        }
        catch (JsonException ex)
        {
            _logger.LogError($"[Provender] {FileName} is not valid JSON: {ex.Message}");
            return RationCatalog.Empty;
        }

        if (root is not JObject obj || obj["rations"] is not JArray rations)
        {
            _logger.LogWarning($"[Provender] {FileName} has no \"rations\" array: no ration is offered.");
            return RationCatalog.Empty;
        }

        _drops = 0;
        var kept = new List<RationEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var hasDefault = false;
        for (var i = 0; i < rations.Count; i++)
        {
            var entry = ReadEntry(rations[i], i, seen, hasDefault);
            if (entry == null)
                continue;
            kept.Add(entry);
            hasDefault |= entry.IsDefault;
        }

        // Each drop has logged its own line naming the id; one summary tells a reader there is more to find above.
        if (_drops > 0)
            _logger.LogWarning($"[Provender] {FileName}: {_drops} dropped (a ration or an id; see the lines above).");
        _logger.LogInfo($"[Provender] rations: {kept.Count} of {rations.Count} loaded.");
        return new RationCatalog(kept);
    }

    private void Drop(string line)
    {
        _drops++;
        _logger.LogWarning(line);
    }

    private RationEntry? ReadEntry(JToken token, int index, HashSet<string> seen, bool hasDefault)
    {
        if (token is not JObject obj)
        {
            Drop($"[Provender] {FileName} ration {index} is not an object: dropped.");
            return null;
        }

        var id = Text(obj["id"]);
        if (string.IsNullOrEmpty(id))
        {
            Drop($"[Provender] {FileName} ration {index} names no id: dropped.");
            return null;
        }
        if (!seen.Add(id!))
        {
            Drop($"[Provender] {FileName} lists ration '{id}' twice: the later one is dropped.");
            return null;
        }

        var item = Text(obj["item"]);
        if (string.IsNullOrEmpty(item) || !_world.IsKnownItem(item!))
        {
            Drop($"[Provender] {FileName}: ration '{id}' names unknown item '{item}': dropped.");
            return null;
        }

        if (!TryInRange(obj["troopsPerItem"], MinTroopsPerItem, MaxTroopsPerItem, out var troopsPerItem))
        {
            Drop($"[Provender] {FileName}: ration '{id}' needs an integer troopsPerItem from {MinTroopsPerItem} to {MaxTroopsPerItem}: dropped.");
            return null;
        }
        if (!TryInRange(obj["morale"], MinMorale, MaxMorale, out var morale))
        {
            Drop($"[Provender] {FileName}: ration '{id}' needs an integer morale from {MinMorale} to {MaxMorale}: dropped.");
            return null;
        }

        var isDefault = false;
        if (obj["default"] is { } flag && flag.Type != JTokenType.Null)
        {
            if (flag is not JValue { Type: JTokenType.Boolean, Value: bool value })
            {
                Drop($"[Provender] {FileName}: ration '{id}' has a \"default\" that is not true or false: dropped.");
                return null;
            }
            isDefault = value;
        }
        if (isDefault && hasDefault)
        {
            Drop($"[Provender] {FileName}: ration '{id}' is a second default: dropped.");
            return null;
        }

        var races = ReadIds(obj["races"], id!, "race", _races.IsValidRaceName);
        var cultures = ReadIds(obj["cultures"], id!, "culture", _world.IsKnownCulture);
        if (!isDefault && races.Count == 0 && cultures.Count == 0)
        {
            Drop($"[Provender] {FileName}: ration '{id}' is not the default and names no known race or culture: dropped.");
            return null;
        }
        return new RationEntry(id!, item!, troopsPerItem, morale, isDefault, races, cultures);
    }

    private List<string> ReadIds(JToken? token, string ration, string kind, Func<string, bool> isKnown)
    {
        var ids = new List<string>();
        if (token == null || token.Type == JTokenType.Null)
            return ids;
        if (token is not JArray array)
        {
            Drop($"[Provender] {FileName}: ration '{ration}': the {kind} list is not an array: ignored.");
            return ids;
        }

        foreach (var item in array)
        {
            var id = Text(item);
            if (string.IsNullOrEmpty(id))
                Drop($"[Provender] {FileName}: ration '{ration}' has a blank or non-text {kind}: dropped.");
            else if (!isKnown(id!))
                Drop($"[Provender] {FileName}: ration '{ration}': unknown {kind} '{id}': dropped.");
            else
                ids.Add(id!);
        }
        return ids;
    }

    private static string? Text(JToken? token) => ((token as JValue)?.Value as string)?.Trim();

    // An integer token only: a string or a fraction is a typo, not a number to round or default.
    private static bool TryInRange(JToken? token, int min, int max, out int value)
    {
        value = 0;
        if (token is JValue { Type: JTokenType.Integer, Value: long number } && number >= min && number <= max)
        {
            value = (int)number;
            return true;
        }
        return false;
    }
}
