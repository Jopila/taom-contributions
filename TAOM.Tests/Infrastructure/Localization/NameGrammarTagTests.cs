using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace TAOM.Tests.Infrastructure.Localization;

/// <summary>The shipped Portuguese and Italian name rows carry the grammar tags of <c>tools/translation_name_tags.json</c>, and the other languages and the cache carry none (#1023).</summary>
[TestClass]
public class NameGrammarTagTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));

    private static string TablePath => Path.Combine(RepoRoot, "tools", "translation_name_tags.json");

    private static string ModuleData => Path.Combine(RepoRoot, "Main", "_Module", "ModuleData");

    private static string LanguagesPath => Path.Combine(ModuleData, "Languages");

    private static readonly Regex LeadingTags = new Regex(@"^(?:\{\.[A-Za-z]+\})+", RegexOptions.Compiled);

    private static readonly Regex AnyGrammarTag = new Regex(@"\{\.[A-Z]{2}\}", RegexOptions.Compiled);

    private static readonly string[] TaggedLanguages = { "BR", "IT" };

    private static readonly string[] UntaggedLanguages = { "CNs", "CNt", "JP", "KO", "TR" };

    /// <summary>The tags each language's text processor reads, from por_br_functions.xml and ItalianTextProcessor.</summary>
    private static readonly Dictionary<string, string[]> ProcessorTags = new Dictionary<string, string[]>
    {
        ["BR"] = new[] { "{.NS}", "{.NP}", "{.MS}", "{.FS}", "{.MP}", "{.FP}" },
        ["IT"] = new[] { "{.FN}", "{.MN}", "{.MS}", "{.FS}", "{.MP}", "{.FP}" },
    };

    private sealed class Rule
    {
        public string Plain = "";
        public HashSet<string> Allowed = new HashSet<string>(StringComparer.Ordinal);
        public Dictionary<string, string> HeadNouns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Tags = new Dictionary<string, string>(StringComparer.Ordinal);
        public Dictionary<string, string> Pins = new Dictionary<string, string>(StringComparer.Ordinal);

        public string TagFor(string id) =>
            Pins.TryGetValue(id, out var pin) ? pin : Tags.TryGetValue(id, out var tag) ? tag : null;
    }

    private static Dictionary<string, Rule> LoadTable()
    {
        Assert.IsTrue(File.Exists(TablePath), $"Tag table not found at {TablePath}");
        var root = JObject.Parse(File.ReadAllText(TablePath));
        var rules = new Dictionary<string, Rule>();
        foreach (var lang in TaggedLanguages)
        {
            var spec = (JObject)root["languages"]![lang]!;
            var rule = new Rule { Plain = (string)spec["plain"]! };
            foreach (var t in (JArray)spec["allowed"]!) rule.Allowed.Add((string)t!);
            foreach (var p in (JObject)spec["head_nouns"]!) rule.HeadNouns[p.Key] = (string)p.Value!;
            foreach (var p in (JObject)spec["tags"]!) rule.Tags[p.Key] = (string)p.Value!;
            foreach (var p in (JObject)spec["pins"]!) rule.Pins[p.Key] = (string)p.Value!;
            rules[lang] = rule;
        }
        return rules;
    }

    private static readonly Dictionary<string, List<(string File, string Id, string Text)>> RowCache =
        new Dictionary<string, List<(string File, string Id, string Text)>>();

    /// <summary>Every row of one language's std_taom_*.xml files: (file name, id, text). Read once per language.</summary>
    private static List<(string File, string Id, string Text)> Rows(string language)
    {
        if (RowCache.TryGetValue(language, out var cached))
            return cached;
        var dir = Path.Combine(LanguagesPath, language);
        Assert.IsTrue(Directory.Exists(dir), $"Language folder not found at {dir}");
        var rows = new List<(string, string, string)>();
        foreach (var file in Directory.GetFiles(dir, "std_taom_*.xml"))
            foreach (var el in XDocument.Load(file).Descendants("string"))
                rows.Add((Path.GetFileName(file), (string)el.Attribute("id")!, (string)el.Attribute("text")! ?? ""));
        return RowCache[language] = rows;
    }

    [TestMethod]
    public void Table_AllowedTags_AreTagsTheLanguageProcessorReads()
    {
        var table = LoadTable();
        foreach (var lang in TaggedLanguages)
        {
            var known = new HashSet<string>(ProcessorTags[lang]);
            foreach (var tag in table[lang].Allowed)
                Assert.IsTrue(known.Contains(tag), $"{lang}: {tag} is not a tag the {lang} text processor reads");
            Assert.IsTrue(table[lang].Allowed.Contains(table[lang].Plain), $"{lang}: the plain tag {table[lang].Plain} is not allowed");
        }
    }

    [TestMethod]
    public void Table_EveryTagPinAndHeadNoun_IsAnAllowedTag()
    {
        var table = LoadTable();
        foreach (var lang in TaggedLanguages)
        {
            var bad = table[lang].Tags.Concat(table[lang].Pins).Concat(table[lang].HeadNouns)
                .Where(p => !table[lang].Allowed.Contains(p.Value)).Select(p => $"{p.Key}={p.Value}").ToList();
            Assert.AreEqual(0, bad.Count, $"{lang}: tags outside the allowed set: {string.Join(", ", bad.Take(10))}");
        }
    }

    [TestMethod]
    public void TaggedLanguages_EveryTableNameRow_StartsWithItsTableTag()
    {
        var table = LoadTable();
        foreach (var lang in TaggedLanguages)
        {
            var problems = new List<string>();
            var seen = 0;
            foreach (var (file, id, text) in Rows(lang))
            {
                var tag = table[lang].TagFor(id);
                if (tag == null) continue;
                seen++;
                if (!text.StartsWith(tag, StringComparison.Ordinal) || LeadingTags.Match(text).Value != tag)
                    problems.Add($"{file} {id}: '{text}' should start with exactly {tag}");
            }
            Assert.IsTrue(seen > 300, $"{lang}: only {seen} name rows matched the table; the scan or the table is broken");
            Assert.AreEqual(0, problems.Count, $"{lang}: {problems.Count} name row(s) lack their tag, run python tools/name_tags.py --lang {lang} --apply:\n  "
                + string.Join("\n  ", problems.Take(15)));
        }
    }

    [TestMethod]
    public void TaggedLanguages_AnyRowWithALeadingTag_IsATableNameWithAnAllowedTag()
    {
        var table = LoadTable();
        foreach (var lang in TaggedLanguages)
        {
            var stray = new List<string>();
            foreach (var (file, id, text) in Rows(lang))
            {
                var m = LeadingTags.Match(text);
                if (!m.Success) continue;
                var tag = table[lang].TagFor(id);
                if (tag == null || m.Value != tag || !table[lang].Allowed.Contains(m.Value))
                    stray.Add($"{file} {id}: '{text}'");
            }
            Assert.AreEqual(0, stray.Count, $"{lang}: leading tags the table does not call for:\n  " + string.Join("\n  ", stray.Take(15)));
        }
    }

    [TestMethod]
    public void TaggedLanguages_NoRowTagsTwice()
    {
        foreach (var lang in TaggedLanguages)
        {
            var doubled = Rows(lang).Where(r => AnyGrammarTag.Matches(LeadingTags.Match(r.Text).Value).Count > 1)
                .Select(r => $"{r.File} {r.Id}: '{r.Text}'").ToList();
            Assert.AreEqual(0, doubled.Count, $"{lang}: rows with stacked leading tags:\n  " + string.Join("\n  ", doubled.Take(15)));
        }
    }

    [TestMethod]
    public void UntaggedLanguages_NameRows_CarryNoGrammarTag()
    {
        var table = LoadTable();
        var ids = new HashSet<string>(TaggedLanguages.SelectMany(l => table[l].Tags.Keys.Concat(table[l].Pins.Keys)));
        foreach (var lang in UntaggedLanguages)
        {
            var tagged = Rows(lang).Where(r => ids.Contains(r.Id) && AnyGrammarTag.IsMatch(r.Text))
                .Select(r => $"{r.File} {r.Id}: '{r.Text}'").ToList();
            Assert.AreEqual(0, tagged.Count, $"{lang} name rows must carry no grammar tag:\n  " + string.Join("\n  ", tagged.Take(15)));
        }
    }

    [TestMethod]
    public void EnglishSources_NameKeys_CarryNoGrammarTag()
    {
        var table = LoadTable();
        var ids = new HashSet<string>(TaggedLanguages.SelectMany(l => table[l].Tags.Keys.Concat(table[l].Pins.Keys)));
        var inline = new Regex("\\{=([^}]+)\\}([^\"]*)\"", RegexOptions.Compiled);
        var problems = new List<string>();
        var files = Directory.GetFiles(ModuleData, "*.xml", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(ModuleData, "*.xslt", SearchOption.TopDirectoryOnly))
            .Concat(Directory.GetFiles(Path.Combine(ModuleData, "characters"), "*.xml"));
        foreach (var file in files)
            foreach (Match m in inline.Matches(File.ReadAllText(file)))
                if (ids.Contains(m.Groups[1].Value) && AnyGrammarTag.IsMatch(m.Groups[2].Value))
                    problems.Add($"{Path.GetFileName(file)} {m.Groups[1].Value}: '{m.Groups[2].Value}'");
        Assert.AreEqual(0, problems.Count, "English source names must carry no grammar tag:\n  " + string.Join("\n  ", problems.Take(15)));
    }

    [TestMethod]
    public void CachedTranslations_CarryNoGrammarTag()
    {
        var problems = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(RepoRoot, "tools", "translation_cache"), "*.json"))
            foreach (var p in JObject.Parse(File.ReadAllText(file)))
                if (AnyGrammarTag.IsMatch(LeadingTags.Match((string)p.Value! ?? "").Value))
                    problems.Add($"{Path.GetFileName(file)} {p.Key}");
        Assert.AreEqual(0, problems.Count, "The translation cache stays tag-free (tags are added when a file is written):\n  "
            + string.Join("\n  ", problems.Take(15)));
    }

    private static string RowText(string language, string id) =>
        Rows(language).First(r => r.Id == id).Text;

    [TestMethod]
    public void ItalianKingdomAndClanNames_AreMasculineNouns()
    {
        // The Italian GENDER code maps {.MN} to 4 and {.FN} to 5, and the implied noun of a kingdom or clan name ("il regno", "il clan") is masculine.
        StringAssert.StartsWith(RowText("IT", "TAOM_gondor"), "{.MN}");
        StringAssert.StartsWith(RowText("IT", "taom_erebor_short_name"), "{.MN}");
        StringAssert.StartsWith(RowText("IT", "aom_clan_gundabad_1_name"), "{.MN}");
        StringAssert.StartsWith(RowText("IT", "TAOM_beni_zilal_short"), "{.MS}");
    }

    [TestMethod]
    public void PortugueseKingdomName_IsTheNoArticleForm()
    {
        StringAssert.StartsWith(RowText("BR", "TAOM_gondor"), "{.NS}");
    }

    [TestMethod]
    public void PortugueseClanLedByAHyphenatedNoun_CarriesTheNounsTag()
    {
        StringAssert.StartsWith(RowText("BR", "aom_clan_bluecraig_2_name"), "{.FS}");
    }

    [TestMethod]
    public void NamesThatLostTheirLeadingArticle_CarryTheArticlesGender()
    {
        Assert.AreEqual("{.FS}Mão Branca", RowText("BR", "aom_clan_isengard_1_name"));
        Assert.AreEqual("{.MP}Lobos Vermelhos", RowText("BR", "aom_dunland_clan_name_01"));
        Assert.AreEqual("{.MP}Spaccacranio", RowText("IT", "aom_clan_isengard_8_name"));
    }

    [TestMethod]
    public void ItalianGondoriano_IsAnAdjectiveAndStaysUntagged()
    {
        Assert.AreEqual("Gondoriano", RowText("IT", "aom_gondor_name"));
    }
}
