using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;
using TAOM.Features.CoopInterop.Missions;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CoopInterop.Missions;

/// <summary>
/// Pins which TAOM mission behaviors a co-op session adds, and that the role table names every behavior
/// the mod adds (#967, C7). The whole-behavior gate is the only filter at add time: host status is not
/// known inside Mission.AfterStart, so everything else is checked live inside the behavior.
/// </summary>
[TestClass]
public class CoopMissionRolePolicyTests
{
    [TestMethod]
    public void ShouldAdd_NoSession_AddsEveryRole()
    {
        foreach (CoopMissionRole role in Enum.GetValues(typeof(CoopMissionRole)))
            Assert.IsTrue(CoopMissionRolePolicy.ShouldAdd(role, sessionActive: false), role.ToString());
    }

    [TestMethod]
    public void ShouldAdd_Session_DropsOnlyUnsafe()
    {
        Assert.IsTrue(CoopMissionRolePolicy.ShouldAdd(CoopMissionRole.Local, true));
        Assert.IsTrue(CoopMissionRolePolicy.ShouldAdd(CoopMissionRole.AgentOwner, true));
        Assert.IsFalse(CoopMissionRolePolicy.ShouldAdd(CoopMissionRole.Unsafe, true));
    }

    [TestMethod]
    public void ShouldAdd_UnlistedBehavior_IsAddedInASession()
    {
        // A behavior the table does not name defaults to Local: the coverage test below is what catches
        // a new one, so a player's session must not lose a behavior just because the table lags.
        Assert.IsTrue(CoopMissionRolePolicy.ShouldAdd(typeof(CoopMissionRolePolicyTests), sessionActive: true));
        Assert.AreEqual(CoopMissionRole.Local, CoopMissionRoles.Of(typeof(CoopMissionRolePolicyTests)));
    }

    [TestMethod]
    public void ShouldAdd_ByType_UsesTheTable()
    {
        Assert.IsFalse(CoopMissionRolePolicy.ShouldAdd(
            typeof(TAOM.Features.SiegeDismount.Hooks.SiegeDismountMissionBehavior), sessionActive: true));
        Assert.IsTrue(CoopMissionRolePolicy.ShouldAdd(
            typeof(TAOM.Features.SiegeDismount.Hooks.SiegeDismountMissionBehavior), sessionActive: false));
        Assert.IsTrue(CoopMissionRolePolicy.ShouldAdd(typeof(TAOM.Features.Warg.WargMissionBehavior), true));
    }

    [TestMethod]
    public void Table_UnsafeSet_IsExactlyTheSixDroppedInASession()
    {
        var unsafeNames = CoopMissionRoles.All
            .Where(p => p.Value == CoopMissionRole.Unsafe).Select(p => p.Key).OrderBy(n => n).ToArray();

        CollectionAssert.AreEqual(new[]
        {
            "EnlistmentMeritMissionBehavior", "FellFoesMissionLogic", "FieldCommissionMissionLogic",
            "LostFootingMissionLogic", "ShaderPrecompilePlayerAgentGuard", "SiegeDismountMissionBehavior",
        }, unsafeNames);
    }

    [TestMethod]
    public void Table_CultureDoctrine_IsLocalAndNotGated()
    {
        Assert.AreEqual(CoopMissionRole.Local,
            CoopMissionRoles.Of(typeof(TAOM.Features.CultureDoctrine.Hooks.CultureDoctrineMissionLogic)));
        Assert.IsTrue(CoopMissionRolePolicy.ShouldAdd(
            typeof(TAOM.Features.CultureDoctrine.Hooks.CultureDoctrineMissionLogic), sessionActive: true));
    }

    [TestMethod]
    public void Table_CoversEveryBehaviorTheModAdds_NoMissingNoStale()
    {
        var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        var added = Regex.Matches(subModule, @"AddTaomBehavior\(\s*new\s+([\w.]+)\(")
            .Cast<Match>().Select(m => m.Groups[1].Value.Split('.').Last()).ToList();
        Assert.AreEqual(31, added.Count, "AddTaomBehavior(new ...) call sites in SubModule.cs changed; recount and update the table");

        var declared = FeatureModules.All.SelectMany(m => m.MissionBehaviors).Select(d => d.BehaviorType.Name).ToList();
        Assert.AreEqual(12, declared.Count, "module mission behavior declarations changed; update the table");

        // Added outside AddTaomBehavior: the load-phase closer (SubModule), the custom battle patches, the
        // host review launcher. All local by construction.
        var outside = new[] { "BattleLoadPhaseBehavior", "CustomBattleTeamFixBehavior", "HostReviewMissionLogic" };

        var expected = new HashSet<string>(added.Concat(declared).Concat(outside));
        var actual = new HashSet<string>(CoopMissionRoles.All.Keys);

        CollectionAssert.AreEquivalent(expected.OrderBy(n => n).ToList(), actual.OrderBy(n => n).ToList(),
            "missing: " + string.Join(", ", expected.Except(actual)) + "; stale: " + string.Join(", ", actual.Except(expected)));
    }
}
