using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;

namespace TAOM.Features.ShadowfaxScreens.UI;

// Shadowfax Screens: the clan, kingdom, character, inventory and quest screens open without their
// slide animations. A CustomPatch<XmlDocument> receives the whole prefab, so nine movie names cover
// every VisualDefinition, including one an engine update adds or renames. A
// PrefabExtensionSetAttributePatch would need one XPath per definition (27), and each would miss
// when its node is renamed. UIExtender.Register in SubModule finds this class on both the solo and
// the co-op registration path. TAOM's own party and career prefabs carry the zero durations in XML.
//
// The explicit null XPath only resolves CS0121 against UIExtenderEx's obsolete three-argument
// constructor; a CustomPatch<XmlDocument> never reads it.
[PrefabExtension("ClanScreen", null)]
[PrefabExtension("ClanMembers", null)]
[PrefabExtension("KingdomManagement", null)]
[PrefabExtension("FiefsPanel", null)]
[PrefabExtension("CharacterDeveloper", null)]
[PrefabExtension("Inventory", null)]
[PrefabExtension("InventoryEquippedItemControls", null)]
[PrefabExtension("InventoryItemTuple", null)]
[PrefabExtension("QuestsScreen", null)]
internal class ShadowfaxScreensPrefab : CustomPatch<XmlDocument>
{
    public override string Id => "TaomShadowfaxScreens";

    public override void Apply(XmlDocument document) => InstantTransitionRewriter.MakeInstant(document);
}
