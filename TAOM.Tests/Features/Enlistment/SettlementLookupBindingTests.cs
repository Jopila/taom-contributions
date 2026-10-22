using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Source-level guard against a lookup that compiles, runs, throws nothing, and always returns null.
///
/// `CampaignObjectManager` registers exactly five object types on 1.4.7 — MobileParty, two Hero
/// lists, Clan and Kingdom. `Find&lt;T&gt;` only descends into a bucket when `typeof(T)` matches one of
/// them, so `Find&lt;Settlement&gt;(anyId)` returns null UNCONDITIONALLY. Settlements resolve through
/// `Settlement.Find`, which goes to `MBObjectManager.Instance.GetObject&lt;Settlement&gt;`.
///
/// This shipped twice: `MobilePartyAttachmentAdapter.MoveIntoSettlement` could never have worked
/// (it was also never called, so nothing surfaced it), and `DutyWorldAdapter` silently failed every
/// spawned hunt duty — including after a fix on 2026-08-07 that corrected the settlement CHOICE
/// while still resolving it through the broken lookup.
///
/// A runtime test cannot catch this without a live Campaign, so the guard is on the source.
/// </summary>
[TestClass]
public class SettlementLookupBindingTests
{
    // Every compiled code root's Adapters folder (core and the client submodule, ADR-013); a root with no
    // such folder is skipped, but core's must exist.
    private static IEnumerable<string> AdapterFiles()
    {
        var dirs = RepoPaths.CodeRoots.Select(root => RepoPaths.RepoPath(root, "Adapters")).Where(Directory.Exists).ToList();
        Assert.IsTrue(dirs.Contains(RepoPaths.RepoPath("Main", "Adapters")), "Main/Adapters was not found");
        return dirs.SelectMany(dir => Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories));
    }

    [TestMethod]
    public void NoAdapterResolvesASettlementThroughCampaignObjectManager()
    {
        var offenders = AdapterFiles()
            .Select(f => new { File = Path.GetFileName(f), Lines = File.ReadAllLines(f) })
            .SelectMany(x => x.Lines.Select((line, i) => new { x.File, Line = line, Number = i + 1 }))
            .Where(x => x.Line.Contains("Find<Settlement>")
                        && !x.Line.TrimStart().StartsWith("//"))
            .Select(x => $"{x.File}:{x.Number}")
            .ToList();

        Assert.AreEqual(
            0,
            offenders.Count,
            "CampaignObjectManager.Find<Settlement> returns null unconditionally on 1.4.7 — use "
            + "Settlement.Find(id) instead. Offending lines: " + string.Join(", ", offenders));
    }
}
