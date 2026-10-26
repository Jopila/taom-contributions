using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.SmithsMeasure.UI;

/// <summary>
/// The Make Tools card (#910, docs/features/smiths-measure.md). Vanilla's Crafting prefab (SandBox, v1.5.5) opens its
/// <c>CraftingScreenWidget</c> with the bottom materials list, a <c>ListPanel</c> bound to <c>{PlayerCurrentMaterials}</c>, as the
/// first of that widget's children; <c>InsertType.Append</c> puts the card right after it as a SIBLING (as the Telchar's Mark
/// tint does after its anchor). The card sits at the left edge, right of the crafting-hero toggle (which covers x 60 to 190, so
/// MarginLeft 200) and above the materials strip: the refinement panel ends near y 624 and the strip's change-amount texts reach
/// 20 px above its top (984), so a 50 px card with a 125 px bottom margin has clear space at 1080p. It is vanilla's main-action
/// button in vanilla's own size (355 by 50) with its brushes (<c>Crafting.MainAction.Button</c> and <c>.Text</c>), so it names no
/// sprite. A HintWidget only relays its parent's hover and a disabled widget is skipped by hit-testing, so the one tooltip is
/// shown by two hints: a sibling of the button, which explains the greyed state as vanilla's MainActionParent does, and a
/// size-less one inside the button for the enabled state, as vanilla's Reset Angle button does. The card root therefore stays
/// hoverable. The look, and its fit at other resolutions, still need an in-game check. The bindings resolve to
/// <see cref="MakeToolsMixin"/>, which is on the view model the screen is mounted with.
/// </summary>
[PrefabExtension("Crafting", "descendant::ListPanel[@DataSource='{PlayerCurrentMaterials}']")]
[CoopSuppressedUi("Make Tools writes the party's stores and the hero's crafting stamina from this client, which a co-op host owns")]
internal class MakeToolsPrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;

    [PrefabExtensionXmlDocument]
    public XmlDocument GetDocument()
    {
        var doc = new XmlDocument();
        doc.LoadXml(
            "<Widget Id=\"TaomMakeToolsCard\" IsVisible=\"@IsMakeToolsVisible\""
            + " WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" SuggestedWidth=\"355\" SuggestedHeight=\"50\""
            + " HorizontalAlignment=\"Left\" VerticalAlignment=\"Bottom\" MarginLeft=\"200\" MarginBottom=\"125\">"
            + "<Children>"
            + "<ButtonWidget Id=\"TaomMakeToolsButton\" DoNotPassEventsToChildren=\"true\" WidthSizePolicy=\"StretchToParent\""
            + " HeightSizePolicy=\"StretchToParent\" Brush=\"Crafting.MainAction.Button\" Command.Click=\"ExecuteMakeTools\""
            + " IsEnabled=\"@IsMakeToolsEnabled\" UpdateChildrenStates=\"true\">"
            + "<Children>"
            + "<TextWidget WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"CoverChildren\" HorizontalAlignment=\"Center\""
            + " VerticalAlignment=\"Center\" MarginTop=\"15\" Brush=\"Crafting.MainAction.Text\" Text=\"@MakeToolsText\" />"
            + "<HintWidget DataSource=\"{MakeToolsHint}\" Command.HoverBegin=\"ExecuteBeginHint\" Command.HoverEnd=\"ExecuteEndHint\" />"
            + "</Children>"
            + "</ButtonWidget>"
            + "<HintWidget DataSource=\"{MakeToolsHint}\" WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\""
            + " Command.HoverBegin=\"ExecuteBeginHint\" Command.HoverEnd=\"ExecuteEndHint\" IsDisabled=\"true\" />"
            + "</Children>"
            + "</Widget>");
        return doc;
    }
}
