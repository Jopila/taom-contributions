using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using TAOM.Core.Validation;

namespace TAOM.Core.Domain;

/// <summary>What the contributors together do to one party's spotting range: a share of its base, and a share of vanilla's cap.</summary>
internal readonly struct SpottingAdjustment
{
    internal SpottingAdjustment(float factor, float capBonus)
    {
        Factor = factor;
        CapBonus = capBonus;
    }

    /// <summary>Added to the number's factor sum (a share of the base sight, not of the total).</summary>
    internal float Factor { get; }

    /// <summary>The cap becomes <c>MaximumSeeingRange x (1 + CapBonus)</c>.</summary>
    internal float CapBonus { get; }
}

/// <summary>
/// How <see cref="IPartySpottingContributor"/>s reach <c>TaomMapVisibilityModel.GetPartySpottingRange</c> (docs/features/far-watch.md
/// "The seam"). Vanilla ends the number with <c>LimitMax(MaximumSeeingRange())</c>, so a factor added afterwards is still
/// clamped at 60 and only a second <c>LimitMax</c> can lift it. The rules, all bounded so a future contributor cannot take
/// sight away or open the map up by accident:
/// factors from every contributor share one sum (as the lookout always has: no upper cutoff), a negative factor is a
/// penalty and all penalties together stop at <see cref="MaxPenalty"/>, a NaN or infinite value adds nothing, and the cap
/// bonus is the largest one offered (never their sum), at most <see cref="MaxCapBonus"/> (a bound on the per-frame cost of a raised sight). Built once in the model's
/// constructor; <see cref="Compose"/> runs per spotting query and allocates nothing (pinned by an allocated-bytes test). <see cref="Apply"/>
/// allocates only the label text, and only for a number that asked for descriptions.
/// </summary>
internal sealed class SpottingContributorComposer
{
    /// <summary>All penalties together never take more than half of the base sight, so the range stays well above zero.</summary>
    internal const float MaxPenalty = -0.5f;

    /// <summary>
    /// The cap never rises above vanilla's 60 times (1 + this) = 77.4. The engine's visibility pass searches a 32 by 32 locator
    /// grid of 5-unit cells with radius = sight, every frame, and the cost of that search grows smoothly with the radius (the span
    /// is capped at 31 cells and the grid wraps, so past about 75 most positions walk every party): there is no step to stay under.
    /// The ceiling only bounds the extra per-frame cost of a raised sight (vanilla's footstep sounds already search at sight + 25).
    /// Far Watch asks for 0.5 and gets this much; sight below the cap still gets its whole +50 % factor. 0.29 is a judgement, not a
    /// measured threshold: an in-game A/B of frame time at 60 and at 77.4 is owed (docs/features/far-watch.md "What a higher cap costs").
    /// </summary>
    internal const float MaxCapBonus = 0.29f;

    private readonly IPartySpottingContributor[] _contributors;

    internal SpottingContributorComposer(IPartySpottingContributor[] contributors) => _contributors = contributors;

    internal SpottingAdjustment Compose(MobileParty party)
    {
        float positives = 0f;
        float penalties = 0f;
        float capBonus = 0f;
        for (int i = 0; i < _contributors.Length; i++)
        {
            float factor = _contributors[i].GetSpottingRangeBonusFactor(party);
            if (FiniteFloatValidator.IsFinite(factor))
            {
                if (factor > 0f) positives += factor;
                else if (factor < 0f) penalties += factor;
            }

            float bonus = _contributors[i].GetSpottingRangeCapBonus(party);
            if (FiniteFloatValidator.IsFinite(bonus) && bonus > capBonus) capBonus = bonus;
        }

        if (penalties < MaxPenalty) penalties = MaxPenalty;
        if (capBonus > MaxCapBonus) capBonus = MaxCapBonus;

        // The service exit gate: a sum of finite floats can still overflow, and the engine must never see it.
        float total = positives + penalties;
        return new SpottingAdjustment(FiniteFloatValidator.IsFinite(total) ? total : 0f, capBonus);
    }

    /// <summary>
    /// Adds the composed factor to the number vanilla built and writes the cap again: unchanged (the same 60 vanilla wrote)
    /// with no cap bonus, lifted by it otherwise. A described number gets vanilla's own "Limit" label so the tooltip keeps
    /// the line's name; an undescribed one allocates no text. No line of the contributors' own is described: the lookout never had one
    /// and the interface carries no label, so a described number shows vanilla's lines, the career passives' and the cap.
    /// </summary>
    internal void Apply(ref ExplainedNumber result, MobileParty party, float maximumSeeingRange, bool includeDescriptions)
    {
        var adjustment = Compose(party);
        result.AddFactor(adjustment.Factor);
        result.LimitMax(maximumSeeingRange * (1f + adjustment.CapBonus), includeDescriptions ? new TextObject("{=6qv6Hdww}Limit") : null);
    }
}
