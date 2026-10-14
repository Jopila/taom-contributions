namespace TAOM.Features.CareerSystem.Domain;

/// <summary>The outcome of a career switch request, so the screen reports the real reason.</summary>
public enum CareerSwitchResult
{
    Switched,
    NotEligible,
    NotEnoughGold,
}
