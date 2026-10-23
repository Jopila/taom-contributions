using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TAOM.Features.ReevesLedger.UI;

/// <summary>
/// The stash and prison icons of the Reeve's Ledger: two siblings placed after the vanilla Consumptions icon, 95 px to
/// each side of the centre, each with a HintWidget child built like the Consumptions one. The vanilla Consumptions widget is
/// a child of TownManagementPopup, whose data source is the TownManagementVM, so the bindings below resolve on
/// <see cref="TownManagementLedgerMixin"/>. The sprites are the game menu's stash and prisoner icons (56 x 42, always
/// loaded in ui_group1). The icons show on towns and castles alike; the vanilla Consumptions icon is towns only.
/// </summary>
[PrefabExtension("TownManagement", "descendant::Widget[@Id='Consumptions']")]
internal class ReevesLedgerIconsPrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;

    [PrefabExtensionXmlNodes]
    public IEnumerable<XmlNode> GetNodes()
    {
        var doc = new XmlDocument();
        doc.LoadXml(
            "<Root>"
            + Icon("TaomStashIcon", "SPGeneral\\GameMenu\\stash_icon", "-95", "StashTooltip")
            + Icon("TaomPrisonIcon", "SPGeneral\\GameMenu\\manage_prisoners", "95", "PrisonTooltip")
            + "</Root>");
        return doc.DocumentElement!.ChildNodes.Cast<XmlNode>().ToList();
    }

    private static string Icon(string id, string sprite, string offsetX, string hint) =>
        "<Widget Id=\"" + id + "\" WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" SuggestedWidth=\"56\" SuggestedHeight=\"42\""
        + " HorizontalAlignment=\"Center\" MarginTop=\"100\" PositionXOffset=\"" + offsetX + "\""
        + " Sprite=\"" + sprite + "\" DoNotPassEventsToChildren=\"true\" IsVisible=\"@ShowLedgerIcons\">"
        + "<Children>"
        + "<HintWidget DataSource=\"{" + hint + "}\" WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\""
        + " Command.HoverBegin=\"ExecuteBeginHint\" Command.HoverEnd=\"ExecuteEndHint\" />"
        + "</Children>"
        + "</Widget>";
}
