using System;
using TaleWorlds.Core;
using TAOM.Features.TelcharsMark;

namespace TAOM.Adapters;

/// <summary>
/// Reads the plain numbers the scorer compares from one <see cref="EquipmentElement"/>. Every figure
/// comes from the modifier-aware getters (adapters.md "Modifier-Preserving Overloads"), so a "Sharp" or
/// "Damaged" prefix changes the verdict exactly as it changes the tooltip. Empty element: null.
/// </summary>
internal static class UpgradeStatsReader
{
    public static ItemUpgradeStats? Read(EquipmentElement element)
    {
        var item = element.Item;
        if (item == null) return null;

        var kind = UpgradeItemKindMap.FromItemType(item.ItemType);
        switch (kind)
        {
            case UpgradeItemKind.None:
                return ItemUpgradeStats.Empty;
            case UpgradeItemKind.Head:
            case UpgradeItemKind.Body:
            case UpgradeItemKind.Leg:
            case UpgradeItemKind.Gloves:
            case UpgradeItemKind.Cape:
                return new ItemUpgradeStats(kind,
                    headArmor: element.GetModifiedHeadArmor(),
                    bodyArmor: element.GetModifiedBodyArmor(),
                    legArmor: element.GetModifiedLegArmor(),
                    armArmor: element.GetModifiedArmArmor());
            case UpgradeItemKind.Harness:
                // Vanilla matches a harness to a mount by ArmorComponent.FamilyType == Monster.FamilyType
                // (SPInventoryVM.cs:4112, v1.5.5); both are ints.
                return new ItemUpgradeStats(kind,
                    mountBodyArmor: element.GetModifiedMountBodyArmor(),
                    mountFamily: item.ArmorComponent?.FamilyType);
            case UpgradeItemKind.Horse:
                return ReadHorse(element, item);
            default:
                return ReadWeapon(element, item, kind);
        }
    }

    private static ItemUpgradeStats ReadHorse(EquipmentElement element, ItemObject item)
    {
        // The engine's mount getters dereference Item.HorseComponent with no null check (EquipmentElement.cs:377-379,
        // :397-399, :417-419, :437-438), and GetModifiedMountHitPoints reads HorseComponent.HitPoints, which is
        // Monster.HitPoints (HorseComponent.cs:35). A Horse-typed item missing either would throw out of the whole
        // rescore, so it gets no verdict instead.
        var monster = item.HorseComponent?.Monster;
        if (monster == null) return ItemUpgradeStats.Empty;

        // Compared without a harness on both sides, so the number is the animal's own.
        var noHarness = default(EquipmentElement);
        return new ItemUpgradeStats(UpgradeItemKind.Horse,
            mountSpeed: element.GetModifiedMountSpeed(in noHarness),
            mountCharge: element.GetModifiedMountCharge(in noHarness),
            mountManeuver: element.GetModifiedMountManeuver(in noHarness),
            mountHitPoints: element.GetModifiedMountHitPoints(),
            mountFamily: monster.FamilyType);
    }

    private static ItemUpgradeStats ReadWeapon(EquipmentElement element, ItemObject item, UpgradeItemKind kind)
    {
        var usages = item.Weapons?.Count ?? 0;
        var maxDamage = 0;
        for (var i = 0; i < usages; i++)
        {
            maxDamage = Math.Max(maxDamage, element.GetModifiedSwingDamageForUsage(i));
            maxDamage = Math.Max(maxDamage, element.GetModifiedThrustDamageForUsage(i));
            maxDamage = Math.Max(maxDamage, element.GetModifiedMissileDamageForUsage(i));
        }

        var stack = usages > 0 ? element.GetModifiedStackCountForUsage(0) : 0;
        var shieldHitPoints = kind == UpgradeItemKind.Shield && usages > 0
            ? element.GetModifiedMaximumHitPointsForUsage(0)
            : 0;
        var shieldArmor = kind == UpgradeItemKind.Shield ? element.GetModifiedBodyArmor() : 0;

        return new ItemUpgradeStats(kind,
            weaponTypeId: (int)item.ItemType,
            bodyArmor: shieldArmor,
            maxDamage: maxDamage,
            stackCount: stack,
            shieldHitPoints: shieldHitPoints);
    }
}
