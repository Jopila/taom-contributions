using System.Collections.Generic;
using TAOM.Features.WatchfulHolds.Models;

namespace TAOM.Adapters;

/// <summary>
/// The campaign state Watchful Holds reads and the penalties it applies, behind object handles so the
/// service names no TaleWorlds type (ADR-007). A handle is whatever <see cref="TryReadParty"/> put in a
/// snapshot; it is only meaningful to the adapter that issued it.
/// </summary>
public interface IWatchfulHoldsAdapter
{
    /// <summary>Reads the party behind an engine handle; false when it is not a live mobile party.</summary>
    bool TryReadParty(object engineParty, out WatchfulPartySnapshot snapshot);

    /// <summary>Every castle and town, built on each call; the service keeps the array for the campaign.</summary>
    FortSite[] GetFortSites();

    /// <summary>Reads a fort live; false when the id is not a castle or town.</summary>
    bool TryReadFort(string fortId, out FortStatus status);

    /// <summary>True when the party's realm and the fort's realm are at war.</summary>
    bool AreAtWar(object partyHandle, string fortId);

    /// <summary>The army leader's party and every party attached to it, as snapshots; empty for an unknown handle.</summary>
    IReadOnlyList<WatchfulPartySnapshot> GetArmyMemberParties(object leaderHandle);

    void Disorganize(object handle);

    /// <summary>Kills then wounds non-hero troops, clamped to the healthy regulars; returns what was applied. Heroes are never hit.</summary>
    (int killed, int wounded) ApplyLosses(object handle, int kill, int wound);

    /// <summary>Removes ceil(total food * <paramref name="fraction"/>) units from the party's own food stacks; returns the units removed.</summary>
    int RemoveFood(object handle, float fraction);

    void LowerMorale(object handle, float amount);

    /// <summary>Lowers the army's cohesion by <paramref name="amount"/>, never below <paramref name="floor"/>.</summary>
    void LowerArmyCohesion(object leaderHandle, float amount, float floor);

    /// <summary>Campaign time in hours.</summary>
    double CampaignHours { get; }
}
