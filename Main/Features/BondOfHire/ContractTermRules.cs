using System;
using TAOM.Core.Validation;

namespace TAOM.Features.BondOfHire;

/// <summary>What the Clans panel shows for a clan's contract: nothing, an unknown length, or a known number of whole days.</summary>
public enum ContractTermKind
{
    Hidden,
    Unknown,
    Known,
}

/// <summary>A contract line: its <see cref="Kind"/>, and the whole days for <see cref="ContractTermKind.Known"/> (0 otherwise).</summary>
public readonly record struct ContractTerm(ContractTermKind Kind, int Days);

/// <summary>
/// Pure rules for the contract line of the kingdom Clans panel (docs/features/bond-of-hire.md, #868). Plain values in, no
/// engine type and no text out, so the view-model extension only converts a clan and picks a string.
/// </summary>
public static class ContractTermRules
{
    /// <summary>
    /// Hidden for a clan that is not a mercenary and for the player's clan (the engine never reads the date for the player). A
    /// remaining time under one day, past, or not a finite number is Unknown. Otherwise Known, rounded DOWN: "at least N more
    /// days" is true only that way. The positive requirement fails NaN, and the cast is range-checked first.
    /// </summary>
    public static ContractTerm Describe(bool isMercenary, bool isPlayerClan, float remainingDays)
    {
        if (!isMercenary || isPlayerClan)
            return new ContractTerm(ContractTermKind.Hidden, 0);
        if (!FiniteFloatValidator.IsFinite(remainingDays) || !(remainingDays >= 1f))
            return new ContractTerm(ContractTermKind.Unknown, 0);

        var whole = Math.Floor((double)remainingDays);
        return new ContractTerm(ContractTermKind.Known, whole >= int.MaxValue ? int.MaxValue : (int)whole);
    }
}
