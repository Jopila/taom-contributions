using System;
using System.Collections.Generic;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

/// <summary>
/// #769 E4 — the single take/refund rule. The career screen used to restate the take gate three
/// times (a dead select command, the click path and the per-diamond display state) and the refund
/// path had none; every caller now asks here and acts only on <see cref="ChoiceLockReason.None"/>.
/// The first failing check is the reason, so the order below is the contract.
///
/// A group with <c>MaxPicks == 0</c> is unlimited and behaves exactly as before forks existed:
/// refunds are free at any time and the keystone exclusivity rule applies. A group with
/// <c>MaxPicks &gt; 0</c> is a fork: it holds at most that many picks, and a pick is refundable
/// only during the visit it was taken in ("lock on leave").
/// </summary>
public static class CareerChoiceRules
{
    public static ChoiceLockReason CanTake(
        CareerDefinition career,
        CareerChoiceDefinition choice,
        ICareerRegistry registry,
        HeroCareerData heroData,
        int freePoints,
        Func<int, bool> isTierAvailable)
    {
        if (career == null || choice == null || registry == null || heroData == null || isTierAvailable == null)
            return ChoiceLockReason.NotInCareer;
        if (heroData.HasChoice(choice.Id)) return ChoiceLockReason.AlreadyTaken;

        var group = FindGroupInCareer(career, choice, registry);
        if (group == null) return ChoiceLockReason.NotInCareer;

        if (!isTierAvailable(group.Tier)) return ChoiceLockReason.TierLocked;

        if (group.MaxPicks > 0)
        {
            if (CountTakenInGroup(group, heroData) >= group.MaxPicks) return ChoiceLockReason.ForkFull;
        }
        else if (KeystoneExclusivityRule.IsLocked(career, choice, registry, heroData))
        {
            return ChoiceLockReason.KeystoneLocked;
        }

        return freePoints <= 0 ? ChoiceLockReason.NoPoints : ChoiceLockReason.None;
    }

    public static ChoiceLockReason CanRefund(
        CareerDefinition career,
        CareerChoiceDefinition choice,
        ICareerRegistry registry,
        HeroCareerData heroData,
        ICollection<string> takenThisVisit)
    {
        if (career == null || choice == null || registry == null || heroData == null || takenThisVisit == null)
            return ChoiceLockReason.NotInCareer;
        if (!heroData.HasChoice(choice.Id)) return ChoiceLockReason.NotTaken;
        if (choice.Id == career.RootChoiceId) return ChoiceLockReason.NotInCareer;

        var group = FindGroupInCareer(career, choice, registry);
        if (group == null) return ChoiceLockReason.NotInCareer;

        if (group.MaxPicks > 0 && !takenThisVisit.Contains(choice.Id)) return ChoiceLockReason.LockedIn;
        return ChoiceLockReason.None;
    }

    /// <summary>
    /// #769 E7: the hero holds a choice in one of this career's fork groups (<c>MaxPicks &gt; 0</c>).
    /// What a retrain, or leaving the career, has to refund.
    /// </summary>
    public static bool HoldsForkPick(
        CareerDefinition career, ICareerRegistry registry, HeroCareerData heroData)
    {
        if (career == null || registry == null || heroData == null) return false;

        foreach (var groupId in career.ChoiceGroupIds)
        {
            var group = registry.GetGroup(groupId);
            if (group == null || group.MaxPicks <= 0) continue;

            foreach (var choiceId in group.ChoiceIds)
            {
                if (heroData.HasChoice(choiceId))
                    return true;
            }
        }
        return false;
    }

    private static CareerChoiceGroupDefinition FindGroupInCareer(
        CareerDefinition career, CareerChoiceDefinition choice, ICareerRegistry registry)
    {
        if (string.IsNullOrEmpty(choice.GroupId)) return null;
        var group = registry.GetGroup(choice.GroupId);
        if (group == null) return null;

        foreach (var groupId in career.ChoiceGroupIds)
        {
            if (groupId == group.Id) return group;
        }
        return null;
    }

    private static int CountTakenInGroup(CareerChoiceGroupDefinition group, HeroCareerData heroData)
    {
        var taken = 0;
        foreach (var choiceId in group.ChoiceIds)
        {
            if (heroData.HasChoice(choiceId)) taken++;
        }
        return taken;
    }
}
