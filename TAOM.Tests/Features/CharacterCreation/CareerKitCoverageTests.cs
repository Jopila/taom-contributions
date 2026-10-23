using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TAOM.Tests.Core;

namespace TAOM.Tests.Features.CharacterCreation;

/// <summary>
/// Every character-creation culture either has its career starting kits (one roster per archetype and
/// sex, <c>player_career_{culture}_{archetype}_{m|f}</c>) or is a documented exception that keeps the
/// youth-title kit through CareerStartingEquipmentService's fallback. The three Arnor realms shipped
/// without kits because nothing failed (#978 review); a new culture now fails here until someone decides.
/// </summary>
[TestClass]
public class CareerKitCoverageTests
{
    /// <summary>Playable cultures deliberately without career rosters. Empty since 2026-10-23, when
    /// every playable culture got its kits; a culture added here needs Mike's word.</summary>
    private static readonly HashSet<string> WithoutCareerKits = new(StringComparer.Ordinal);

    private static readonly string[] Archetypes = { "ranged", "cavalry", "infantry" };

    [TestMethod]
    public void EveryCharacterCreationCulture_HasAllSixCareerKits_OrIsADocumentedException()
    {
        var md = CultureDataFixture.ModuleDataPath();
        var cultures = JArray.Parse(File.ReadAllText(Path.Combine(md, "charactercreation", "cultures.json")))
            .Select(t => (string?)t["culture_id"]).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        Assert.IsTrue(cultures.Count > 20, $"read only {cultures.Count} cultures from cultures.json");

        var kitFile = File.ReadAllText(Path.Combine(md, "equipmentsets", "taom_career_starting_equipment.xml"));
        var rosters = new HashSet<string>(
            Regex.Matches(kitFile, "<EquipmentRoster id=\"(player_career_[a-z_]+)\"").Cast<Match>().Select(m => m.Groups[1].Value),
            StringComparer.Ordinal);

        var problems = new List<string>();
        foreach (var culture in cultures)
        {
            var missing = Archetypes.SelectMany(a => new[] { "m", "f" }.Select(s => $"player_career_{culture}_{a}_{s}"))
                .Where(id => !rosters.Contains(id)).ToList();
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
}
