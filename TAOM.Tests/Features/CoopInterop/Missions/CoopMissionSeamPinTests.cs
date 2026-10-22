using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CoopInterop.Missions;

/// <summary>
/// Source pins for the co-op ownership checks inside TAOM mission behaviors (#967, C7). Each check sits
/// BEFORE the action it guards, because a check after the write protects nothing. The seams run engine
/// code that no unit test reaches, so the order is pinned in the source text (comments stripped).
/// Single-player stays identical: <c>CoopMissionOwnership</c> answers "owned" with no session.
/// </summary>
[TestClass]
public class CoopMissionSeamPinTests
{
    private const string Owned = "CoopMissionOwnership.IsOwnedHere(";

    private static string Source(string path) => RepoPaths.ReadSource(path, stripComments: true);

    // The text of one member, from its signature to the next member at the same indent.
    private static string Member(string source, string signatureFragment)
    {
        var start = source.IndexOf(signatureFragment, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"'{signatureFragment}' is gone; update this pin.");
        var next = Regex.Match(source.Substring(start + signatureFragment.Length), @"\n    (?:public|private|protected|internal)\b");
        return next.Success ? source.Substring(start, signatureFragment.Length + next.Index) : source.Substring(start);
    }

    private static void CheckPrecedes(string body, string check, string action, string what)
    {
        var c = body.IndexOf(check, StringComparison.Ordinal);
        var a = body.IndexOf(action, StringComparison.Ordinal);
        Assert.IsTrue(c >= 0, $"{what}: '{check}' is missing.");
        Assert.IsTrue(a >= 0, $"{what}: '{action}' is gone; update this pin.");
        Assert.IsTrue(c < a, $"{what}: the ownership check must come before '{action}'.");
    }

    [TestMethod]
    public void AuthorityAndOwnershipAreRegistered()
    {
        var ioc = Source("Main/Features/CoopInterop/CoopInteropIoC.cs");
        StringAssert.Contains(ioc, "Register<Missions.ICoopMissionAuthority, Missions.CoopMissionAuthority>(Reuse.Singleton)");
    }

    [TestMethod]
    public void Ownership_NoContainerOrNoSession_AnswersOwnedWithoutTheRegistry()
    {
        var src = Source("Main/Features/CoopInterop/Missions/CoopMissionOwnership.cs");
        StringAssert.Contains(src, "authority == null || authority.IsLocallyControlled(agent)");
        StringAssert.Contains(src, "authority == null || authority.IsBattleHost(mission)");

        var authority = Source("Main/Features/CoopInterop/Missions/CoopMissionAuthority.cs");
        var owned = Member(authority, "public bool IsLocallyControlled(Agent? agent)");
        CheckPrecedes(owned, "if (!_session.IsSessionActive) return true;", "LinkFor(", "IsLocallyControlled");
        var host = Member(authority, "public bool IsBattleHost(Mission? mission)");
        CheckPrecedes(host, "if (!_session.IsSessionActive) return true;", "LinkFor(", "IsBattleHost");
    }

    [TestMethod]
    public void Authority_UsesCompiledDelegatesAndNeverReflectionInvoke()
    {
        var src = Source("Main/Features/CoopInterop/Missions/CoopMissionAuthority.cs");
        StringAssert.Contains(src, ".Compile()");
        Assert.IsFalse(Regex.IsMatch(src, @"\.Invoke\("), "a per-agent call must go through a compiled delegate, not MethodInfo.Invoke");
    }

    [TestMethod]
    public void BehaviorTreeAgentComponent_RunsATreeOnlyForAnOwnedAgent()
    {
        var src = Source("Main/BehaviorTreeWrapper/BehaviorTreeAgentComponent.cs");
        var tick = Member(src, "internal void TickOnMissionThread(float dt)");
        CheckPrecedes(tick, Owned + "Agent)", "Tree.RunTree()", "tree tick");
        // The skipped tick restarts the cadence, so a non-owner peer asks once per evaluation delay, not every frame.
        CheckPrecedes(tick, Owned + "Agent)", "timeSinceLastEvaluation = 0f;", "tree tick");
        // Never decide in the build callback: a mount's controller is AI there whatever co-op will make it.
        Assert.IsFalse(src.Contains("OnAgentBuild"), "ownership is read each tick, never at build time");
    }

    [TestMethod]
    public void OnWargDied_ActsOnlyForAnOwnedRider()
    {
        var src = Source("Main/Features/Warg/BehaviorTreeElements/OnWargDied.cs");
        var notify = Member(src, "public override void Notify(object[] data)");
        CheckPrecedes(notify, Owned + "rider)", "FinishRageMode.Reset", "OnWargDied");
        CheckPrecedes(notify, Owned + "rider)", "CleanIfEnemyDied.ResetAiControlledAgent", "OnWargDied");
    }

    [TestMethod]
    public void BehaviorTreeNotifyAll_StaysUngated_BecauseOnlyOnWargDiedActs()
    {
        var src = Source("Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs");
        var notifyAll = Member(src, "private void NotifyAll(List<BannerlordBTListener> listeners, object[] data)");
        Assert.IsFalse(notifyAll.Contains("CoopMission"), "gate the one listener that acts (OnWargDied), not the dispatcher");
        // The other event listeners only write their own blackboard or log.
        StringAssert.Contains(Source("Main/Features/Spider/BehaviorTreeElements/OnSpiderDied.cs"), "LogDebug(");
        var rage = Member(Source("Main/Features/Warg/BehaviorTreeElements/WargTryToGoRage.cs"), "public override void Notify(object[] data)");
        Assert.IsFalse(rage.Contains("TakeDamage") || rage.Contains("RegisterBlow") || rage.Contains("ChangeMorale"));
    }

