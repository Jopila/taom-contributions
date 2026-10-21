using TAOM.Features.Execution;

namespace TAOM.Features.LordCourtesies.Domain;

/// <summary>
/// The cheap facts about the lord in the conversation, taken at the engine boundary and safe to read in a
/// dialog condition. Ids and plain values only (ADR-007).
/// </summary>
public sealed class LordCourtesySnapshot
{
    public LordCourtesySnapshot(string id)
    {
        Id = id;
    }

    public string Id { get; }

    public string? ClanId { get; set; }

    public string? KingdomId { get; set; }

    /// <summary>
    /// The lord's clan map faction id with that faction's culture, or a clanless hero's own culture
    /// (<c>FactionSides.OfHero</c>): the pair the alignment table resolves. <see cref="KingdomId"/> stays for the
    /// same-realm gate.
    /// </summary>
    public SideIds Side { get; set; } = SideIds.None;

    public bool IsInPlayerClan { get; set; }

    public bool AtWarWithPlayer { get; set; }

    /// <summary>True when he leads his own lord party (not a lord idling in a settlement with no party).</summary>
    public bool LeadsOwnParty { get; set; }

    /// <summary>
    /// True when his party is with the player's: the same settlement, or close enough to join the player's
    /// encounter, with the same map event (none counts). Always false while a messenger conversation is open, because
    /// that conversation looks like a visit to the engine.
    /// </summary>
    public bool IsWithPlayer { get; set; }

    /// <summary>True when his party has room for more men (fewer members than its size limit).</summary>
    public bool HasRoom { get; set; }

    /// <summary>True when his party is being disbanded.</summary>
    public bool IsDisbanding { get; set; }

    /// <summary>True when the player's party holds at least one regular troop to give.</summary>
    public bool PlayerHasTroops { get; set; }

    /// <summary>The player's relation with him, as the engine reports it.</summary>
    public int Relation { get; set; }

    /// <summary>How many settlements of any kind his clan owns; 0 means a landless clan.</summary>
    public int Holdings { get; set; }

    /// <summary>How many of those are towns and castles, the settlements the map reveal marks.</summary>
    public int Fortifications { get; set; }
}
