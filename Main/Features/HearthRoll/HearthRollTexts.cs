using TaleWorlds.Localization;

namespace TAOM.Features.HearthRoll;

/// <summary>
/// The two age lines of Hearth Roll (#906), as inline <c>{=taom_hr_*}default</c> literals so the harvester registers them. One
/// year takes the singular line and every other count the plural. Two keys cannot express every language's plural rules (Russian
/// and Polish need a third form), so a translation may use a label form that does not agree with the number.
/// </summary>
internal static class HearthRollTexts
{
    internal static TextObject Age(int years)
        => (years == 1 ? new TextObject("{=taom_hr_age_one}{AGE} year old") : new TextObject("{=taom_hr_age_many}{AGE} years old"))
            .SetTextVariable("AGE", years);
}
