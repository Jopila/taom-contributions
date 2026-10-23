using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Composition;

/// <summary>
/// ADR-013 decision 3: no mission is expected on a dedicated server until a staged run proves it. If one opens,
/// AddTaomMissionBehaviors logs once and adds no TAOM behavior (the caller still closes the loading window).
/// </summary>
[TestClass]
public class SubModuleMissionServerGuardTests
{
    [TestMethod]
    public void AddTaomMissionBehaviors_OnADedicatedServer_LogsAndAddsNothing()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        int phase = src.IndexOf("ReportPatchFailures(new TextObject(\"{=taom_patch_apply_phase_mission_start}", StringComparison.Ordinal);
        int diag = src.IndexOf("var battleLoadDiagSvc", StringComparison.Ordinal);
        Assert.IsTrue(phase >= 0 && diag > phase, "an anchor moved");

        var between = src.Substring(phase, diag - phase);
        // The exact positive condition: an inverted check (`if (!IoC...`) does not match (Codex B4c review F1).
        const string condition = "if (IoC.Resolve<Features.CoopInterop.IDedicatedServerProvider>().IsDedicatedServer)";
        int guard = between.IndexOf(condition, StringComparison.Ordinal);
        Assert.IsTrue(guard >= 0, "no dedicated server check between the mission-start patch report and the battle-load bracket");
        Assert.AreEqual(-1, between.IndexOf("IsDedicatedServer", guard + condition.Length, StringComparison.Ordinal),
            "one server check only");

        int ret = between.IndexOf("return;", guard, StringComparison.Ordinal);
        Assert.IsTrue(ret > guard, "the guard returns");
        StringAssert.Contains(between.Substring(guard, ret - guard), "LogError(", "the guard logs before it returns");
    }

    // The patch category index leaves out [ClientOnly] classes only when ClientComposition.IsDedicatedServer() is
    // true, and the 6c guards read the same seam; an inverted body would strip them from a client and index them on
    // a server, with every other test green.
    [TestMethod]
    public void ClientCompositionIsDedicatedServer_ReadsTheBuildConstantWithoutNegation()
    {
        var src = RepoPaths.ReadSource("Main/Composition/ClientComposition.cs", stripComments: true);

        var seam = Regex.Match(src, @"static\s+Func<bool>\s+IsDedicatedServer\s*=\s*(?<init>[^;]*);");
        var read = Regex.Match(src, @"static\s+bool\s+ReadDedicatedServer\s*\(\s*\)\s*=>\s*(?<body>[^;]*);");

        Assert.IsTrue(seam.Success && read.Success, "an anchor moved");
        Assert.AreEqual("ReadDedicatedServer", seam.Groups["init"].Value.Trim(), "the seam's default must be the engine read");
        Assert.AreEqual("GameNetwork.IsDedicatedServer", Regex.Replace(read.Groups["body"].Value, @"\s+", " ").Trim(),
            "the body must be the exact positive build-constant read, no negation");
    }
}
