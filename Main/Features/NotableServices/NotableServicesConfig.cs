namespace TAOM.Features.NotableServices;

/// <summary>
/// JSON DTO for <c>notable_services/notable_services.json</c>, validated by <see cref="NotableServicesConfigProvider"/>
/// (docs/features/notable-services.md). Every value is a float so the shared finite-number check applies; the two
/// counts (<see cref="RevealCount"/>, <see cref="MaxMenPerGift"/>) are rounded where they are used.
/// </summary>
public class NotableServicesConfig
{
    /// <summary>Denars per unmet notable for an introduction. Range [0, 100000].</summary>
    public float IntroducePrice { get; set; } = 50f;

    /// <summary>Denars for one pointing-out of the holds nearby. Range [0, 100000].</summary>
    public float RevealPrice { get; set; } = 100f;

    /// <summary>How many nearby towns and castles one pointing-out marks. Range [1, 12], rounded.</summary>
    public float RevealCount { get; set; } = 3f;

    /// <summary>The farthest hold the notable names, as a share of the campaign map's diagonal. Range (0, 1].</summary>
    public float RevealMaxDiagonalFraction { get; set; } = 0.2f;

    /// <summary>The relation the notable needs before he names the holds. Range [-100, 100].</summary>
    public float RevealMinRelation { get; set; } = 0f;

    /// <summary>Relation per man given, before the relation divisor. Range [0, 10].</summary>
    public float GainPerMan { get; set; } = 0.5f;

    /// <summary>The most relation one gift can earn. Range [0, 100].</summary>
    public float MaxGainPerGift { get; set; } = 20f;

    /// <summary>Militia added to the settlement for each troop given. Range [0, 10].</summary>
    public float MilitiaPerTroop { get; set; } = 1f;

    /// <summary>Hearth added to a village for each prisoner given. Range [0, 10].</summary>
    public float HearthPerPrisoner { get; set; } = 1f;

    /// <summary>Prosperity added to a town for each prisoner given. Range [0, 100].</summary>
    public float ProsperityPerPrisoner { get; set; } = 1f;

    /// <summary>Roguery experience for each prisoner given. Range [0, 1000].</summary>
    public float RogueryXpPerPrisoner { get; set; } = 10f;

    /// <summary>The most men one gift counts, and the most the screen lets the player move. Range [1, 1000].</summary>
    public float MaxMenPerGift { get; set; } = 100f;
}
