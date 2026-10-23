using System;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Equipment;

/// <summary>
/// Maps a career rank to its kit tier. The roster data has four tiers, so the first four ranks keep
/// their own tier (ordinal for ordinal) and every rank above them draws the best tier. The issue
/// ledger is keyed by tier, not by rank.
/// </summary>
public static class EnlistmentKitTiers
{
    /// <summary>The best (highest) kit tier; every rank at or above it draws this tier.</summary>
    public const EnlistmentKitTier Best = EnlistmentKitTier.Sergeant;

    public static EnlistmentKitTier For(ServiceRank rank) => (EnlistmentKitTier)Math.Min((int)rank, (int)Best);

    /// <summary>
    /// "A kit waits" (#835): the quartermaster's own ledger has not covered this rank's kit tier. Asking
    /// the ledger keeps the board, the notice and the quartermaster's answer on one rule (an undefined
    /// stored tier counts as nothing drawn). The ledger holds a kit TIER, not a rank, so ranks that
    /// share the top tier never promise a second kit.
    /// </summary>
    public static bool KitWaits(ServiceRank rank, IEquipmentIssueLedger ledger) =>
        !ledger.HasIssuedForTier(For(rank));
}
