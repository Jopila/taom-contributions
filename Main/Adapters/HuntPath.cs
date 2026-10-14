using System;

namespace TAOM.Adapters;

/// <summary>
/// The Eye of Sauron hunter's rebuilt long-term path (#765 M8): the waypoints from the hunter to the player.
/// <see cref="Empty"/> when the rebuild failed or the path has no points.
/// </summary>
public sealed class HuntPath
{
    public static readonly HuntPath Empty = new(Array.Empty<float>(), Array.Empty<float>());

    public HuntPath(float[] xs, float[] ys)
    {
        Xs = xs ?? Array.Empty<float>();
        Ys = ys ?? Array.Empty<float>();
    }

    public float[] Xs { get; }

    public float[] Ys { get; }

    public int Count => Math.Min(Xs.Length, Ys.Length);
}
