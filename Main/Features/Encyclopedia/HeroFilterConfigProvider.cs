using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.Encyclopedia;

/// <summary>
/// Loads and validates <c>ModuleData/encyclopedia/hero_filters.json</c>. A bad group is dropped with a warning, never
/// repaired: the other groups keep working, and a missing or unreadable file leaves the hero filters off.
/// </summary>
public class HeroFilterConfigProvider : IHeroFilterConfigProvider
{
    internal const float MinBound = 18f;
    internal const float MaxBound = 10000f;
    internal const int BoundCount = 4;

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;

    public HeroFilterConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public HeroFilterConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "encyclopedia", "hero_filters.json");
        if (!File.Exists(path))
        {
            _logger.LogError($"HeroFilterConfigProvider: hero_filters.json not found: {path}; the encyclopedia hero filters are off.");
            return new HeroFilterConfig();
        }

        try
        {
            var raw = JsonConvert.DeserializeObject<HeroFilterConfig>(File.ReadAllText(path)) ?? new HeroFilterConfig();
            var config = Validate(raw);
            _logger.LogInfo($"Loaded {config.Groups.Count} encyclopedia hero filter groups");
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError($"HeroFilterConfigProvider: failed to parse hero_filters.json: {ex.Message}; the encyclopedia hero filters are off.");
            return new HeroFilterConfig();
        }
    }

    private HeroFilterConfig Validate(HeroFilterConfig raw)
    {
        var kept = new List<HeroFilterGroup>();
        var claimedRaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int droppedGroups = 0, droppedRaces = 0;

        foreach (var group in raw.Groups ?? new List<HeroFilterGroup>())
        {
            if (group == null || !HeroFilterRules.IsKnownGroup(group.Id))
            {
                _logger.LogWarning($"HeroFilterConfigProvider: group '{group?.Id}' has no label in the mod; dropped.");
                droppedGroups++;
                continue;
            }
            if (kept.Any(g => g.Id == group.Id))
            {
                _logger.LogWarning($"HeroFilterConfigProvider: group '{group.Id}' appears twice; the later one is dropped.");
                droppedGroups++;
                continue;
            }
            if (!BoundsAreValid(group.AgeUpperBounds))
            {
                _logger.LogWarning($"HeroFilterConfigProvider: group '{group.Id}' needs {BoundCount} finite, strictly increasing ageUpperBounds " +
                    $"above {MinBound} and at most {MaxBound}; dropped.");
                droppedGroups++;
                continue;
            }

            var races = new List<string>();
            foreach (var race in group.Races ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(race))
                {
                    _logger.LogWarning($"HeroFilterConfigProvider: group '{group.Id}' lists an empty race id; dropped.");
                    droppedRaces++;
                }
                else if (!claimedRaces.Add(race))
                {
                    _logger.LogWarning($"HeroFilterConfigProvider: race '{race}' is already in an earlier group; removed from '{group.Id}'.");
                    droppedRaces++;
                }
                else
                {
                    races.Add(race);
                }
            }
            kept.Add(new HeroFilterGroup { Id = group.Id, Races = races, AgeUpperBounds = group.AgeUpperBounds.ToList() });
        }

        var defaultGroup = kept.Any(g => g.Id == raw.DefaultGroup) ? raw.DefaultGroup : (kept.FirstOrDefault()?.Id ?? "");
        if (defaultGroup != raw.DefaultGroup && kept.Count > 0)
            _logger.LogWarning($"HeroFilterConfigProvider: defaultGroup '{raw.DefaultGroup}' is missing or not a valid group; using '{defaultGroup}'.");
        if (droppedGroups + droppedRaces > 0)
            _logger.LogWarning($"HeroFilterConfigProvider: dropped {droppedGroups} group(s) and {droppedRaces} race entry(ies) from hero_filters.json.");

        return new HeroFilterConfig { DefaultGroup = defaultGroup, Groups = kept };
    }

    private static bool BoundsAreValid(List<float>? bounds)
    {
        if (bounds == null || bounds.Count != BoundCount) return false;
        for (var i = 0; i < bounds.Count; i++)
        {
            if (!FiniteFloatValidator.IsFinite(bounds[i]) || bounds[i] <= MinBound || bounds[i] > MaxBound) return false;
            if (i > 0 && bounds[i] <= bounds[i - 1]) return false;
        }
        return true;
    }
}
