using System;
using TAOM.Adapters;

namespace TAOM.Features.SmithsMeasure;

/// <summary>What the Make Tools card shows: hidden, or ready, or the one thing that is missing.</summary>
public enum MakeToolsVerdict
{
    Ready,
    Disabled,
    NeedsIron,
    NeedsHardwood,
    TooTired,
}

/// <summary>The verdict with the stamina it would cost and the Crafting experience it would pay.</summary>
public readonly struct MakeToolsQuote
{
    public MakeToolsQuote(MakeToolsVerdict verdict, int staminaCost, float skillXp)
    {
        Verdict = verdict;
        StaminaCost = staminaCost;
        SkillXp = skillXp;
    }

    public MakeToolsVerdict Verdict { get; }

    /// <summary>Crafting stamina one press costs, at least 1.</summary>
    public int StaminaCost { get; }

    /// <summary>Crafting experience one press pays, never negative.</summary>
    public float SkillXp { get; }
}

/// <summary>
/// Make Tools (#910): one iron and one hardwood make one Tools good. The cost is the smithing model's own refining cost for the
/// recipe, floored at 1 because the model's stacked reductions can reach 0 or below. The experience is 30 percent of the value
/// the recipe adds (the tools minus the two inputs), never below 0. Everything the engine owns arrives through
/// <see cref="ICraftingWorkshopAdapter"/>.
/// </summary>
public sealed class MakeToolsService
{
    private const int MinimumStaminaCost = 1;
    private const double XpShareOfValueAdded = 0.30;

    // Vanilla allows every smithy action only above this stamina (CraftingVM.cs:1038), and a co-op host refuses at or below it.
    private const int VanillaStaminaFloor = 10;

    private readonly ISmithsMeasureSettingsProvider _settings;

    public MakeToolsService(ISmithsMeasureSettingsProvider settings) => _settings = settings;

    /// <summary>The verdict for this hero's workshop. A switched-off feature answers <see cref="MakeToolsVerdict.Disabled"/> without reading the workshop.</summary>
    public MakeToolsQuote Quote(ICraftingWorkshopAdapter workshop)
    {
        if (!_settings.MakeToolsEnabled)
            return new MakeToolsQuote(MakeToolsVerdict.Disabled, 0, 0f);

        var cost = Math.Max(MinimumStaminaCost, workshop.RefiningCost);
        var xp = SkillXpFor(workshop);
        if (workshop.IronCount < 1)
            return new MakeToolsQuote(MakeToolsVerdict.NeedsIron, cost, xp);
        if (workshop.WoodCount < 1)
            return new MakeToolsQuote(MakeToolsVerdict.NeedsHardwood, cost, xp);
        if (workshop.Stamina <= VanillaStaminaFloor || workshop.Stamina < cost)
            return new MakeToolsQuote(MakeToolsVerdict.TooTired, cost, xp);
        return new MakeToolsQuote(MakeToolsVerdict.Ready, cost, xp);
    }

    /// <summary>Makes one Tools if the verdict is ready: takes the inputs, gives the tools, spends the stamina, pays the experience.</summary>
    public bool TryMake(ICraftingWorkshopAdapter workshop)
    {
        var quote = Quote(workshop);
        if (quote.Verdict != MakeToolsVerdict.Ready)
            return false;

        var stamina = workshop.Stamina;
        workshop.TakeInputs();
        workshop.GiveTools();
        workshop.SetStamina(stamina - quote.StaminaCost);
        if (quote.SkillXp > 0f)
            workshop.AddCraftingXp(quote.SkillXp);
        return true;
    }

    // long arithmetic: int.MaxValue minus int.MinValue overflows an int.
    private static float SkillXpFor(ICraftingWorkshopAdapter workshop)
    {
        var added = (long)workshop.ToolsValue - workshop.IronValue - workshop.WoodValue;
        return added <= 0 ? 0f : (float)(added * XpShareOfValueAdded);
    }
}
