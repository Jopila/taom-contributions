using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Party.Hooks;
using TAOM.Tests.Migration;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;

namespace TAOM.Tests.Features.ShadowfaxScreens.Party;

/// <summary>
/// Drift guards for the Party Fast Open patches against the installed engine. Harmony binds prefix, postfix and
/// finalizer parameters by name, so each non-special name must match a target parameter of the same type. The
/// CharacterImageIdentifier test pins the premise that makes sharing one CharacterCode between rows safe.
/// </summary>
[TestClass]
public class PartyFastOpenBindingTests
{
    private static bool _gameLoaded;

    private static readonly Type[] Patches =
    {
        typeof(PartyVM_Ctor_ShadowfaxParty_Patch), typeof(PartyVM_OnFinalize_ShadowfaxParty_Patch),
        typeof(PartyVM_InitializeTroopLists_ShadowfaxParty_Patch), typeof(PartyVM_RefreshTroopsUpgradeable_ShadowfaxParty_Patch),
        typeof(PartyVM_RefreshValues_ShadowfaxParty_Patch), typeof(PartyVM_TransferAllTroops_ShadowfaxParty_Patch),
        typeof(PartyCharacterVM_GetCharacterCode_ShadowfaxParty_Patch),
        typeof(PartyCharacterVM_GetNumOfCategoryItemPartyHas_ShadowfaxParty_Patch),
    };

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static MethodBase? TargetOf(Type patch)
    {
        var info = HarmonyMethod.Merge(patch.GetCustomAttributes<HarmonyPatch>().Select(a => a.info).ToList());
        return info.methodType == MethodType.Constructor
            ? AccessTools.Constructor(info.declaringType, info.argumentTypes)
            : AccessTools.Method(info.declaringType, info.methodName, info.argumentTypes);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EveryPatch_ResolvesItsTarget_AndBindsOnlyRealParameterNames()
    {
        RequireGame();

        foreach (var patch in Patches)
        {
            var target = TargetOf(patch);
            Assert.IsNotNull(target, patch.Name + " names no resolvable target");
            var targetParams = target!.GetParameters().ToDictionary(p => p.Name!, p => p.ParameterType);

            foreach (var method in patch.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                foreach (var p in method.GetParameters().Where(p => !p.Name!.StartsWith("__", StringComparison.Ordinal)))
                {
                    Assert.IsTrue(targetParams.TryGetValue(p.Name!, out var type), $"{patch.Name}.{method.Name}: no target parameter '{p.Name}'");
                    Assert.AreEqual(type, p.ParameterType, $"{patch.Name}.{method.Name}: parameter '{p.Name}' type");
                }
            }
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EveryPatch_CarriesThePartyCategory_AndTheModuleDeclaresItOnce()
    {
        RequireGame();

        foreach (var patch in Patches)
        {
            var categories = patch.GetCustomAttributes<HarmonyPatchCategory>().Select(c => c.info.category).ToList();
            CollectionAssert.AreEqual(new[] { "PatchXXX_ShadowfaxParty" }, categories, patch.Name);
        }

        Assert.AreEqual(1, new ShadowfaxScreensModule().PatchCategories.Count(d => d.Category == "PatchXXX_ShadowfaxParty"));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void GetCharacterCode_IsThePrivateInstanceMethod_WithTheNamesTheCachePatchBinds()
    {
        RequireGame();

        var method = typeof(PartyCharacterVM).GetMethod("GetCharacterCode", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(method, "PartyCharacterVM.GetCharacterCode is gone or no longer a private instance method");
        Assert.AreEqual(typeof(CharacterCode), method!.ReturnType);
        CollectionAssert.AreEqual(new[] { "character", "type", "side" }, method.GetParameters().Select(p => p.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { typeof(CharacterObject), typeof(PartyScreenLogic.TroopType), typeof(PartyScreenLogic.PartyRosterSide) },
            method.GetParameters().Select(p => p.ParameterType).ToArray());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void GetNumOfCategoryItemPartyHas_IsPublicInstance_ReturningInt()
    {
        RequireGame();

        var method = typeof(PartyCharacterVM).GetMethod(nameof(PartyCharacterVM.GetNumOfCategoryItemPartyHas));

        Assert.IsNotNull(method);
        Assert.AreEqual(typeof(int), method!.ReturnType);
        CollectionAssert.AreEqual(new[] { "items", "itemCategory" }, method.GetParameters().Select(p => p.Name).ToArray());
        CollectionAssert.AreEqual(new[] { typeof(ItemRoster), typeof(ItemCategory) },
            method.GetParameters().Select(p => p.ParameterType).ToArray());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PartyVm_TargetsExist_WithTheShapesTheBracketsAssume()
    {
        RequireGame();
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.IsNotNull(typeof(PartyVM).GetConstructor(new[] { typeof(PartyScreenLogic) }));
        foreach (var name in new[] { "InitializeTroopLists", "RefreshTroopsUpgradeable" })
        {
            var m = typeof(PartyVM).GetMethod(name, all, null, Type.EmptyTypes, null);
            Assert.IsNotNull(m, name);
            Assert.AreEqual(typeof(void), m!.ReturnType, name);
        }
        Assert.IsNotNull(typeof(PartyVM).GetMethod("TransferAllTroops", all, null, new[] { typeof(PartyScreenLogic.PartyCommand) }, null));
        Assert.IsNotNull(typeof(PartyVM).GetMethod(nameof(PartyVM.OnFinalize), all, null, Type.EmptyTypes, null));
        Assert.IsNotNull(typeof(PartyVM).GetMethod(nameof(PartyVM.RefreshValues), all, null, Type.EmptyTypes, null));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CharacterImageIdentifierCtor_ReadsOnlyCharacterCodeCode()
    {
        RequireGame();

        var ctor = typeof(CharacterImageIdentifier).GetConstructor(new[] { typeof(CharacterCode) });
        Assert.IsNotNull(ctor);

        var touched = new List<string>();
        foreach (var instruction in PatchProcessor.GetOriginalInstructions(ctor!))
        {
            if (instruction.operand is MethodBase called && called.DeclaringType == typeof(CharacterCode))
                touched.Add(called.Name);
            if (instruction.operand is FieldInfo field && field.DeclaringType == typeof(CharacterCode))
                touched.Add(field.Name);
        }

        CollectionAssert.AreEqual(new[] { "get_Code" }, touched.Distinct().ToArray(),
            "CharacterImageIdentifier now reads more than CharacterCode.Code: re-check that a shared CharacterCode still renders per row");
    }
}
