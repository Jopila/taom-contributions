using System.Collections.Generic;
using TAOM.Features.Namegiving;

namespace TAOM.Adapters;

/// <summary>
/// The engine side of Namegiving (docs/features/namegiving.md): reads settlements as plain values and writes a custom name.
/// Every method needs a running campaign. The reads change nothing; the three writes are the only mutations the feature makes.
/// </summary>
public interface IFiefNamingAdapter
{
    /// <summary>One settlement by its string id, or null when it does not exist or is not a town, castle or village.</summary>
    FiefNameSnapshot? Snapshot(string settlementId);

    /// <summary>The string ids of the villages bound to a town or castle, empty for anything else.</summary>
    IReadOnlyList<string> VillageIds(string fiefId);

    /// <summary>Every settlement's id and display name, the base of the uniqueness check. Data names are not unique.</summary>
    IReadOnlyList<NamedSettlement> AllNames();

    /// <summary>The player clan's influence now.</summary>
    float Influence { get; }

    /// <summary>True when the main hero is the ruler of a kingdom.</summary>
    bool IsRuler { get; }

    /// <summary>
    /// The engine's reason a map-screen action is unavailable right now (a prisoner, in a mission, in an encounter), or null
    /// when the action is allowed.
    /// </summary>
    string? MapActionBlockedReason();

    /// <summary>Names the settlement <paramref name="name"/> (stored as a non-localized player name), then charges the player clan <paramref name="cost"/> influence.</summary>
    void ApplyPlayerName(string settlementId, string name, int cost);

    /// <summary>Gives the settlement a localized lore name: <paramref name="text"/> is the whole literal, <c>{=key}English</c>, exactly as the harvester registered it.</summary>
    void ApplyLore(string settlementId, string text);

    /// <summary>Clears the custom name, so the settlement shows the map's own name again.</summary>
    void Restore(string settlementId);
}
