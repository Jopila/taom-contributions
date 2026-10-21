using TAOM.Features.Execution;

namespace TAOM.Features.Namegiving;

/// <summary>The three kinds of settlement a realm's ruler can rename; the kind sets the influence price.</summary>
public enum FiefKind
{
    Town,
    Castle,
    Village,
}

/// <summary>
/// Why a rename is not allowed right now. <see cref="NamegivingService.Check(FiefNameSnapshot)"/> returns the first that applies,
/// and the declared order is not the check order: Disabled, NotSinglePlayer, NotRuler and MapAction (the target-independent
/// gates, in that order) come first, then NotRealm and ShortOfInfluence for the target itself.
/// </summary>
public enum RenameBlock
{
    None,
    Disabled,
    NotSinglePlayer,
    NotRuler,
    NotRealm,
    MapAction,
    ShortOfInfluence,
}

/// <summary>What is wrong with a typed name, in the order the rules are checked.</summary>
public enum NameProblem
{
    None,
    Length,
    Brackets,
    Characters,
    Edge,
    Double,
    Same,
    Taken,
}

/// <summary>Who named a settlement: nobody (the map's own name), TAOM's lore table, or a player (or anything else).</summary>
public enum NameOrigin
{
    None,
    Lore,
    Player,
}

/// <summary>What a lore pass does to one settlement.</summary>
public enum LoreAction
{
    Keep,
    Set,
    Restore,
}

/// <summary>One settlement's naming facts as plain values: its display name, the raw custom name and the side data a lore pass needs.</summary>
/// <param name="Id">The settlement's string id.</param>
/// <param name="Kind">Town, castle or village.</param>
/// <param name="Name">The name the game shows now.</param>
/// <param name="CustomName">The raw text of the custom name, or an empty string when the settlement has none.</param>
/// <param name="InPlayerRealm">True when the settlement belongs to the kingdom the player's clan is in.</param>
/// <param name="MapFactionId">The string id of the settlement's map faction.</param>
/// <param name="CultureId">The string id of the map faction's culture.</param>
public sealed record FiefNameSnapshot(string Id, FiefKind Kind, string Name, string CustomName, bool InPlayerRealm, string MapFactionId, string CultureId);

/// <summary>One settlement's id and display name, for the uniqueness check.</summary>
public readonly record struct NamedSettlement(string Id, string Name);

/// <summary>One row of the picker: a fief or village, its price and whether (and why not) it can be renamed.</summary>
/// <param name="Id">The settlement's string id.</param>
/// <param name="Name">The name it shows now.</param>
/// <param name="Cost">The influence price of renaming it.</param>
/// <param name="Block">Why it cannot be renamed, or <see cref="RenameBlock.None"/>.</param>
/// <param name="Detail">The engine's own reason when <paramref name="Block"/> is <see cref="RenameBlock.MapAction"/>, else empty.</param>
public sealed record RenameTarget(string Id, string Name, int Cost, RenameBlock Block, string Detail);

/// <summary>What the Rename button shows for the selected fief.</summary>
/// <param name="Visible">True when the button shows at all.</param>
/// <param name="Enabled">True when some row of the picker can be renamed.</param>
/// <param name="Cost">The price of renaming the selected fief itself.</param>
/// <param name="Block">Why the button is greyed; <see cref="RenameBlock.None"/> when it is enabled.</param>
/// <param name="Detail">The engine's reason when <paramref name="Block"/> is <see cref="RenameBlock.MapAction"/>, else empty.</param>
public readonly record struct RenameButtonState(bool Visible, bool Enabled, int Cost, RenameBlock Block, string Detail);

/// <summary>The outcome of an apply: success, or the first gate or rule that refused it. The names and the price stay with the caller, which holds the picked row and the typed text.</summary>
public sealed class RenameResult
{
    private RenameResult(bool success, RenameBlock block, NameProblem problem, string detail)
    {
        Success = success;
        Block = block;
        Problem = problem;
        Detail = detail;
    }

    public bool Success { get; }

    public RenameBlock Block { get; }

    public NameProblem Problem { get; }

    public string Detail { get; }

    public static RenameResult Done() => new(true, RenameBlock.None, NameProblem.None, "");

    public static RenameResult Blocked(RenameBlock block, string detail) => new(false, block, NameProblem.None, detail);

    public static RenameResult Invalid(NameProblem problem) => new(false, RenameBlock.None, problem, "");
}

/// <summary>
/// A settlement that takes a lore name while one side holds it. <see cref="Text"/> is the whole localized literal,
/// <c>{=key}English</c>, written out in full so tools/harvest_literal_loc_keys.py can register the key.
/// </summary>
public sealed class LoreRow
{
    public LoreRow(string settlementId, string text, FactionSide side)
    {
        SettlementId = settlementId;
        Text = text;
        Side = side;
        var close = text.IndexOf('}');
        Key = text.Substring(2, close - 2);
        English = text.Substring(close + 1);
    }

    public string SettlementId { get; }

    /// <summary>The literal as written, for example <c>{=taom_namegiving_lore_minas_ithil}Minas Ithil</c>.</summary>
    public string Text { get; }

    /// <summary>The localization key without braces.</summary>
    public string Key { get; }

    public string English { get; }

    public FactionSide Side { get; }
}
