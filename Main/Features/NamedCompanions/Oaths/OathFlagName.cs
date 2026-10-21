using System.Text.RegularExpressions;

namespace TAOM.Features.NamedCompanions.Oaths;

/// <summary>The one naming rule for oath flags, shared by the config validator and the ledger.</summary>
public static class OathFlagName
{
    private static readonly Regex Pattern = new("^[a-z0-9_.]{1,64}$", RegexOptions.Compiled);

    /// <summary>Lower-cases the name and checks it against <c>^[a-z0-9_.]{1,64}$</c>.</summary>
    public static bool TryNormalize(string raw, out string normalized)
    {
        normalized = raw?.ToLowerInvariant();
        return normalized != null && Pattern.IsMatch(normalized);
    }
}
