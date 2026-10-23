using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TAOM.Features.TelcharsMark.UI;

// Two translucent washes behind a row's controls: green for a better item, yellow for a sidegrade.
// Inserted as a SIBLING right after the tutorial highlight inside Body (InsertType.Append puts the node
// after the match, as TimeAccelerationPrefab does after PauseButton). InventoryItemTuple.xml:250-252 (v1.5.5):
// Widget Id="Body" opens with that TutorialHighlightItemBrushWidget, then Widget Id="MainControls". The
// tuple widget resolves its children by Id path, never by index, so a new sibling shifts nothing.
// DoNotAcceptEvents keeps the wash from eating clicks and hover on the row beneath it.
// Sprite BlankWhiteSquare_9 is a NineRegionSprite in Native/GUI/NativeSpriteData.xml (also used by
// CareerEnergyBarPrefab). Colors are RRGGBBAA: #4CC25A55 green, #E8C54A55 yellow, about a third opaque.
// Literal sizes only: a !Constant that the tuple prefab does not define resolves to nothing.
[PrefabExtension("InventoryItemTuple",
    "descendant::Widget[@Id='Body']/Children/TutorialHighlightItemBrushWidget")]
internal class UpgradeTintPrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;

    [PrefabExtensionXmlDocument]
    public XmlDocument GetDocument()
    {
        var doc = new XmlDocument();
        doc.LoadXml(
            "<Widget Id=\"TaomUpgradeTint\"" +
            " WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\"" +
            " DoNotAcceptEvents=\"true\">" +
            "<Children>" +
            "<Widget Id=\"TaomUpgradeTintBetter\"" +
            " WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\"" +
            " DoNotAcceptEvents=\"true\" Sprite=\"BlankWhiteSquare_9\" Color=\"#4CC25A55\"" +
            " IsVisible=\"@TaomIsUpgrade\" />" +
            "<Widget Id=\"TaomUpgradeTintSidegrade\"" +
            " WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\"" +
            " DoNotAcceptEvents=\"true\" Sprite=\"BlankWhiteSquare_9\" Color=\"#E8C54A55\"" +
            " IsVisible=\"@TaomIsSidegrade\" />" +
            "</Children>" +
            "</Widget>");
        return doc;
    }
}
