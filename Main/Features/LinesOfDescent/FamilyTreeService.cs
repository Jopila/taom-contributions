using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;

namespace TAOM.Features.LinesOfDescent;

/// <summary>
/// Lines of Descent (#905): finds the head of a hero's line, builds the tree of descendants under it and phrases how each
/// card is related to the selected hero. Pure logic over <see cref="IHeroFamilyAdapter"/>; it holds no state and saves nothing.
/// </summary>
public sealed class FamilyTreeService : IFamilyTreeService
{
    public const int MaxClimbSteps = 32;
    public const int MaxGenerations = 12;
    public const int MaxCards = 300;

    private readonly IHeroFamilyAdapter _family;

    public FamilyTreeService(IHeroFamilyAdapter family)
    {
        _family = family;
    }

    // ---- root -------------------------------------------------------------------------------------------------

    /// <summary>The rank of a hero as a head of line; <see cref="RootTier.None"/> when the hero has no clan or is not listed.</summary>
    public RootTier TierOf(string heroId)
    {
        if (!_family.Exists(heroId) || !_family.IsListed(heroId) || _family.ClanId(heroId) == null)
            return RootTier.None;
        var inKingdom = _family.KingdomId(heroId) != null;
        var leader = _family.IsClanLeader(heroId);
        if (_family.IsKingdomLeader(heroId))
            return RootTier.KingdomLeader;
        if (inKingdom && _family.InRulingClan(heroId))
            return RootTier.RulingClanMember;
        if (inKingdom && leader)
            return RootTier.ClanLeaderInKingdom;
        if (inKingdom)
            return RootTier.KingdomMember;
        if (_family.InMinorFaction(heroId))
            return leader ? RootTier.MinorFactionLeader : RootTier.MinorFactionMember;
        return leader ? RootTier.ClanLeader : RootTier.ClanMember;
    }

    public string FindRoot(string selectedId)
    {
        var climb = Climb(selectedId, MaxClimbSteps);
        return climb[climb.Count - 1];
    }

    // The selected hero first, then each better-ranked parent up to the root, at most maxSteps parents.
    private List<string> Climb(string selectedId, int maxSteps)
    {
        var visited = new HashSet<string> { selectedId };
        var climb = new List<string> { selectedId };
        var current = selectedId;
        for (var step = 0; step < maxSteps; step++)
        {
            var father = Candidate(_family.FatherId(current), visited);
            var mother = Candidate(_family.MotherId(current), visited);
            var next = father;
            if (mother != null && (father == null || TierOf(mother) < TierOf(father)))
                next = mother;
            if (next == null)
                break;
            visited.Add(next);
            climb.Add(next);
            current = next;
        }

        return climb;
    }

    private string? Candidate(string? id, HashSet<string> visited)
        => id != null && !visited.Contains(id) && TierOf(id) != RootTier.None ? id : null;

    // ---- relation ---------------------------------------------------------------------------------------------

    public RelationPhrase? Describe(string queriedId, string baseId)
        => new RelationPathFinder(_family, baseId).PhraseFor(queriedId);

    // ---- tree -------------------------------------------------------------------------------------------------

    private sealed class Pending
    {
        public string HeroId = "";
        public int Generation;
        public List<FamilyTreeCard> Members = new List<FamilyTreeCard>();
        public List<object> Children = new List<object>();
        public int More;
        public string? PathParentId;
        public bool Placed;
    }

    public FamilyTreeResult? Build(string selectedId)
    {
        if (!_family.Exists(selectedId))
            return null;
        // The climb stops a generation short of the cap, so the selected hero is always inside the drawn generations.
        var climb = Climb(selectedId, MaxGenerations - 1);
        climb.Reverse();
        var rootId = climb[0];
        var paths = new RelationPathFinder(_family, selectedId);
        var drawn = new HashSet<string>();
        var cards = 0;

        var root = NewNode(rootId, 1, selectedId, paths, drawn, ref cards);
        var pathNodes = DrawPath(climb, root, selectedId, paths, drawn, ref cards);
        var queue = new Queue<Pending>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            var kids = ChildrenOf(node.HeroId);
            if (node.Generation >= MaxGenerations)
            {
                node.More = kids.Count;
                continue;
            }

            for (var i = 0; i < kids.Count; i++)
            {
                if (TryPlacePathNode(node, kids[i], pathNodes, queue))
                    continue;
                if (drawn.Contains(kids[i]))
                {
                    node.Children.Add(Repeat(kids[i], selectedId, paths));
                    continue;
                }

                if (cards + RowCost(kids[i], drawn) > MaxCards)
                {
                    // The row does not fit whole: it and every later child go into the count, bar the selected hero's line.
                    for (var j = i; j < kids.Count; j++)
                        if (!TryPlacePathNode(node, kids[j], pathNodes, queue) && !drawn.Contains(kids[j]))
                            node.More++;
                    break;
                }

                var child = NewNode(kids[i], node.Generation + 1, selectedId, paths, drawn, ref cards);
                node.Children.Add(child);
                queue.Enqueue(child);
            }
        }

