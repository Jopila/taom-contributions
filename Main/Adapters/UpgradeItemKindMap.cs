using TaleWorlds.Core;
using TAOM.Features.TelcharsMark;

namespace TAOM.Adapters;

/// <summary>
/// Maps the engine's item type to the feature's slot kind. Pure over the enum, so a test pins every
/// value (<c>UpgradeItemKindMapTests</c>): a new engine value must be decided there, not default silently.
/// </summary>
internal static class UpgradeItemKindMap
{
    public static UpgradeItemKind FromItemType(ItemObject.ItemTypeEnum type)
    {
        switch (type)
        {
            case ItemObject.ItemTypeEnum.HeadArmor: return UpgradeItemKind.Head;
            case ItemObject.ItemTypeEnum.BodyArmor: return UpgradeItemKind.Body;
            case ItemObject.ItemTypeEnum.LegArmor: return UpgradeItemKind.Leg;
            case ItemObject.ItemTypeEnum.HandArmor: return UpgradeItemKind.Gloves;
            case ItemObject.ItemTypeEnum.Cape: return UpgradeItemKind.Cape;
            case ItemObject.ItemTypeEnum.HorseHarness: return UpgradeItemKind.Harness;
            case ItemObject.ItemTypeEnum.Horse: return UpgradeItemKind.Horse;
            case ItemObject.ItemTypeEnum.Shield: return UpgradeItemKind.Shield;
            case ItemObject.ItemTypeEnum.Arrows:
            case ItemObject.ItemTypeEnum.Bolts:
            case ItemObject.ItemTypeEnum.SlingStones:
            case ItemObject.ItemTypeEnum.Bullets:
                return UpgradeItemKind.Ammo;
            case ItemObject.ItemTypeEnum.OneHandedWeapon:
            case ItemObject.ItemTypeEnum.TwoHandedWeapon:
            case ItemObject.ItemTypeEnum.Polearm:
            case ItemObject.ItemTypeEnum.Bow:
            case ItemObject.ItemTypeEnum.Crossbow:
            case ItemObject.ItemTypeEnum.Sling:
            case ItemObject.ItemTypeEnum.Thrown:
            case ItemObject.ItemTypeEnum.Pistol:
            case ItemObject.ItemTypeEnum.Musket:
                return UpgradeItemKind.Weapon;
            // Invalid, Goods, Animal, Book, Banner, and ChestArmor (referenced by vanilla filters,
            // never an equipment slot of its own): no verdict.
            default: return UpgradeItemKind.None;
        }
    }

    /// <summary>
    /// SPInventoryVM.EquipmentMode is (int)SPInventoryVM.EquipmentModes {Civilian, Battle, Stealth}
    /// (v1.5.5 SPInventoryVM.cs:26-31). Pinned per value by <c>UpgradeItemKindMapTests</c>.
    /// </summary>
    public static InventoryEquipmentMode ModeFromEngine(int engineMode)
    {
        switch (engineMode)
        {
            case 0: return InventoryEquipmentMode.Civilian;
            case 2: return InventoryEquipmentMode.Stealth;
            default: return InventoryEquipmentMode.Battle;
        }
    }
}
