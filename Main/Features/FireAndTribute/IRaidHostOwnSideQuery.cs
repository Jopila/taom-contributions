namespace TAOM.Features.FireAndTribute;

/// <summary>
/// The narrow view the party AI model takes of the live hosts: whether a party of a kingdom meets a host of its own side.
/// Vanilla calls the model on its AI worker thread, so both members read one immutable snapshot that the main thread
/// replaces whole; neither touches the engine, IoC, MCM or the logger.
/// </summary>
public interface IRaidHostOwnSideQuery
{
    /// <summary>False while no host lives: the model's fast path, read before any party field.</summary>
    bool AnyHost { get; }

    /// <summary>True when <paramref name="hostPartyId"/> is a live host and <paramref name="kingdomId"/> a kingdom of its side.</summary>
    bool IsOwnSide(string? kingdomId, string? hostPartyId);
}
