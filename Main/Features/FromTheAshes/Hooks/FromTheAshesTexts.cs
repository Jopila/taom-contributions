using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.FromTheAshes.Hooks;

/// <summary>The player-facing words of From the Ashes: the wait-menu status line, the tooltips of a blocked option and the end messages.</summary>
internal static class FromTheAshesTexts
{
    /// <summary>Blocks the menu hides rather than explains: the option is not about this place or this player.</summary>
    internal static bool IsHidden(AshesBlock block) =>
        block == AshesBlock.Disabled || block == AshesBlock.NotApplicable || block == AshesBlock.NotLooted
        || block == AshesBlock.NotOwner || block == AshesBlock.Busy;

    /// <summary>Whether a start option for <paramref name="wanted"/> shows: the block is not hidden and the check is for this option's variant.</summary>
    internal static bool IsShown(AshesStartCheck check, AshesVariant wanted) => !IsHidden(check.Block) && check.Variant == wanted;

    internal static TextObject Status(AshesResult result)
    {
        TextObject text;
        switch (result.Variant)
        {
            case AshesVariant.Thralls:
                text = new TextObject("{=taom_ashes_wait_thralls}Your thralls clear the burnt timbers of {SETTLEMENT} under the lash.");
                break;
            case AshesVariant.Project:
                text = new TextObject("{=taom_ashes_wait_project}Your company labours on the {BUILDING} of {SETTLEMENT}.");
                text.SetTextVariable("BUILDING", result.ProjectName);
                break;
            default:
                text = new TextObject("{=taom_ashes_wait_rebuild}Your company clears the burnt timbers of {SETTLEMENT} and raises new walls.");
                break;
        }

        return text.SetTextVariable("SETTLEMENT", result.SettlementName);
    }

    /// <summary>
    /// The tooltip for a start option the player can see but not use, or null for a block that has none: the hidden blocks
    /// and None. Every shown block is named, and the default returns null, so a new block gets no tooltip until it is named here.
    /// </summary>
    internal static TextObject? Reason(AshesStartCheck check)
    {
        switch (check.Block)
        {
            case AshesBlock.NotSinglePlayer:
                return new TextObject("{=taom_ashes_coop}Not available in a shared campaign.");
            case AshesBlock.NotYours:
                return new TextObject("{=taom_ashes_not_yours}Only the realm that holds this village may rebuild it.");
            case AshesBlock.NoProject:
                return new TextObject("{=taom_ashes_no_project}No construction is under way here.");
            case AshesBlock.Enlisted:
            case AshesBlock.ForeignArmy:
                return new TextObject("{=taom_ashes_enlisted}You cannot do this while serving in another lord's army.");
            case AshesBlock.NeedLabour:
                return (check.Variant == AshesVariant.Thralls
                        ? new TextObject("{=taom_ashes_need_thralls}You need {COUNT} captives to labour.")
                        : new TextObject("{=taom_ashes_need_troops}You need {COUNT} healthy men to labour."))
                    .SetTextVariable("COUNT", check.Count);
            case AshesBlock.NeedMaterial:
                return NeedItems(check.Missing);
            default:
                return null;
        }
    }

    /// <summary>The message a job's end shows, or null for an outcome that needs none (no job, still running, stopped by choice).</summary>
    internal static TextObject? EndMessage(AshesResult result)
    {
        switch (result.Outcome)
        {
            case AshesOutcome.Completed when result.Kind == AshesJobKind.ProjectHelp:
                return new TextObject("{=taom_ashes_done_project}Your company's labour hastens the {BUILDING}.")
                    .SetTextVariable("BUILDING", result.ProjectName);
            case AshesOutcome.Completed when result.Variant == AshesVariant.Thralls:
                return new TextObject("{=taom_ashes_done_thralls}{SETTLEMENT} stands again, raised by your thralls.")
                    .SetTextVariable("SETTLEMENT", result.SettlementName);
            case AshesOutcome.Completed:
                return new TextObject("{=taom_ashes_done_rebuild}{SETTLEMENT} rises from its ashes. Its people will not forget who helped.")
                    .SetTextVariable("SETTLEMENT", result.SettlementName);
            case AshesOutcome.CancelledRecovered:
                return new TextObject("{=taom_ashes_cancel_recovered}The villagers finished the work without you.");
            case AshesOutcome.Cancelled:
                return new TextObject("{=taom_ashes_cancel_generic}The work is abandoned.");
            case AshesOutcome.ShortOfMaterials:
                return new TextObject("{=taom_ashes_short_rebuild}The work on {SETTLEMENT} stops short: you need {ITEMS}.")
                    .SetTextVariable("SETTLEMENT", result.SettlementName)
                    .SetTextVariable("ITEMS", ItemList(result.Missing));
            default:
                return null;
        }
    }

    /// <summary>Shows the end message of <paramref name="result"/>, if it has one; a short rebuild is shown in red.</summary>
    internal static void Report(AshesResult result)
    {
        var message = EndMessage(result);
        if (message != null)
            Show(message, warning: result.Outcome == AshesOutcome.ShortOfMaterials);
    }

    internal static void Show(TextObject text, bool warning) =>
        InformationManager.DisplayMessage(warning ? new InformationMessage(text.ToString(), Colors.Red) : new InformationMessage(text.ToString()));

    private static TextObject NeedItems(IReadOnlyList<AshesMaterial> missing) =>
        new TextObject("{=taom_ashes_need_items}You need {ITEMS}.").SetTextVariable("ITEMS", ItemList(missing));

    private static string ItemList(IReadOnlyList<AshesMaterial> missing) => string.Join(", ", missing.Select(Describe));

    private static string Describe(AshesMaterial material) =>
        new TextObject("{=!}{COUNT} {NAME}").SetTextVariable("COUNT", material.Count)
            .SetTextVariable("NAME", MBObjectManager.Instance?.GetObject<ItemObject>(material.ItemId)?.Name?.ToString() ?? material.ItemId)
            .ToString();
}
