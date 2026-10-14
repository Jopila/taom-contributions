namespace TAOM.Adapters;

/// <summary>A landing point the adapter accepted (valid, on the player's island, no enemy of the hunter nearby).</summary>
public readonly struct HuntLanding
{
    public HuntLanding(bool ok, float x, float y)
    {
        Ok = ok;
        X = x;
        Y = y;
    }

    public bool Ok { get; }

    public float X { get; }

    public float Y { get; }
}
