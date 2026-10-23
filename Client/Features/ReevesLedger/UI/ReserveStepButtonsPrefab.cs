using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TAOM.Features.ReevesLedger.UI;

/// <summary>
/// The -10K and +10K buttons of the Reeve's Ledger: two siblings placed after the vanilla ReserveListPanel, one to each side
/// of its plus button, inside the ReservePanel whose data source is the TownManagementReserveControlVM, so the bindings below
/// resolve on <see cref="ReserveStepMixin"/>. Each is built in the shape of vanilla's governor button (SandBox
/// TownManagement.xml:105-111): a fixed 80 x 44 wrapper Widget carrying the id, alignment, offset and visibility, with its own
/// HintWidget (the disabled hint) beside a ButtonWidget that stretches to the wrapper. Gauntlet never hovers a disabled widget,
/// so a hint inside the button could not say why it is disabled; the wrapper's hint can. The button is in the vanilla
/// ButtonBrush2 with a TextWidget in the reserve text brush and a second HintWidget for the enabled state.
/// Positions are measured from the vanilla sizes and need an in-game check: bottom-aligned 28 px up, 85 px either side of
/// the centre, level with the 69 px plus button.
/// </summary>
[PrefabExtension("TownManagement", "descendant::ListPanel[@Id='ReserveListPanel']")]
internal class ReserveStepButtonsPrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;

    [PrefabExtensionXmlNodes]
    public IEnumerable<XmlNode> GetNodes()
    {
        var doc = new XmlDocument();
        doc.LoadXml(
            "<Root>"
            + Button("TaomReserveWithdrawStep", "-85", "CanWithdrawReserveStep", "ExecuteWithdrawReserveStep", "WithdrawReserveStepText", "WithdrawReserveStep")
            + Button("TaomReserveAddStep", "85", "CanAddReserveStep", "ExecuteAddReserveStep", "AddReserveStepText", "AddReserveStep")
            + "</Root>");
        return doc.DocumentElement!.ChildNodes.Cast<XmlNode>().ToList();
    }

    private static string Button(string id, string offsetX, string enabled, string click, string text, string hintBase) =>
        "<Widget Id=\"" + id + "\" WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" SuggestedWidth=\"80\" SuggestedHeight=\"44\""
        + " HorizontalAlignment=\"Center\" VerticalAlignment=\"Bottom\" MarginBottom=\"28\" PositionXOffset=\"" + offsetX + "\""
        + " IsVisible=\"@IsReserveStepVisible\">"
        + "<Children>"
        + Hint(hintBase + "DisabledHint")
        + "<ButtonWidget Id=\"" + id + "Button\" WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\""
        + " Brush=\"ButtonBrush2\" DoNotPassEventsToChildren=\"true\" IsEnabled=\"@" + enabled + "\" Command.Click=\"" + click + "\">"
        + "<Children>"
        + "<TextWidget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\" HorizontalAlignment=\"Center\""
        + " VerticalAlignment=\"Center\" Brush=\"TownManagement.Reserve.Amount.Text\" Text=\"@" + text + "\" />"
        + Hint(hintBase + "Hint")
        + "</Children>"
        + "</ButtonWidget>"
        + "</Children>"
        + "</Widget>";

    private static string Hint(string property) =>
        "<HintWidget DataSource=\"{" + property + "}\" WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\""
        + " Command.HoverBegin=\"ExecuteBeginHint\" Command.HoverEnd=\"ExecuteEndHint\" />";
}
