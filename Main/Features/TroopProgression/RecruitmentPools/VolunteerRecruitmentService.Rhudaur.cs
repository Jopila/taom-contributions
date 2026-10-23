using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

// Volunteer recruitment pool for Rhudaur, the hill kingdom of Arnor's three (#978; troops generated
// by tools/generate_rhudaur_troops.py). The data half of VolunteerRecruitmentService, registered by
// the static ctor in the core file. Lookup priority: conditional > settlement > clan > culture
// (troops.md).
public partial class VolunteerRecruitmentService
{
    private static void InitializeRhudaurCulture()
    {
        // Two roots reach the whole tree: the Hillman Levy heads the hill-men line (and, through the
        // Hill Spearman, the Hill Rider), and the Dunadan of the Angle splits into the Angle Rangers
        // and the Guard of Cameth Brin. Levy-heavy: the Dunedain are few among the hill-men.
        CultureMap["rhudaur"] = new List<VolunteerChance>
        {
            new VolunteerChance("rhudaur_levy", 6),
            new VolunteerChance("rhudaur_dunadan", 2)
        };
    }
}
