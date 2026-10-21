using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Core.Domain;

/// <summary>
/// Cross-feature seam for spotting-range changes on <c>TaomMapVisibilityModel</c>.
///
/// <para>The engine holds ONE MapVisibilityModel slot and CareerSystem's model owns it
/// (SubModule.cs registration). Any other feature wanting to change a party's sight (FieldCamp's
/// lookout, Far Watch's scout) contributes through this interface instead of registering a second model, which would
/// silently unseat the first (the collision docs/roadmap.md used to invite).</para>
///
/// <para>A contributor has two members. <see cref="GetSpottingRangeBonusFactor"/> is a share of the party's BASE sight that
/// joins every other contributor's in one sum: positive widens (no upper cutoff), negative is a penalty, and all penalties
/// together stop at <see cref="SpottingContributorComposer.MaxPenalty"/>. <see cref="GetSpottingRangeCapBonus"/> is a share of
/// vanilla's cap of 60 (which clamps the number last): the composer takes the LARGEST bonus offered, never their sum, and clamps
/// it at <see cref="SpottingContributorComposer.MaxCapBonus"/>, which bounds the extra per-frame cost of a raised sight (cap 77.4).
/// A NaN or infinite value from either member adds nothing.</para>
/// </summary>
public interface IPartySpottingContributor
{
    /// <summary>
    /// Additional spotting-range factor for this party, e.g. 0.25 for +25%. Return 0 for none.
    /// Called from the model's hot path: no allocation, no IoC, null-tolerant on the party.
    /// </summary>
    float GetSpottingRangeBonusFactor(MobileParty party);

    /// <summary>
    /// How much this contributor lifts vanilla's cap on the spotting range, as a share of that cap (0.25 lifts 60 to 75; any request is clamped as described above).
    /// Return 0 for none. Same hot-path contract as <see cref="GetSpottingRangeBonusFactor"/>.
    /// </summary>
    float GetSpottingRangeCapBonus(MobileParty party);
}
