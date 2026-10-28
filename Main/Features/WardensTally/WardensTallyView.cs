using System.Collections.Generic;
using TAOM.Adapters;

namespace TAOM.Features.WardensTally;

/// <summary>What the dungeon menu's Warden's Tally line says (docs/features/wardens-tally.md), as numbers and names, with chances as fractions of a day.</summary>
public sealed class WardensTallyView
{
    /// <summary>Prisoners held, lords included.</summary>
    public int Held { get; set; }

    public int Capacity { get; set; }

    /// <summary>How many of the prisoners are lords.</summary>
    public int Lords { get; set; }

    /// <summary>The one daily chance every lord of the dungeon shares, with its terms; null when the engine vetoes every lord.</summary>
    public TallyOdds? Odds { get; set; }

    /// <summary>Lords whose kingdom has a War Chronicle prisoner-escape boost that applies to them.</summary>
    public List<TallyBoost> Boosts { get; set; } = new List<TallyBoost>();

    /// <summary>Names of the lords the engine's <c>CanHeroBeReleased</c> veto holds.</summary>
    public List<string> Vetoed { get; set; } = new List<string>();
}

/// <summary>The shared daily chance and what it is made of.</summary>
public sealed class TallyOdds
{
    public float Chance { get; set; }

    /// <summary>The base chance before the halving and the terms.</summary>
    public float Base { get; set; }

    /// <summary>The player's clan holds the lords: the chance is halved.</summary>
    public bool Halved { get; set; }

    /// <summary>The engine's described terms, in the engine's order, each a fraction of the base chance.</summary>
    public List<EscapeTermLine> Terms { get; set; } = new List<EscapeTermLine>();
}

public sealed class TallyBoost
{
    public TallyBoost(string name, float combinedChance)
    {
        Name = name;
        CombinedChance = combinedChance;
    }

    public string Name { get; }

    /// <summary>One minus the odds of escaping neither by vanilla's roll nor by the extra roll.</summary>
    public float CombinedChance { get; }
}
