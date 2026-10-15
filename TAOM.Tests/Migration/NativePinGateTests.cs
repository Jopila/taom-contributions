using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Migration;

/// <summary>
/// Keeps every per-build native address table (a <c>KnownBuilds</c> dictionary) inside the engine-bump
/// gate. The pin tests read the installed <c>TaleWorlds.Native.dll</c>, so they are LiveInstall; without
/// <c>BindingVerification</c> an unlisted build reads Skipped and a bump ships without its row (the
/// v1.5.5 SkeletonBuffer row was missed that way). Source-only, so it runs in every suite.
/// </summary>
[TestClass]
public class NativePinGateTests
{
    private static readonly Regex TableDeclaration =
        new(@"static\s+readonly\s+[^;=]*\bKnownBuilds\s*=", RegexOptions.Compiled);

    private static readonly Lazy<List<(string Class, string Source)>> Classes = new(ScanPinTestClasses);

    private static List<(string Class, string Source)> PinTestClasses() => Classes.Value;

    /// <summary>Whether <paramref name="category"/> is on the declaration of class <paramref name="name"/>
    /// itself, not on a method or another class in the same file.</summary>
    private static bool IsClassTagged(string source, string name, string category) =>
        Regex.IsMatch(source, @"\[TestCategory\(""" + Regex.Escape(category) + @"""\)\][^{]*\bclass\s+" + Regex.Escape(name) + @"\b");

    private static List<(string Class, string Source)> ScanPinTestClasses()
    {
        var root = RepoPaths.RepoPath("TAOM.Tests");
        var found = new List<(string, string)>();
        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                || path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                continue;
            var text = File.ReadAllText(path);
            if (TableDeclaration.IsMatch(text))
                found.Add((Path.GetFileNameWithoutExtension(path), text));
        }
        return found;
    }

    [TestMethod]
    public void KnownBuildsTables_EachClass_IsTaggedBindingVerificationAndLiveInstall()
    {
        var classes = PinTestClasses();
        Assert.IsTrue(classes.Count >= 2, "Expected at least the ClipBudget and SkeletonBuffer tables, found " + classes.Count);

        foreach (var (name, source) in classes)
        {
            Assert.IsTrue(IsClassTagged(source, name, "LiveInstall"), name + " must be tagged LiveInstall.");
            Assert.IsTrue(IsClassTagged(source, name, "BindingVerification"),
                name + " must be tagged BindingVerification, or an unlisted engine build skips instead of failing the gate.");
        }
    }

    [TestMethod]
    public void IsClassTagged_ATagOnAMethodOrAnotherClass_DoesNotCount()
    {
        const string tagged = @"[TestClass]
[TestCategory(""BindingVerification"")]
public class Pins
{
}
";
        const string methodOnly = @"[TestClass]
public class Pins
{
    [TestMethod]
    [TestCategory(""BindingVerification"")]
    public void T() { }
}
";
        const string otherClass = @"[TestCategory(""BindingVerification"")]
public class Other
{
}
[TestClass]
public class Pins
{
}
";

        Assert.IsTrue(IsClassTagged(tagged, "Pins", "BindingVerification"));
        Assert.IsFalse(IsClassTagged(methodOnly, "Pins", "BindingVerification"));
        Assert.IsFalse(IsClassTagged(otherClass, "Pins", "BindingVerification"));
    }

    [TestMethod]
    public void KnownBuildsTables_EachClass_IsNamedInTheEngineBumpSkill()
    {
        var skill = RepoPaths.ReadSource(".claude/skills/engine-bump/SKILL.md");
        foreach (var (name, _) in PinTestClasses())
            Assert.IsTrue(skill.Contains(name), name + " holds a KnownBuilds table and must be listed in the engine-bump skill.");
    }
}
