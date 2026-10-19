using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.TelcharsMark;

/// <summary>
/// The Patch109 patch targets bind through HarmonyPatchBindingTests. This pins the one reflection site outside
/// them: the open delegate InventoryUpgradeScreen binds to UpdateFilteredStatusOfItem to re-run the filters
/// without the public ExecuteFilter* calls.
/// </summary>
[TestClass]
public class UpgradeScreenAdapterBindingTests
{
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void UpdateFilteredStatusOfItem_BindsAsAnOpenDelegate()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        Assert.IsTrue(InventoryUpgradeScreen.IsFilterDelegateBound,
            "SPInventoryVM.UpdateFilteredStatusOfItem(SPItemVM) did not bind: ReapplyFilters would do nothing.");
    }
}
