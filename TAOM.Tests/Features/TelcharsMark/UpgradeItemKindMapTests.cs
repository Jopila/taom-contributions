using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TAOM.Adapters;
using TAOM.Features.TelcharsMark;

namespace TAOM.Tests.Features.TelcharsMark;

/// <summary>Pure mappings over engine enums. Enum values are compile-time constants, so no game is needed.</summary>
[TestClass]
public class UpgradeItemKindMapTests
{
    // Every engine item type is decided here. A value the engine adds later fails
    // EveryItemType_IsDecidedHere until someone chooses its kind (see docs/features/telchars-mark.md How-To).
    private static readonly Dictionary<ItemObject.ItemTypeEnum, UpgradeItemKind> Expected =
        new Dictionary<ItemObject.ItemTypeEnum, UpgradeItemKind>
        {
            [ItemObject.ItemTypeEnum.Invalid] = UpgradeItemKind.None,
            [ItemObject.ItemTypeEnum.Horse] = UpgradeItemKind.Horse,
            [ItemObject.ItemTypeEnum.OneHandedWeapon] = UpgradeItemKind.Weapon,
            [ItemObject.ItemTypeEnum.TwoHandedWeapon] = UpgradeItemKind.Weapon,
            [ItemObject.ItemTypeEnum.Polearm] = UpgradeItemKind.Weapon,
            [ItemObject.ItemTypeEnum.Arrows] = UpgradeItemKind.Ammo,
            [ItemObject.ItemTypeEnum.Bolts] = UpgradeItemKind.Ammo,
            [ItemObject.ItemTypeEnum.SlingStones] = UpgradeItemKind.Ammo,
            [ItemObject.ItemTypeEnum.Shield] = UpgradeItemKind.Shield,
            [ItemObject.ItemTypeEnum.Bow] = UpgradeItemKind.Weapon,
            [ItemObject.ItemTypeEnum.Crossbow] = UpgradeItemKind.Weapon,
            [ItemObject.ItemTypeEnum.Sling] = UpgradeItemKind.Weapon,
            [ItemObject.ItemTypeEnum.Thrown] = UpgradeItemKind.Weapon,
            [ItemObject.ItemTypeEnum.Goods] = UpgradeItemKind.None,
            [ItemObject.ItemTypeEnum.HeadArmor] = UpgradeItemKind.Head,
            [ItemObject.ItemTypeEnum.BodyArmor] = UpgradeItemKind.Body,
            [ItemObject.ItemTypeEnum.LegArmor] = UpgradeItemKind.Leg,
            [ItemObject.ItemTypeEnum.HandArmor] = UpgradeItemKind.Gloves,
            [ItemObject.ItemTypeEnum.Pistol] = UpgradeItemKind.Weapon,
            [ItemObject.ItemTypeEnum.Musket] = UpgradeItemKind.Weapon,
            [ItemObject.ItemTypeEnum.Bullets] = UpgradeItemKind.Ammo,
            [ItemObject.ItemTypeEnum.Animal] = UpgradeItemKind.None,
            [ItemObject.ItemTypeEnum.Book] = UpgradeItemKind.None,
            [ItemObject.ItemTypeEnum.ChestArmor] = UpgradeItemKind.None,
            [ItemObject.ItemTypeEnum.Cape] = UpgradeItemKind.Cape,
            [ItemObject.ItemTypeEnum.HorseHarness] = UpgradeItemKind.Harness,
            [ItemObject.ItemTypeEnum.Banner] = UpgradeItemKind.None,
        };

    [TestMethod]
    public void EveryItemType_IsDecidedHere()
    {
        var all = Enum.GetValues(typeof(ItemObject.ItemTypeEnum)).Cast<ItemObject.ItemTypeEnum>().ToList();
        var undecided = all.Where(t => !Expected.ContainsKey(t)).ToList();
        Assert.AreEqual(0, undecided.Count,
            "Item types with no decided UpgradeItemKind: " + string.Join(", ", undecided));
    }

    [TestMethod]
    public void EveryItemType_MapsToItsDecidedKind()
    {
        foreach (var pair in Expected)
            Assert.AreEqual(pair.Value, UpgradeItemKindMap.FromItemType(pair.Key), pair.Key.ToString());
    }

    [TestMethod]
    public void EveryEquipmentKind_IsReachableFromSomeItemType()
    {
        var reached = new HashSet<UpgradeItemKind>(Expected.Values);
        foreach (UpgradeItemKind kind in Enum.GetValues(typeof(UpgradeItemKind)))
            Assert.IsTrue(reached.Contains(kind), kind + " has no item type mapping to it");
    }

    [TestMethod]
    public void ModeFromEngine_EveryEngineEquipmentMode_MapsToItsTaomMode()
    {
        Assert.AreEqual(InventoryEquipmentMode.Civilian, UpgradeItemKindMap.ModeFromEngine((int)SPInventoryVM.EquipmentModes.Civilian));
        Assert.AreEqual(InventoryEquipmentMode.Battle, UpgradeItemKindMap.ModeFromEngine((int)SPInventoryVM.EquipmentModes.Battle));
        Assert.AreEqual(InventoryEquipmentMode.Stealth, UpgradeItemKindMap.ModeFromEngine((int)SPInventoryVM.EquipmentModes.Stealth));
        Assert.AreEqual(3, Enum.GetValues(typeof(SPInventoryVM.EquipmentModes)).Length,
            "the engine added an equipment mode: decide its InventoryEquipmentMode");
    }
}
