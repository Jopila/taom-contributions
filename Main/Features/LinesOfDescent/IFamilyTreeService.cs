namespace TAOM.Features.LinesOfDescent;

/// <summary>Lines of Descent (#905): the root of a hero's line, the tree under it, and one hero's relation to another.</summary>
public interface IFamilyTreeService
{
    /// <summary>The hero at the head of the selected hero's line; the hero itself when no parent qualifies.</summary>
    string FindRoot(string selectedId);

    /// <summary>The tree under the selected hero's root, every card labelled against the selected hero, or null for an unknown hero.</summary>
    FamilyTreeResult? Build(string selectedId);

    /// <summary>How the queried hero is related to the base hero, or null when there is no path within the hop cap.</summary>
    RelationPhrase? Describe(string queriedId, string baseId);
}
