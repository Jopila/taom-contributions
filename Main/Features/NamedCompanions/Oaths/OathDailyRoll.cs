using System.Globalization;
using System.Text;

namespace TAOM.Features.NamedCompanions.Oaths;

/// <summary>
/// The deterministic roll behind a topic's <c>random_chance</c>. The same character, topic and day always give the
/// same value, so the offer holds for the whole day (no flicker between renders, no re-roll by reopening the
/// conversation) and changes from day to day. FNV-1a over UTF-8 bytes, because <c>string.GetHashCode</c> is not
/// stable across runtimes.
/// </summary>
public static class OathDailyRoll
{
    private const uint OffsetBasis = 2166136261;
    private const uint Prime = 16777619;

    /// <summary>A value in 0..99.</summary>
    public static int Percent(string characterId, string topicId, int day)
    {
        var text = characterId + "|" + topicId + "|" + day.ToString(CultureInfo.InvariantCulture);
        var hash = OffsetBasis;
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash *= Prime;
        }
        return (int)(hash % 100);
    }
}
