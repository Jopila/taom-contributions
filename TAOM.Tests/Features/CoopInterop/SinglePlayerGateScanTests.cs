using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CoopInterop;

/// <summary>
/// The single-player predicate has one home, <c>CoopSessionPolicy.IsSinglePlayerAuthority</c> (113d3483b). A copy of its four terms
/// (session, defer to host, dedicated server and authority) in a statement elsewhere drifts the day a term changes. The scan
/// matches on the authority term, because the three-term copies in other features (no authority term) are equivalent in practice
/// and stay as they are (docs/reviews/rca-camp-work-2026-10-26.md, Follow-ups).
/// </summary>
[TestClass]
public class SinglePlayerGateScanTests
{
    private static readonly string[] Terms = { "IsSessionActive", "ShouldDeferToHost", "IsDedicatedServer", "IsAuthority" };

    // A statement is the text between two of ; { } in the comment-stripped source, so a copy split over lines is still one.
    internal static IEnumerable<string> FourTermStatements(string source) =>
        Regex.Split(RepoPaths.StripComments(source), "[;{}]").Where(statement => Terms.All(statement.Contains));

    private static IEnumerable<string> Offenders() =>
        RepoPaths.CSharpFiles(RepoPaths.CodeRoots)
            .Where(file => Path.GetFileName(file) != "CoopSessionPolicy.cs")
            .Where(file => FourTermStatements(File.ReadAllText(file)).Any())
            .Select(file => file.Replace(RepoPaths.RepoPath(), "").TrimStart(Path.DirectorySeparatorChar));

    [TestMethod]
    public void NoFileOutsideTheCoopSessionPolicy_WritesTheFourTermGate()
    {
        var offenders = Offenders().ToList();

        Assert.AreEqual(0, offenders.Count,
            "call CoopSessionPolicy.IsSinglePlayerAuthority(coop, server) instead of writing its four terms: " + string.Join(", ", offenders));
    }

    [TestMethod]
    public void TheScan_SeesTheFormOnOneLine_AcrossLines_AndInAnyOrder()
    {
        const string oneLine = "bool A => !c.IsSessionActive && !c.ShouldDeferToHost && !s.IsDedicatedServer && c.IsAuthority;";
        const string split = "bool B =>\n    !c.IsSessionActive\n    && !c.ShouldDeferToHost\n    && !s.IsDedicatedServer\n    && c.IsAuthority;";
        const string reordered = "bool C => c.IsAuthority && !s.IsDedicatedServer && !c.ShouldDeferToHost && !c.IsSessionActive;";

        Assert.AreEqual(1, FourTermStatements(oneLine).Count());
        Assert.AreEqual(1, FourTermStatements(split).Count());
        Assert.AreEqual(1, FourTermStatements(reordered).Count());
    }

    [TestMethod]
    public void TheScan_LeavesTheThreeTermCopiesAndCommentsAlone()
    {
        const string threeTerm = "bool A => !c.IsSessionActive && !c.ShouldDeferToHost && !s.IsDedicatedServer;";
        const string comment = "// ShouldDeferToHost as well as IsAuthority, IsSessionActive and IsDedicatedServer\nint x = 1;";
        const string apart = "void M() { if (c.IsSessionActive) { } if (c.ShouldDeferToHost) { } if (s.IsDedicatedServer) { } if (c.IsAuthority) { } }";

        Assert.AreEqual(0, FourTermStatements(threeTerm).Count());
        Assert.AreEqual(0, FourTermStatements(comment).Count());
        Assert.AreEqual(0, FourTermStatements(apart).Count());
    }

    [TestMethod]
    public void ThePolicyItself_StillHoldsTheOneCopy()
    {
        var policy = File.ReadAllText(RepoPaths.RepoPath("Main", "Features", "CoopInterop", "CoopSessionPolicy.cs"));

        Assert.AreEqual(1, FourTermStatements(policy).Count());
    }
}
