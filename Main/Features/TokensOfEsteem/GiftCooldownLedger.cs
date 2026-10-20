using System;
using System.Collections.Generic;
using System.Globalization;

namespace TAOM.Features.TokensOfEsteem;

/// <summary>
/// The day of the last gift to each relation owner, saved by <c>GiftBehavior.SyncData</c> as a flat Dictionary&lt;string, string&gt; (the
/// <c>HardQuestionsState</c> shape). Keys are "v" and "last|ownerId"; the value is the campaign day number. The conversation
/// flow (built by the behavior for every campaign) builds its own ledger, so a new campaign starts empty and a load restores exactly what the save held. Nothing here is a
/// singleton, so there is no session reset to forget.
/// </summary>
public sealed class GiftCooldownLedger
{
    public const int Version = 1;

    private const string LastPrefix = "last|";

    private readonly Dictionary<string, int> _lastDay = new(StringComparer.Ordinal);

    public int Count => _lastDay.Count;

    /// <summary>The day of the last gift to this hero, or null when there was none.</summary>
    public int? LastGiftDay(string heroId) =>
        !string.IsNullOrEmpty(heroId) && _lastDay.TryGetValue(heroId, out var day) ? day : null;

    /// <summary>Records a gift on <paramref name="day"/>. A blank id or a negative day is ignored.</summary>
    public void Stamp(string heroId, int day)
    {
        if (!string.IsNullOrEmpty(heroId) && day >= 0)
            _lastDay[heroId] = day;
    }

    public Dictionary<string, string> Encode()
    {
        var data = new Dictionary<string, string> { ["v"] = Version.ToString(CultureInfo.InvariantCulture) };
        foreach (var pair in _lastDay)
            data[LastPrefix + pair.Key] = pair.Value.ToString(CultureInfo.InvariantCulture);
        return data;
    }

    /// <summary>Replaces the ledger with <paramref name="data"/>; returns how many entries were refused as damaged.</summary>
    public int Decode(IDictionary<string, string>? data)
    {
        _lastDay.Clear();
        if (data == null)
            return 0;

        var skipped = 0;
        foreach (var pair in data)
        {
            if (pair.Key == "v")
                continue;
            if (!pair.Key.StartsWith(LastPrefix, StringComparison.Ordinal) || pair.Key.Length == LastPrefix.Length
                || !int.TryParse(pair.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var day) || day < 0)
            {
                skipped++;
                continue;
            }
            _lastDay[pair.Key.Substring(LastPrefix.Length)] = day;
        }
        return skipped;
    }
}
