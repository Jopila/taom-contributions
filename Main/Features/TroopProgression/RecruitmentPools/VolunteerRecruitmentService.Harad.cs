using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

// Volunteer recruitment pools for the Harad sphere: the Shaghana and Abanissa sub-factions plus the aserai
// Harad kingdom (clan and culture pools). The data half of VolunteerRecruitmentService, registered by the
// static ctor in the core file (T5 refactor split: one file per culture so a culture's pools + design
// rationale live together; lookup/weighting logic stays in VolunteerRecruitmentService.cs). Lookup
// priority: conditional > settlement > clan > culture (troops.md).
//
// The old harad_levy / harad_noble pair was deleted in the 2026-10-10 Harad rework. Each pool's roots
// now follow its culture's own basic_troop / elite_basic_troop (taom_spcultures.xml / spcultures.xslt):
//   aserai   : harad_reg_levy   / harad_serp_noble
//   shaghana : harad_sreg_levy  / harad_redserp_noble
//   abanissa : owns no troops, uses the aserai tree (harad_reg_levy / harad_serp_noble)
// The Pezarsan camel line root harad_pz_levy (level 16, aserai) is offered to aserai and abanissa at weight 2.
//
// The War Elephant Rider (harad_elephant_rider) and the Mumakil Rider (harad_mumakil_rider) are in NO pool
// since 2026-10-15: players buy them from the Elite Emissary for War Drums (special resources). That is why
// clan_aserai_1 (Ayerikka) has no clan pool any more. Its old pool was the aserai pool plus the two riders,
// so without them it would only shadow the culture fallback; it resolves through the aserai culture pool
// like every other Harad clan that has no pool of its own.
public partial class VolunteerRecruitmentService
{
    // --- Shaghâna Clan Mappings ---

    private static void InitializeShaghanaClans()
    {
        AddClan("clan_shaghana_1", ("harad_sreg_levy", 7), ("harad_redserp_noble", 3));
        AddClan("clan_shaghana_2", ("harad_sreg_levy", 7), ("harad_redserp_noble", 3));
        AddClan("clan_shaghana_3", ("harad_sreg_levy", 7), ("harad_redserp_noble", 3));
        AddClan("clan_shaghana_4", ("harad_sreg_levy", 7), ("harad_redserp_noble", 3));
        AddClan("clan_shaghana_5", ("harad_sreg_levy", 7), ("harad_redserp_noble", 3));
        AddClan("clan_shaghana_6", ("harad_sreg_levy", 7), ("harad_redserp_noble", 3));
        AddClan("clan_shaghana_7", ("harad_sreg_levy", 7), ("harad_redserp_noble", 3));
        AddClan("clan_shaghana_8", ("harad_sreg_levy", 7), ("harad_redserp_noble", 3));
        AddClan("clan_shaghana_9", ("harad_sreg_levy", 7), ("harad_redserp_noble", 3));
    }

    // --- Shaghâna Culture Fallback ---

    private static void InitializeShaghânaCulture()
    {
        CultureMap["shaghana"] = new List<VolunteerChance>
        {
            new VolunteerChance("harad_sreg_levy", 7),
            new VolunteerChance("harad_redserp_noble", 3)
        };
    }

    // --- Âbanissa Clan Mappings ---

    private static void InitializeAbanissaClans()
    {
        AddClan("clan_abanissa_1", ("harad_reg_levy", 7), ("harad_serp_noble", 3), ("harad_pz_levy", 2));
        AddClan("clan_abanissa_2", ("harad_reg_levy", 7), ("harad_serp_noble", 3), ("harad_pz_levy", 2));
        AddClan("clan_abanissa_3", ("harad_reg_levy", 7), ("harad_serp_noble", 3), ("harad_pz_levy", 2));
        AddClan("clan_abanissa_4", ("harad_reg_levy", 7), ("harad_serp_noble", 3), ("harad_pz_levy", 2));
        AddClan("clan_abanissa_5", ("harad_reg_levy", 7), ("harad_serp_noble", 3), ("harad_pz_levy", 2));
        AddClan("clan_abanissa_6", ("harad_reg_levy", 7), ("harad_serp_noble", 3), ("harad_pz_levy", 2));
        AddClan("clan_abanissa_7", ("harad_reg_levy", 7), ("harad_serp_noble", 3), ("harad_pz_levy", 2));
        AddClan("clan_abanissa_8", ("harad_reg_levy", 7), ("harad_serp_noble", 3), ("harad_pz_levy", 2));
    }

    // --- Âbanissa Culture Fallback ---

    private static void InitializeAbanissaCulture()
    {
        CultureMap["abanissa"] = new List<VolunteerChance>
        {
            new VolunteerChance("harad_reg_levy", 7),
            new VolunteerChance("harad_serp_noble", 3),
            new VolunteerChance("harad_pz_levy", 2)
        };
    }

    // --- Harad Culture Fallback (Culture.aserai — the Harad kingdom) ---
    // The Harad kingdom uses engine culture id "aserai" (cheatsheet). It had no CultureMap pool — only the
    // Shaghâna/Âbanissa sub-factions did — so HasCulturePool("aserai") was false and Harad conquests never
    // converted (Codex review, 2026-06-02). Offers the aserai basic/elite roots (harad_reg_levy /
    // harad_serp_noble) plus the Pezarsan camel line root harad_pz_levy at weight 2; the same pool as Âbanissa.
    private static void InitializeHaradCulture()
    {
        CultureMap["aserai"] = new List<VolunteerChance>
        {
            new VolunteerChance("harad_reg_levy",   7),
            new VolunteerChance("harad_serp_noble", 3),
            new VolunteerChance("harad_pz_levy",    2),
        };
    }
}
