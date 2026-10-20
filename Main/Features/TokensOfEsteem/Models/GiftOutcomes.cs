namespace TAOM.Features.TokensOfEsteem.Models;

/// <summary>Which conversation branch a gift line sits on: a companion in the party, or a lord on the diplomacy branch.</summary>
public enum GiftEntry
{
    Companion,
    Lord,
}

/// <summary>What the gift line does for this hero right now.</summary>
public enum GiftGateKind
{
    /// <summary>No line at all (feature off, wrong kind of hero, a child, a prisoner, a lord of the player's own clan, at war).</summary>
    Hidden,

    /// <summary>The line shows but cannot be chosen; <see cref="GiftGate.DaysLeft"/> says how long.</summary>
    Cooldown,

    /// <summary>The lord's relation with the player is below -50: he refuses, and the relation drops by 1.</summary>
    RefusesHostile,

    /// <summary>A Free lord refuses an Evil player's gift. No penalty.</summary>
    RefusesAlignment,

    /// <summary>The player may choose what to give.</summary>
    Open,
}

/// <summary>The gate for one hero and one branch. <see cref="DaysLeft"/> is set only for <see cref="GiftGateKind.Cooldown"/>.</summary>
public sealed record GiftGate(GiftGateKind Kind, int DaysLeft = 0);

public enum GiftOptionKind
{
    Good,
    Purse,
}

/// <summary>Why a listed gift cannot be chosen. <see cref="None"/> means it can.</summary>
public enum GiftBlock
{
    None,

    /// <summary>A purse larger than the player's gold.</summary>
    CannotAfford,

    /// <summary>The gift would add no relation (relation 90 or more, or a gain that rounds to 0).</summary>
    NoEffect,
}

/// <summary>
/// One line of the list: a stack of goods (<see cref="ItemId"/>, one is given) or a gold purse (<see cref="Amount"/>).
/// <see cref="Gain"/> is the relation the gift is worth now, after the taste bonus, the scaling and the cap.
/// </summary>
public sealed record GiftOption(
    GiftOptionKind Kind,
    string ItemId,
    string Name,
    int Count,
    int Amount,
    int BaseGain,
    int TasteBonus,
    int Gain,
    GiftBlock Block)
{
    /// <summary>True when the recipient's taste matched the gift.</summary>
    public bool Suited => TasteBonus > 0;
}

public enum GiftResult
{
    /// <summary>Nothing was taken: the goods were gone, the gold short, or the gate closed.</summary>
    Failed,

    /// <summary>The gift changed hands. The service has already started the cooldown.</summary>
    Given,
}

/// <summary>
/// What a finished gift did. <see cref="Landed"/> is the relation change that actually landed (the engine's diplomacy model
/// can make it differ from the ask), 0 when none did.
/// </summary>
public sealed record GiftOutcome(GiftResult Result, bool Suited, int Landed)
{
    public static GiftOutcome Failed { get; } = new(GiftResult.Failed, false, 0);
}
