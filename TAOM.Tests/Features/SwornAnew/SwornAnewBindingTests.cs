using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TAOM.Composition;
using TAOM.Features.SwornAnew;
using TAOM.Features.SwornAnew.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.SwornAnew;

/// <summary>
/// Pins the vanilla seams Sworn Anew rides (#869), reading the installed engine with no campaign: the private condition of the
/// ruler's prisoner line still exists with its shape, the line still uses it, the category applies exactly this postfix and
/// the replacement JIT-compiles, the two diplomacy conditions still ask the defection model, and the model slot still has the
/// generic argument the module declares it under.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SwornAnewBindingTests
{
    private const string ConditionName = "conversation_player_start_defection_with_prisoner_on_condition";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static MethodInfo Condition()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var method = Patch116_PrisonerDefectionLine_Postfix.TargetMethod() as MethodInfo;
        Assert.IsNotNull(method, "LordDefectionCampaignBehavior." + ConditionName + " did not resolve.");
        return method!;
    }

    private static bool CallsByName(MethodBase body, string name) =>
        PatchProcessor.GetOriginalInstructions(body).Any(ci => ci.operand is MethodInfo mi && mi.Name == name);

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void Target_IsThePrivateParameterlessBoolConditionOfTheDefectionBehavior_InInstalledEngine()
    {
        var target = Condition();

        Assert.AreEqual(typeof(LordDefectionCampaignBehavior), target.DeclaringType);
        Assert.AreEqual(typeof(bool), target.ReturnType);
        Assert.AreEqual(0, target.GetParameters().Length);
        Assert.IsFalse(target.IsStatic);
        Assert.IsTrue(target.IsPrivate, "a private condition has no caller outside the behavior's own AddPlayerLine");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void ThePostfix_TakesTheResultByReference_UnderTheNameHarmonyInjects()
    {
        var postfix = typeof(Patch116_PrisonerDefectionLine_Postfix).GetMethod("Postfix")!;

        var parameters = postfix.GetParameters();
        CollectionAssert.AreEqual(new[] { "__result" }, parameters.Select(p => p.Name).ToArray());
        Assert.AreEqual(typeof(bool).MakeByRefType(), parameters[0].ParameterType);
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void ThePrisonerLine_IsStillRegisteredWithThatCondition_InInstalledEngine()
    {
        var register = AccessTools.Method(typeof(LordDefectionCampaignBehavior), "AddLordDefectionPersuasionOptions");
        Assert.IsNotNull(register, "AddLordDefectionPersuasionOptions is gone: the line's registration moved.");

        var il = PatchProcessor.GetOriginalInstructions(register!).ToList();
        var lineAt = il.FindIndex(ci => ci.opcode == OpCodes.Ldstr && (string)ci.operand == "player_prisoner_talk");
        Assert.IsTrue(lineAt >= 0, "the 'player_prisoner_talk' line id is no longer registered here.");

        // AddPlayerLine(id, input, output, text, condition, consequence): the condition delegate is built between the line id and the next line's id.
        var nextLine = il.FindIndex(lineAt + 1, ci => ci.opcode == OpCodes.Ldstr && ci.operand is string s && s.StartsWith("player_prisoner_talk_pre_barter", StringComparison.Ordinal));
        var window = il.Skip(lineAt).Take(nextLine < 0 ? il.Count - lineAt : nextLine - lineAt);
        Assert.IsTrue(window.Any(ci => ci.opcode == OpCodes.Ldftn && ci.operand is MethodInfo mi && mi.Name == ConditionName),
            "the 'player_prisoner_talk' line no longer uses " + ConditionName + ": the Patch116 veto would never reach the line.");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void PatchCategory_AppliedToTheInstalledEngine_AttachesThisPostfixAndTheJitAccepts()
    {
        const string id = "taom.tests.swornanew.jit";
        var target = Condition();
        var harmony = new Harmony(id);
        try
        {
            PatchCategoryIndex.Build(typeof(SwornAnewModule).Assembly).Apply(harmony, SwornAnewModule.PatchCategory);

            var info = Harmony.GetPatchInfo(target);
            Assert.IsNotNull(info, "applying the category patched nothing: the class is not in it");
            var postfixes = info!.Postfixes.Where(p => p.owner == id).ToList();
            Assert.AreEqual(1, postfixes.Count, "exactly one postfix is attached");
            Assert.AreEqual(typeof(Patch116_PrisonerDefectionLine_Postfix), postfixes[0].PatchMethod.DeclaringType);
            Assert.AreEqual(0, info.Prefixes.Count(p => p.owner == id), "no prefix is attached");
            RuntimeHelpers.PrepareMethod(target.MethodHandle);
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    [DataTestMethod]
    [DataRow("conversation_player_is_asking_to_recruit_enemy_on_condition")]
    [DataRow("conversation_player_is_asking_to_recruit_neutral_on_condition")]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheDiplomacyConditions_StillAskTheDefectionModel_InInstalledEngine(string condition)
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var method = AccessTools.Method(typeof(LordDefectionCampaignBehavior), condition);
        Assert.IsNotNull(method, condition + " did not resolve.");

        Assert.IsTrue(CallsByName(method!, "CanHeroDefectToFaction"),
            condition + " no longer asks DefectionModel.CanHeroDefectToFaction: TaomDefectionModel would be inert for the diplomacy option.");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheModelSlot_StillTakesDefaultDefectionModelAsItsGenericArgument()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        // GameModelDecl.Of<TSlot, TModel> needs TModel : MBGameModel<TSlot>; the engine declares DefectionModel : MBGameModel<DefaultDefectionModel>.
        var baseType = typeof(DefectionModel).BaseType!;
        Assert.AreEqual("MBGameModel`1", baseType.Name);
        Assert.AreEqual(typeof(DefaultDefectionModel), baseType.GetGenericArguments()[0]);

        var decl = new SwornAnewModule().GameModels.Single();
        Assert.AreEqual(typeof(DefaultDefectionModel), decl.SlotType);
    }
}
