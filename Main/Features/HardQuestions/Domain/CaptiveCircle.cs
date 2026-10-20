using System.Collections.Generic;

namespace TAOM.Features.HardQuestions.Domain;

/// <summary>
/// The people a hard deed touches, read from the engine once per deed. Lists never contain the captive himself
/// or anyone of the player's own clan. Each lord carries the relation owner the active DiplomacyModel will charge
/// (his clan leader under the vanilla mapping, the lord himself under Personal Relations), so lords who share an
/// owner collapse to one charge.
/// </summary>
public sealed class CaptiveCircle
{
    public CaptiveCircle(LordRef victim)
    {
        Victim = victim;
    }

    /// <summary>The captive, with the relation owner filled in.</summary>
    public LordRef Victim { get; }

    public IReadOnlyList<LordRef> Family { get; set; } = new List<LordRef>();

    /// <summary>
    /// The captive's clan leader, null when the captive is the leader or has no clan. The hit list does not depend
    /// on the mapping; tests run it with distinct and with shared RelationIds.
    /// </summary>
    public LordRef? ClanLeader { get; set; }

    /// <summary>The captive's other living clan members who are lords.</summary>
    public IReadOnlyList<LordRef> ClanLords { get; set; } = new List<LordRef>();

    public LordRef? KingdomLeader { get; set; }

    public IReadOnlyList<LordRef> KingdomLords { get; set; } = new List<LordRef>();

    public IReadOnlyList<LordRef> HonorPositiveLords { get; set; } = new List<LordRef>();

    public IReadOnlyList<CompanionRef> Companions { get; set; } = new List<CompanionRef>();
}
