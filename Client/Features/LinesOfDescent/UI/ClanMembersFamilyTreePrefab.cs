using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TAOM.Features.LinesOfDescent.UI;

// Lines of Descent (#905): the Family Tree button on the clan screen's Members tab, added as the last child of the selected
// member's "Last Seen Location" row (SandBox ClanMembers.xml:93, v1.5.5). That row sits inside the IsAnyValidMemberSelected
// subtree with DataSource {CurrentSelectedMember}, so the bindings below resolve on the ClanLordItemVM that
// ClanLordItemFamilyMixin extends. Container safety (gui-ui.md): the row is a plain ListPanel of widgets, not a data-bound
// list, and nothing indexes its children (grepped, v1.5.5); a new tail child shifts none of the existing ones. The row id also
// appears in the generated prefab copy, which TAOM does not use, and the co-op module adds its own button as a sibling, never
// inside the row. InsertType.Child with Index int.MaxValue appends the nodes in order (UIExtenderEx 2.13.3
// PrefabComponent.InsertAsChild), and the path ends in /Children so they land inside the ListPanel's Children element. The
// targeter and button copy the vanilla Show On Map pair in the same row (GamepadNavigationIndex 0, a scope per button). Brushes
// are Native Popup.xml:2 and :195. The look and the row's width still need an in-game check.
[PrefabExtension("ClanMembers", "descendant::ListPanel[@Id='LastSeenLocationParent']/Children")]
internal sealed class ClanMembersFamilyTreePrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Child;

    public override int Index => int.MaxValue;

    [PrefabExtensionXmlNodes]
    public IEnumerable<XmlNode> GetNodes()
    {
        var doc = new XmlDocument();
        doc.LoadXml(
            "<Root>"
            + "<NavigationScopeTargeter ScopeID=\"ClanMembersFamilyTreeButtonScope\" ScopeParent=\"..\\TaomFamilyTreeButton\""
            + " RightNavigationScope=\"None\" ExtendDiscoveryAreaTop=\"10\" ExtendDiscoveryAreaBottom=\"10\" />"
            + "<ButtonWidget Id=\"TaomFamilyTreeButton\" WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\""
            + " SuggestedWidth=\"180\" SuggestedHeight=\"40\" VerticalAlignment=\"Center\" MarginLeft=\"14\""
            + " Brush=\"Popup.Button.Base\" Command.Click=\"ExecuteOpenFamilyTree\" DoNotPassEventsToChildren=\"true\""
            + " UpdateChildrenStates=\"true\" GamepadNavigationIndex=\"0\">"
            + "<Children>"
            + "<TextWidget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\""
            + " Brush=\"Popup.Button.Text\" Text=\"@FamilyTreeText\" />"
            + "</Children>"
            + "</ButtonWidget>"
            + "</Root>");
        return doc.DocumentElement!.ChildNodes.Cast<XmlNode>().ToList();
    }
}
