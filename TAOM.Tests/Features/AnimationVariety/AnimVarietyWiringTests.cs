using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;
using TAOM.Features;
using TAOM.Features.AnimationVariety;
using TAOM.Features.AnimationVariety.Hooks;
using TAOM.Tests.Infrastructure;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.AnimationVariety;

[TestClass]
public class AnimVarietyWiringTests
{
    private static string BehaviorText() => File.ReadAllText(
        RepoPaths.RepoPath("Main", "Features", "AnimationVariety", "Hooks", "AnimVarietyMissionBehavior.cs"));

    [TestMethod]
    public void FeatureModules_ListTheAnimationVarietyModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<AnimationVarietyModule>().Count());
    }

    [TestMethod]
    public void Module_DeclaresTheMissionBehavior()
    {
        var decls = new AnimationVarietyModule().MissionBehaviors;

        Assert.AreEqual(2, decls.Count, "the swing probe and the gait variety behavior");
        Assert.AreEqual(typeof(AnimVarietyMissionBehavior), decls[0].BehaviorType);
    }

    [TestMethod]
    public void Behavior_StaysUnderTheEntryPointLimit()
    {
        var path = RepoPaths.RepoPath("Main", "Features", "AnimationVariety", "Hooks", "AnimVarietyMissionBehavior.cs");

        Assert.IsTrue(File.ReadAllLines(path).Length < 150);
    }

    [TestMethod]
    public void OnAgentHit_MissileBlow_ReturnsBeforeAnyAllocation()
    {
        var text = BehaviorText();
        int gate = text.IndexOf("blow.IsMissile", System.StringComparison.Ordinal);
        int firstAllocation = text.IndexOf("new AnimVarietyAgent(affectorAgent)", System.StringComparison.Ordinal);

        Assert.IsTrue(gate > 0, "OnAgentHit reads blow.IsMissile");
        Assert.IsTrue(firstAllocation > gate, "the missile gate runs before the wrapper is allocated");
    }

    // The compiler allocates a lambda's display class on entry to the scope that declares a captured
    // variable (method entry for a method local or parameter), which can precede an early return, so
    // source order cannot see it. The compiled IL can.
    [TestMethod]
    public void OnAgentHit_CompiledBody_AllocatesNoClosure()
    {
        var method = typeof(AnimVarietyMissionBehavior).GetMethod(nameof(AnimVarietyMissionBehavior.OnAgentHit))!;
        var il = method.GetMethodBody()!.GetILAsByteArray()!;
        var module = method.Module;
        var closures = new System.Collections.Generic.List<string>();

        for (int i = 0; i + 4 < il.Length; i++)
        {
            if (il[i] != 0x73) continue; // newobj
            int token = System.BitConverter.ToInt32(il, i + 1);
            try
            {
                var ctor = module.ResolveMethod(token);
                if (ctor?.DeclaringType != null && ctor.DeclaringType.Name.Contains("DisplayClass"))
                    closures.Add(ctor.DeclaringType.Name);
            }
            catch (System.ArgumentException) { /* operand bytes that only look like newobj */ }
        }

        Assert.AreEqual(0, closures.Count,
            "OnAgentHit allocates a closure on every hit, missiles included: " + string.Join(", ", closures));
    }

    [TestMethod]
    public void OnMissionTick_ClearsTheAgentListRightAfterTheServiceReturns()
    {
        var text = BehaviorText();
        int tick = text.IndexOf("_service.Tick(dt, _snapshot)", System.StringComparison.Ordinal);
        int clear = text.IndexOf("_agents.Clear()", tick, System.StringComparison.Ordinal);
        int nextMethod = text.IndexOf("OnAgentHit(Agent", tick, System.StringComparison.Ordinal);

        Assert.IsTrue(tick > 0 && clear > tick && clear < nextMethod,
            "the wrappers (and their Agent references) are dropped when the tick ends, not kept until the next one");
    }

    // On by default while the swing alternatives are being tested (Mike, 2026-10-18). MCM persists each
    // value in TAOM.json, so turning it off later means renaming the setting, not flipping this default.
    [TestMethod]
    public void ProbeSettingsProvider_WithoutMcm_IsOnByDefault()
    {
        Assert.IsTrue(new AnimVarietyProbeSettingsProvider().SwingAlternativeProbe);
        Assert.IsTrue(new TaomSettings().EnableSwingAlternativeProbe);
    }

    [TestMethod]
    public void ProbeSetting_Hint_SaysItOnlyLogsAndCostsAPerFrameSweep()
    {
        var hint = HintOf(nameof(TaomSettings.EnableSwingAlternativeProbe));

        StringAssert.Contains(hint, "only logs");
        StringAssert.Contains(hint, "per-frame");
        StringAssert.Contains(hint, "act_taom_");
    }

    internal static string HintOf(string property)
    {
        var attribute = typeof(TaomSettings).GetProperty(property)!.GetCustomAttributes(false)
            .Single(a => a.GetType().GetProperty("HintText") != null);
        return (string)attribute.GetType().GetProperty("HintText")!.GetValue(attribute)!;
    }

    // Constructing an MBActionSet runs no native call, but the reference-assembly build has no body for it.
    [TestMethod]
    [TestCategory("RequiresGame")]
    public void AgentAdapter_ActionSetKey_IsTheEngineIndexTheProbeKeysItsSetsBy()
    {
        // AnimVarietyAgent.ActionSetKey is _agent.ActionSet.GetHashCode() and the adapter turns the key
        // back into a set with GetActionSetWithIndex, so the hash must be the index (MBActionSet.cs:36-39).
        Assert.AreEqual(7, MBActionSet.GetActionSetWithIndex(7).GetHashCode());
    }
}
