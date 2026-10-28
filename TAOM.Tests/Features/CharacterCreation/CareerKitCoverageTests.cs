using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TAOM.Features.CareerSystem;
using TAOM.Features.CharacterCreation;
using TAOM.Tests.Core;

namespace TAOM.Tests.Features.CharacterCreation;

/// <summary>
/// Every character-creation culture either has a career starting kit for every career it offers (one roster per
/// archetype and sex, <c>player_career_{culture}_{archetype}_{m|f}</c>, the id CareerEquipmentRosterIds builds from
/// the career's archetype) or is a documented exception that keeps the youth-title kit through
/// CareerStartingEquipmentService's fallback. The three Arnor realms shipped without kits because nothing failed
/// (#978 review). The archetypes are read from the careers the culture offers (taom_careers.xml through
/// CareerSystemIoC's archetype map), so Ered Luin, whose careers are infantry, two-handed and ranged only (dwarves do not
/// ride and it fields no war ram), needs no cavalry kit, and a culture that gains a career gains its kit demand the same
/// day. A culture that offers no career at all is CareerCultureCoverageTests' finding.
/// </summary>
[TestClass]
public class CareerKitCoverageTests
{
    /// <summary>Playable cultures deliberately without career rosters. Empty since 2026-10-23, when
    /// every playable culture got its kits; a culture added here needs Mike's word.</summary>
    private static readonly HashSet<string> WithoutCareerKits = new(StringComparer.Ordinal);

    [TestMethod]
    public void EveryCharacterCreationCulture_HasAKitForEveryCareerArchetypeItOffers_OrIsADocumentedException()
    {
        var md = CultureDataFixture.ModuleDataPath();
        var cultures = JArray.Parse(File.ReadAllText(Path.Combine(md, "charactercreation", "cultures.json")))
            .Select(t => (string?)t["culture_id"]).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        Assert.IsTrue(cultures.Count > 20, $"read only {cultures.Count} cultures from cultures.json");

        var kitFile = File.ReadAllText(Path.Combine(md, "equipmentsets", "taom_career_starting_equipment.xml"));
        var rosters = new HashSet<string>(
            Regex.Matches(kitFile, "<EquipmentRoster id=\"(player_career_[a-z_]+)\"").Cast<Match>().Select(m => m.Groups[1].Value),
            StringComparer.Ordinal);

        var needed = KitIdsByCulture(md);
        Assert.IsTrue(needed.Count > 20, $"derived career kits for only {needed.Count} cultures from taom_careers.xml");

        var problems = new List<string>();
        foreach (var culture in cultures)
        {
            needed.TryGetValue(culture!, out var ids);
            var missing = (ids ?? new SortedSet<string>(StringComparer.Ordinal)).Where(id => !rosters.Contains(id)).ToList();
            var exempt = WithoutCareerKits.Contains(culture!);
            if (missing.Count > 0 && !exempt)
                problems.Add($"{culture}: no {string.Join(", ", missing)}");
            if (missing.Count == 0 && exempt)
                problems.Add($"{culture}: has its kits now; remove it from WithoutCareerKits");
        }

        Assert.AreEqual(0, problems.Count,
            "Build the kits (skeletons in taom_career_starting_equipment.xml, then "
            + "tools/generate_career_kits.py --apply) or list the culture in WithoutCareerKits:\n  "
            + string.Join("\n  ", problems));
    }

    /// <summary>culture id -> the roster ids its careers' archetypes need, both sexes.</summary>
    private static Dictionary<string, SortedSet<string>> KitIdsByCulture(string moduleDataPath)
    {
        var archetypeOf = CareerSystemIoC.GetCareerArchetypeMap();
        var needed = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var careers = XDocument.Load(Path.Combine(moduleDataPath, "career_system", "taom_careers.xml")).Descendants("Career");
        foreach (var career in careers)
        {
            // An unmapped career gets no kit at all; CareerArchetypeServiceTests owns that finding.
            if (!archetypeOf.TryGetValue((string?)career.Attribute("id") ?? string.Empty, out var archetype))
                continue;
            foreach (var culture in career.Descendants("Culture").Select(c => (string?)c.Attribute("id")))
            {
                if (string.IsNullOrEmpty(culture))
                    continue;
                if (!needed.TryGetValue(culture!, out var ids))
                    needed[culture!] = ids = new SortedSet<string>(StringComparer.Ordinal);
                ids.Add(CareerEquipmentRosterIds.Build(culture!, archetype, isFemale: false));
                ids.Add(CareerEquipmentRosterIds.Build(culture!, archetype, isFemale: true));
            }
        }
        return needed;
    }
}
