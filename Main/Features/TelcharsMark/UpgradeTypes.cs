namespace TAOM.Features.TelcharsMark;

public enum UpgradeVerdict
{
    None,
    Better,
    Sidegrade,
}

public enum UpgradeItemKind
{
    None,
    Head,
    Body,
    Leg,
    Gloves,
    Cape,
    Harness,
    Horse,
    Weapon,
    Ammo,
    Shield,
}

/// <summary>The outfit the inventory shows. Values mirror v1.5.5 <c>SPInventoryVM.EquipmentModes</c>.</summary>
public enum InventoryEquipmentMode
{
    Civilian = 0,
    Battle = 1,
    Stealth = 2,
}

/// <summary>Plain numbers for one item, read by the adapter. No TaleWorlds types (ADR-007).</summary>
public sealed class ItemUpgradeStats
{
    public static readonly ItemUpgradeStats Empty = new ItemUpgradeStats(UpgradeItemKind.None);

    public ItemUpgradeStats(
        UpgradeItemKind kind,
        int weaponTypeId = 0,
        int headArmor = 0,
        int bodyArmor = 0,
        int legArmor = 0,
        int armArmor = 0,
        int mountBodyArmor = 0,
        int maxDamage = 0,
        int stackCount = 0,
        int shieldHitPoints = 0,
        int mountSpeed = 0,
        int mountCharge = 0,
        int mountManeuver = 0,
        int mountHitPoints = 0,
        int? mountFamily = null)
    {
        Kind = kind;
        WeaponTypeId = weaponTypeId;
        HeadArmor = headArmor;
        BodyArmor = bodyArmor;
        LegArmor = legArmor;
        ArmArmor = armArmor;
        MountBodyArmor = mountBodyArmor;
        MaxDamage = maxDamage;
        StackCount = stackCount;
        ShieldHitPoints = shieldHitPoints;
        MountSpeed = mountSpeed;
        MountCharge = mountCharge;
        MountManeuver = mountManeuver;
        MountHitPoints = mountHitPoints;
        MountFamily = mountFamily;
    }

    public UpgradeItemKind Kind { get; }

    /// <summary>The engine item type as an int; weapons, ammo and shields match on it.</summary>
    public int WeaponTypeId { get; }
    public int HeadArmor { get; }
    public int BodyArmor { get; }
    public int LegArmor { get; }
    public int ArmArmor { get; }
    public int MountBodyArmor { get; }
    public int MaxDamage { get; }
    public int StackCount { get; }
    public int ShieldHitPoints { get; }
    public int MountSpeed { get; }
    public int MountCharge { get; }
    public int MountManeuver { get; }
    public int MountHitPoints { get; }

    /// <summary>Monster family of a horse, or the family a harness fits; null when unknown.</summary>
    public int? MountFamily { get; }
}

/// <summary>What the shown character has equipped, per slot; null means the slot is empty.</summary>
public sealed class InventoryLoadout
{
    public const int WeaponSlotCount = 4;

    public ItemUpgradeStats? Head { get; set; }
    public ItemUpgradeStats? Body { get; set; }
    public ItemUpgradeStats? Leg { get; set; }
    public ItemUpgradeStats? Gloves { get; set; }
    public ItemUpgradeStats? Cape { get; set; }
    public ItemUpgradeStats? Horse { get; set; }
    public ItemUpgradeStats? Harness { get; set; }

    /// <summary>The four weapon slots in slot order; an entry is null when that slot is empty.</summary>
    public ItemUpgradeStats?[] Weapons { get; } = new ItemUpgradeStats?[WeaponSlotCount];

    public InventoryEquipmentMode EquipmentMode { get; set; } = InventoryEquipmentMode.Battle;
}

public sealed class ItemCandidate
{
    public ItemCandidate(ItemUpgradeStats stats, bool isEquipable, bool canCharacterUse,
        bool isCivilian, bool isStealth)
    {
        Stats = stats;
        IsEquipable = isEquipable;
        CanCharacterUse = canCharacterUse;
        IsCivilian = isCivilian;
        IsStealth = isStealth;
    }

    public ItemUpgradeStats Stats { get; }
    public bool IsEquipable { get; }
    public bool CanCharacterUse { get; }
    public bool IsCivilian { get; }
    public bool IsStealth { get; }
}
