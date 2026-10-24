using System.Collections.Generic;
using TAOM.Features.SettlementServices;

namespace TAOM.Adapters;

/// <summary>
/// What Watch-Silver and Tidings of the Lists read and write in the engine (#859, docs/features/settlement-services.md): the
/// governed fief of the hero the player is talking to, a fief's loyalty, the towns holding a tournament, and the player's purse.
/// The adapter only reads facts and performs the two writes; every rule belongs to the services.
/// </summary>
public interface ISettlementServicesAdapter
{
    /// <summary>The conversation hero's governed fief, or null when there is no conversation hero or he governs nothing.</summary>
    GovernedFiefSnapshot? ReadGovernedFief();

    /// <summary>
    /// Sets the fief's loyalty to <paramref name="value"/> (an absolute value, never a delta) and returns the loyalty read back
    /// from the fief, which the engine clamps to its own range.
    /// </summary>
    float SetLoyalty(string settlementId, float value);

    /// <summary>Every town that holds a tournament now, with its land distance from the town the player stands in. Reads only.</summary>
    IReadOnlyList<TournamentSiteSnapshot> ReadTournamentSites();

    /// <summary>The distance at and above which the engine treats a town as unreachable.</summary>
    float TournamentDistanceCutoff { get; }

    /// <summary>The player's gold.</summary>
    int PlayerGold { get; }

    /// <summary>True while the player is disguised.</summary>
    bool IsPlayerDisguised { get; }

    /// <summary>Takes up to <paramref name="amount"/> gold from the player and returns what the purse actually lost (gold before minus gold after).</summary>
    int ChargePlayer(int amount);

    /// <summary>Gives <paramref name="amount"/> gold back to the player, silently. Does nothing for an amount of zero or less.</summary>
    void Refund(int amount);
}
