using System.Linq;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Smelting;

namespace TAOM.Features.ManyHammers.Hooks;

/// <summary>
/// Takes the locked rows out of the smelting list in place and fixes the selection, through the view model's public surface
/// only. It never rebuilds the list: it runs inside <c>RefreshList</c>'s own postfix, so a rebuild would re-enter it.
/// </summary>
internal static class SmeltingListFilter
{
    internal static void Apply(SmeltingVM vm)
    {
        var list = vm.SmeltableItemList;
        if (list == null) return;

        var rows = list.ToList();
        var locks = rows.Select(row => row.IsLocked).ToList();
        var selected = vm.CurrentSelectedItem;
        var sameItem = rows.Select(row => selected != null && row.EquipmentElement.Item == selected.EquipmentElement.Item).ToList();
        var result = BulkCraftRules.SelectionAfterHidingLocked(locks, selected != null, selected?.IsLocked == true, sameItem);

        for (var i = rows.Count - 1; i >= 0; i--)
        {
            if (locks[i]) list.RemoveAt(i);
        }

        switch (result.Selection)
        {
            case SelectionChange.Reselect:
                rows[result.ReselectIndex].ExecuteSelection();
                break;
            case SelectionChange.Clear:
                Clear(vm);
                break;
        }
    }

    // As vanilla does for an empty list (SmeltingVM.RefreshList): no selection, nothing highlighted. Only the RefreshList
    // postfix gets here, and every RefreshList caller refreshes the smithy afterwards (SmeltSelectedItems, the switch to
    // the Smelting tab, BannerlordCoop's refresh handler).
    private static void Clear(SmeltingVM vm)
    {
        var old = vm.CurrentSelectedItem;
        if (old != null) old.IsSelected = false;
        vm.CurrentSelectedItem = null!;
    }
}
