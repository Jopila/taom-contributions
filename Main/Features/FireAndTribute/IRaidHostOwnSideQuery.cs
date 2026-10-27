namespace TAOM.Features.FireAndTribute;

/// <summary>
/// The narrow view other features take of the live hosts: the party AI model asks whether a party of a kingdom meets a
/// host of its own side, and the Refuge raid whether a party is a host at all.
/// Vanilla calls the model on its AI worker thread, so every member reads one immutable snapshot that the main thread
/// replaces whole, and <see cref="IsOwnSide"/> also reads the alignment map, which is written only at construction;
/// none touches the engine, IoC, MCM or the logger.
/// </summary>
public interface IRaidHostOwnSideQuery
{
    /// <summary>False while no host lives: the model's fast path, read before any party field.</summary>
    bool AnyHost { get; }

    /// <summary>
    /// True when <paramref name="hostPartyId"/> is a live host and the kingdom (its id and culture id) resolves to the
    /// host's side.
    /// </summary>
    bool IsOwnSide(string? kingdomId, string? kingdomCultureId, string? hostPartyId);

    /// <summary>True when <paramref name="partyId"/> is a live host. The Refuge raid asks it on the main thread (#1033).</summary>
    bool IsHost(string? partyId);
}
