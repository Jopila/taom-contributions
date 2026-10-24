using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TAOM.Features.LoremastersLine.UI;

// The Loremaster's Line (#877): the engine's FillBarHorizontalWidget inserted as a child of the character screen's skill
// bar, bound to the selected skill (the bar's data source is CurrentSkill, a SkillVM), so the bindings below resolve on
// LoremastersLineSkillMixin. No TAOM widget and no per-frame TAOM code: the engine widget sets the band's width from
// InitialAmount / MaxAmount (the band's levels over the bar's levels) times its parent's width, right-aligned, as
// vanilla's DeclareWarDecisionPanel does.
// It goes in BEFORE PercentageIndicatorWidget: InsertType.Child inserts before the child at Index (UIExtenderEx
// PrefabComponent.InsertAsChild), and the bar's children are 0 the empty-bar sprite, 1 ProgressClip, 2
// FullLearningRateClipParent, 3 PercentageIndicatorWidget (CharacterDeveloper.xml, v1.5.5; Gauntlet drops comments, so
// they do not count). The red band then draws over the empty bar, the progress fill (ProgressClip, for a skill already
// past its floor) and the green full-rate clip, under the level stick
// and the perks; the perks list stays last, so its buttons keep their clicks. The root takes no events, as the perks
// list does in vanilla.
// The band uses vanilla's clip technique: a clipped parent the width of the band, with a 1000 wide fill inside it,
// right-aligned, so the sprite does not squeeze. The band's tooltip is a HintWidget filling the band, which is the hover
// target: the band passes no events to its children (CollectEnableWidgetsAt gathers children first, so an enabled child
// would take the hover, and the hint relays only its parent's hover), and the arrow above it is decoration.
// The colours are RRGGBBAA: #E03232FF for the arrow, #FF3333FF for the fill (vanilla's green zone draws the same sprite
// at #00FF00FF, full alpha, so the sprite takes a tint cleanly). Both sprites are redeclared by TAOMSpriteData.xml in ui_taom
// (AlwaysLoad), the TAOM re-skin that draws; TAOM's triangle points up, so the arrow is flipped to point at the bar.
// Literal sizes only.
[PrefabExtension("CharacterDeveloper",
    "descendant::PerkSelectionBarWidget[@Id='PerkSelectionBarWidget']/Children")]
internal class LoremastersLinePrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Child;

    public override int Index => 3;

    [PrefabExtensionXmlDocument]
    public XmlDocument GetDocument()
    {
        var doc = new XmlDocument();
        doc.LoadXml(
            "<FillBarHorizontalWidget Id=\"TaomLoremastersLine\"" +
            " WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\"" +
            " DoNotAcceptEvents=\"true\" IsVisible=\"@HasLoremastersLine\" FillWidget=\"Band\"" +
            " InitialAmount=\"@LoremastersLineBandLevels\" MaxAmount=\"@LoremastersLineBarLevels\">" +
            "<Children>" +
            "<Widget Id=\"Band\" WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"StretchToParent\"" +
            " SuggestedWidth=\"0\" DoNotPassEventsToChildren=\"true\">" +
            "<Children>" +
            "<Widget Id=\"BandClip\" WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\"" +
            " ClipContents=\"true\">" +
            "<Children>" +
            "<Widget Id=\"BandFill\" WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"StretchToParent\"" +
            " SuggestedWidth=\"1000\" HorizontalAlignment=\"Right\"" +
            " Sprite=\"CharacterDeveloper\\progress_bar_fill\" Color=\"#FF3333FF\" />" +
            "</Children>" +
            "</Widget>" +
            "<Widget Id=\"Arrow\" WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\"" +
            " SuggestedWidth=\"30\" SuggestedHeight=\"30\" HorizontalAlignment=\"Left\" PositionXOffset=\"-15\"" +
            " VerticalAlignment=\"Top\" PositionYOffset=\"-30\" VerticalFlip=\"true\" IsEnabled=\"false\"" +
            " Sprite=\"CharacterDeveloper\\learning_rate_limit_triangle\" Color=\"#E03232FF\" />" +
            "<HintWidget Id=\"BandHint\" DataSource=\"{LoremastersLineTooltip}\" WidthSizePolicy=\"StretchToParent\"" +
            " HeightSizePolicy=\"StretchToParent\" Command.HoverBegin=\"ExecuteBeginHint\"" +
            " Command.HoverEnd=\"ExecuteEndHint\" />" +
            "</Children>" +
            "</Widget>" +
            "</Children>" +
            "</FillBarHorizontalWidget>");
        return doc;
    }
}
