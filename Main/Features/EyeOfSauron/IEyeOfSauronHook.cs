using System;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.EyeOfSauron;

/// <summary>
/// The narrow seam the two campaign models read for the Eye of Sauron hunt (#765 M8): the hunter's speed floor in
/// <c>TaomPartySpeedModel</c> and the host's size bonus in <c>TaomPartySizeModel</c>. Both run on the engine's
/// parallel party tick as well as the main thread, so an implementation may read only state published from the main
/// thread (a volatile snapshot), never MCM, <c>MobileParty.MainParty</c> or a position.
/// </summary>
public interface IEyeOfSauronHook
{
    /// <summary>
    /// Raises <paramref name="result"/> to the hunter's speed floor (the player's speed times the MCM multiplier) while
    /// the hunter is within the player's sight. Any other party, or the hunter out of sight, is left untouched.
    /// </summary>
    void ApplyHunterSpeedFloor(string? partyId, ref ExplainedNumber result);

    /// <summary>
    /// The result-frame body count to add to the hunter's party-size limit so its host is never shed: enough to lift
    /// <paramref name="currentLimit"/> to the larger of <paramref name="rawCount"/> and the weighted count. Zero for any
    /// other party. <paramref name="party"/> is an opaque token handed only to <paramref name="weightedCountOf"/>, which
    /// is invoked for the hunter alone; the model passes a delegate it built once, so no call allocates.
    /// </summary>
    float GetHostSizeBonus(string? partyId, float currentLimit, float sumOfFactors, int rawCount, object? party,
        Func<object?, float> weightedCountOf);
}
