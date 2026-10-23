using System.Collections.Generic;
using TAOM.Adapters;

namespace TAOM.Features.LinesOfDescent;

/// <summary>
/// One breadth-first search from a base hero across the family graph (Lines of Descent, #905). The edge order is
/// father, mother, spouse, children, siblings, former spouses, and the first hero to reach a node keeps it, so equal-length
/// paths always resolve the same way. One search labels every card in a tree.
/// </summary>
internal sealed class RelationPathFinder
{
    internal const int HopCap = 6;
    internal const int VisitedCap = 5000;

    private enum Edge
    {
        Parent,
        Child,
        Spouse,
        Sibling,
        ExSpouse,
    }

    private readonly IHeroFamilyAdapter _family;
    private readonly string _baseId;
    private readonly int _visitedCap;
    private readonly Dictionary<string, (string From, Edge Edge)> _found = new Dictionary<string, (string, Edge)>();

    internal RelationPathFinder(IHeroFamilyAdapter family, string baseId, int visitedCap = VisitedCap)
    {
        _family = family;
        _baseId = baseId;
        _visitedCap = visitedCap;
        Search();
    }

    /// <summary>The heroes the search reached, the base hero included.</summary>
    internal int VisitedCount => _found.Count + 1;

    private void Search()
    {
        if (!_family.Exists(_baseId))
            return;
        var depth = new Dictionary<string, int> { [_baseId] = 0 };
        var queue = new Queue<string>();
        queue.Enqueue(_baseId);
        while (queue.Count > 0 && depth.Count < _visitedCap)
        {
            var u = queue.Dequeue();
            if (depth[u] >= HopCap)
                continue;
            foreach (var (v, edge) in Neighbours(u))
            {
                if (depth.Count >= _visitedCap)
                    return;
                if (depth.ContainsKey(v) || !_family.Exists(v) || !_family.IsListed(v))
                    continue;
                depth[v] = depth[u] + 1;
                _found[v] = (u, edge);
                queue.Enqueue(v);
            }
        }
    }

    private IEnumerable<(string, Edge)> Neighbours(string u)
    {
        var father = _family.FatherId(u);
        if (father != null)
            yield return (father, Edge.Parent);
        var mother = _family.MotherId(u);
        if (mother != null)
            yield return (mother, Edge.Parent);
        var spouse = _family.SpouseId(u);
        if (spouse != null)
            yield return (spouse, Edge.Spouse);
        foreach (var c in CurrentChildren(u))
            yield return (c, Edge.Child);
        foreach (var s in _family.SiblingIds(u))
            if (SharesAParent(u, s))
                yield return (s, Edge.Sibling);
        foreach (var x in _family.ExSpouseIds(u))
            yield return (x, Edge.ExSpouse);
    }

    // The hero's children whose own father or mother link points back, oldest first, ties by id: the same order the tree draws.
    // A child entry the child no longer agrees with is stale and is no relation.
    private List<string> CurrentChildren(string parent)
    {
        var result = new List<string>();
        foreach (var c in _family.ChildIds(parent))
            if (_family.FatherId(c) == parent || _family.MotherId(c) == parent)
                result.Add(c);
        result.Sort((a, b) =>
        {
            var byBirth = _family.BirthDay(a).CompareTo(_family.BirthDay(b));
            return byBirth != 0 ? byBirth : string.CompareOrdinal(a, b);
        });
        return result;
    }

    // Siblings need a current parent in common, not just a place in a parent's child list.
    private bool SharesAParent(string a, string b)
    {
        var father = _family.FatherId(a);
        var mother = _family.MotherId(a);
        return (father != null && father == _family.FatherId(b)) || (mother != null && mother == _family.MotherId(b));
    }

    /// <summary>The phrase from the queried hero to the base, or null for the base itself or a hero out of reach.</summary>
    internal RelationPhrase? PhraseFor(string queriedId)
    {
        if (queriedId == _baseId || !_found.ContainsKey(queriedId))
            return null;
        var steps = new List<RelationStep>();
        var current = queriedId;
        while (current != _baseId)
        {
            var (from, edge) = _found[current];
            var female = _family.IsFemale(current);
            switch (edge)
            {
                case Edge.Parent:
                    // current is the parent of `from`.
                    steps.Add(new RelationStep(female ? RelationKind.Mother : RelationKind.Father, from));
                    break;
                case Edge.Child:
                    steps.Add(ChildStep(current, from, female));
                    break;
                case Edge.Spouse:
                    steps.Add(new RelationStep(female ? RelationKind.Wife : RelationKind.Husband, from));
                    break;
                case Edge.Sibling:
                    steps.Add(new RelationStep(female ? RelationKind.Sister : RelationKind.Brother, from));
                    break;
                default:
                    steps.Add(new RelationStep(female ? RelationKind.ExWife : RelationKind.ExHusband, from));
                    break;
            }

            current = from;
        }

        return new RelationPhrase(steps);
    }

    // The last step (into the base) names both parents when the child has both; the others name the next hero only.
    private RelationStep ChildStep(string child, string parent, bool female)
    {
        var kind = female ? RelationKind.Daughter : RelationKind.Son;
        if (parent == _baseId)
        {
            var father = _family.FatherId(child);
            var mother = _family.MotherId(child);
            if (father != null && mother != null)
                return new RelationStep(kind, parent, father, mother);
        }

        return new RelationStep(kind, parent);
    }
}
