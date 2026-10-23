using System;
using TAOM.Core.Validation;

namespace TAOM.Features.ArmyTargeting.HoldTheWalls;

/// <summary>
/// The pure rules of Hold the Walls (#871): how long a lord holds, how engine hours are rounded, what is left of the
/// engine's own pause, and what ends a hold early. No engine type, no clock.
/// </summary>
internal static class HoldTheWallsMath
{
    /// <summary>
    /// Hours from the capture to the end of the hold: <c>BaseHours + PerTraitPointHours x (Calculating - Valor)</c>, clamped to
    /// <c>MinHours..MaxHours</c> (12 to 30 shipped, base 22), with the shortest hold kept above the engine's 10 hour pause and
    /// the longest at most 32 (<see cref="CapturedFortHoldConfig.CodeCapHours"/>, because the hold counts toward the army's
    /// 48 hour inactivity disband), whatever the config says. A non-finite result is the shortest hold.
    /// </summary>
    public static float HoldHours(int valor, int calculating, CapturedFortHoldConfig config)
    {
        float cap = Math.Min(config.MaxHours, CapturedFortHoldConfig.CodeCapHours);
        float floor = Math.Min(Math.Max(config.MinHours, CapturedFortHoldConfig.VanillaPauseHours + 1f), cap);
        double raw = config.BaseHours + config.PerTraitPointHours * ((double)calculating - valor);
        if (!FiniteFloatValidator.IsFinite(raw))
            return floor;
        return (float)Math.Max(floor, Math.Min(cap, raw));
    }

    /// <summary>
    /// Whole hours for <c>MobilePartyAi.DisableForHours</c>: the remaining time rounded up, so a hold never ends early;
    /// 0 when nothing remains or the value is not finite, and at most 32 so a corrupt clock cannot park an army for days.
    /// </summary>
    public static int EngineHours(double remainingHours)
    {
        if (!(remainingHours > 0.0) || !FiniteFloatValidator.IsFinite(remainingHours))
            return 0;
        return (int)Math.Min(Math.Ceiling(remainingHours), CapturedFortHoldConfig.CodeCapHours);
    }

    /// <summary>Hours left of the engine's own 10 hour pause, counted from the capture; zero or less once it has run out.</summary>
    public static double VanillaRemainingHours(double captureHours, double nowHours) =>
        captureHours + CapturedFortHoldConfig.VanillaPauseHours - nowHours;

    /// <summary>
    /// What ends a running hold early. Each requirement is a positive one, so a NaN reading fails it and releases the army
    /// to the engine's own rule, which is always safe.
    /// </summary>
    public static HoldEnd ReleaseReason(float cohesion, float lowestFoodDays, bool fortUnderSiege, CapturedFortHoldConfig config)
    {
        if (fortUnderSiege)
            return HoldEnd.Siege;
        if (!(cohesion >= config.ReleaseCohesion))
            return HoldEnd.Cohesion;
        if (!(lowestFoodDays >= config.ReleaseFoodDays))
            return HoldEnd.Food;
        return HoldEnd.None;
    }

    /// <summary>
    /// Whether an army may start a hold: cohesion at the hold floor, and nothing that would release it on the next hour
    /// (a siege, thin food, cohesion under the release floor). A NaN reading refuses.
    /// </summary>
    public static bool MayStart(float cohesion, float lowestFoodDays, bool fortUnderSiege, CapturedFortHoldConfig config) =>
        cohesion >= config.MinCohesionToHold && ReleaseReason(cohesion, lowestFoodDays, fortUnderSiege, config) == HoldEnd.None;
}
