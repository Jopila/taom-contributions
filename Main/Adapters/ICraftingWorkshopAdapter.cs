namespace TAOM.Adapters;

/// <summary>
/// One crafting hero at the smithing screen, as Make Tools sees them (#910, ADR-007): the party's iron and hardwood, the hero's
/// crafting stamina and the smithing model's numbers for the recipe. Plain values in and out, so the engine's sealed hero,
/// roster and items never reach the service. The recipe is one iron (the third grade) and one hardwood for one Tools good.
/// </summary>
public interface ICraftingWorkshopAdapter
{
    /// <summary>Iron (third grade) in the main party's stores.</summary>
    int IronCount { get; }

    /// <summary>Hardwood in the main party's stores.</summary>
    int WoodCount { get; }

    /// <summary>The hero's crafting stamina now.</summary>
    int Stamina { get; }

    /// <summary>The smithing model's stamina cost of the recipe for this hero, before any floor; it can be 0 or below.</summary>
    int RefiningCost { get; }

    /// <summary>The engine value of one Tools good.</summary>
    int ToolsValue { get; }

    /// <summary>The engine value of one iron.</summary>
    int IronValue { get; }

    /// <summary>The engine value of one hardwood.</summary>
    int WoodValue { get; }

    /// <summary>Removes one iron and one hardwood from the party's stores.</summary>
    void TakeInputs();

    /// <summary>Adds one Tools good to the party's stores.</summary>
    void GiveTools();

    /// <summary>Sets the hero's crafting stamina (the engine floors it at 0).</summary>
    void SetStamina(int value);

    /// <summary>Gives the hero Crafting experience.</summary>
    void AddCraftingXp(float xp);
}
