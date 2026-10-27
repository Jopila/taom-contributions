using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using TaleWorlds.ModuleManager;

namespace TAOM.Adapters;

/// <summary>Boundary implementation of <see cref="IKeptPairSourceAdapter"/>: reads the language XML of the loaded modules, skipping a file that does not parse.</summary>
public class KeptPairSourceAdapter : IKeptPairSourceAdapter
{
    // The modules whose language files hold vanilla's rows, in load order: a later module's row for an id wins.
    private static readonly string[] VanillaModules = { "Native", "SandBoxCore", "SandBox", "StoryMode" };

    public string? LanguageId(string languageDir)
    {
        var file = Path.Combine(LanguageFolder("Native", languageDir) ?? string.Empty, "language_data.xml");
        if (!File.Exists(file))
            return null;
        return (string?)XDocument.Load(file).Root?.Attribute("id");
    }

    public IReadOnlyDictionary<string, string> VanillaRows(string languageDir)
    {
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var module in VanillaModules)
            foreach (var file in XmlFiles(LanguageFolder(module, languageDir), "*.xml"))
                if (!file.EndsWith("language_data.xml", StringComparison.OrdinalIgnoreCase))
                    ReadRows(file, rows);
        return rows;
    }

    public IReadOnlyDictionary<string, string> TaomRows(string languageDir)
    {
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in XmlFiles(LanguageFolder("TAOM", languageDir), "std_taom_*.xml"))
            ReadRows(file, rows);
        return rows;
    }

    private static string? LanguageFolder(string module, string languageDir)
    {
        var info = ModuleHelper.GetModuleInfo(module);
        return info == null ? null : Path.Combine(info.FolderPath, "ModuleData", "Languages", languageDir);
    }

    private static IEnumerable<string> XmlFiles(string? folder, string pattern) =>
        folder != null && Directory.Exists(folder)
            ? Directory.GetFiles(folder, pattern).OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            : Enumerable.Empty<string>();

    private static void ReadRows(string file, Dictionary<string, string> rows)
    {
        try
        {
            foreach (var el in XDocument.Load(file).Descendants("string"))
            {
                var id = (string?)el.Attribute("id");
                if (!string.IsNullOrEmpty(id))
                    rows[id!] = (string?)el.Attribute("text") ?? string.Empty;
            }
        }
        catch (XmlException)
        {
            // Skipped on purpose; see the class summary.
        }
    }
}
