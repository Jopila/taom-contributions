using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.AlignmentDesertion;

/// <summary>ADR-002: entry points stay under 150 lines.</summary>
[TestClass]
public class AlignmentDesertionBehaviorLineCountTests
{
    [TestMethod]
    public void AlignmentDesertionBehavior_StaysUnder150Lines()
    {
        var path = RepoPaths.RepoPath("Main", "Features", "AlignmentDesertion", "Hooks", "AlignmentDesertionBehavior.cs");
        var lines = File.ReadAllLines(path).Length;
        Assert.IsTrue(lines < 150, "ADR-002: AlignmentDesertionBehavior.cs is 150 lines or more (" + lines + ")");
    }
}
