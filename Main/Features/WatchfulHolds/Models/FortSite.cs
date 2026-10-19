namespace TAOM.Features.WatchfulHolds.Models;

/// <summary>A castle or town that watches the roads: its settlement id and map position. Static for a campaign.</summary>
public readonly struct FortSite
{
    public FortSite(string id, float x, float y)
    {
        Id = id;
        X = x;
        Y = y;
    }

    public string Id { get; }

    public float X { get; }

    public float Y { get; }
}
