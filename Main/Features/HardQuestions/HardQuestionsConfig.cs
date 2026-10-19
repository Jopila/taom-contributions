namespace TAOM.Features.HardQuestions;

/// <summary>
/// JSON DTO for <c>hard_questions/hard_questions.json</c>, validated by
/// <see cref="HardQuestionsConfigProvider"/>. Penalties are at most 0, rewards and multipliers at least 0,
/// chances in [0, 1]. Trait values are experience points, relation values are relation points.
/// </summary>
public class HardQuestionsConfig
{
    // --- Bribe ---
    public float BribeMultiplierClan { get; set; } = 1.5f;
    public float BribeMultiplierKingdom { get; set; } = 3.5f;

    /// <summary>Applied to the price when the lord is Calculating (a haggler takes less).</summary>
    public float CalculatingBribeFactor { get; set; } = 0.5f;
    public float CharmXpClan { get; set; } = 50f;
    public float CharmXpKingdom { get; set; } = 90f;

    // --- Force ---
    public float WoundChance { get; set; } = 0.15f;
    public float DeathChance { get; set; } = 0.05f;
    public float RogueryXpClan { get; set; } = 80f;
    public float RogueryXpKingdom { get; set; } = 150f;
    public float ForceLordRelationClan { get; set; } = -20f;
    public float ForceLordRelationKingdom { get; set; } = -40f;
    public float ForceFamilyRelationClan { get; set; } = -10f;
    public float ForceFamilyRelationKingdom { get; set; } = -20f;
    public float ForceKingRelationKingdom { get; set; } = -16f;
    public float ForceMercyXpClan { get; set; } = -160f;
    public float ForceMercyXpKingdom { get; set; } = -300f;
    public float ForceHonorXpClan { get; set; } = -100f;
    public float ForceHonorXpKingdom { get; set; } = -200f;
    public float DeathExtraHonorXp { get; set; } = -200f;
    public float CompanionMercyMaxLoss { get; set; } = -2f;

    // --- Strip ---
    public float StripHonorXp { get; set; } = -1000f;
    public float StripRenownLoss { get; set; } = -200f;
    public float StripLordRelation { get; set; } = -100f;
    public float StripKingdomRelation { get; set; } = -15f;
    public float StripHonorLordRelation { get; set; } = -10f;
    public float CompanionHonorMaxLoss { get; set; } = -10f;

    // --- Alignment scaling of trait experience losses ---
    public float SameSideTraitMultiplier { get; set; } = 1.5f;
    public float EnemySideTraitMultiplier { get; set; } = 0.5f;
    public float NeutralTraitMultiplier { get; set; } = 1f;
}
