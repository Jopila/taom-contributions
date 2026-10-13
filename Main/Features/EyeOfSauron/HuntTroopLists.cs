using System;
using System.Collections.Generic;

namespace TAOM.Features.EyeOfSauron;

/// <summary>One troop of a hunter's host and its share of the strength the top-up adds.</summary>
public readonly struct HostTroop
{
    public HostTroop(string troopId, float share)
    {
        TroopId = troopId;
        Share = share;
    }

    public string TroopId { get; }

    public float Share { get; }
}

/// <summary>
/// The fixed lore data of the hunt (#765 M8): who may hunt, in what order, and the high-tier troops of each host.
/// A compiled constant like <c>NazgulRegistry</c>, not a config file. Every troop id is pinned against
/// <c>Main/_Module/ModuleData</c> by <c>HuntTroopListsDataTests</c>; the shares of each host sum to 1.
/// </summary>
public static class HuntTroopLists
{
    public const string WitchKingId = "lord_1_15";
    public const string KhamulId = "lord_1_48";
    public const string GlorfindelId = "lord_R2_1";
    public const string DolGuldurKingdomId = "dolguldur";

    /// <summary>
    /// The Nine, in the order the Eye sends them against a Free player: the Witch-king first, Khamul last (he rules
    /// Dol Guldur, so re-tasking him strips that kingdom of its ruler's army). The middle order is the registry's.
    /// </summary>
    public static readonly IReadOnlyList<string> WraithOrder = new[]
    {
        WitchKingId,
        "lord_1_155",
        "lord_1_16",
        "lord_1_28",
        "lord_1_38",
        "lord_1_48_1",
        "lord_1_48_2",
        "lord_1_48_3",
        KhamulId,
    };

    /// <summary>An Evil player is hunted by Glorfindel alone, sent by Elrond.</summary>
    public static readonly IReadOnlyList<string> ElvenOrder = new[] { GlorfindelId };

    /// <summary>Mordor's host: Black Numenorean temple knights and guards, shadowbows, knights, Barad-dur guards.</summary>
    public static readonly IReadOnlyList<HostTroop> Mordor = new[]
    {
        new HostTroop("mordor_num_temple_knight", 0.25f),
        new HostTroop("mordor_num_temple_guard", 0.25f),
        new HostTroop("mordor_num_shadowbow", 0.20f),
        new HostTroop("mordor_num_knight", 0.15f),
        new HostTroop("mordor_uruk_baraddurguard", 0.15f),
    };

    /// <summary>Khamul's host from Dol Guldur: his shadow knights, guards, reapers and bowmen.</summary>
    public static readonly IReadOnlyList<HostTroop> DolGuldur = new[]
    {
        new HostTroop("dg_khamul_shadow_knight", 0.30f),
        new HostTroop("dg_khamul_shadow_guard", 0.25f),
        new HostTroop("dg_khamul_shadow_reaper", 0.20f),
        new HostTroop("dg_khamul_shadow_bowman", 0.25f),
    };

    /// <summary>Glorfindel's host of the Eldar from Rivendell.</summary>
    public static readonly IReadOnlyList<HostTroop> Rivendell = new[]
    {
        new HostTroop("rivendell_glorfindel_guard", 0.30f),
        new HostTroop("rivendell_knight_golden_flower", 0.25f),
        new HostTroop("rivendell_royal_knight", 0.25f),
        new HostTroop("rivendell_gondolin_battlemaster", 0.20f),
    };

    /// <summary>The host for a hunter: the Eldar for Glorfindel, Dol Guldur's for a Dol Guldur hunter, else Mordor's.</summary>
    public static IReadOnlyList<HostTroop> HostFor(string heroId, string? factionId)
    {
        if (string.Equals(heroId, GlorfindelId, StringComparison.Ordinal))
            return Rivendell;
        return string.Equals(factionId, DolGuldurKingdomId, StringComparison.Ordinal) ? DolGuldur : Mordor;
    }
}