        return new FamilyTreeResult(rootId, Freeze(root), cards);
    }

    // The root-to-selected line is drawn before anything else, so no cap can leave the selected hero out. A link the engine's
    // child list does not confirm ends the line there.
    private Dictionary<string, Pending> DrawPath(List<string> climb, Pending root, string selectedId, RelationPathFinder paths, HashSet<string> drawn, ref int cards)
    {
        var pathNodes = new Dictionary<string, Pending>();
        var last = root;
        for (var i = 1; i < climb.Count; i++)
        {
            if (drawn.Contains(climb[i]) || !ChildrenOf(last.HeroId).Contains(climb[i]))
                break;
            var next = NewNode(climb[i], last.Generation + 1, selectedId, paths, drawn, ref cards);
            next.PathParentId = last.HeroId;
            pathNodes[climb[i]] = next;
            last = next;
        }

        return pathNodes;
    }

    // A pre-drawn node of the selected hero's line goes under its own parent, once, in the child's place in birth order.
    private static bool TryPlacePathNode(Pending parent, string kidId, Dictionary<string, Pending> pathNodes, Queue<Pending> queue)
    {
        if (!pathNodes.TryGetValue(kidId, out var onPath) || onPath.Placed || onPath.PathParentId != parent.HeroId)
            return false;
        onPath.Placed = true;
        parent.Children.Add(onPath);
        queue.Enqueue(onPath);
        return true;
    }

    // A "See above" stub row: one repeated card, no children. It does not count against the card cap.
    private FamilyTreeNode Repeat(string heroId, string selectedId, RelationPathFinder paths)
        => new FamilyTreeNode(new[] { Card(heroId, selectedId, paths, false, true) }, new FamilyTreeNode[0]);

    // The cards a couple row would add: the hero and each spouse not drawn yet.
    private int RowCost(string heroId, HashSet<string> drawn)
        => 1 + CoupleOf(heroId).Count(c => !drawn.Contains(c.Id));

    private Pending NewNode(string heroId, int generation, string selectedId, RelationPathFinder paths, HashSet<string> drawn, ref int cards)
    {
        drawn.Add(heroId);
        var node = new Pending { HeroId = heroId, Generation = generation };
        node.Members.Add(Card(heroId, selectedId, paths, false, false));
        cards++;
        foreach (var (id, former) in CoupleOf(heroId))
        {
            var again = !drawn.Add(id);
            node.Members.Add(Card(id, selectedId, paths, former, again));
            if (!again)
                cards++;
        }

        return node;
    }

    private FamilyTreeCard Card(string heroId, string selectedId, RelationPathFinder paths, bool exSpouse, bool repeat)
    {
        RelationPhrase? phrase = null;
        if (heroId != selectedId)
            phrase = paths.PhraseFor(heroId) ?? RelationPhrase.Kin(selectedId);
        return new FamilyTreeCard(heroId, phrase, heroId == selectedId, exSpouse, repeat);
    }

    // The current spouse, then former spouses in the engine's order: duplicates, the hero and the current spouse removed.
    private IEnumerable<(string Id, bool Former)> CoupleOf(string heroId)
    {
        var seen = new HashSet<string> { heroId };
        var spouse = _family.SpouseId(heroId);
        if (spouse != null && seen.Add(spouse) && _family.IsListed(spouse))
            yield return (spouse, false);
        foreach (var ex in _family.ExSpouseIds(heroId))
            if (seen.Add(ex) && _family.Exists(ex) && _family.IsListed(ex))
                yield return (ex, true);
    }

    // The hero's own listed children once each, oldest first, ties by id; a child whose parent is someone else is stale.
    private List<string> ChildrenOf(string heroId)
    {
        return _family.ChildIds(heroId)
            .Distinct()
            .Where(c => _family.Exists(c) && _family.IsListed(c)
                && (_family.FatherId(c) == heroId || _family.MotherId(c) == heroId))
            .OrderBy(c => _family.BirthDay(c))
            .ThenBy(c => c, System.StringComparer.Ordinal)
            .ToList();
    }

    private static FamilyTreeNode Freeze(Pending p)
    {
        var children = new List<FamilyTreeNode>();
        foreach (var c in p.Children)
            children.Add(c is Pending pending ? Freeze(pending) : (FamilyTreeNode)c);
        if (p.More > 0)
            children.Add(new FamilyTreeNode(new FamilyTreeCard[0], new FamilyTreeNode[0], p.More));
        return new FamilyTreeNode(p.Members, children);
    }
}
