using System;
using System.Collections.Generic;

namespace TAOM.Features.WatchfulHolds;

/// <summary>
/// <c>watchful_holds/watchful_holds.json</c>: the numbers behind Watchful Holds. The compiled values here are the
/// defaults a missing file, a parse failure or a rejected field falls back to. Validated on load by
/// <see cref="WatchfulHoldsConfigProvider"/>; the two MCM switches live in <c>WatchfulHoldsSettingsProvider</c>.
/// </summary>
public sealed class WatchfulHoldsConfig
{
    public const string DefaultCultureKey = "default";

    /// <summary>The radius must be above zero and at most this.</summary>
    public const float MaxRadius = 50f;
    public const int MaxCount = 100000;
    public const int MaxExtra = 10000;
    public const float MaxMultiplier = 5f;
    public const float MaxMeter = 100f;

    /// <summary>Map units from a fort inside which its garrison watches the road.</summary>
    public float Radius { get; set; } = 5f;

    /// <summary>The fewest garrison troops a fort needs to harass.</summary>
    public int MinGarrison { get; set; } = 30;

    /// <summary>The fewest men a party needs to be harassed.</summary>
    public int MinPartySize { get; set; } = 30;

    public float PlayerChancePerHour { get; set; } = 0.30f;

    public float AiChancePerHour { get; set; } = 0.15f;

    /// <summary>Share of the party's men wounded, before the random extra.</summary>
    public float WoundFraction { get; set; } = 0.02f;

    /// <summary>Random extra wounded, 0 to this value.</summary>
    public int WoundExtraMax { get; set; } = 5;

    public float KillFraction { get; set; } = 0.01f;

    public int KillExtraMax { get; set; } = 1;

    /// <summary>Share of the party's food lost.</summary>
    public float FoodLossFraction { get; set; } = 0.05f;

    public float MoraleLoss { get; set; } = 10f;

    /// <summary>Army cohesion lost per hit on the player's army leader.</summary>
    public float CohesionLoss { get; set; } = 5f;

    /// <summary>A hit never takes army cohesion below this, and never raises a cohesion already below it.</summary>
    public float CohesionFloor { get; set; } = 10f;

    /// <summary>Hours between two horn blasts.</summary>
    public float HornCooldownHours { get; set; } = 24f;

    /// <summary>Multipliers by the fort owner's culture id, lowercase; <c>default</c> covers any culture without a row.</summary>
    public Dictionary<string, WatchfulCultureRow> Cultures { get; set; } = DefaultCultures();

    /// <summary>The row for a culture id, or the default row when the culture has none (or the id is blank).</summary>
    public WatchfulCultureRow RowFor(string? cultureId)
    {
        if (Cultures == null)
            return new WatchfulCultureRow();
        var key = cultureId?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(key) && Cultures.TryGetValue(key!, out var row) && row != null)
            return row;
        return Cultures.TryGetValue(DefaultCultureKey, out var fallback) && fallback != null ? fallback : new WatchfulCultureRow();
    }

    /// <summary>The shipped table: every culture at 1, the realms that live by the Shadow's watchfulness at 1.25 on the chance.</summary>
    public static Dictionary<string, WatchfulCultureRow> DefaultCultures()
    {
        var table = new Dictionary<string, WatchfulCultureRow>(StringComparer.Ordinal)
        {
            [DefaultCultureKey] = new WatchfulCultureRow(),
        };
        foreach (var culture in new[] { "mordor", "dolguldur", "isengard", "lothlorien", "mirkwood" })
            table[culture] = new WatchfulCultureRow { ChanceMultiplier = 1.25f };
        return table;
    }
}

/// <summary>Per-culture multipliers on a fort owner's culture.</summary>
public sealed class WatchfulCultureRow
{
    /// <summary>Multiplies the per-hour chance; the product is clamped to [0, 1].</summary>
    public float ChanceMultiplier { get; set; } = 1f;

    /// <summary>Multiplies the fraction part of the kill and wound counts the player takes, not the random extras.</summary>
    public float LossMultiplier { get; set; } = 1f;
}
