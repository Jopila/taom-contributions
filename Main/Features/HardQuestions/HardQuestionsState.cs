using System;
using System.Collections.Generic;
using System.Globalization;
using TAOM.Features.HardQuestions.Domain;

namespace TAOM.Features.HardQuestions;

/// <summary>
/// Which captive lords were already questioned or stripped during this captivity, saved by
/// <c>HardQuestionsCampaignBehavior.SyncData</c> as a flat Dictionary&lt;string, string&gt; (the
/// ArmourAcquisitionState shape). Keys are "v" and "used|heroId"; the value is a bit field per hero: 1 clan
/// topic, 2 kingdom topic, 4 stripped (so 1 to 7). A container singleton that outlives a campaign: the behavior
/// resets it in its constructor (csharp-architecture.md, "Singleton Services Holding Per-Campaign State").
/// </summary>
public sealed class HardQuestionsState
{
    public const int Version = 1;

    private const string UsedPrefix = "used|";
    private const int ClanBit = 1;
    private const int KingdomBit = 2;
    private const int StrippedBit = 4;
    private const int AllBits = ClanBit | KingdomBit | StrippedBit;

    private readonly Dictionary<string, int> _used = new(StringComparer.Ordinal);

    public bool IsAsked(HardQuestionTopic topic, string heroId) => Has(heroId, TopicBit(topic));

    public void MarkAsked(HardQuestionTopic topic, string heroId) => Set(heroId, TopicBit(topic));

    public bool IsStripped(string heroId) => Has(heroId, StrippedBit);

    public void MarkStripped(string heroId) => Set(heroId, StrippedBit);

    /// <summary>Forgets every topic used on the hero: his captivity is over.</summary>
    public void ClearHero(string heroId) => _used.Remove(heroId);

    public void Reset() => _used.Clear();

    public Dictionary<string, string> Encode()
    {
        var data = new Dictionary<string, string> { ["v"] = Version.ToString(CultureInfo.InvariantCulture) };
        foreach (var pair in _used)
            data[UsedPrefix + pair.Key] = pair.Value.ToString(CultureInfo.InvariantCulture);
        return data;
    }

    /// <summary>Replaces the state with <paramref name="data"/>; returns how many entries were refused as damaged.</summary>
    public int Decode(IDictionary<string, string>? data)
    {
        Reset();
        if (data == null)
            return 0;

        var skipped = 0;
        foreach (var pair in data)
        {
            if (pair.Key == "v")
                continue;
            if (!pair.Key.StartsWith(UsedPrefix, StringComparison.Ordinal) || pair.Key.Length == UsedPrefix.Length
                || !int.TryParse(pair.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bits)
                || bits < 1 || bits > AllBits)
            {
                skipped++;
                continue;
            }
            _used[pair.Key.Substring(UsedPrefix.Length)] = bits;
        }
        return skipped;
    }

    private static int TopicBit(HardQuestionTopic topic) => topic == HardQuestionTopic.Clan ? ClanBit : KingdomBit;

    private bool Has(string heroId, int bit) => _used.TryGetValue(heroId, out var bits) && (bits & bit) != 0;

    private void Set(string heroId, int bit)
    {
        _used.TryGetValue(heroId, out var bits);
        _used[heroId] = bits | bit;
    }
}
