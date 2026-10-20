namespace TAOM.Features.LordCourtesies;

/// <summary>
/// JSON DTO for <c>lord_courtesies/lord_courtesies.json</c>, validated by <see cref="LordCourtesiesConfigProvider"/>.
/// It holds the holdings question's values only: the troop gift is paid by Bonds of Battle's hand-over rule. Costs
/// are positive amounts of relation the service subtracts, never stored as negative values. All values are whole
/// numbers in practice; they are floats so the shared finite-number check applies.
/// </summary>
public class LordCourtesiesConfig
{
    /// <summary>The relation a lord needs with the player before he names his holdings.</summary>
    public float RevealMinRelation { get; set; } = 5f;

    /// <summary>Relation lost for asking a lord who does not know the player well enough.</summary>
    public float RefusalCost { get; set; } = 1f;

    /// <summary>Relation lost for asking a willing lord whose clan holds nothing.</summary>
    public float LandlessCost { get; set; } = 5f;
}
