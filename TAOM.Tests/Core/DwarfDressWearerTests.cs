using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// A dwarf dress (<c>Item.sk_dwarf_dress_*</c>, "Civilian Female Dress" in the Armory) may sit only
/// where a woman draws it. Mike's rule (2026-10-16): <c>is_female</c> decides who is a woman, and every
/// other dwarf wears a tunic.
///
/// <para>
/// Sex is <c>is_female</c> on a character, absent meaning male (<c>BasicCharacterObject.Deserialize</c>),
/// and <c>IsFemaleTemplate</c> on a template roster: <c>DefaultEquipmentSelectionModel</c> adds that flag
/// only for a woman and pools exact matches, and a <c>="false"</c> sets nothing. A spawned agent draws
/// its Body from one of its sets at random, so one dress among a man's sets dresses him part of the
/// time. Issue #56 (2026-04-02) gave "neutral" Erebor roles one dress and one tunic; the engine has no
/// neutral, so those men wore dresses until 2026-10-16.
/// </para>
///
/// <para>
/// The rule fails closed: a dress anywhere else fails, whoever wears it, so a runtime roster no
/// character names (a <c>player_*_m</c> kit, an enlistment kit) is covered too. A new female-only
/// context gets a clause in <see cref="IsInAWomansContext"/>.
/// <see cref="TownsfolkAndNotableSexConsistencyTests"/> owns the other direction (who is a woman).
/// Reads the repo's own ModuleData, so it needs no game install and no engine assemblies.
/// </para>
/// </summary>
[TestClass]
public class DwarfDressWearerTests
{
    private const string DressPrefix = "Item.sk_dwarf_dress_";

    private static readonly string LanguagesSegment =
        $"{Path.DirectorySeparatorChar}Languages{Path.DirectorySeparatorChar}";

    private static IEnumerable<(string File, XDocument Doc)> ModuleDataDocuments()
    {
        foreach (var file in Directory.GetFiles(CultureDataFixture.ModuleDataPath(), "*.xml",
                                                SearchOption.AllDirectories))
        {
            if (file.IndexOf(LanguagesSegment, StringComparison.OrdinalIgnoreCase) >= 0) continue;
            yield return (Path.GetFileName(file), XDocument.Load(file));
        }
    }

    private static bool IsTrue(XElement e, string attribute) =>
        string.Equals((string?)e.Attribute(attribute), "true", StringComparison.OrdinalIgnoreCase);

    private static bool IsDress(XElement e) =>
        (e.Name.LocalName == "equipment" || e.Name.LocalName == "Equipment")
        && ((string?)e.Attribute("id") ?? string.Empty).StartsWith(DressPrefix, StringComparison.Ordinal);

    /// <summary>The character that wears the dress inline, else the outermost roster that holds it.</summary>
    private static XElement? Owner(XElement dress) =>
        dress.Ancestors("NPCCharacter").FirstOrDefault() ?? dress.Ancestors("EquipmentRoster").LastOrDefault();

    /// <summary>
    /// True when only a woman draws this dress: an <c>is_female="true"</c> character, or a roster
    /// flagged <c>IsFemaleTemplate="true"</c> or named <c>mother_*</c> that no male character names.
    /// </summary>
    internal static bool IsInAWomansContext(XElement dress, ISet<string> rostersMenName)
    {
        var owner = Owner(dress);
        if (owner == null) return false;
        if (owner.Name.LocalName == "NPCCharacter") return IsTrue(owner, "is_female");

        // father_* and mother_* are drawn by id from code, so no XML reference shows who wears them.
        // A father roster is a man's whatever its flags; a mother roster that carries template flags
        // also joins a template pool, which a man can draw from.
        var id = (string?)owner.Attribute("id") ?? string.Empty;
        var flags = owner.Elements("Flags").ToList();
        return id.Length > 0
               && !rostersMenName.Contains(id)
               && !id.StartsWith("father_", StringComparison.Ordinal)
               && (flags.Any(f => IsTrue(f, "IsFemaleTemplate"))
                   || (id.StartsWith("mother_", StringComparison.Ordinal) && flags.Count == 0));
    }

    private const string Dress = "<Equipment slot=\"Body\" id=\"Item.sk_dwarf_dress_normal_a\" />";

