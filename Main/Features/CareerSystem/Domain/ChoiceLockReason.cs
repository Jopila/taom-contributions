namespace TAOM.Features.CareerSystem.Domain;

/// <summary>
/// #769 E4 — why <c>CareerChoiceRules</c> refuses to take or refund a choice. <c>None</c> means the
/// action is allowed; the other values are the first failing check, in the rule's check order.
/// </summary>
public enum ChoiceLockReason
{
    None,

    /// <summary>The choice's group is not one of the career's, or the choice is the career's root (never refundable).</summary>
    NotInCareer,
    AlreadyTaken,
    NotTaken,
    TierLocked,
    ForkFull,
    KeystoneLocked,
    NoPoints,
    LockedIn,
}
