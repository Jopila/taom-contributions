using TAOM.Features.ManyHammers;

namespace TAOM.Adapters;

/// <summary>One repeatable unit of smithy work (smelting one weapon, running one refining formula) behind the engine's crafting behavior.</summary>
public interface ICraftBenchUnit
{
    /// <summary>The live numbers for the next unit: stamina, its cost, its effect on the nine materials, the party's stock.</summary>
    UnitSnapshot Read();

    /// <summary>Does one unit through the engine's own crafting behavior.</summary>
    void DoOne();
}
