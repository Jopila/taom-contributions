using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.TokensOfEsteem.Models;

namespace TAOM.Features.TokensOfEsteem;

/// <summary>
/// Loads and validates <c>gifts/gift_catalog.json</c> (docs/features/tokens-of-esteem.md). The catalog is keyed by item category
/// id, and every id it names is checked against the game once it is up: a category against the item categories, a race against
/// the race names, a culture against the cultures. An id the game does not know is dropped with one log line (a race or culture
/// from its taste group, a category with its entry), never replaced by a guess. Each number is read as an integer and must sit
/// in range (lord base 1 to 10, bonus 1 to 10) or its entry or group is dropped. Nothing about a number defaults, because no
/// number has a sensible default. Every drop is counted, and one summary warning follows when any happened. Built once the
/// registries are ready, then kept for the process (Reuse.Singleton), so an edit needs a full restart.
/// </summary>
public sealed class GiftCatalogProvider : IGiftCatalogProvider
{
    internal const int MinValue = 1;
    internal const int MaxValue = 10;
    private const string FileName = "gift_catalog.json";

    private readonly IPathService _paths;
    private readonly IGiftItemAdapter _registry;
    private readonly IRaceManager _races;
    private readonly IModLogger _logger;
    private GiftCatalog? _catalog;
    private int _drops;

    public GiftCatalogProvider(IPathService paths, IGiftItemAdapter registry, IRaceManager races, IModLogger logger)
    {
        _paths = paths;
        _registry = registry;
        _races = races;
        _logger = logger;
    }

    public GiftCatalog Get()
    {
        if (_catalog != null)
            return _catalog;

        try
        {
            // The categories and races exist only once a campaign is up. Not ready: no catalog yet, and none pinned.
            if (!_registry.IsRegistryReady)
                return GiftCatalog.Empty;

            var path = Path.Combine(_paths.ModuleDataPath, "gifts", FileName);
            if (!File.Exists(path))
            {
                _logger.LogWarning($"[Gifts] {FileName} not found at {path}: no gift is listed.");
                return _catalog = GiftCatalog.Empty;
            }
            return _catalog = Load(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            // Cached empty: a fault is logged once, not on every conversation line. A restart retries.
            _logger.LogError($"[Gifts] loading {FileName} failed: {ex.Message}");
            return _catalog = GiftCatalog.Empty;
        }
    }

    internal GiftCatalog Load(string json)
    {
        JToken root;
        try
        {
            root = JToken.Parse(json);
        }
        catch (JsonException ex)
        {
            _logger.LogError($"[Gifts] {FileName} is not valid JSON: {ex.Message}");
            return GiftCatalog.Empty;
        }

        if (root is not JObject obj || obj["entries"] is not JArray entries)
        {
            _logger.LogWarning($"[Gifts] {FileName} has no \"entries\" array: no gift is listed.");
            return GiftCatalog.Empty;
        }

        _drops = 0;
        var kept = new List<GiftCatalogEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = ReadEntry(entries[i], i, seen);
            if (entry != null)
                kept.Add(entry);
        }

        // Each drop has logged its own line naming the id; one summary tells a reader there is more to find above.
        if (_drops > 0)
            _logger.LogWarning($"[Gifts] {FileName}: {_drops} dropped (an entry, a taste group or an id; see the lines above).");
        _logger.LogInfo($"[Gifts] gift catalog: {kept.Count} of {entries.Count} categories loaded.");
        return new GiftCatalog(kept);
    }

    private void Drop(string line)
    {
        _drops++;
        _logger.LogWarning(line);
    }

    private GiftCatalogEntry? ReadEntry(JToken token, int index, HashSet<string> seen)
    {
        if (token is not JObject obj)
        {
            Drop($"[Gifts] {FileName} entry {index} is not an object: dropped.");
            return null;
        }

        var category = ((obj["category"] as JValue)?.Value as string)?.Trim();
        if (string.IsNullOrEmpty(category))
        {
            Drop($"[Gifts] {FileName} entry {index} names no category: dropped.");
            return null;
        }

        if (!seen.Add(category!))
        {
            Drop($"[Gifts] {FileName} lists category '{category}' twice: the later entry is dropped.");
            return null;
        }

        if (!_registry.IsKnownCategory(category!))
        {
            Drop($"[Gifts] {FileName}: unknown item category '{category}': dropped.");
            return null;
        }

        if (!TryInRange(obj["lordBase"], out var lordBase))
        {
            Drop($"[Gifts] {FileName}: category '{category}' needs an integer lordBase from {MinValue} to {MaxValue}: dropped.");
            return null;
        }

        var tastes = new List<TasteGroup>();
        if (obj["tastes"] is JArray groups)
        {
            for (var g = 0; g < groups.Count; g++)
            {
                var group = ReadGroup(groups[g], category!, g);
                if (group != null)
                    tastes.Add(group);
            }
        }
        return new GiftCatalogEntry(category!, lordBase, tastes);
    }

    private TasteGroup? ReadGroup(JToken token, string category, int index)
    {
        var where = $"category '{category}' taste group {index}";
        if (token is not JObject obj)
        {
            Drop($"[Gifts] {FileName}: {where} is not an object: dropped.");
            return null;
        }

        if (!TryInRange(obj["bonus"], out var bonus))
        {
            Drop($"[Gifts] {FileName}: {where} needs an integer bonus from {MinValue} to {MaxValue}: dropped.");
            return null;
        }

        var races = ReadIds(obj["races"], where, "race", _races.IsValidRaceName);
        var cultures = ReadIds(obj["cultures"], where, "culture", _registry.IsKnownCulture);
        if (races.Count == 0 && cultures.Count == 0)
        {
            Drop($"[Gifts] {FileName}: {where} names no known race or culture: dropped.");
            return null;
        }
        return new TasteGroup(races, cultures, bonus);
    }

    private List<string> ReadIds(JToken? token, string where, string kind, Func<string, bool> isKnown)
    {
        var ids = new List<string>();
        if (token is not JArray array)
            return ids;

        foreach (var item in array)
        {
            var id = ((item as JValue)?.Value as string)?.Trim();
            if (string.IsNullOrEmpty(id))
                Drop($"[Gifts] {FileName}: {where} has a blank or non-text {kind}: dropped.");
            else if (!isKnown(id!))
                Drop($"[Gifts] {FileName}: {where}: unknown {kind} '{id}': dropped.");
            else
                ids.Add(id!);
        }
        return ids;
    }

    // An integer token only: a string or a fraction is a typo, not a number to round or default.
    private static bool TryInRange(JToken? token, out int value)
    {
        value = 0;
        if (token is JValue { Type: JTokenType.Integer, Value: long number } && number >= MinValue && number <= MaxValue)
        {
            value = (int)number;
            return true;
        }
        return false;
    }
}
