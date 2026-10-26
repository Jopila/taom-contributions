using System;

namespace TAOM.Features.ManyHammers;

/// <summary>
/// What the smithy needs to know to judge one more unit of work (a smelt or a refine), as plain numbers: the
/// hero's stamina, the stamina one unit costs, what one unit does to each of the nine crafting materials (negative
/// takes, positive gives), what the party holds of each, and how many units the source could ever supply (the stack
/// being smelted; unbounded for a refining formula). The slot order is the engine's <c>CraftingMaterials</c>.
/// </summary>
public sealed class UnitSnapshot
{
    /// <summary>The engine's material count (<c>CraftingMaterials.NumCraftingMats</c>).</summary>
    public const int Slots = 9;

    public UnitSnapshot(int stamina, int energyCost, int[]? delta, int[]? stock, int available)
    {
        Stamina = stamina;
        EnergyCost = energyCost;
        Available = Math.Max(0, available);
        Delta = Normalize(delta);
        Stock = Normalize(stock);
    }

    public int Stamina { get; }

    /// <summary>The stamina one unit costs, as the smithing model reports it.</summary>
    public int EnergyCost { get; }

    public int Available { get; }

    /// <summary>Nine slots, copied; a missing or short array reads as zeros.</summary>
    public int[] Delta { get; }

    /// <summary>Nine slots, copied; a missing or short array reads as zeros.</summary>
    public int[] Stock { get; }

    private static int[] Normalize(int[]? source)
    {
        var copy = new int[Slots];
        if (source != null)
            Array.Copy(source, copy, Math.Min(source.Length, Slots));
        return copy;
    }
}
