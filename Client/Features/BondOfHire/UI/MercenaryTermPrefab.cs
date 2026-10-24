using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TAOM.Features.BondOfHire.UI;

/// <summary>
/// The contract line of the kingdom Clans panel (#868, docs/features/bond-of-hire.md). Vanilla's ClansPanel mounts with
/// DataSource {Clan} (KingdomManagement.xml), and the vertical, height-covering ListPanel "MembersList" inside it holds two
/// children, the Members header (0) and the fixed 440 px members grid (1); Gauntlet drops comments, so they do not count.
/// <c>InsertType.Child</c> inserts BEFORE the child at <c>Index</c>, so Index 1 puts the line between them, under the header.
/// The list is not data bound and nothing refers to it by id, so a third child breaks no positional access. The line is one
/// TextWidget in vanilla's own paragraph brush, a fixed 560 px wide and covering its text in height so a long line wraps left
/// aligned instead of running off the panel (a StretchToParent child of a CoverChildren parent would fill all the space offered,
/// #866, so the height never stretches), taking no events, with no hint, so there is no hover target to place. It
/// binds to <see cref="MercenaryTermMixin"/>, which is on the same view model the panel is mounted with, so no DataSource step is
/// needed. The look, and its fit above the Support and Expel block at 1080p, still need an in-game check.
/// </summary>
[PrefabExtension("ClansPanel", "descendant::ListPanel[@Id='MembersList']/Children")]
internal class MercenaryTermPrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Child;

    public override int Index => 1;

    [PrefabExtensionXmlNodes]
    public IEnumerable<XmlNode> GetNodes()
    {
        var doc = new XmlDocument();
        doc.LoadXml(
            "<Root>"
            + "<TextWidget Id=\"TaomMercenaryTermRow\" DoNotAcceptEvents=\"true\" IsVisible=\"@IsMercenaryTermVisible\""
            + " WidthSizePolicy=\"Fixed\" SuggestedWidth=\"560\" HeightSizePolicy=\"CoverChildren\" HorizontalAlignment=\"Left\""
            + " MarginLeft=\"35\" MarginTop=\"2\" MarginBottom=\"8\" Brush=\"Kingdom.ParagraphSmall.Text\" Brush.TextHorizontalAlignment=\"Left\" Text=\"@MercenaryTermText\" />"
            + "</Root>");
        return doc.DocumentElement!.ChildNodes.Cast<XmlNode>().ToList();
    }
}
