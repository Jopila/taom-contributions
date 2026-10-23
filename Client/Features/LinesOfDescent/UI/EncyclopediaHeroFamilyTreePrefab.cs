using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TAOM.Features.LinesOfDescent.UI;

// Lines of Descent (#905): the Family Tree button on the encyclopedia hero page, inserted as the sibling just before the Family
// grid's gamepad scope targeter (TAOM's EncyclopediaHeroPage.xml:233), so it sits between the Family divider and the grid inside
// the page's vertical ListPanel. The anchor is a NavigationScopeTargeter that exists once; a wiring test pins it. The shared
// prefab is not edited. InsertType.Prepend is InsertBefore on the matched node (UIExtenderEx 2.13.3 PrefabComponent), and the
// nodes after the first follow it in order, so the button's own scope targeter and the button both land before the anchor. The
// button joins the page's navigation the way the Family divider beside it does (GamepadNavigationIndex 0). Bindings
// resolve on EncyclopediaHeroPageVM, which EncyclopediaHeroFamilyTreeMixin extends, and the button shows only when the page
// lists family. Do NOT copy the Messengers button brushes: Popup.PartySelection.Confirm and .Button.Text are declared nowhere.
// The parent is a plain vertical ListPanel (:121) with no widget class of its own, and no view model reads its children by position.
// Brushes: Native Popup.xml:2 and :195.
[PrefabExtension("EncyclopediaHeroPage", "descendant::NavigationScopeTargeter[@ScopeID='EncyclopediaHeroFamilyContentScope']")]
internal sealed class EncyclopediaHeroFamilyTreePrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Prepend;

    [PrefabExtensionXmlNodes]
    public IEnumerable<XmlNode> GetNodes()
    {
        var doc = new XmlDocument();
        doc.LoadXml(
            "<Root>"
            + "<NavigationScopeTargeter ScopeID=\"EncyclopediaHeroFamilyTreeButtonScope\" ScopeParent=\"..\\TaomFamilyTreeButton\""
            + " ScopeMovements=\"Horizontal\" />"
            + "<ButtonWidget Id=\"TaomFamilyTreeButton\" WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\""
            + " SuggestedWidth=\"180\" SuggestedHeight=\"40\" HorizontalAlignment=\"Left\" MarginLeft=\"20\" MarginTop=\"16\""
            + " Brush=\"Popup.Button.Base\" Command.Click=\"ExecuteOpenFamilyTree\" DoNotPassEventsToChildren=\"true\""
            + " UpdateChildrenStates=\"true\" IsVisible=\"@HasFamilyTree\" GamepadNavigationIndex=\"0\">"
            + "<Children>"
            + "<TextWidget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\""
            + " Brush=\"Popup.Button.Text\" Text=\"@FamilyTreeText\" />"
            + "</Children>"
            + "</ButtonWidget>"
            + "</Root>");
        return doc.DocumentElement!.ChildNodes.Cast<XmlNode>().ToList();
    }
}
