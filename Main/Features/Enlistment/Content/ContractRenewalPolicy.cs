using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment.Content;

/// <summary>When the end-of-term offer may be made (#836). Pure.</summary>
public static class ContractRenewalPolicy
{
    /// <summary>
    /// The cheap gate: a plainly attached soldier whose term has ended and who was not already offered
    /// for this end day (the in-memory guard). Needs no adapter read, so the daily tick can stop here almost every day.
    /// </summary>
    public static bool IsDue(EnlistmentState state, double nowDays, double? contractEndDay, double? offeredForEndDay)
    {
        if (state != EnlistmentState.EnlistedAttached || !contractEndDay.HasValue)
            return false;
        if (double.IsNaN(nowDays) || double.IsInfinity(nowDays) || nowDays < contractEndDay.Value)
            return false;
        return !(offeredForEndDay.HasValue && offeredForEndDay.Value == contractEndDay.Value);
    }
}
