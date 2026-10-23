using System.Collections.Generic;

namespace TAOM.Features.LinesOfDescent;

/// <summary>
/// How highly a parent ranks as the head of a line (Lines of Descent, #905). Lower is better; the climb to the root
/// moves to the better-ranked parent and takes the father on a tie. <see cref="None"/> is a parent who cannot be a
/// root at all (no clan, or not listed in the encyclopedia).
/// </summary>
public enum RootTier
{
    KingdomLeader = 1,
    RulingClanMember = 2,
    ClanLeaderInKingdom = 3,
    KingdomMember = 4,
    MinorFactionLeader = 5,
    MinorFactionMember = 6,
    ClanLeader = 7,
    ClanMember = 8,
    None = 99,
}

/// <summary>The relation words. The word is chosen by the subject's gender; the target is named by the phrase step.</summary>
public enum RelationKind
{
    Father,
    Mother,
    Son,
    Daughter,
    Husband,
    Wife,
    Brother,
    Sister,
    ExHusband,
    ExWife,
}

/// <summary>
/// One step of a relation: the subject is the X of <see cref="TargetId"/>. <see cref="FatherId"/> and
/// <see cref="MotherId"/> are both set only for the two-parent form ("daughter of A and B"), used for the last
/// child-to-parent step; <see cref="TargetId"/> is then one of the two.
/// </summary>
public sealed class RelationStep
{
    public RelationStep(RelationKind kind, string targetId, string? fatherId = null, string? motherId = null)
    {
        Kind = kind;
        TargetId = targetId;
        FatherId = fatherId;
        MotherId = motherId;
    }

    public RelationKind Kind { get; }

    public string TargetId { get; }

    public string? FatherId { get; }

    public string? MotherId { get; }

    public bool IsTwoParent => FatherId != null && MotherId != null;
}

/// <summary>
/// How a card's hero is related to the hero whose tree is open: a chain of steps from the card's hero to the base
/// hero, or the plain "Kin of" form when no short path exists. Empty steps and no kin id never occur.
/// </summary>
public sealed class RelationPhrase
{
    public RelationPhrase(IReadOnlyList<RelationStep> steps, string? kinOfId = null)
    {
        Steps = steps;
        KinOfId = kinOfId;
    }

    public IReadOnlyList<RelationStep> Steps { get; }

    /// <summary>Set for the "Kin of {HERO}" form, which has no steps.</summary>
    public string? KinOfId { get; }

    public bool IsKin => KinOfId != null;

    public static RelationPhrase Kin(string baseId) => new RelationPhrase(new RelationStep[0], baseId);
}

/// <summary>One card: a hero (or a "See above" repeat of a hero already drawn).</summary>
public sealed class FamilyTreeCard
{
    public FamilyTreeCard(string heroId, RelationPhrase? phrase, bool isSelected, bool isExSpouse, bool isRepeat)
    {
        HeroId = heroId;
        Phrase = phrase;
        IsSelected = isSelected;
        IsExSpouse = isExSpouse;
        IsRepeat = isRepeat;
    }

    public string HeroId { get; }

    /// <summary>Null for the selected hero's own card.</summary>
    public RelationPhrase? Phrase { get; }

    public bool IsSelected { get; }

    /// <summary>True for a former spouse. The popup draws every spouse alike; the service tests pin the order.</summary>
    public bool IsExSpouse { get; }

    /// <summary>True for the "See above" stub of a hero drawn elsewhere in the tree.</summary>
    public bool IsRepeat { get; }
}

/// <summary>
/// A couple row and the lines under it. <see cref="Members"/> are the hero, the current spouse, then the former
/// spouses; a node with <see cref="MoreCount"/> above zero has no members and stands for children left out by a cap.
/// </summary>
public sealed class FamilyTreeNode
{
    public FamilyTreeNode(IReadOnlyList<FamilyTreeCard> members, IReadOnlyList<FamilyTreeNode> children, int moreCount = 0)
    {
        Members = members;
        Children = children;
        MoreCount = moreCount;
    }

    public IReadOnlyList<FamilyTreeCard> Members { get; }

    public IReadOnlyList<FamilyTreeNode> Children { get; }

    public int MoreCount { get; }
}

/// <summary>A built tree: the root hero's id, the node tree and the number of hero cards drawn.</summary>
public sealed class FamilyTreeResult
{
    public FamilyTreeResult(string rootId, FamilyTreeNode root, int cardCount)
    {
        RootId = rootId;
        Root = root;
        CardCount = cardCount;
    }

    public string RootId { get; }

    public FamilyTreeNode Root { get; }

    public int CardCount { get; }
}
