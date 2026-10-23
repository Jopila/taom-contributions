using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CoopInterop;

/// <summary>
/// A patch class that caches a service in a static field must be reset when the module unloads, or
/// after a reload-in-process it holds a reference into a disposed IoC container and silently drops
/// everything it was meant to log. Codex review #46 found that and fixed it for four classes by
/// hand. Patch71 then shipped the same omission (#486 second-pass review), which is five classes
/// maintained by memory alone.
///
/// So this scans source rather than trusting vigilance: every <c>ResetForUnload()</c> that exists
/// must actually be CALLED from <c>SubModule.OnSubModuleUnloaded</c>. Declaring one and forgetting
/// to wire it is exactly as broken as never writing it, and looks more correct.
/// </summary>
[TestClass]
public class ResetForUnloadSweepTests
{
    private static readonly Regex ResetDecl = new Regex(
        @"public\s+static\s+void\s+ResetForUnload\s*\(", RegexOptions.Compiled);

    private static readonly Regex ClassDecl = new Regex(
        @"\b(?:public|internal)\s+(?:static\s+|sealed\s+|partial\s+)*class\s+(\w+)", RegexOptions.Compiled);

    /// <summary>Every class declaring a public static ResetForUnload(), by class name.</summary>
    private static IReadOnlyList<string> DeclaringClasses()
    {
        var names = new List<string>();

        foreach (var path in RepoPaths.CSharpFiles())
        {
            var text = File.ReadAllText(path);
            if (!ResetDecl.IsMatch(text)) continue;

            var classes = ClassDecl.Matches(text)
                .Cast<Match>()
                .Select(m => (m.Index, Name: m.Groups[1].Value))
                .OrderBy(c => c.Index)
                .ToList();

            foreach (Match decl in ResetDecl.Matches(text))
            {
                var owner = classes.LastOrDefault(c => c.Index < decl.Index);
                names.Add(owner.Name ?? Path.GetFileNameWithoutExtension(path));
            }
        }

        return names.Distinct().OrderBy(n => n).ToList();
    }

    [TestMethod]
    public void EveryResetForUnload_IsCalledFromOnSubModuleUnloaded()
    {
        var declaring = DeclaringClasses();
        Assert.AreNotEqual(0, declaring.Count,
            "scan found no ResetForUnload declarations at all — the regex is broken, not the codebase.");

        var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        var start = subModule.IndexOf("OnSubModuleUnloaded", System.StringComparison.Ordinal);
        Assert.AreNotEqual(-1, start, "SubModule.OnSubModuleUnloaded not found.");
        var sweep = subModule.Substring(start);

        var unwired = declaring
            .Where(name => !sweep.Contains(name + ".ResetForUnload("))
            .ToList();

        Assert.AreEqual(0, unwired.Count,
            "These classes declare ResetForUnload() but SubModule.OnSubModuleUnloaded never calls it, so " +
            "their cached statics survive into a disposed IoC container on a reload-in-process:\n  " +
            string.Join("\n  ", unwired) +
            "\n\nAdd the call to the sweep in Main/SubModule.cs.");
    }
    [TestMethod]
    public void TaomClient_DeclaresNoResetForUnload()
    {
        var offenders = RepoPaths.CSharpFiles("Client")
            .Where(f => ResetDecl.IsMatch(File.ReadAllText(f)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "A TAOM.Client class may not declare ResetForUnload(): the sweep in Main/SubModule.cs.OnSubModuleUnloaded "
            + "is the only caller and ClientSubModule has none. Offenders: " + string.Join(", ", offenders));
    }
}