    [TestMethod]
    [DataRow("<NPCCharacter id=\"w\" race=\"dwarf\" is_female=\"true\"><Equipments><EquipmentRoster>" + Dress + "</EquipmentRoster></Equipments></NPCCharacter>", "", true)]
    [DataRow("<NPCCharacter id=\"m\" race=\"dwarf\"><Equipments><EquipmentRoster>" + Dress + "</EquipmentRoster></Equipments></NPCCharacter>", "", false)]
    [DataRow("<NPCCharacter id=\"human_man\"><Equipments><EquipmentRoster>" + Dress + "</EquipmentRoster></Equipments></NPCCharacter>", "", false)]
    [DataRow("<EquipmentRoster id=\"child_f\"><EquipmentSet>" + Dress + "</EquipmentSet><Flags IsChildEquipmentTemplate=\"true\" IsFemaleTemplate=\"true\" /></EquipmentRoster>", "", true)]
    [DataRow("<EquipmentRoster id=\"child_m\"><EquipmentSet>" + Dress + "</EquipmentSet><Flags IsChildEquipmentTemplate=\"true\" IsFemaleTemplate=\"false\" /></EquipmentRoster>", "", false)]
    [DataRow("<EquipmentRoster id=\"taom_erebor_lord_teen_male\"><EquipmentSet>" + Dress + "</EquipmentSet><Flags IsLordTemplate=\"true\" /></EquipmentRoster>", "", false)]
    [DataRow("<EquipmentRoster id=\"mother_char_creation_none_erebor\"><EquipmentSet>" + Dress + "</EquipmentSet></EquipmentRoster>", "", true)]
    [DataRow("<EquipmentRoster id=\"father_char_creation_none_erebor\"><EquipmentSet>" + Dress + "</EquipmentSet></EquipmentRoster>", "", false)]
    [DataRow("<EquipmentRoster id=\"player_career_erebor_infantry_m\"><EquipmentSet>" + Dress + "</EquipmentSet></EquipmentRoster>", "", false)]
    [DataRow("<EquipmentRoster id=\"enlist_erebor_infantry_recruit\"><EquipmentSet>" + Dress + "</EquipmentSet></EquipmentRoster>", "", false)]
    [DataRow("<EquipmentRoster id=\"child_f\"><EquipmentSet>" + Dress + "</EquipmentSet><Flags IsFemaleTemplate=\"true\" /></EquipmentRoster>", "child_f", false)]
    [DataRow("<EquipmentRoster id=\"father_char_creation_none_erebor\"><EquipmentSet>" + Dress + "</EquipmentSet><Flags IsFemaleTemplate=\"true\" /></EquipmentRoster>", "", false)]
    [DataRow("<EquipmentRoster id=\"mother_char_creation_none_erebor\"><EquipmentSet>" + Dress + "</EquipmentSet><Flags IsLordTemplate=\"true\" IsFemaleTemplate=\"false\" /></EquipmentRoster>", "", false)]
    [DataRow("<EquipmentRoster><EquipmentSet>" + Dress + "</EquipmentSet><Flags IsFemaleTemplate=\"true\" /></EquipmentRoster>", "", false)]
    [DataRow("<EquipmentSet>" + Dress + "</EquipmentSet>", "", false)]
    public void IsInAWomansContext_OwnerOfTheDress_DecidesTheVerdict(string xml, string rosterAManNames, bool expected)
    {
        var dress = XElement.Parse(xml).DescendantsAndSelf().Single(IsDress);
        var menName = new HashSet<string>(StringComparer.Ordinal);
        if (rosterAManNames.Length > 0) menName.Add(rosterAManNames);

        Assert.AreEqual(expected, IsInAWomansContext(dress, menName), xml);
    }

    [TestMethod]
    public void EveryDwarfDress_SitsWhereOnlyAWomanDrawsIt()
    {
        // One pass, so each file is released after its walk. A roster can be named by a man in a
        // file read after it, so the "named by a man" half of the rule is applied once the walk ends.
        var rostersMenName = new HashSet<string>(StringComparer.Ordinal);
        var noneYet = new HashSet<string>(StringComparer.Ordinal);
        var dresses = new List<(string Label, bool Woman, string? RosterId)>();
        foreach (var (file, doc) in ModuleDataDocuments())
        {
            foreach (var man in doc.Descendants("NPCCharacter").Where(n => !IsTrue(n, "is_female")))
                foreach (var set in man.Descendants("EquipmentSet"))
                    rostersMenName.Add((string?)set.Attribute("id") ?? string.Empty);

            foreach (var dress in doc.Descendants().Where(IsDress))
            {
                var owner = Owner(dress);
                var rosterId = owner?.Name.LocalName == "EquipmentRoster" ? (string?)owner.Attribute("id") : null;
                dresses.Add(($"{file}: {(string?)owner?.Attribute("id")}", IsInAWomansContext(dress, noneYet), rosterId));
            }
        }

        var offenders = dresses
            .Where(d => !d.Woman || (d.RosterId != null && rostersMenName.Contains(d.RosterId)))
            .Select(d => d.Label)
            .Distinct()
            .ToList();

        Assert.IsTrue(dresses.Count > 0, "No dwarf dress was found; the walk is broken.");
        Assert.AreEqual(0, offenders.Count,
            "A dwarf dress outside a woman's context (an is_female character, an IsFemaleTemplate roster, a "
            + "mother_ roster, none named by a man). Use sk_dwarf_tunic_normal_* or sk_dwarf_tunic_noble_*; a new "
            + "female-only context (a player_*_f kit, a roster only women name) gets a clause in IsInAWomansContext:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }
}
