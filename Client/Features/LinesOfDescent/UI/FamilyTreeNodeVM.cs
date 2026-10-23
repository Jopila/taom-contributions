using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Items;
using TaleWorlds.Library;

namespace TAOM.Features.LinesOfDescent.UI;

/// <summary>
/// One hero card of the Lines of Descent popup (#905). It is vanilla's encyclopedia family card (portrait, name, link to the
/// hero's page) built with no base hero, which skips vanilla's relation call; the role line is the phrase the core service built,
/// or the "See above" stub for a hero drawn elsewhere in the tree.
/// </summary>
internal sealed class FamilyTreeCardVM : EncyclopediaFamilyMemberVM
{
    public FamilyTreeCardVM(Hero hero, FamilyTreeCard card) : base(hero, null)
    {
        IsSelected = card.IsSelected;
        if (card.IsRepeat)
            Role = FamilyTreeTexts.Repeat().ToString();
        else if (card.Phrase != null)
            Role = FamilyTreeTexts.Render(card.Phrase).ToString();
        else
            Role = "";
    }

    [DataSourceProperty]
    public bool IsSelected { get; }
}

/// <summary>
/// One couple row of the tree and the branch under it, bound by TaomFamilyTreeNode.xml. A node of only <see cref="MoreText"/>
/// stands for children a cap left out. The whole tree is built in the constructor, before the movie loads: the vanilla tree
/// widget draws its connecting lines as list items are added and never redraws them.
/// </summary>
internal sealed class FamilyTreeNodeVM : ViewModel
{
    public FamilyTreeNodeVM(FamilyTreeNode node)
    {
        Members = new MBBindingList<FamilyTreeCardVM>();
        Branch = new MBBindingList<FamilyTreeNodeVM>();
        foreach (var card in node.Members)
        {
            var hero = Hero.Find(card.HeroId);
            if (hero != null)
                Members.Add(new FamilyTreeCardVM(hero, card));
        }

        IsMore = node.MoreCount > 0;
        MoreText = IsMore ? FamilyTreeTexts.More(node.MoreCount).ToString() : "";
        foreach (var child in node.Children)
        {
            var vm = new FamilyTreeNodeVM(child);
            if (vm.HasContent)
                Branch.Add(vm);
        }
    }

    [DataSourceProperty]
    public MBBindingList<FamilyTreeCardVM> Members { get; }

    [DataSourceProperty]
    public MBBindingList<FamilyTreeNodeVM> Branch { get; }

    [DataSourceProperty]
    public bool IsMore { get; }

    [DataSourceProperty]
    public string MoreText { get; }

    /// <summary>False for a row whose heroes no longer resolve and that stands for nothing else; the parent drops it.</summary>
    internal bool HasContent => IsMore || Members.Count > 0;

    public override void OnFinalize()
    {
        base.OnFinalize();
        foreach (var member in new List<FamilyTreeCardVM>(Members))
            member.OnFinalize();
        foreach (var child in new List<FamilyTreeNodeVM>(Branch))
            child.OnFinalize();
    }
}
