using System.Globalization;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

/// <summary>
/// #769 E12 — the tree version a hero was built against, stored as a hero flag
/// (<c>career_tree_v2</c>). A hero with no such flag is on version 1, the original tree.
/// A flag must not contain a comma: flags persist as one comma-joined string per hero.
/// </summary>
public static class CareerTreeVersion
{
    private const string Prefix = "career_tree_v";

    public static string FlagFor(int version) => Prefix + version;

    /// <summary>The highest version flag the hero carries, or 1 when it has none.</summary>
    public static int StoredVersion(HeroCareerData heroData)
    {
        var highest = 1;
        if (heroData == null) return highest;

        foreach (var flag in heroData.Flags)
        {
            if (flag == null || !flag.StartsWith(Prefix, System.StringComparison.Ordinal)) continue;

            // NumberStyles.None: digits only, so a sign or a space is not a version.
            var suffix = flag.Substring(Prefix.Length);
            if (int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var version)
                && version > highest)
                highest = version;
        }
        return highest;
    }

    /// <summary>
    /// The single writer of the version flag: mark a hero as built on <paramref name="career"/>'s
    /// current tree. It first drops every version flag the hero carries, because
    /// <see cref="StoredVersion"/> reads the highest and a flag left by an earlier career would
    /// otherwise hide a later bump. Without a stamp the first load after character creation would
    /// read the missing flag as an old save and refund the new player's legitimate picks. A
    /// version-1 career carries no flag.
    /// </summary>
    public static void Stamp(ICareerDataService dataService, string heroStringId, CareerDefinition career)
    {
        if (dataService == null || career == null) return;

        var heroData = dataService.GetOrCreateData(heroStringId);
        if (heroData != null)
            heroData.Flags.RemoveAll(flag => flag != null && flag.StartsWith(Prefix, System.StringComparison.Ordinal));

        if (career.TreeVersion >= 2)
            dataService.SetFlag(heroStringId, FlagFor(career.TreeVersion));
    }
}
