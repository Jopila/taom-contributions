using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using Attr = Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionSetAttributePatch.Attribute;

namespace TAOM.Features.HearthRoll.UI;

// Hearth Roll (#906): the clan Members list row, vanilla SandBox GUI/Prefabs/Clan/Management/ClanLordTuple.xml (v1.5.5). The
// prefab has no widget ids, so the three XPaths are structural; HearthRollUiWiringTests pins that each matches once and that the
// name node binds @Name. Container safety (gui-ui.md): the row's ButtonWidget Children and the ListPanel Children are plain widget
// lists, not data-bound collections, and no vanilla code indexes them. TAOM's Player Switcher picker
// (Main/_Module/GUI/PreFabs/FacGen/PreBuildCharacterSelection.xml) reuses ClanLordTuple bound to HeroPickItemVM, which has neither
// property: the tint stays hidden and the label stays empty there, but the name width change applies to its rows too.

// The tint: first child of the row button, so it draws behind the row's ListPanel. Colour and alpha are prefab constants.
[PrefabExtension("ClanLordTuple", "/Prefab/Window/ButtonWidget/Children")]
internal sealed class ClanLordTupleTintPrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Child;

    public override int Index => 0;

    [PrefabExtensionXmlNodes]
    public IEnumerable<XmlNode> GetNodes()
    {
        var doc = new XmlDocument();
        doc.LoadXml(
            "<Root>"
            + "<Widget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\" Sprite=\"BlankWhiteSquare_9\""
            + " Color=\"#680A0DFF\" AlphaFactor=\"0.25\" IsVisible=\"@IsTaomUnhappyCompanion\" DoNotAcceptEvents=\"true\" />"
            + "</Root>");
        return doc.DocumentElement!.ChildNodes.Cast<XmlNode>().ToList();
    }
}

// The name widget stretches to fill the row; shrinking it to its text lets the age label sit right after the name. MaxWidth caps
// that at the width vanilla gave it, so a long name wraps as in vanilla (OnMeasure uses MaxWidth as the measure width for
// CoverChildren) while a short one sits next to the age and shares the row's free width with the location text.
[PrefabExtension("ClanLordTuple", "/Prefab/Window/ButtonWidget/Children/ListPanel/Children/TextWidget[1]")]
internal sealed class ClanLordTupleNameWidthPrefab : PrefabExtensionSetAttributePatch
{
    public override List<Attr> Attributes => new List<Attr>
    {
        new Attr("WidthSizePolicy", "CoverChildren"),
        new Attr("MaxWidth", "226"),
    };
}

// The age label: Append is a sibling right after the name widget (not a child of it), in the row's ListPanel.
[PrefabExtension("ClanLordTuple", "/Prefab/Window/ButtonWidget/Children/ListPanel/Children/TextWidget[1]")]
internal sealed class ClanLordTupleAgeLabelPrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;

    [PrefabExtensionXmlNodes]
    public IEnumerable<XmlNode> GetNodes()
    {
        var doc = new XmlDocument();
        doc.LoadXml(
            "<Root>"
            + "<TextWidget WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"StretchToParent\" MarginLeft=\"8\" MarginTop=\"5\""
            + " MarginBottom=\"5\" Brush=\"Clan.Tuple.Name.Text\" Text=\"@TaomAgeText\" AutoHideIfEmpty=\"true\" DoNotAcceptEvents=\"true\" />"
            + "</Root>");
        return doc.DocumentElement!.ChildNodes.Cast<XmlNode>().ToList();
    }
}
