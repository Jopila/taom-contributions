using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TAOM.Features.FiefGranting.UI;

/// <summary>
/// The fief-count row of a fief vote (#866). Vanilla's KingdomDecisionOptionItem is the one template for every decision
/// type: a vertical ListPanel of the sponsor row, the card Widget (the selection ButtonWidget and its disabled-state hint) and
/// the progress bar. The XPath finds the card Widget through its ButtonWidget (the one with Command.Click="ExecuteSelection";
/// its parent is the Children element, whose parent is the card Widget) and Append inserts the row as the card's next sibling,
/// between the card and the progress bar. It sits OUTSIDE the ButtonWidget on purpose: Gauntlet never hovers a disabled widget,
/// and a card is disabled when the player lacks the influence to back it, and for every card once the ruler has chosen
/// (DecisionOptionVM.RefreshCanChooseOption), yet its hover list must still work. The row is vanilla's own shape (SandBox
/// Inventory.xml:471-475): one TextWidget with a HintWidget child that relays the parent's hover, so the HintWidget needs no
/// size; a StretchToParent child of a CoverChildren panel would fill all the space the card layout offers. The row binds to
/// <see cref="DecisionOptionFiefCountMixin"/>, which is on the same view model the ListPanel is mounted with, so no DataSource
/// step is needed. No sprite is used. The look and the vertical fit above the stage bar still need an in-game check.
/// </summary>
[PrefabExtension("KingdomDecisionOptionItem", "descendant::ButtonWidget[@Command.Click='ExecuteSelection']/parent::Children/parent::Widget")]
internal class DecisionOptionFiefCountPrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;

    [PrefabExtensionXmlNodes]
    public IEnumerable<XmlNode> GetNodes()
    {
        var doc = new XmlDocument();
        doc.LoadXml(
            "<Root>"
            + "<TextWidget Id=\"TaomFiefCountRow\" IsVisible=\"@ShowFiefCount\" WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"CoverChildren\""
            + " HorizontalAlignment=\"Center\" MarginTop=\"4\" Brush=\"Kingdom.DecisionParagraph.Text\" Text=\"@FiefCountText\">"
            + "<Children>"
            + "<HintWidget DataSource=\"{FiefCountHint}\" Command.HoverBegin=\"ExecuteBeginHint\" Command.HoverEnd=\"ExecuteEndHint\" />"
            + "</Children>"
            + "</TextWidget>"
            + "</Root>");
        return doc.DocumentElement!.ChildNodes.Cast<XmlNode>().ToList();
    }
}
