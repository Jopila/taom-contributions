namespace TAOM.Features.UnderTheEaves;

/// <summary>
/// The rule of Under the Eaves (#849, docs/features/under-the-eaves.md): a main party standing in forest, outside a settlement, sees
/// 30% of its base sight less, and half of that with the Keen Sight perk. Pure: three facts in, a sight factor out, so it runs
/// anywhere and holds nothing to save. The factor joins every other contributor's in one sum (<c>Base x (1 + sum)</c>), and the
/// composer floors all penalties together at <c>SpottingContributorComposer.MaxPenalty</c>, so no bound is repeated here.
/// </summary>
internal static class UnderTheEavesRule
{
    /// <summary>
    /// A compiled constant, never read from config, so no float needs validating. Vanilla already cuts a target in forest by this
    /// share of its spotting ratio (<c>PartySpottingDifficultyInForests</c>), and -0.3 leaves 0.2 of the composer's -0.5 penalty
    /// budget to the other contributor that takes sight away, a Far Watch settlement ride.
    /// </summary>
    internal const float ForestPenalty = -0.3f;

    /// <summary>
    /// The share of the penalty that stays with Keen Sight: vanilla's perk (a scout's "sight penalty for traveling in forests") has a
    /// primary bonus of -0.5, and vanilla's target ratio applies it as <c>penalty + penalty x bonus</c>, which is this half
    /// (pinned in UnderTheEavesEngineFactsTests).
    /// </summary>
    internal const float KeenSightShare = 0.5f;

    /// <summary>
    /// The sight factor: 0 unless the party is in forest and not inside a settlement (111 gates stand on forest faces, and a party
    /// inside one is not travelling the wood), else <see cref="ForestPenalty"/>, scaled by <see cref="KeenSightShare"/> with the perk.
    /// </summary>
    internal static float Factor(bool inForest, bool inSettlement, bool keenSight)
    {
        if (!inForest || inSettlement)
            return 0f;
        return keenSight ? ForestPenalty * KeenSightShare : ForestPenalty;
    }
}
