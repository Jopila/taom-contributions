using System;

namespace TAOM.Features.SmithsMeasure;

/// <summary>
/// The smelt cap of Smith's Measure (#910) on the engine's smelting output, a plain array in <c>CraftingMaterials</c> order:
/// iron ore (0), the six iron grades (1 to 6), hardwood (7), charcoal (8). Slots 0 to 6 are lowered to the cap. Hardwood is
/// dropped when any of them comes out, else lowered to the cap. Charcoal, which holds the engine's own cost as a negative
/// number, every negative value and any slot past charcoal are left alone. The cap only ever lowers a positive value, so a
/// result is never larger than the engine's. The input is not changed.
/// </summary>
internal static class SmeltingYieldCap
{
    private const int LastMetalSlot = 6;
    private const int WoodSlot = 7;

    public static int[] Apply(int[] output, int cap)
    {
        if (output == null || cap < 1)
            return output!;

        var result = (int[])output.Clone();
        var metalOut = false;
        for (var slot = 0; slot <= LastMetalSlot && slot < result.Length; slot++)
        {
            if (result[slot] > 0)
                metalOut = true;
            result[slot] = Math.Min(result[slot], cap);
        }

        if (result.Length > WoodSlot && result[WoodSlot] > 0)
            result[WoodSlot] = metalOut ? 0 : Math.Min(result[WoodSlot], cap);

        return result;
    }
}
