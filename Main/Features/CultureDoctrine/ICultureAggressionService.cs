using TAOM.Features.CultureDoctrine.Domain;

namespace TAOM.Features.CultureDoctrine;

/// <summary>
/// The melee half of a culture's doctrine: the <see cref="CultureAggression"/> profile a
/// soldier's AI decision values are scaled by, answered per culture StringId. Called from the
/// agent-stat models on the main thread at spawn and on the async AI thread on every agent
/// property refresh an order change triggers, so the answer is a dictionary lookup behind a bool
/// over immutable data, and never logs or allocates.
/// </summary>
public interface ICultureAggressionService
{
    /// <summary>The culture's profile, or <see cref="CultureAggression.Vanilla"/> when the
    /// doctrine or its aggression tier is off or the culture has no row.</summary>
    CultureAggression Profile(string? cultureId);

    /// <summary>The multiplier an AI soldier in a formation holding a Dismount order puts on his liking
    /// for polearms (<c>AiWeaponFavorMultiplierPolearm</c>); 1 when the horse stand or the doctrine is
    /// off. It reaches the whole standing wall of a <c>dismount: true</c> team, not the dismounted riders
    /// alone, since the order is on the formations the wall is made of. Global, not per culture: only a
    /// stand ever orders a formation to dismount.</summary>
    float StandPolearmFavor { get; }
}
