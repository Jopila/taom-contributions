using System.Collections.Generic;

namespace TAOM.Features.HardQuestions.Domain;

/// <summary>
/// The people a hard deed touches, read from the engine once per deed. Lists never contain the captive himself
/// or anyone of the player's own clan. Each lord carries the relation owner the engine will charge, so lords of
/// one clan collapse to one charge.
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

    public LordRef? KingdomLeader { get; set; }

    public IReadOnlyList<LordRef> KingdomLords { get; set; } = new List<LordRef>();

    public IReadOnlyList<LordRef> HonorPositiveLords { get; set; } = new List<LordRef>();

    public IReadOnlyList<CompanionRef> Companions { get; set; } = new List<CompanionRef>();
}
