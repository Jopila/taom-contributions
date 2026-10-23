using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Composition;

/// <summary>
/// ADR-013, B4c. The two mission views live in TAOM.Client, so SubModule adds them through core factory seams at
/// the old add sites. The order is load bearing (behaviors tick in reverse registration order), so each seam is
/// pinned where the `new` used to be.
/// </summary>
[TestClass]
public class SubModuleMissionViewWiringTests
{
    private static string Source() => RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

    private static int At(string src, string needle)
    {
        int i = src.IndexOf(needle, StringComparison.Ordinal);
        Assert.IsTrue(i >= 0, "missing in SubModule.cs: " + needle);
        return i;
    }

    [TestMethod]
    public void AddTaomMissionBehaviors_AddsTheMovementControllerBetweenCombatAndWarg()
    {
        var src = Source();
        int combat = At(src, "AddTaomBehavior(new AdvancedCombatBehavior());");
        int create = At(src, "IoC.Resolve<IAutonomousMovementControllerFactory>().Create()");
        int guarded = At(src, "if (movementController != null) AddTaomBehavior(movementController);");
        int warg = At(src, "AddTaomBehavior(new WargMissionBehavior());");

        Assert.IsTrue(combat < create && create < guarded && guarded < warg,
            "the controller seam sits between AdvancedCombatBehavior and WargMissionBehavior");

        var offenders = RepoPaths.CSharpFiles("Main")
            .Where(f => RepoPaths.StripComments(File.ReadAllText(f)).Contains("new AutonomousMovementPlayerController("))
            .Select(Path.GetFileName).ToList();
        CollectionAssert.AreEqual(new string[0], offenders, "no file under Main may construct the client controller");
    }

    [TestMethod]
    public void AddTaomMissionBehaviors_AddsTheActionBarAfterTheTickProfiler_BeforeTheModules()
    {
        var src = Source();
        int profiler = At(src, "new Features.MissionPerf.Hooks.MissionTickProfilerBehavior(");
        int create = At(src, "IoC.Resolve<Features.CompanionTactics.BattleActionBar.IBattleActionBarViewFactory>().Create()");
        int guarded = At(src, "if (actionBar != null) AddTaomBehavior(actionBar);");
        int modules = At(src, "FeatureModuleHooks.AddMissionBehaviors(mission, AddTaomBehavior);");

        Assert.IsTrue(profiler < create && create < guarded && guarded < modules,
            "the action bar seam sits after the tick profiler and before the feature modules' behaviors");
        Assert.IsFalse(src.Contains("new Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView("),
            "SubModule must not construct the client view");
    }
}
