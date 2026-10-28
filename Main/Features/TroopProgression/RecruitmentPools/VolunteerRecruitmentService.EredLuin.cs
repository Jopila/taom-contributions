using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

// Volunteer recruitment pool for Ered Luin, the dwarf kingdom of the Blue Mountains (troops generated
// by tools/generate_eredluin_troops.py). The data half of VolunteerRecruitmentService, registered by
// the static ctor in the core file. Lookup priority: conditional > settlement > clan > culture
// (troops.md).
public partial class VolunteerRecruitmentService
{
    private static void InitializeEredLuinCulture()
    {
        // Two roots reach the whole tree: the Delver heads the axe and mattock lines and the Blue
        // Mountain bowmen, and the Noble of Ered Luin splits into the Broadbeam guards and the
        // Firebeard crossbowmen. Delver-heavy, as Cardolan's levy.
        CultureMap["eredluin"] = new List<VolunteerChance>
        {
            new VolunteerChance("eredluin_delver", 6),
            new VolunteerChance("eredluin_noble", 2)
        };
    }
}
