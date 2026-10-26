using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Composition;
using TAOM.Features.SeasonedHosts;
using TAOM.Features.SeasonedHosts.Hooks;
using TAOM.Features.SeasonedHosts.Models;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.SeasonedHosts;

/// <summary>
/// Pins the engine seams Seasoned Hosts (#890) rides, on the installed engine and with no campaign: the steward patch target
/// resolves with its one (Settlement) overload and the category attaches exactly one prefix that JIT-compiles; each model overrides
/// a virtual its Default base declares; and each engine consumer still calls the model member, so none of the four is inert.
/// </summary>
[TestClass]
public class SeasonedHostsBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static bool CallsMember(MethodBase method, string name) =>
        PatchProcessor.GetOriginalInstructions(method).Any(ci => ci.operand is MethodInfo mi && mi.Name == name);

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void StewardTarget_ResolvesWithItsSingleSettlementOverload_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var overloads = typeof(DefaultSkillLevelingManager).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == "OnSettlementProjectFinished").ToList();

        Assert.AreEqual(1, overloads.Count, "the attribute names the method without argument types, so a second overload would be ambiguous");
        CollectionAssert.AreEqual(new[] { typeof(Settlement) }, overloads[0].GetParameters().Select(p => p.ParameterType).ToArray());
        Assert.AreEqual(typeof(void), overloads[0].ReturnType);
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void StewardCategory_AppliedToTheInstalledEngine_AttachesOneSkippingPrefixAndTheJitAccepts()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        const string id = "taom.tests.seasonedhosts.jit";
        var target = AccessTools.Method(typeof(DefaultSkillLevelingManager), "OnSettlementProjectFinished", new[] { typeof(Settlement) });
        Assert.IsNotNull(target, "DefaultSkillLevelingManager.OnSettlementProjectFinished(Settlement) did not resolve.");
        var harmony = new Harmony(id);
        try
        {
            PatchCategoryIndex.Build(typeof(SeasonedHostsModule).Assembly).Apply(harmony, SeasonedHostsModule.PatchCategory);

            var info = Harmony.GetPatchInfo(target);
            Assert.IsNotNull(info, "applying the category patched nothing: the class is not in it");
            var prefixes = info!.Prefixes.Where(p => p.owner == id).ToList();
            Assert.AreEqual(1, prefixes.Count, "exactly one prefix is attached");
            Assert.AreEqual(typeof(Patch118_ProjectStewardXp), prefixes[0].PatchMethod.DeclaringType);
            Assert.AreEqual(typeof(bool), prefixes[0].PatchMethod.ReturnType, "a skipping prefix returns bool");
            Assert.AreEqual(0, info.Postfixes.Count(p => p.owner == id) + info.Transpilers.Count(p => p.owner == id) + info.Finalizers.Count(p => p.owner == id));
            RuntimeHelpers.PrepareMethod(target.MethodHandle);
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void BuildingsBehavior_StillCallsTheProjectFinishedGrant_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var method = AccessTools.Method(typeof(BuildingsCampaignBehavior), "OnBuildingLevelChanged");
        Assert.IsNotNull(method, "BuildingsCampaignBehavior.OnBuildingLevelChanged did not resolve.");
        Assert.IsTrue(CallsMember(method!, "OnSettlementProjectFinished"),
            "vanilla no longer grants the project Steward XP from here, so the Patch118 skip may be inert");
    }

    [DataTestMethod]
    [DataRow(typeof(DefaultGenericXpModel), typeof(TaomGenericXpModel), "GetXpMultiplier")]
    [DataRow(typeof(DefaultPartyTrainingModel), typeof(TaomPartyTrainingModel), "GetEffectiveDailyExperience")]
    [DataRow(typeof(DefaultPartyTrainingModel), typeof(TaomPartyTrainingModel), "CalculateXpGainFromBattles")]
    [DataRow(typeof(DefaultCharacterDevelopmentModel), typeof(TaomCharacterDevelopmentModel), "CalculateLearningRate")]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void EachOverride_MatchesAVirtualMemberOfItsDefaultBase(Type defaultBase, Type model, string name)
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var ours = model.GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.IsNotNull(ours, model.Name + " does not override " + name);
        var theirs = defaultBase.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null,
            ours!.GetParameters().Select(p => p.ParameterType).ToArray(), null);
        Assert.IsNotNull(theirs, defaultBase.Name + "." + name + " no longer has this signature");
        Assert.IsTrue(theirs!.IsVirtual && !theirs.IsFinal, defaultBase.Name + "." + name + " is not overridable");
        Assert.AreEqual(theirs.ReturnType, ours.ReturnType);
    }

    [DataTestMethod]
    [DataRow(typeof(HeroDeveloper), "AddSkillXp", "GetXpMultiplier")]
    [DataRow(typeof(HeroDeveloper), "GetFocusFactor", "CalculateLearningRate")]
    [DataRow(typeof(MobilePartyTrainingBehavior), "OnDailyTickParty", "GetEffectiveDailyExperience")]
    [DataRow(typeof(MapEventParty), "CommitXpGain", "CalculateXpGainFromBattles")]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void EachEngineConsumer_StillCallsTheModelMember_InInstalledEngine(Type owner, string consumer, string member)
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var method = AccessTools.Method(owner, consumer);
        Assert.IsNotNull(method, owner.Name + "." + consumer + " did not resolve.");
        Assert.IsTrue(CallsMember(method!, member), owner.Name + "." + consumer + " no longer calls " + member + ", so the override would be inert");
    }
}
