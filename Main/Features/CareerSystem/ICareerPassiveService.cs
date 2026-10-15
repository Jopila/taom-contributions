using TaleWorlds.CampaignSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public interface ICareerPassiveService
{
    void RefreshCache(ICareerDataService dataService, ICareerRegistry registry);

    /// <summary>
    /// The unfiltered per-type total: every passive of <paramref name="type"/> the hero holds, summed,
    /// whatever its attack_type_mask or troop_class. The reader for every non-combat type.
    /// </summary>
    float GetPassiveMagnitude(string heroStringId, PassiveEffectType type);

    /// <summary>
    /// Sum of the cached magnitudes for <paramref name="type"/> whose authored
    /// <see cref="AttackTypeMask"/> applies to the hit described by <paramref name="hitMask"/>
    /// (a single Melee or Ranged bit). All-masked entries apply to every hit. Used by the damage
    /// path so a "+X% ranged damage" pip only fires on ranged hits.
    /// </summary>
    float GetMaskedMagnitude(string heroStringId, PassiveEffectType type, AttackTypeMask hitMask);

    /// <summary>
    /// #769 E9: the sum of the cached magnitudes for <paramref name="type"/> that reach a troop of class
    /// <paramref name="troopClass"/>: a passive counts when its troop_class is All or shares a bit with the
    /// troop's, and, when <paramref name="hitMask"/> is given, when its attack_type_mask matches the hit.
    /// A troop of class None still receives the All-class passives. With no hit mask, a passive whose mask is None
    /// (an unparseable one) is skipped. Serves the per-blow damage path, the troop counters, the console and the
    /// army-wide <see cref="ApplyFactor"/> / <see cref="ApplyFlat"/> reads for the class-filtered types
    /// (<see cref="PassiveEffectConsumers.ClassFiltered"/>).
    /// </summary>
    float GetTroopMagnitude(string heroStringId, PassiveEffectType type, TroopClassMask troopClass, AttackTypeMask? hitMask = null);

    bool HasActivePassive(string heroStringId, PassiveEffectType type);

    // Phase 9b #173 — replaces the static CareerPassiveHelper.ApplyFactor / ApplyFlat. Per ADR-007,
    // these accept `string heroStringId` (primitive) not sealed `Hero` — boundary GameModels
    // extract `hero?.StringId` at the call site. `ExplainedNumber` is a public TaleWorlds value
    // type, not a sealed reference; its public API is mockable, so it's adapter-safe.
    // For a class-filtered type (PassiveEffectConsumers.ClassFiltered) both read the ARMY-WIDE pips only, as a troop of
    // class None would: these callers have no troop class, so a class-only pip must not count at full magnitude.
    void ApplyFactor(string heroStringId, ref ExplainedNumber result, PassiveEffectType type);
    void ApplyFlat(string heroStringId, ref ExplainedNumber result, PassiveEffectType type);
}
