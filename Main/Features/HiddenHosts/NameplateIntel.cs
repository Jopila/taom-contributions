namespace TAOM.Features.HiddenHosts;

/// <summary>What a party's nameplate shows (#846). <see cref="CountText"/> is null when the plate is left to vanilla; a masked plate also loses its wounded and prisoner text.</summary>
internal readonly struct NameplateIntel
{
    internal static NameplateIntel Unmasked => default;

    /// <param name="named">
    /// Only a horizon plate can lose its name, and it does unless <paramref name="named"/>: the scout's report (#847) names six
    /// hosts so that they keep theirs past the horizon (their count still reads "...").
    /// </param>
    internal NameplateIntel(string countText, bool isHorizon, bool named = false)
    {
        Masked = true;
        CountText = countText;
        HidesName = isHorizon && !named;
    }

    internal bool Masked { get; }

    internal string? CountText { get; }

    /// <summary>The plate shows "Unknown party" instead of the party's name: it is at the horizon and no scout named it.</summary>
    internal bool HidesName { get; }
}
