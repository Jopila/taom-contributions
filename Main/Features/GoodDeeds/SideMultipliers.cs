namespace TAOM.Features.GoodDeeds;

/// <summary>The multiplier for each way two sides can stand to each other, one set per deed.</summary>
public sealed record SideMultipliers(float Same, float Neutral, float Opposite)
{
    public float For(SidePairing pairing) => pairing switch
    {
        SidePairing.Same => Same,
        SidePairing.Opposite => Opposite,
        _ => Neutral,
    };
}
