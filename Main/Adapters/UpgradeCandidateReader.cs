using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TAOM.Features.TelcharsMark;

namespace TAOM.Adapters;

/// <summary>
/// Reads the scorer's view of one inventory row. Read fresh on every rescore: vanilla rewrites
/// CanCharacterUseItem on every row when the shown character or outfit changes (SPInventoryVM.cs:4468-4478).
/// </summary>
internal static class UpgradeCandidateReader
{
    public static ItemCandidate Read(SPItemVM vm)
    {
        var stats = UpgradeStatsReader.Read(vm.ItemRosterElement.EquipmentElement) ?? ItemUpgradeStats.Empty;
        return new ItemCandidate(stats, vm.IsEquipableItem, vm.CanCharacterUseItem, vm.IsCivilianItem, vm.IsStealthItem);
    }
}
