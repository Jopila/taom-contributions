using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Core;

/// <summary>
/// No production code null-checks <c>PartyBase.MainParty</c>. The getter (v1.5.5 PartyBase.cs:209-219) returns
/// null without a campaign, but with one it dereferences <c>Campaign.Current.MainParty.Party</c>, so it throws
/// while <c>Campaign.MainParty</c> is null: the check throws in the one case it is written for. The messenger
/// popup shipped that check (fixed in v2.0.34); the stored <c>Campaign.Current?.MainParty</c> is the safe test.
/// </summary>
[TestClass]
public class MainPartyNullCheckScanTests
{
    private static readonly Regex NullCheck = new(@"PartyBase\s*\.\s*MainParty\s*==\s*null", RegexOptions.Compiled);

    [TestMethod]
    public void ProductionSource_NeverNullChecksTheComputedPartyBaseMainParty()
    {
        var offenders = Directory.EnumerateFiles(RepoPaths.RepoPath("Main"), "*.cs", SearchOption.AllDirectories)
            .Where(p => RepoPaths.MentionsOutsideComments(p, "MainParty"))
            .Where(p => NullCheck.IsMatch(RepoPaths.StripComments(File.ReadAllText(p))))
            .Select(p => p.Substring(RepoPaths.RepoPath().Length).TrimStart('\\', '/'))
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "PartyBase.MainParty is a computed getter that dereferences Campaign.Current.MainParty, so it throws "
            + "while the main party is null instead of returning null; use `Campaign.Current?.MainParty == null`. "
            + "Offending files: " + string.Join(", ", offenders));
    }
}
