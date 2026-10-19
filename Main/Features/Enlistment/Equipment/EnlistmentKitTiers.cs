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
}
