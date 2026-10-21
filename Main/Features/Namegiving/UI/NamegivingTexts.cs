using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TAOM.Features.Namegiving.UI;

/// <summary>
/// Every line of player text Namegiving shows, as inline <c>{=taom_namegiving_*}default</c> literals so the harvester registers
/// them. The engine's own reasons (a blocked map action, not enough influence) are passed through or reused by vanilla key,
/// never copied.
/// </summary>
internal static class NamegivingTexts
{
    internal static TextObject Button() => new("{=taom_namegiving_button}Rename");

    internal static string PickTitle() => new TextObject("{=taom_namegiving_pick_title}Rename a Fief").ToString();

    internal static string PickBody() => new TextObject("{=taom_namegiving_pick_body}Choose the fief or village to rename.").ToString();

    internal static string Entry(string name, int cost) =>
        new TextObject("{=taom_namegiving_entry}{NAME} ({COST} influence)")
            .SetTextVariable("NAME", name).SetTextVariable("COST", cost).ToString();

    internal static string InputTitle(string name) =>
        new TextObject("{=taom_namegiving_input_title}Rename {NAME}").SetTextVariable("NAME", name).ToString();

    internal static string InputBody(int cost) =>
        new TextObject("{=taom_namegiving_input_body}Enter a new name. It costs {COST} influence.").SetTextVariable("COST", cost).ToString();

    internal static string ConfirmTitle() => new TextObject("{=taom_namegiving_confirm_title}Confirm the New Name").ToString();

    internal static string ConfirmBody(string oldName, string newName, int cost) =>
        new TextObject("{=taom_namegiving_confirm_body}{OLD} will be known as {NEW}. This costs {COST} influence.")
            .SetTextVariable("OLD", oldName).SetTextVariable("NEW", newName).SetTextVariable("COST", cost).ToString();

    internal static string Done(string oldName, string newName) =>
        new TextObject("{=taom_namegiving_done}{OLD} is now known as {NEW}.")
            .SetTextVariable("OLD", oldName).SetTextVariable("NEW", newName).ToString();

    /// <summary>Why a rename is refused, or an empty string for a state the player never sees (the button is hidden then).</summary>
    internal static string Block(RenameBlock block, string engineReason)
    {
        switch (block)
        {
            case RenameBlock.NotRuler:
                return new TextObject("{=taom_namegiving_not_ruler}Only the ruler of the realm can rename its fiefs.").ToString();
            case RenameBlock.NotRealm:
                return new TextObject("{=taom_namegiving_not_realm}This fief no longer belongs to your realm.").ToString();
            case RenameBlock.MapAction:
                return engineReason;
            case RenameBlock.ShortOfInfluence:
                return GameTexts.FindText("str_warning_you_dont_have_enough_influence").ToString();
            default:
                return "";
        }
    }

    /// <summary>What is wrong with a typed name; empty for a valid one.</summary>
    internal static string Problem(NameProblem problem)
    {
        switch (problem)
        {
            case NameProblem.Length:
                return new TextObject("{=taom_namegiving_len}A name must be 1 to 50 characters long.").ToString();
            case NameProblem.Characters:
                return new TextObject("{=taom_namegiving_chars}A name may hold only letters, digits, spaces and punctuation.").ToString();
            case NameProblem.Brackets:
                return new TextObject("{=taom_namegiving_brackets}A name cannot contain braces or square brackets.").ToString();
            case NameProblem.Edge:
                return new TextObject("{=taom_namegiving_edge}A name cannot start or end with a space.").ToString();
            case NameProblem.Double:
                return new TextObject("{=taom_namegiving_double}A name cannot contain two spaces in a row.").ToString();
            case NameProblem.Same:
                return new TextObject("{=taom_namegiving_same}That is already its name.").ToString();
            case NameProblem.Taken:
                return new TextObject("{=taom_namegiving_taken}Another place already bears that name.").ToString();
            default:
                return "";
        }
    }
}
