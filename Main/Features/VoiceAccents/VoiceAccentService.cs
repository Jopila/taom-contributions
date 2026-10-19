using System;
using System.Collections.Generic;
using TAOM.Core.Domain;
using TAOM.Core.Logging;

namespace TAOM.Features.VoiceAccents;

/// <summary>
/// Decides what a character sounds like in dialogue (docs/features/voice-accents.md). The engine's
/// <c>GetAccentClass</c> returns "" for every non-vanilla culture, which makes
/// <c>GetSoundPathForCharacter</c> pick a random vanilla voice. This service gives a mapped culture
/// a fitting vanilla accent, and silences creature races, silent cultures and any culture nobody
/// mapped. Pure: no TaleWorlds types, so the decision matrix is unit-tested.
/// </summary>
public sealed class VoiceAccentService
{
    private static readonly HashSet<string> VanillaCultureIds = new HashSet<string>(StringComparer.Ordinal)
    {
        "empire", "vlandia", "sturgia", "khuzait", "aserai", "battania", "forest_bandits",
        "sea_raiders", "mountain_bandits", "desert_bandits", "steppe_bandits", "looters",
    };

    /// <summary>
    /// The accent of the current speaker's race row, held for the length of one <see cref="SoundPath"/> call.
    /// <c>GetAccentClass</c> receives a culture only, and the base <c>GetSoundPathForCharacter</c> is its single
    /// caller and calls it synchronously, so this is the speaker for that read. Null outside a call or when the
    /// race has no row; never silent.
    /// </summary>
    [ThreadStatic]
    private static string? _speakerRaceAccent;

    private readonly IVoiceAccentsConfigProvider _config;
    private readonly IRaceManager _races;
    private readonly IModLogger _logger;
    private readonly HashSet<string> _loggedUnmapped = new HashSet<string>(StringComparer.Ordinal);

    public VoiceAccentService(IVoiceAccentsConfigProvider config, IRaceManager races, IModLogger logger)
    {
        _config = config;
        _races = races;
        _logger = logger;
    }

    /// <summary>
    /// The dialogue sound path: "" when the character must not speak a vanilla line, else the vanilla
    /// model's answer, computed with the speaker's race accent in scope so <see cref="ResolveAccent"/> can use a
    /// race row. A race row decides first, for silence and for speech. The previous value is restored
    /// afterwards, also when the delegate throws.
    /// </summary>
    public string SoundPath(string? cultureId, int raceId, Func<string> vanillaSoundPath)
    {
        var raceAccent = RaceAccent(raceId);
        if (raceAccent == VoiceAccentMap.Silent || (raceAccent == null && IsSilentCulture(cultureId)))
            return string.Empty;

        var previous = _speakerRaceAccent;
        _speakerRaceAccent = raceAccent;
        try
        {
            return vanillaSoundPath();
        }
        finally
        {
            _speakerRaceAccent = previous;
        }
    }

    /// <summary>The race row's accent, or null for an unknown id or a race without a row. The id is checked
    /// first, because <c>GetRaceNameFromId</c> answers "human" for an unknown id.</summary>
    private string? RaceAccent(int raceId)
    {
        if (!_races.IsValidRaceId(raceId))
            return null;
        var race = VoiceAccentMap.Normalize(_races.GetRaceNameFromId(raceId));
        return race != null && _config.GetMap().Races.TryGetValue(race, out var accent) ? accent : null;
    }

    private bool IsSilentCulture(string? cultureId)
    {
        var culture = VoiceAccentMap.Normalize(cultureId);
        if (culture == null)
            return false;
        if (_config.GetMap().Cultures.TryGetValue(culture, out var accent))
            return accent == VoiceAccentMap.Silent;
        if (VanillaCultureIds.Contains(culture))
            return false;

        lock (_loggedUnmapped)
        {
            if (_loggedUnmapped.Add(culture))
                _logger.LogWarning($"VoiceAccentService: culture '{culture}' has no row in voice_accents.json, its characters are silent in dialogue");
        }
        return true;
    }

    /// <summary>The accent class for the current speaker's race row, else the culture row, or null when the base model should decide.</summary>
    public string? ResolveAccent(string? cultureId, bool isHighClass)
    {
        var accent = _speakerRaceAccent;
        if (accent == null)
        {
            var culture = VoiceAccentMap.Normalize(cultureId);
            if (culture == null || !_config.GetMap().Cultures.TryGetValue(culture, out accent))
                return null;
        }

        if (accent == VoiceAccentMap.Imperial)
            return isHighClass ? "imperial_high" : "imperial_low";
        return accent;
    }
}
