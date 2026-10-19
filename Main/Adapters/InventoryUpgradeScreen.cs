using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Library;
using TAOM.Features.TelcharsMark;

namespace TAOM.Adapters;

/// <summary>
/// Telchar's Mark's view of ONE open inventory screen. The screen's own mixin builds it at the end of the
/// SPInventoryVM constructor, so it never borrows another feature's captured VM. Rows come from a lookup
/// the caller supplies (the row mixin's registry), so this adapter names no feature UI type.
/// </summary>
internal sealed class InventoryUpgradeScreen : IUpgradeScreen
{
    // SPInventoryVM.UpdateFilteredStatusOfItem(SPItemVM) is private (v1.5.5 SPInventoryVM.cs:5133). It is the one
    // method that writes IsFiltered, so re-running it is the only way to refresh the hide without the public
    // ExecuteFilter* calls, which raise InventoryFilterChangedEvent. Bound once as an open instance delegate.
    // Null after engine drift: ReapplyFilters then does nothing and the next vanilla filter pass still runs.
    private static readonly Action<SPInventoryVM, SPItemVM>? UpdateFilteredStatus = BindUpdateFilteredStatus();

    private readonly SPInventoryVM _vm;
    private readonly Func<SPItemVM, IUpgradeRow?> _rowFor;

    public InventoryUpgradeScreen(SPInventoryVM vm, Func<SPItemVM, IUpgradeRow?> rowFor)
    {
        _vm = vm;
        _rowFor = rowFor;
    }

    internal static bool IsFilterDelegateBound => UpdateFilteredStatus != null;

    public IReadOnlyList<IUpgradeRow> GetRows()
    {
        var right = _vm.RightItemListVM;
        var left = _vm.LeftItemListVM;
        var rows = new List<IUpgradeRow>((right?.Count ?? 0) + (left?.Count ?? 0));
        AddRows(rows, right);
        AddRows(rows, left);
        return rows;
    }

    private void AddRows(List<IUpgradeRow> rows, MBBindingList<SPItemVM>? list)
    {
        if (list == null) return;
        foreach (var vm in list)
        {
            if (vm == null) continue;
            var row = _rowFor(vm);
            if (row != null) rows.Add(row);
        }
    }

    public InventoryLoadout GetLoadout()
    {
        // The slot VMs follow the shown outfit and character: the EquipmentMode setter and a character switch
        // rebuild them before they call RefreshInformationValues (SPInventoryVM.cs:1642-1647, :4314-4315), and
        // AfterTransfer refreshes the shown set's slot before its postfix runs (:4189-4196).
        var loadout = new InventoryLoadout
        {
            EquipmentMode = UpgradeItemKindMap.ModeFromEngine(_vm.EquipmentMode),
            Head = Read(_vm.CharacterHelmSlot),
            Body = Read(_vm.CharacterTorsoSlot),
            Leg = Read(_vm.CharacterBootSlot),
            Gloves = Read(_vm.CharacterGloveSlot),
            Cape = Read(_vm.CharacterCloakSlot),
            Horse = Read(_vm.CharacterMountSlot),
            Harness = Read(_vm.CharacterMountArmorSlot),
        };
        loadout.Weapons[0] = Read(_vm.CharacterWeapon1Slot);
        loadout.Weapons[1] = Read(_vm.CharacterWeapon2Slot);
        loadout.Weapons[2] = Read(_vm.CharacterWeapon3Slot);
        loadout.Weapons[3] = Read(_vm.CharacterWeapon4Slot);
        return loadout;
    }

    private static ItemUpgradeStats? Read(SPItemVM? slot) =>
        slot == null ? null : UpgradeStatsReader.Read(slot.ItemRosterElement.EquipmentElement);

    public void ReapplyFilters()
    {
        var update = UpdateFilteredStatus;
        if (update == null) return;
        Reapply(update, _vm.RightItemListVM);
        Reapply(update, _vm.LeftItemListVM);
    }

    private void Reapply(Action<SPInventoryVM, SPItemVM> update, MBBindingList<SPItemVM>? list)
    {
        if (list == null) return;
        foreach (var item in list)
        {
            if (item != null) update(_vm, item);
        }
    }

    private static Action<SPInventoryVM, SPItemVM>? BindUpdateFilteredStatus()
    {
        try
        {
            var method = AccessTools.Method(typeof(SPInventoryVM), "UpdateFilteredStatusOfItem", new[] { typeof(SPItemVM) });
            return method == null ? null : AccessTools.MethodDelegate<Action<SPInventoryVM, SPItemVM>>(method);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
