using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>A settlement's dungeon for the Warden's Tally menu line, from the same rosters as vanilla's "There are N prisoners here." (whose N counts heroes only, so it is <see cref="HeldLords"/>).</summary>
public sealed class DungeonReading
{
    /// <summary>Prisoners that are not heroes (<c>PrisonRoster.TotalRegulars</c>).</summary>
    public int HeldRegulars { get; set; }

    /// <summary>Prisoners that are heroes (<c>PrisonRoster.TotalHeroes</c>), the lords the dungeon holds.</summary>
    public int HeldLords { get; set; }

    /// <summary>The settlement party's prisoner size limit (<c>PartyBase.PrisonerSizeLimit</c>).</summary>
    public int Capacity { get; set; }

    /// <summary>One entry per prisoner hero the rosters name, in roster order.</summary>
    public List<DungeonLord> Lords { get; set; } = new List<DungeonLord>();
}

/// <summary>One prisoner lord of a dungeon: his name, his escape snapshot and the perk terms behind its factor.</summary>
public sealed class DungeonLord
{
    public string Name { get; set; } = string.Empty;

    public PrisonerEscapeSnapshot Snapshot { get; set; } = new PrisonerEscapeSnapshot();

    /// <summary>
    /// The terms of <see cref="PrisonerEscapeSnapshot.EscapeFactor"/>, one line per term the engine describes, from the
    /// same <c>ExplainedNumber</c> the factor is read from, so the lines and the factor cannot differ.
    /// </summary>
    public List<EscapeTermLine> Terms { get; set; } = new List<EscapeTermLine>();
}

/// <summary>One described term of the escape factor: the engine's own description text and its signed change.</summary>
public sealed class EscapeTermLine
{
    public EscapeTermLine(string description, float change)
    {
        Description = description;
        Change = change;
    }

    /// <summary>The engine's description, such as a perk's name.</summary>
    public string Description { get; }

    /// <summary>The term's change to the chance as a fraction of it: 0.1 is +10%, -0.25 is -25%.</summary>
    public float Change { get; }
}
