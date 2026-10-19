using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LordBanners;

/// <summary>
/// Decides which banner items a lord may be given (docs/features/lord-banners.md). The engine keeps an item when
/// its culture is null or the hero's own, so a lord of a TAOM culture, which owns no banner item, gets none.
/// For the six vanilla banner cultures with no row the answer stays the engine's. Any other culture also gets
/// the neutral banners and the banners of the vanilla cultures listed in its row. Generic over the item type
/// with a projection, so the service never names an engine type.
/// </summary>
public sealed class LordBannerService
{
    private readonly ILordBannerCulturesConfigProvider _config;

    public LordBannerService(ILordBannerCulturesConfigProvider config)
    {
        _config = config;
    }

    public List<T> Allowed<T>(string? heroCultureId, int level, IEnumerable<T> items, Func<T, (string? CultureId, int Level)> facts)
    {
        var hero = LordBannerMap.Normalize(heroCultureId);
        var row = RowFor(hero);
        var allowed = new List<T>();
        foreach (var item in items)
        {
            var (itemCultureId, itemLevel) = facts(item);
            if (itemLevel == level && CultureAllowed(LordBannerMap.Normalize(itemCultureId), hero, row))
                allowed.Add(item);
        }
        return allowed;
    }

    private IReadOnlyList<string>? RowFor(string? hero)
    {
        if (hero != null && _config.GetMap().Cultures.TryGetValue(hero, out var row))
            return row;
        return hero != null && !LordBannerMap.IsBannerCulture(hero) ? Array.Empty<string>() : null;
    }

    private static bool CultureAllowed(string? item, string? hero, IReadOnlyList<string>? row)
    {
        if (item == null || item == hero)
            return true;
        return row != null && (item == LordBannerMap.Neutral || row.Contains(item));
    }
}
