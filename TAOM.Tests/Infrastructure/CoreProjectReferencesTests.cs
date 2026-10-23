using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// ADR-013, B4c. WinForms is TAOM.Client's: with the references gone from the core project, the compiler proves
/// that no core file uses System.Windows.Forms or System.Drawing.
/// </summary>
[TestClass]
public class CoreProjectReferencesTests
{
    [TestMethod]
    public void MainCsproj_ReferencesNoWinForms()
    {
        var csproj = File.ReadAllText(RepoPaths.RepoPath("Main", "TAOM.csproj"));

        StringAssert.DoesNotMatch(csproj, new System.Text.RegularExpressions.Regex("Include=\"System\\.Windows\\.Forms\""));
        StringAssert.DoesNotMatch(csproj, new System.Text.RegularExpressions.Regex("Include=\"System\\.Drawing\""));
    }
}
