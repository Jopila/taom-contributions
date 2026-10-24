using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>ADR-002: entry points stay under 150 lines.</summary>
[TestClass]
public class EnlistmentHooksLineCountTests
{
    /// <summary>
    /// Files already over the ceiling at HEAD before this pin existed. Each is a known exception, not
    /// a licence: do not add to this list, and do not grow these files.
    /// </summary>
    private static readonly Dictionary<string, int> KnownOverCeiling = new Dictionary<string, int>
    {
        { "EnlistedSoldierPlacement.cs", 175 },
        { "EnlistmentBattleBehavior.cs", 303 },
    };

    [TestMethod]
    public void EveryEnlistmentHooksFile_StaysUnder150Lines_ExceptTheNamedOnes()
    {
        var files = Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "Enlistment", "Hooks"), "*.cs");
        Assert.IsTrue(files.Length >= 15);
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            var lines = File.ReadAllLines(file).Length;
            if (KnownOverCeiling.TryGetValue(name, out var ceiling))
                Assert.IsTrue(lines <= ceiling, "ADR-002: the known exception " + name + " grew past " + ceiling);
            else
                Assert.IsTrue(lines < 150, "ADR-002: " + name + " is 150 lines or more");
        }
    }
}
