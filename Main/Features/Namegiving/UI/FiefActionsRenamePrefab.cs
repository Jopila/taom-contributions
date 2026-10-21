using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TAOM.Features.Namegiving.UI;

/// <summary>
/// The Rename button of Namegiving: one block inserted as the last child of the Fiefs panel's action column (vanilla
/// FiefsPanel.xml, the vertical ListPanel "ActionButtonsContainer", the only match), under the Annex button and its cost. It
/// is a child, not an Append: Append inserts a sibling of the container, which sits in a horizontal row outside the
/// FiefsActionButtonsScope gamepad scope. FiefsPanel is mounted with DataSource {Settlement} (KingdomManagement.xml), so the
/// block's root steps one level up with <c>{..}</c> to the KingdomManagementVM that <see cref="FiefNamingMixin"/> extends;
/// every binding below resolves there. The shape is vanilla's governor-button shape (as Reeve's Ledger): a fixed, always-enabled
/// wrapper Widget holds the disabled-reason HintWidget beside the ButtonWidget, because Gauntlet never hovers a disabled widget,
/// so a hint inside the button could not say why it is grey (RCA reeves-ledger finding 1). The cost row repeats vanilla's
/// influence icon. {..} is verified in source; the look and a gamepad's Right from Annex still need an in-game check.
/// </summary>
[PrefabExtension("FiefsPanel", "descendant::ListPanel[@Id='ActionButtonsContainer']/Children")]
internal class FiefActionsRenamePrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Child;

    public override int Index => int.MaxValue;

    [PrefabExtensionXmlNodes]
    public IEnumerable<XmlNode> GetNodes()
    {
        var doc = new XmlDocument();
        doc.LoadXml(
            "<Root>"
            + "<ListPanel Id=\"TaomRenameFief\" DataSource=\"{..}\" WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"CoverChildren\""
            + " HorizontalAlignment=\"Center\" StackLayout.LayoutMethod=\"VerticalTopToBottom\" IsVisible=\"@IsRenameVisible\">"
            + "<Children>"
            + "<Widget Id=\"TaomRenameFiefFrame\" WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" SuggestedWidth=\"227\" SuggestedHeight=\"40\""
            + " HorizontalAlignment=\"Center\" MarginTop=\"10\">"
            + "<Children>"
            + Hint("RenameDisabledHint")
            + "<ButtonWidget Id=\"TaomRenameFiefButton\" DoNotPassEventsToChildren=\"true\" WidthSizePolicy=\"StretchToParent\""
            + " HeightSizePolicy=\"StretchToParent\" HorizontalAlignment=\"Center\" Brush=\"ButtonBrush2\" Command.Click=\"ExecuteRenameFief\""
            + " IsEnabled=\"@CanRename\" UpdateChildrenStates=\"true\" GamepadNavigationIndex=\"1\">"
            + "<Children>"
            + "<TextWidget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\" Brush=\"Kingdom.GeneralButtons.Text\""
            + " IsEnabled=\"@CanRename\" Text=\"@RenameText\" />"
            + Hint("RenameHint")
            + "</Children>"
            + "</ButtonWidget>"
            + "</Children>"
            + "</Widget>"
            + "<ListPanel DoNotAcceptEvents=\"true\" DoNotPassEventsToChildren=\"true\" WidthSizePolicy=\"CoverChildren\""
            + " HeightSizePolicy=\"CoverChildren\" HorizontalAlignment=\"Center\" MarginTop=\"3\">"
            + "<Children>"
            + "<TextWidget WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"CoverChildren\" HorizontalAlignment=\"Center\""
            + " VerticalAlignment=\"Center\" Brush=\"Kingdom.GeneralButtons.Text\" IntText=\"@RenameCost\" IsEnabled=\"@CanRename\" />"
            + "<Widget WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" SuggestedWidth=\"17\" SuggestedHeight=\"27\""
            + " HorizontalAlignment=\"Right\" VerticalAlignment=\"Center\" Sprite=\"SPKingdom\\influence_icon_small\" />"
            + "</Children>"
            + "</ListPanel>"
            + "</Children>"
            + "</ListPanel>"
            + "</Root>");
        return doc.DocumentElement!.ChildNodes.Cast<XmlNode>().ToList();
    }

    private static string Hint(string property) =>
        "<HintWidget DataSource=\"{" + property + "}\" WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\""
        + " Command.HoverBegin=\"ExecuteBeginHint\" Command.HoverEnd=\"ExecuteEndHint\" />";
}
