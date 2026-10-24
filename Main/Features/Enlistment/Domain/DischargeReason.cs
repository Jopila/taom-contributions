namespace TAOM.Features.Enlistment.Domain;

/// <summary>
/// Why a service term ended. Consumed by the discharge pipeline to pick consequences
/// (rewards, penalties, messages); every reason runs the same fixed-order pipeline that
/// unconditionally restores party presence.
///
/// PRODUCER STATUS (core phase): PlayerRequest, CommanderDead,
/// CommanderUnavailableGraceExpired, HeirSuccessionOrPossessionMismatch and
/// CommanderAtWarWithYourRealm (#832, raised by the reconciler) are live, and so is
/// Desertion (two producers: the release dialogue and the wait menu, #833).
/// ContractNotRenewed (#836: "Leave with honor" at the end of a term) is live.
/// Commission is RESERVED for the content phase; SaveNormalization is reserved for load paths that can safely run the
/// world-touching pipeline — see the note on <see cref="SaveNormalization"/>.
/// </summary>
public enum DischargeReason
{
    /// <summary>Contract served out or player asked to leave at a legal point.</summary>
    PlayerRequest = 0,

    /// <summary>Commission retirement — honorable exit with distinction rewards. (Content phase.)</summary>
    Commission = 1,

    /// <summary>
    /// Walked out during the minimum term after the commander refused release. Costs relation with the
    /// commander and Honor trait XP (enlistment_config.json, skipped if he is dead), forfeits the
    /// deferred wages, and the deserter keeps his kit. No war, no crime, no renown (#833).
    /// </summary>
    Desertion = 2,

    CommanderDead = 3,

    /// <summary>Commander stayed party-less (captured/disbanded) past the grace window.</summary>
    CommanderUnavailableGraceExpired = 4,

    /// <summary>The player chose to leave with honor at the end of his term (#836). Honourable, penalty-free.</summary>
    ContractNotRenewed = 5,

    /// <summary>EnlistedHeroId no longer matches MainHero (co-op join, heir succession). Quiet, penalty-free.</summary>
    HeirSuccessionOrPossessionMismatch = 6,

    /// <summary>
    /// Load-time normalization could not faithfully restore service. Quiet, penalty-free.
    /// NOTE: EnlistmentStore.Deserialize failures deliberately do NOT route here — SyncData
    /// runs before the world is valid, so the store resets record-only and the load
    /// normalizer's ownerless-parked rescue handles the world side. This reason exists for
    /// normalizer-driven discharges once one needs a distinct label from the identity guard.
    /// </summary>
    SaveNormalization = 7,

    /// <summary>
    /// The commander's realm went to war with the player's own kingdom or, for an independent player,
    /// his clan (#832). Honourable: wages
    /// settled, kit reclaimed. The player is released on the map, not into the commander's town.
    /// </summary>
    CommanderAtWarWithYourRealm = 8,
}
