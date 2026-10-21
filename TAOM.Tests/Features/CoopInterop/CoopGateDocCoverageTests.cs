using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CoopInterop;

/// <summary>
/// #921: a campaign behavior that a feature module declares must say how it behaves in co-op.
/// docs/features/coop-interop.md records that in the "Gated (host-only)" table or the
/// "Deliberately NOT gated" list. A behavior counts as classified only where a table row or a list
/// entry starts with its backticked type name (a line beginning "| `Name`" or a paragraph beginning
/// "`Name`"); a passing mention inside another row's prose does not count. Behaviors registered by
/// hand in SubModule.cs are not covered.
/// </summary>
[TestClass]
public class CoopGateDocCoverageTests
{
    private const string DocPath = "docs/features/coop-interop.md";

    /// <summary>
    /// Behaviors that predate this test and are not yet classified. The list may only shrink: a new
    /// behavior gets its row in coop-interop.md and is never added here.
    /// </summary>
    private static readonly string[] Baseline =
    {
        "ArmourAcquisitionCampaignBehavior",
        "ArmouryMenuBehavior",
        "CreatureBroodCampaignDiagBehavior",
        "CreatureBroodSpawnBehavior",
        "LordHarnessEventBehavior",
        "LordsLadderBehavior",
        "TournamentRewardsBehavior",
        "TrollBandSpawnBehavior",
        "WandererAllegianceDialogBehavior",
        "RealmBordersCampaignBehavior",
        // Landed on bannerlord-1.5.x while this test was in review (2026-10-19), so they predate it too.
        "HostReviewCampaignBehavior",
        "HiddenHostsBehavior",
        "HiddenHostsPlainSightBehavior",
        "TaomBuildingSeedingBehavior",
        "LordCourtesiesDialogBehavior",
    };

    [TestMethod]
    public void EveryFeatureModuleCampaignBehavior_HasACoopRow()
    {
        var section = ClassifiedSection();
        var missing = DeclaredNames()
            .Where(n => !Baseline.Contains(n) && !IsClassified(section, n))
            .ToList();

        Assert.AreEqual(0, missing.Count,
            "Campaign behaviors with no co-op classification: " + string.Join(", ", missing)
            + ". Add a row to the 'Gated (host-only)' table or to the 'Deliberately NOT gated' list in "
            + DocPath + ".");
    }

    [TestMethod]
    public void Baseline_HasNoStaleEntries()
    {
        var section = ClassifiedSection();
        var declared = DeclaredNames();
        var stale = Baseline
            .Where(n => !declared.Contains(n) || IsClassified(section, n))
            .ToList();

        Assert.AreEqual(0, stale.Count,
            "Baseline entries that are now classified in " + DocPath + " or no longer declared by a "
            + "feature module: " + string.Join(", ", stale) + ". Remove it from the baseline.");
    }

    private static HashSet<string> DeclaredNames() =>
        new(FeatureModules.All.SelectMany(m => m.CampaignBehaviors).Select(d => d.BehaviorType.Name),
            StringComparer.Ordinal);

    // A gated-table row starts "| `Name`"; a "Deliberately NOT gated" entry is a paragraph that starts "`Name`".
    private static bool IsClassified(string section, string name) =>
        Regex.IsMatch(section, @"(?:^\| |\n\n)" + "`" + Regex.Escape(name) + "`", RegexOptions.Multiline);

    /// <summary>From the "### Gated (host-only)" heading up to the next "## " heading, which spans the
    /// "### Deliberately NOT gated" section as well.</summary>
    private static string ClassifiedSection()
    {
        var doc = RepoPaths.ReadSource(DocPath);
        var start = doc.IndexOf("### Gated (host-only)", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "Heading '### Gated (host-only)' not found in " + DocPath);
        var next = Regex.Match(doc.Substring(start), @"^## ", RegexOptions.Multiline);
        return next.Success ? doc.Substring(start, next.Index) : doc.Substring(start);
    }
}
