using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// The engine side of Lines of Descent (#905): the family graph and the standing facts the root ranking needs, keyed by
/// <c>Hero.StringId</c>. Every method needs a running campaign and changes nothing. A lookup of an unknown id answers the
/// empty value (null, empty list, false), never throws. The lists are the engine's raw lists: they can hold duplicates
/// (the parent and spouse setters append on every change) and stale entries, and the service cleans them.
/// </summary>
public interface IHeroFamilyAdapter
{
    bool Exists(string heroId);

    string? FatherId(string heroId);

    string? MotherId(string heroId);

    string? SpouseId(string heroId);

    /// <summary>Former spouses in the engine's order, duplicates included.</summary>
    IReadOnlyList<string> ExSpouseIds(string heroId);

    /// <summary>The hero's children in the engine's list order, which is birth order unless a setter re-ran.</summary>
    IReadOnlyList<string> ChildIds(string heroId);

    /// <summary>Siblings: the union of both parents' children, the hero excluded, no duplicates.</summary>
    IReadOnlyList<string> SiblingIds(string heroId);

    bool IsFemale(string heroId);

    /// <summary>The birth day as days on the campaign clock, so heroes sort by age.</summary>
    double BirthDay(string heroId);

    /// <summary>True when the hero has an encyclopedia page (the page's own validity test).</summary>
    bool IsListed(string heroId);

    string? ClanId(string heroId);

    bool IsClanLeader(string heroId);

    string? KingdomId(string heroId);

    bool IsKingdomLeader(string heroId);

    /// <summary>True when the hero's clan rules the hero's kingdom.</summary>
    bool InRulingClan(string heroId);

    /// <summary>True when the hero's clan is a minor faction.</summary>
    bool InMinorFaction(string heroId);
}
