namespace TAOM.Features.HiddenHosts;

/// <summary>What a party's nameplate shows (#846). <see cref="CountText"/> is null when the plate is left to vanilla; a masked plate also loses its wounded and prisoner text.</summary>
internal readonly struct NameplateIntel
{
    internal static NameplateIntel Unmasked => default;

    internal NameplateIntel(string countText, bool isHorizon)
    {
        Masked = true;
        CountText = countText;
        IsHorizon = isHorizon;
    }

    internal bool Masked { get; }

    internal string? CountText { get; }

    /// <summary>The party is past nine tenths of its own spotting reach: its count is "..." and it has no name.</summary>
    internal bool IsHorizon { get; }
}
