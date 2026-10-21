using System;

namespace TAOM.Features.HallOfFire;

/// <summary>
/// Rewrites the engine's conversation atmosphere name for a TAOM culture (docs/features/hall-of-fire.md).
/// Native defines <c>conv_*</c> atmospheres for the six vanilla culture ids only, so a settlement of any other
/// culture asks for a name that does not exist and keeps the previous backdrop. A mapped culture's prefix
/// <c>conv_&lt;culture&gt;_</c> is swapped once for <c>conv_&lt;set&gt;_</c>; wilderness tags and unmapped cultures
/// pass through. A hideout needs no special case: vanilla gives it a wilderness tag
/// <c>conv_&lt;snow|desert|steppe|forest|plains&gt;_&lt;time&gt;_0</c>, so the culture prefix never matches.
/// Pure: no TaleWorlds types.
/// </summary>
public sealed class HallOfFireService
{
    private readonly IHallOfFireConfigProvider _config;

    public HallOfFireService(IHallOfFireConfigProvider config)
    {
        _config = config;
    }

    /// <summary>
    /// The map is read by the normalized culture id, but the prefix is matched against the id exactly as the engine
    /// wrote it into the tag (<c>Settlement.Culture.StringId</c>, verbatim). One lookup, never chained.
    /// </summary>
    public string? Resolve(string? vanillaTag, string? cultureId)
    {
        if (vanillaTag == null || cultureId == null)
            return vanillaTag;

        var key = HallOfFireMap.NormalizeKey(cultureId);
        if (key == null || !_config.GetMap().Cultures.TryGetValue(key, out var set))
            return vanillaTag;

        var prefix = "conv_" + cultureId + "_";
        if (!vanillaTag.StartsWith(prefix, StringComparison.Ordinal))
            return vanillaTag;

        return "conv_" + set + "_" + vanillaTag.Substring(prefix.Length);
    }
}
