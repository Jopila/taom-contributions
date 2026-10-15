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
/// while <c>Campaign.MainParty</c> is null: the check throws in the one case it is written for, whichever way
/// round it is written. The messenger popup (<c>== null</c>) and the hunt popup (<c>!= null</c>) shipped that
/// check (both fixed in v2.0.34); the stored <c>Campaign.Current?.MainParty</c> is the safe test. The scan
/// covers both polarities, the reversed forms, the <c>is</c> patterns and <c>?.</c>, which guards only the
/// no-campaign case (read <c>Campaign.Current?.MainParty?.Party</c> instead); comments are blanked first, string
/// literals are not (<see cref="RepoPaths.StripComments"/>).
/// </summary>
[TestClass]
public class MainPartyNullCheckScanTests
{
    private static readonly Regex NullCheck = new(
        @"\bPartyBase\s*\.\s*MainParty\s*(?:[!=]=\s*null\b|is\s+(?:not\s+)?null\b|\?\.)"
        + @"|\bnull\s*[!=]=\s*PartyBase\s*\.\s*MainParty\b",
        RegexOptions.Compiled);

    [DataTestMethod]
    [DataRow("if (PartyBase.MainParty == null) return;")]
    [DataRow("PartyBase.MainParty != null && x")]
    [DataRow("if (null == PartyBase.MainParty) return;")]
    [DataRow("null != PartyBase.MainParty && x")]
    [DataRow("if (PartyBase.MainParty is null) return;")]
    [DataRow("PartyBase.MainParty is not null && x")]
    [DataRow("PartyBase . MainParty!=null")]
    [DataRow("return PartyBase.MainParty?.MapEvent != null;")]
    [DataRow("var roster = PartyBase.MainParty?.ItemRoster;")]
    [DataRow("PartyBase . MainParty ?. ItemRoster")]
    public void NullCheck_EveryFormOfTheComputedGetterCheck_IsCaught(string source)
    {
        Assert.IsTrue(NullCheck.IsMatch(RepoPaths.StripComments(source)), source);
    }

    [DataTestMethod]
    [DataRow("if (Campaign.Current?.MainParty == null) return;")]
    [DataRow("Campaign.Current?.MainParty != null && x")]
    [DataRow("var party = PartyBase.MainParty;")]
    [DataRow("PartyBase.MainParty.MobileParty")]
    [DataRow("// PartyBase.MainParty != null")]
    [DataRow("/* null == PartyBase.MainParty */ x")]
    [DataRow("x = y; // if (PartyBase.MainParty is not null)")]
    [DataRow("var roster = Campaign.Current?.MainParty?.Party?.ItemRoster;")]
    public void NullCheck_TheStoredPartyOrACommentOrAPlainRead_IsNotCaught(string source)
    {
        Assert.IsFalse(NullCheck.IsMatch(RepoPaths.StripComments(source)), source);
    }

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

    [TestMethod]
    public void HunterPartyAdapter_IsMapFree_ChecksTheStoredMainParty()
    {
        // The hunt popup's guard: `PartyBase.MainParty != null` threw while the main party was null.
        var source = RepoPaths.ReadSource("Main/Adapters/HunterPartyAdapter.cs", stripComments: true);
        var start = source.IndexOf("bool IsMapFree()", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "HunterPartyAdapter.IsMapFree() not found");
        var end = source.IndexOf(';', start);
        var body = source.Substring(start, end - start);

        StringAssert.Contains(body, "Campaign.Current?.MainParty != null");
        Assert.IsFalse(body.Contains("PartyBase"), "IsMapFree must not read the computed PartyBase.MainParty: " + body);
    }
}
