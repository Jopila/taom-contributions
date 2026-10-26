namespace TAOM.Features.ManyHammers;

/// <summary>What happens to the smelting list's selection once the locked rows are gone.</summary>
public enum SelectionChange
{
    /// <summary>Leave the selection as it is.</summary>
    Unchanged,

    /// <summary>Select the row at <see cref="LockFilterResult.ReselectIndex"/> of the original list.</summary>
    Reselect,

    /// <summary>No row is left to select.</summary>
    Clear,
}

/// <summary>What to do with the selection, and the original-list index to select for a reselect.</summary>
/// <param name="Selection">The change to make.</param>
/// <param name="ReselectIndex">An index into the original list for <see cref="SelectionChange.Reselect"/>, else -1.</param>
public readonly record struct LockFilterResult(SelectionChange Selection, int ReselectIndex);
