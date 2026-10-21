namespace TAOM.Adapters;

/// <summary>A town or castle with its offset and gate distance from the main party.</summary>
public readonly struct ScoutSite
{
    public string Id { get; init; }

    public string Name { get; init; }

    public bool OwnedByPlayerClan { get; init; }

    public float Dx { get; init; }

    public float Dy { get; init; }

    /// <summary>Main party to the settlement's gate.</summary>
    public float Distance { get; init; }
}
