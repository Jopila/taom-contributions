namespace TAOM.Adapters;

/// <summary>What the player chose from a tribute envoy's offer (#870 slice 2).</summary>
public enum EnvoyChoice
{
    /// <summary>Pay the price: the host spares the player's clan.</summary>
    Pay,

    /// <summary>Try once to cut the price, with a Charm roll.</summary>
    Haggle,

    /// <summary>Ask for time; the envoy returns after the configured days at the same price and nothing is spared meanwhile.</summary>
    Delay,

    /// <summary>Send the envoy away empty-handed.</summary>
    Refuse,

    /// <summary>Seize the envoy: the side's next host rides against the player's kingdom first, and every later tribute costs more.</summary>
    Seize,
}

/// <summary>The line the player reads after a choice.</summary>
public enum EnvoyOutcome
{
    Paid,

    /// <summary>The player chose to pay with too little gold; the offer comes back.</summary>
    CannotAfford,

    HaggleWon,

    HaggleLost,

    Delayed,

    Refused,

    Seized,
}

/// <summary>
/// What an envoy's popup shows: the host's keyed name, the villages and price it asks, the player's gold, and which
/// options are open. Plain values, so a service builds it without touching an engine type.
/// </summary>
public sealed class EnvoyOffer
{
    /// <summary>The host's name as a keyed string (<c>{=key}English</c>).</summary>
    public string KeyedHostName { get; set; } = string.Empty;

    /// <summary>How many villages of the player's clan the tribute covers.</summary>
    public int Villages { get; set; }

    public int Price { get; set; }

    /// <summary>The player's gold when the offer is made.</summary>
    public int Gold { get; set; }

    /// <summary>Whether the haggle option is shown: the tribute is large enough and no try was made.</summary>
    public bool CanHaggle { get; set; }

    /// <summary>Days of grace the "ask for time" option gives.</summary>
    public int DelayDays { get; set; }

    public bool CanAfford => Gold >= Price;
}

/// <summary>The player as the tribute envoy reads them: their clan, its kingdom, their gold and Charm, and the villages bound to the clan's fiefs.</summary>
public readonly struct EnvoyPlayerReading
{
    public EnvoyPlayerReading(bool exists, string? clanId, string? kingdomId, int gold, int charm, int villages)
    {
        Exists = exists;
        ClanId = clanId;
        KingdomId = kingdomId;
        Gold = gold;
        Charm = charm;
        Villages = villages;
    }

    public static EnvoyPlayerReading Missing => default;

    public bool Exists { get; }

    public string? ClanId { get; }

    /// <summary>The kingdom the player's clan belongs to, or null when it has none.</summary>
    public string? KingdomId { get; }

    public int Gold { get; }

    public int Charm { get; }

    /// <summary>The villages bound to the fiefs the clan holds (towns and castles), the ones a host would raid in its name.</summary>
    public int Villages { get; }
}
