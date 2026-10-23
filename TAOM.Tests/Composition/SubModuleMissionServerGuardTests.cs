using System;
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
}
