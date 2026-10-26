using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.BanditVoices;
using TAOM.Features.BanditVoices.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.BanditVoices;

/// <summary>
/// Pins the vanilla seams Bandit Voices rides (#900) against the installed engine, with no campaign: the private condition
/// still returns a bool with no parameters, still writes the two text variables the postfix overwrites, the private
/// <c>_interactedBandits</c> field still binds as a non-generic dictionary keyed by the party, and the category attaches
/// exactly one postfix to the target.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class BanditVoicesBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static MethodInfo Target()
    {
        RequireGame();
        var method = AccessTools.Method(typeof(BanditInteractionsCampaignBehavior), "bandit_neutral_greet_on_condition");
        Assert.IsNotNull(method, "BanditInteractionsCampaignBehavior.bandit_neutral_greet_on_condition did not resolve.");
        return method!;
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheTarget_IsThePrivateInstanceBoolCondition_WithNoParameters_InInstalledEngine()
    {
        var target = Target();

        Assert.IsFalse(target.IsStatic);
        Assert.IsTrue(target.IsPrivate);
        Assert.AreEqual(typeof(bool), target.ReturnType);
        Assert.AreEqual(0, target.GetParameters().Length);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ThePostfix_DeclaresTheNameHarmonyInjectsBy()
    {
        var postfix = typeof(BanditGreetingPatch).GetMethod("Postfix")!;

        CollectionAssert.AreEqual(new[] { "__result" }, postfix.GetParameters().Select(p => p.Name!).ToArray());
        Assert.AreEqual(typeof(bool), postfix.GetParameters()[0].ParameterType);
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheTarget_StillWritesTheTwoVariablesThePostfixOverwrites_InInstalledEngine()
    {
        var strings = PatchProcessor.GetOriginalInstructions(Target())
            .Where(ci => ci.opcode == System.Reflection.Emit.OpCodes.Ldstr)
            .Select(ci => (string)ci.operand)
            .ToList();

        Assert.IsTrue(strings.Contains("BANDIT_NEUTRAL_GREETING"), "vanilla no longer sets BANDIT_NEUTRAL_GREETING here");
        Assert.IsTrue(strings.Contains("BANDIT_ULTIMATUM"), "vanilla no longer sets BANDIT_ULTIMATUM here");
        Assert.IsTrue(strings.Any(s => s.StartsWith("{=A5IJmN0X}", StringComparison.Ordinal)), "vanilla's harmless ultimatum key moved");
        Assert.IsTrue(strings.Any(s => s.StartsWith("{=ASRpFaGF}", StringComparison.Ordinal)), "vanilla's brothers ultimatum key moved");
        Assert.AreEqual(BanditVoicesService.HarmlessUltimatum, strings.Single(s => s.StartsWith("{=A5IJmN0X}", StringComparison.Ordinal)), "the harmless default text drifted from vanilla's");
        Assert.AreEqual(BanditVoicesService.BrothersUltimatum, strings.Single(s => s.StartsWith("{=ASRpFaGF}", StringComparison.Ordinal)), "the brothers default text drifted from vanilla's");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheInteractedBanditsField_IsAPrivateDictionaryKeyedByTheParty_InInstalledEngine()
    {
        RequireGame();
        var field = AccessTools.Field(typeof(BanditInteractionsCampaignBehavior), "_interactedBandits");

        Assert.IsNotNull(field, "BanditInteractionsCampaignBehavior._interactedBandits is gone.");
        Assert.IsTrue(field!.IsPrivate);
        Assert.IsTrue(typeof(IDictionary).IsAssignableFrom(field.FieldType), "the field is no longer a non-generic dictionary");
        Assert.AreEqual(typeof(MobileParty), field.FieldType.GetGenericArguments()[0], "the dictionary is no longer keyed by the party");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheAdapterBinding_ResolvesTheField_AndReadsAFreshBehaviorsEmptyDictionary()
    {
        RequireGame();
        var logger = Substitute.For<IModLogger>();

        var accessor = BanditEncounterTextAdapter.BindInteractedBandits(logger);

        Assert.IsNotNull(accessor, "the field reference did not bind");
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        var behavior = (BanditInteractionsCampaignBehavior)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(BanditInteractionsCampaignBehavior));
        var dictionary = (IDictionary)Activator.CreateInstance(
            AccessTools.Field(typeof(BanditInteractionsCampaignBehavior), "_interactedBandits")!.FieldType)!;
        AccessTools.Field(typeof(BanditInteractionsCampaignBehavior), "_interactedBandits")!.SetValue(behavior, dictionary);

        Assert.AreSame(dictionary, accessor!(behavior), "the field reference reads a different object than the field holds");
        Assert.IsTrue(BanditEncounterTextAdapter.FirstMeeting(accessor(behavior), new object()),
            "a key the dictionary does not hold is a first meeting");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ADictionaryWithTheParty_IsNotAFirstMeeting_AndAnUnreadableOneIsNotEither()
    {
        var party = new object();
        var seen = new Dictionary<object, int> { [party] = 1 };

        Assert.IsFalse(BanditEncounterTextAdapter.FirstMeeting(seen, party));
        Assert.IsTrue(BanditEncounterTextAdapter.FirstMeeting(new Dictionary<object, int>(), party));
        Assert.IsFalse(BanditEncounterTextAdapter.FirstMeeting(null, party));
        Assert.IsFalse(BanditEncounterTextAdapter.FirstMeeting(seen, null));
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void PatchCategory_AppliedToTheInstalledEngine_AttachesOnePostfixToTheTarget_AndTheJitAccepts()
    {
        const string id = "taom.tests.banditvoices.jit";
        var target = Target();
        var harmony = new Harmony(id);
        try
        {
            PatchCategoryIndex.Build(typeof(BanditVoicesModule).Assembly).Apply(harmony, BanditVoicesModule.PatchCategory);

            var info = Harmony.GetPatchInfo(target);
            Assert.IsNotNull(info, "the target was not patched: the class is not in the category");
            Assert.AreEqual(1, info!.Postfixes.Count(p => p.owner == id), "postfix count");
            Assert.AreEqual(0, info.Prefixes.Count(p => p.owner == id), "prefix count");
            Assert.AreEqual(0, info.Transpilers.Count(p => p.owner == id), "transpiler count");
            Assert.AreEqual(0, info.Finalizers.Count(p => p.owner == id), "finalizer count");
            RuntimeHelpers.PrepareMethod(target.MethodHandle);
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }
}
