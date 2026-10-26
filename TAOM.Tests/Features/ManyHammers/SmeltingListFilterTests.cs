using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Smelting;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TAOM.Features.ManyHammers.Hooks;

namespace TAOM.Tests.Features.ManyHammers;

/// <summary>
/// The in-place list filter against real smithy view models, built bare as InventoryPanelRefreshTests does. A bare row's
/// <c>IsLocked</c> setter would call a null callback, so the test writes the private fields the constructor would set.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SmeltingListFilterTests
{
    private static T Bare<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    private static void SetField(object target, string name, object? value) =>
        typeof(SmeltingItemVM).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private static SmeltingItemVM Row(ItemObject item, bool locked, List<SmeltingItemVM>? selectedLog = null)
    {
        var row = Bare<SmeltingItemVM>();
        typeof(SmeltingItemVM).GetProperty("EquipmentElement")!.SetValue(row, new EquipmentElement(item));
        SetField(row, "_isLocked", locked);
        SetField(row, "_onSelection", new Action<SmeltingItemVM>(r => selectedLog?.Add(r)));
        return row;
    }

    private static SmeltingVM Vm(IEnumerable<SmeltingItemVM> rows, SmeltingItemVM? selected)
    {
        var vm = Bare<SmeltingVM>();
        vm.SmeltableItemList = new MBBindingList<SmeltingItemVM>();
        foreach (var row in rows) vm.SmeltableItemList.Add(row);
        vm.CurrentSelectedItem = selected!;
        return vm;
    }

    [TestMethod]
    public void EveryRowLocked_AndAnUnlockedSelectionNotInTheList_EmptiesTheListAndClearsTheSelection()
    {
        // The review's HIGH case: the last unlocked weapon was smelted, vanilla kept its gone row selected.
        var gone = Row(new ItemObject(), locked: false);
        gone.IsSelected = true;
        var vm = Vm(new[] { Row(new ItemObject(), true), Row(new ItemObject(), true) }, gone);

        SmeltingListFilter.Apply(vm);

        Assert.AreEqual(0, vm.SmeltableItemList.Count);
        Assert.IsNull(vm.CurrentSelectedItem);
        Assert.IsFalse(gone.IsSelected);
    }

    [TestMethod]
    public void ALockedSelection_MovesToTheUnlockedRowOfTheSameItem()
    {
        var axe = new ItemObject();
        var sword = new ItemObject();
        var selectedLog = new List<SmeltingItemVM>();
        var lockedAxe = Row(axe, locked: true);
        var swordRow = Row(sword, false, selectedLog);
        var axeRow = Row(axe, false, selectedLog);
        var vm = Vm(new[] { lockedAxe, swordRow, axeRow }, lockedAxe);

        SmeltingListFilter.Apply(vm);

        CollectionAssert.AreEqual(new[] { swordRow, axeRow }, vm.SmeltableItemList.ToArray());
        Assert.AreEqual(1, selectedLog.Count);
        Assert.AreSame(axeRow, selectedLog[0], "the reselect goes to the unlocked row of the same weapon, not the first row");
    }

    [TestMethod]
    public void AnUnlockedSelection_StaysWhileLockedRowsLeave()
    {
        var keep = Row(new ItemObject(), false);
        var vm = Vm(new[] { Row(new ItemObject(), true), keep }, keep);

        SmeltingListFilter.Apply(vm);

        CollectionAssert.AreEqual(new[] { keep }, vm.SmeltableItemList.ToArray());
        Assert.AreSame(keep, vm.CurrentSelectedItem);
    }
}