    [TestMethod]
    public void BoneCheck_RegistersABlowOnlyForAnOwnedAttacker()
    {
        var src = Source("Main/Features/AdvancedCombat/BoneCheck.cs");
        CheckPrecedes(src, Owned, "_onCollisionCallback?.Invoke(", "bone hit");
        StringAssert.Contains(src, "is AgentAdapter");
    }

    [TestMethod]
    public void SignatureStrikes_AttackerAndVictimAreChecked()
    {
        var logic = Source("Main/Features/SignatureStrikes/Hooks/SignatureStrikesMissionLogic.cs");
        var hit = Member(logic, "public override void OnMeleeHit(");
        CheckPrecedes(hit, Owned + "attacker)", "_buffer.Enqueue(", "OnMeleeHit");

        var runner = Source("Main/Features/SignatureStrikes/Hooks/SignatureStrikeRunner.cs");
        var run = Member(runner, "public void Run(Mission mission, in StrikeRequest request)");
        CheckPrecedes(run, Owned + "attacker)", "_sound.Play(", "ring attacker");
        CheckPrecedes(run, Owned + "victim)", "victim.ChangeMorale(", "ring victim morale");
    }

    [TestMethod]
    public void CrewSpawners_SpawnOnlyForAnOwnedRider()
    {
        var howdah = Source("Main/Features/Elephant/HowdahCrewSpawner.cs");
        CheckPrecedes(Member(howdah, "private void SpawnQueued("), Owned + "mahout)", "Spawn(machine, mahout, mission)", "howdah crew");

        var mumak = Source("Main/Features/Mumakil/MumakilCrewSpawner.cs");
        CheckPrecedes(Member(mumak, "private void SpawnQueued("), Owned + "rider)", "Spawn(platform, rider, mission)", "mumakil crew");
    }

    [TestMethod]
    public void BannerBearers_TryAssignBanner_SkipsAFormationThisPeerDoesNotOwn()
    {
        var src = Source("Main/Features/BannerBearers/Hooks/BannerBearerAssignmentMissionLogic.cs");
        CheckPrecedes(Member(src, "private void TryAssignBanner("), Owned, "bannerLogic.SetFormationBanner(", "banner");
    }

    [TestMethod]
    public void DreadPulse_DrainsOnlyOwnedTargets()
    {
        var src = Source("Main/Features/DreadAura/Hooks/DreadPulseRunner.cs");
        CheckPrecedes(Member(src, "public void Run("), Owned + "target)", "target.ChangeMorale(", "dread pulse");
    }

    [TestMethod]
    public void CareerAbilities_BuffAndInspireOnlyOwnedAllies()
    {
        var src = Source("Main/Features/CareerSystem/Abilities/MissionAbilityExecutionContext.cs");
        CheckPrecedes(Member(src, "public void ApplyMoraleBurst("), Owned + "ally)", "ai.Morale =", "morale burst");
        CheckPrecedes(Member(src, "private void ApplyAoeBuff("), Owned + "ally)", "CareerAbilityBuffTracker.AddAllyContribution", "aoe buff");
    }

    [TestMethod]
    public void RaceAbilityFear_FrightensOnlyOwnedVictims()
    {
        var src = Source("Main/Features/RaceAbilities/Hooks/RaceAbilityDeaths.cs");
        CheckPrecedes(Member(src, "private void Frighten("), Owned + "victim)", "victim.ChangeMorale(", "race fear");
    }

    [TestMethod]
    public void CreatureBanditBackstop_NeverRoutsAPuppet()
    {
        var src = Source("Main/Features/CreatureBandits/CreatureBanditMissionBehavior.cs");
        var removed = Member(src, "public override void OnAgentRemoved(");
        // Controller, not the registry: the dying agent may already be deregistered.
        CheckPrecedes(removed, "Controller == AgentControllerType.None", "SetRouted(", "backstop");
        Assert.IsFalse(removed.Contains("CoopMissionOwnership"), "a dying agent may be deregistered: read Controller");
    }

    [TestMethod]
    public void SmartCavalryAvoidance_SteersOnlyOwnedRiders()
    {
        var src = Source("Main/Features/SmartCavalryAI/Hooks/SmartCavalryAIMissionBehavior.cs");
        CheckPrecedes(Member(src, "private void ApplyCollisionAvoidance("), Owned + "agent)", "agent.SetMovementDirection(", "avoidance");
    }

    [TestMethod]
    public void CreatureSiegeRole_ExcludesLaddersOnlyForAIControlledAgents()
    {
        var src = Source("Main/Features/CreatureSiegeRole/CreatureSiegeRoleService.cs");
        // Already safe: a puppet is Controller None, so IsAIControlled is false and it is never excluded.
        CheckPrecedes(src, "var isAIControlled = agent.IsAIControlled;", "agent.ExcludeFace(", "siege role");
        StringAssert.Contains(src, "if (isAIControlled && !route.Excluded)");
    }
}
