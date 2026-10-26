using TAOM.Core.Validation;

namespace TAOM.Features.Encyclopedia;

/// <summary>When the hero page's skills heading carries the hero's level (#907).</summary>
public static class HeroLevelHeader
{
    /// <summary>From the engine's coming-of-age on, and never while the page hides the hero's information.</summary>
    public static bool ShouldShowLevel(float age, int heroComesOfAge, bool informationHidden) =>
        !informationHidden && FiniteFloatValidator.IsFinite(age) && age >= heroComesOfAge;
}
