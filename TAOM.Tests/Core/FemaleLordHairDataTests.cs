using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Core;

/// <summary>
/// A human lady whose face is only a face_key_template rolls her hair from the template's hair tags. The
/// human templates in TAOM_bodyproperties.xml carry men's tags (fighter_gondor: TiedAcrossBack,
/// LongAndBushy and others), which match no adult female hair in vanilla skins.xml. With no match, the
/// native roll falls back to the hair indices of the template's min and max keys, and female index 0 is
/// the nameless Bald mesh, so about half such ladies start bald and the rest share one style (#978 review).
/// Each one therefore carries her own hair_tags inside her face, as the Harad ladies do with "aserai".
/// </summary>
[TestClass]
public class FemaleLordHairDataTests
{
    private static string ModuleData => RepoPath("Main", "_Module", "ModuleData");

    [TestMethod]
    public void EveryHumanLadyOnAFaceTemplate_CarriesHerOwnHairTags()
    {
        var missing = new List<string>();
        foreach (var file in Directory.EnumerateFiles(ModuleData, "*.xml", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "Languages" + Path.DirectorySeparatorChar)) continue;
            XDocument doc;
            try { doc = XDocument.Load(file); }
            catch (Exception) { continue; }

            foreach (var npc in doc.Descendants("NPCCharacter"))
            {
                if (!IsTrue(npc, "is_female") || !IsTrue(npc, "is_hero")) continue;
                if ((string)npc.Attribute("occupation") != "Lord") continue;
                var race = (string)npc.Attribute("race");
                if (race != null && race != "human") continue;

                var face = npc.Element("face");
                if (face?.Element("face_key_template") == null) continue;
                if (face.Element("BodyProperties") != null) continue;
                if (face.Element("hair_tags")?.Elements("hair_tag").Any() == true) continue;

                missing.Add($"{Path.GetFileName(file)}: {(string)npc.Attribute("id")}");
            }
        }

        Assert.AreEqual(0, missing.Count,
            "Human ladies on a face template with no hair_tags of their own (half will start bald):\n" +
            string.Join("\n", missing));
    }

    private static bool IsTrue(XElement e, string attribute) =>
        string.Equals((string)e.Attribute(attribute), "true", StringComparison.OrdinalIgnoreCase);
}
