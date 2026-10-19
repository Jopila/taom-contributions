using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TAOM.Features.TelcharsMark.UI;

// The "show only better items" checkbox, added as the LAST child of the center panel's TopMenu widget
// (Inventory.xml:117, v1.5.5) so it draws above the tab-bar art. InsertType.Child inserts BEFORE the child at
// Index, and Index defaults to 0 (UIExtenderEx 2.13.3 PrefabComponent.InsertAsChild); int.MaxValue appends.
// It is deliberately NOT inside CenterItems: that NavigatableListPanel is indexed by position
// (IntValue="@ActiveFilterIndex", :146), so a new child there would shift every filter's index. CenterItems is
// 500 wide and centered, so an offset of 300 puts the 32 px box just right of the last filter button.
// Vanilla's checkbox idiom, as TAOM's CustomBattleScreen.xml:224 uses it: a Toggle button whose IsSelected binds
// the flag and whose ToggleIndicator child shows the tick. The brushes are declared in
// Native/GUI/Brushes/SPOptions.xml (:153, :177). The hint mirrors vanilla's filter hints (Inventory.xml:122-125).
// Mouse only: the button sits outside TopMenu's gamepad navigation scopes. Literal sizes, no !Constants.
[PrefabExtension("Inventory", "descendant::Widget[@Id='TopMenu']/Children")]
internal class UpgradeToggleButtonPrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Child;

    public override int Index => int.MaxValue;

    [PrefabExtensionXmlDocument]
    public XmlDocument GetDocument()
    {
        var doc = new XmlDocument();
        doc.LoadXml(
            "<ButtonWidget Id=\"TaomShowOnlyUpgradesButton\"" +
            " WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\"" +
            " SuggestedWidth=\"32\" SuggestedHeight=\"32\"" +
            " HorizontalAlignment=\"Center\" VerticalAlignment=\"Top\"" +
            " MarginTop=\"100\" PositionXOffset=\"300\"" +
            " Brush=\"SPOptions.Checkbox.Empty.Button\"" +
            " ButtonType=\"Toggle\" IsSelected=\"@TaomShowOnlyUpgrades\"" +
            " ToggleIndicator=\"ToggleIndicator\" UpdateChildrenStates=\"true\"" +
            " DoNotPassEventsToChildren=\"true\"" +
            " IsVisible=\"@TaomUpgradeLensEnabled\">" +
            "<Children>" +
            "<ImageWidget Id=\"ToggleIndicator\" WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\"" +
            " Brush=\"SPOptions.Checkbox.Full.Button\" />" +
            "<HintWidget DataSource=\"{TaomShowOnlyUpgradesHint}\" DoNotAcceptEvents=\"true\"" +
            " WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\"" +
            " Command.HoverBegin=\"ExecuteBeginHint\" Command.HoverEnd=\"ExecuteEndHint\" />" +
            "</Children>" +
            "</ButtonWidget>");
        return doc;
    }
}
