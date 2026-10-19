using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TAOM.Features.TelcharsMark.Hooks;
using TAOM.Features.TelcharsMark.UI;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.TelcharsMark;

/// <summary>
/// A Harmony patch needs its target attribute, its category attribute and the SubModule apply line, or it is
/// dead code with no error, warning or log line (lessons/harmony-il.md: Patch39 shipped missing one). These pin
/// all three for every Patch109 class, plus the bindings nothing else checks.
/// </summary>
[TestClass]
public class Patch109WiringTests
{
    private const string Category = "Patch109_TelcharsMark";

    private static readonly Type[] PatchClasses =
    {
        typeof(Patch109_SPInventoryVMRefreshInformation),
        typeof(Patch109_SPInventoryVMAfterTransfer),
        typeof(Patch109_SPInventoryVMUpdateFiltered),
    };

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EveryPatchClass_HasTargetAndCategory()
    {
        foreach (var patch in PatchClasses)
        {
            Assert.AreEqual(1, patch.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length,
                patch.Name + " lost its [HarmonyPatch] target attribute.");
            var categories = patch.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
                .Cast<HarmonyPatchCategory>()
                .Select(c => c.info.category)
                .ToList();
            CollectionAssert.Contains(categories, Category,
                patch.Name + " lost its [HarmonyPatchCategory]: SubModule's apply call would skip it.");
        }
    }

    [TestMethod]
    public void SubModule_AppliesTheCategory_AndIoCRegistersTheFeature()
    {
        var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        StringAssert.Contains(subModule, "TryPatchCategory(\"" + Category + "\")",
            "SubModule.cs no longer applies " + Category + ": every Telchar's Mark postfix is dead code.");

        var ioc = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);
        StringAssert.Contains(ioc, "TelcharsMarkIoC.RegisterTelcharsMarkFeature(container)",
            "IoC.cs no longer registers Telchar's Mark: the screen mixin cannot resolve its service.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void UpdateFilteredStatusOfItem_ParameterIsNamedItem()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        // Harmony injects the postfix's `item` argument by name; a rename would bind nothing.
        var target = typeof(SPInventoryVM).GetMethod("UpdateFilteredStatusOfItem",
            BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(SPItemVM) }, null);
        Assert.IsNotNull(target, "SPInventoryVM.UpdateFilteredStatusOfItem(SPItemVM) is gone.");
        Assert.AreEqual("item", target!.GetParameters()[0].Name);
    }

    [TestMethod]
    public void ToggleButton_IsAppendedAsTheLastChild()
    {
        // InsertType.Child inserts before the child at Index, and Index defaults to 0, which put the box under
        // the tab-bar art. int.MaxValue appends it.
        Assert.AreEqual(int.MaxValue, new UpgradeToggleButtonPrefab().Index);
    }
}
