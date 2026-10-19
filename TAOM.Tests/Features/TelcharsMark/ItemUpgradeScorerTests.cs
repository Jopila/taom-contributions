using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TelcharsMark;

namespace TAOM.Tests.Features.TelcharsMark;

[TestClass]
public class ItemUpgradeScorerTests
{
    private const int Sword = 2;
    private const int Spear = 4;
    private const int ArrowType = 5;
    private const int ShieldType = 8;
    private const int Bow = 9;
    private const int Horse1 = 1;
    private const int Horse2 = 2;

    private readonly ItemUpgradeScorer _sut = new ItemUpgradeScorer();

    private static ItemCandidate Cand(ItemUpgradeStats stats, bool equipable = true, bool canUse = true,
        bool civilian = true, bool stealth = true) =>
        new ItemCandidate(stats, equipable, canUse, civilian, stealth);

    private static ItemUpgradeStats Armor(UpgradeItemKind kind, int head = 0, int body = 0, int leg = 0, int arm = 0) =>
        new ItemUpgradeStats(kind, headArmor: head, bodyArmor: body, legArmor: leg, armArmor: arm);

    private static ItemUpgradeStats Weapon(int type, int dmg) =>
        new ItemUpgradeStats(UpgradeItemKind.Weapon, weaponTypeId: type, maxDamage: dmg);

    private static ItemUpgradeStats Ammo(int dmg, int stack) =>
        new ItemUpgradeStats(UpgradeItemKind.Ammo, weaponTypeId: ArrowType, maxDamage: dmg, stackCount: stack);

    private static ItemUpgradeStats Shield(int armor, int hp) =>
        new ItemUpgradeStats(UpgradeItemKind.Shield, weaponTypeId: ShieldType, bodyArmor: armor, shieldHitPoints: hp);

    private static ItemUpgradeStats Horse(int speed, int charge, int man, int hp, int? family = Horse1) =>
        new ItemUpgradeStats(UpgradeItemKind.Horse, mountSpeed: speed, mountCharge: charge,
            mountManeuver: man, mountHitPoints: hp, mountFamily: family);

    private static ItemUpgradeStats Harness(int armor, int? family = Horse1) =>
        new ItemUpgradeStats(UpgradeItemKind.Harness, mountBodyArmor: armor, mountFamily: family);

    /// <summary>A loadout with the given weapon slots, in slot order; missing entries stay empty.</summary>
    private static InventoryLoadout WithWeapons(params ItemUpgradeStats?[] slots)
    {
        var loadout = new InventoryLoadout();
        for (var i = 0; i < slots.Length; i++) loadout.Weapons[i] = slots[i];
        return loadout;
    }

    // ---- gating ----

    [TestMethod]
    public void Score_NotEquipable_None() =>
        Assert.AreEqual(UpgradeVerdict.None,
            _sut.Score(Cand(Armor(UpgradeItemKind.Head, head: 50), equipable: false), new InventoryLoadout()));

    [TestMethod]
    public void Score_CannotUse_None() =>
        Assert.AreEqual(UpgradeVerdict.None,
            _sut.Score(Cand(Armor(UpgradeItemKind.Head, head: 50), canUse: false), new InventoryLoadout()));

    [TestMethod]
    public void Score_KindNone_None() =>
        Assert.AreEqual(UpgradeVerdict.None, _sut.Score(Cand(ItemUpgradeStats.Empty), new InventoryLoadout()));

    [TestMethod]
    public void Score_CivilianModeNonCivilianItem_None() =>
        Assert.AreEqual(UpgradeVerdict.None,
            _sut.Score(Cand(Armor(UpgradeItemKind.Head, head: 50), civilian: false),
                new InventoryLoadout { EquipmentMode = InventoryEquipmentMode.Civilian }));

    [TestMethod]
    public void Score_CivilianModeCivilianItem_Better() =>
        Assert.AreEqual(UpgradeVerdict.Better,
            _sut.Score(Cand(Armor(UpgradeItemKind.Head, head: 50), civilian: true),
                new InventoryLoadout { EquipmentMode = InventoryEquipmentMode.Civilian }));

    [TestMethod]
    public void Score_StealthModeNonStealthItem_None() =>
        Assert.AreEqual(UpgradeVerdict.None,
            _sut.Score(Cand(Armor(UpgradeItemKind.Head, head: 50), stealth: false),
                new InventoryLoadout { EquipmentMode = InventoryEquipmentMode.Stealth }));

    [TestMethod]
    public void Score_StealthModeStealthItem_Better() =>
        Assert.AreEqual(UpgradeVerdict.Better,
            _sut.Score(Cand(Armor(UpgradeItemKind.Head, head: 50), stealth: true),
                new InventoryLoadout { EquipmentMode = InventoryEquipmentMode.Stealth }));

    [TestMethod]
    public void Score_BattleMode_IgnoresCivilianAndStealthFlags() =>
        Assert.AreEqual(UpgradeVerdict.Better,
            _sut.Score(Cand(Armor(UpgradeItemKind.Head, head: 50), civilian: false, stealth: false),
                new InventoryLoadout { EquipmentMode = InventoryEquipmentMode.Battle }));

    // ---- armour ----

    [TestMethod]
    public void Score_EmptyArmourSlot_Better() =>
        Assert.AreEqual(UpgradeVerdict.Better,
            _sut.Score(Cand(Armor(UpgradeItemKind.Body, body: 1)), new InventoryLoadout()));

    [TestMethod]
    public void Score_HeadHigherMain_Better() =>
        Assert.AreEqual(UpgradeVerdict.Better, _sut.Score(Cand(Armor(UpgradeItemKind.Head, head: 20)),
            new InventoryLoadout { Head = Armor(UpgradeItemKind.Head, head: 10) }));

    [TestMethod]
    public void Score_BodyHigherMain_Better() =>
        Assert.AreEqual(UpgradeVerdict.Better, _sut.Score(Cand(Armor(UpgradeItemKind.Body, body: 20)),
            new InventoryLoadout { Body = Armor(UpgradeItemKind.Body, body: 10) }));

    [TestMethod]
    public void Score_LegHigherMain_Better() =>
        Assert.AreEqual(UpgradeVerdict.Better, _sut.Score(Cand(Armor(UpgradeItemKind.Leg, leg: 20)),
            new InventoryLoadout { Leg = Armor(UpgradeItemKind.Leg, leg: 10) }));

    [TestMethod]
    public void Score_GlovesHigherMain_Better() =>
        Assert.AreEqual(UpgradeVerdict.Better, _sut.Score(Cand(Armor(UpgradeItemKind.Gloves, arm: 20)),
            new InventoryLoadout { Gloves = Armor(UpgradeItemKind.Gloves, arm: 10) }));

    [TestMethod]
    public void Score_CapeHigherBodyMain_Better() =>
        Assert.AreEqual(UpgradeVerdict.Better, _sut.Score(Cand(Armor(UpgradeItemKind.Cape, body: 6)),
            new InventoryLoadout { Cape = Armor(UpgradeItemKind.Cape, body: 3) }));

    [TestMethod]
    public void Score_EqualMainHigherTotal_Better() =>
        Assert.AreEqual(UpgradeVerdict.Better,
            _sut.Score(Cand(Armor(UpgradeItemKind.Head, head: 10, body: 1)),
                new InventoryLoadout { Head = Armor(UpgradeItemKind.Head, head: 10) }));

    [TestMethod]
    public void Score_LowerMainHigherTotal_Sidegrade()
    {
        // candidate: 9*34 + 10*42 = 726 ; equipped: 10*34 = 340
        var cand = Armor(UpgradeItemKind.Head, head: 9, body: 10);
        Assert.AreEqual(UpgradeVerdict.Sidegrade,
            _sut.Score(Cand(cand), new InventoryLoadout { Head = Armor(UpgradeItemKind.Head, head: 10) }));
    }

    [TestMethod]
    public void Score_LowerMainLowerTotal_None() =>
        Assert.AreEqual(UpgradeVerdict.None, _sut.Score(Cand(Armor(UpgradeItemKind.Head, head: 9)),
            new InventoryLoadout { Head = Armor(UpgradeItemKind.Head, head: 10) }));

    [TestMethod]
    public void Score_EqualMainEqualTotal_None() =>
        Assert.AreEqual(UpgradeVerdict.None, _sut.Score(Cand(Armor(UpgradeItemKind.Head, head: 10)),
            new InventoryLoadout { Head = Armor(UpgradeItemKind.Head, head: 10) }));

    [TestMethod]
    public void WeightedTotal_FourSlots_IsWeightedSum()
    {
        var stats = Armor(UpgradeItemKind.Head, head: 3, body: 5, leg: 7, arm: 11);
        Assert.AreEqual(3 * 34f + 5 * 42f + 7 * 12f + 11 * 12f, _sut.WeightedTotal(stats), 0.0001f);
    }

    // ---- harness ----

    [TestMethod]
    public void Score_HarnessWithoutHorse_None() =>
        Assert.AreEqual(UpgradeVerdict.None, _sut.Score(Cand(Harness(30)), new InventoryLoadout()));

    [TestMethod]
    public void Score_HarnessFamilyMismatch_None() =>
        Assert.AreEqual(UpgradeVerdict.None, _sut.Score(Cand(Harness(30, Horse2)),
            new InventoryLoadout { Horse = Horse(1, 1, 1, 1, Horse1) }));

    [TestMethod]
    public void Score_HarnessMatchingFamilyEmptySlot_Better() =>
        Assert.AreEqual(UpgradeVerdict.Better, _sut.Score(Cand(Harness(30, Horse1)),
            new InventoryLoadout { Horse = Horse(1, 1, 1, 1, Horse1) }));

    [TestMethod]
    public void Score_HarnessMatchingFamilyHigherArmour_Better() =>
        Assert.AreEqual(UpgradeVerdict.Better, _sut.Score(Cand(Harness(30, Horse1)),
            new InventoryLoadout { Horse = Horse(1, 1, 1, 1, Horse1), Harness = Harness(20, Horse1) }));

    [TestMethod]
    public void Score_HarnessMatchingFamilyEqualOrLowerArmour_None()
    {
        var load = new InventoryLoadout { Horse = Horse(1, 1, 1, 1, Horse1), Harness = Harness(20, Horse1) };
        Assert.AreEqual(UpgradeVerdict.None, _sut.Score(Cand(Harness(20, Horse1)), load));
        Assert.AreEqual(UpgradeVerdict.None, _sut.Score(Cand(Harness(10, Horse1)), load));
    }

    [TestMethod]
    public void Score_HarnessUnknownFamily_IsNotAMismatch() =>
        Assert.AreEqual(UpgradeVerdict.Better, _sut.Score(Cand(Harness(30, null)),
            new InventoryLoadout { Horse = Horse(1, 1, 1, 1, Horse1) }));

    [TestMethod]
    public void Score_HarnessHorseFamilyUnknown_IsNotAMismatch() =>
        Assert.AreEqual(UpgradeVerdict.Better, _sut.Score(Cand(Harness(30, Horse1)),
            new InventoryLoadout { Horse = Horse(1, 1, 1, 1, null) }));

    // ---- horse ----

    [TestMethod]
    public void Score_HorseEmptySlot_Better() =>
        Assert.AreEqual(UpgradeVerdict.Better, _sut.Score(Cand(Horse(1, 1, 1, 1)), new InventoryLoadout()));

    [TestMethod]
    public void Score_HorseScoreIncludesFivePercentHitPoints_Better()
    {
        // equipped: 10+10+10+100*0.05 = 35 ; candidate: 10+10+10+200*0.05 = 40
        var load = new InventoryLoadout { Horse = Horse(10, 10, 10, 100) };
        Assert.AreEqual(UpgradeVerdict.Better, _sut.Score(Cand(Horse(10, 10, 10, 200)), load));
    }

    [TestMethod]
    public void Score_HorseLowerOrEqualScore_None()
    {
        var load = new InventoryLoadout { Horse = Horse(10, 10, 10, 100) };
        Assert.AreEqual(UpgradeVerdict.None, _sut.Score(Cand(Horse(10, 10, 10, 100)), load));
        Assert.AreEqual(UpgradeVerdict.None, _sut.Score(Cand(Horse(9, 10, 10, 100)), load));
    }

    // ---- weapons and ammo: vanilla's one-click target (SPInventoryVM.cs:4053-4088) ----

    [TestMethod]
    public void Score_WeaponAnyEmptySlot_Better() =>
        Assert.AreEqual(UpgradeVerdict.Better,
            _sut.Score(Cand(Weapon(Sword, 1)), WithWeapons(Weapon(Spear, 1), Weapon(Bow, 1), Ammo(1, 1), null)));

    [TestMethod]
    public void Score_WeaponSameTypeEquippedAndEmptySlot_Better() =>
        Assert.AreEqual(UpgradeVerdict.Better,
            _sut.Score(Cand(Weapon(Sword, 10)), WithWeapons(Weapon(Sword, 50))));

    [TestMethod]
    public void Score_WeaponAllFull_ComparesFirstSameTypeSlot_WeakerThanFirst_None()
    {
        // The first sword is the stronger one; vanilla's click replaces it, so 60 is not an upgrade.
        var load = WithWeapons(Weapon(Sword, 80), Weapon(Sword, 50), Weapon(Spear, 10), Weapon(Bow, 10));
        Assert.AreEqual(UpgradeVerdict.None, _sut.Score(Cand(Weapon(Sword, 60)), load));
    }

    [TestMethod]
    public void Score_WeaponAllFull_ComparesFirstSameTypeSlot_StrongerThanFirst_Better()
    {
        var load = WithWeapons(Weapon(Sword, 80), Weapon(Sword, 50), Weapon(Spear, 10), Weapon(Bow, 10));
        Assert.AreEqual(UpgradeVerdict.Better, _sut.Score(Cand(Weapon(Sword, 90)), load));
    }

    [TestMethod]
    public void Score_WeaponAllFull_EqualToFirstSameType_None()
    {
        var load = WithWeapons(Weapon(Spear, 10), Weapon(Sword, 50), Weapon(Bow, 10), Ammo(1, 1));
        Assert.AreEqual(UpgradeVerdict.None, _sut.Score(Cand(Weapon(Sword, 50)), load));
    }

    [TestMethod]
    public void Score_WeaponAllFullNoSameType_None()
    {
        var load = WithWeapons(Weapon(Spear, 1), Weapon(Bow, 1), Ammo(1, 1), Shield(1, 1));
        Assert.AreEqual(UpgradeVerdict.None, _sut.Score(Cand(Weapon(Sword, 999)), load));
    }

    [TestMethod]
    public void Score_AmmoAllFull_HigherDamageThanFirstSameType_Better()
    {
        var load = WithWeapons(Weapon(Bow, 1), Ammo(5, 50), Ammo(9, 50), Weapon(Sword, 1));
        Assert.AreEqual(UpgradeVerdict.Better, _sut.Score(Cand(Ammo(6, 10)), load));
    }

    [TestMethod]
    public void Score_AmmoAllFull_EqualDamageHigherStack_Better()
    {
        var load = WithWeapons(Weapon(Bow, 1), Ammo(5, 20), Weapon(Sword, 1), Weapon(Spear, 1));
        Assert.AreEqual(UpgradeVerdict.Better, _sut.Score(Cand(Ammo(5, 30)), load));
    }

    [TestMethod]
    public void Score_AmmoAllFull_EqualDamageEqualOrLowerStack_None()
    {
        var load = WithWeapons(Weapon(Bow, 1), Ammo(5, 20), Weapon(Sword, 1), Weapon(Spear, 1));
        Assert.AreEqual(UpgradeVerdict.None, _sut.Score(Cand(Ammo(5, 20)), load));
        Assert.AreEqual(UpgradeVerdict.None, _sut.Score(Cand(Ammo(5, 10)), load));
    }

    [TestMethod]
    public void Score_AmmoAllFull_LowerDamageHigherStack_None()
    {
        var load = WithWeapons(Weapon(Bow, 1), Ammo(5, 20), Weapon(Sword, 1), Weapon(Spear, 1));
        Assert.AreEqual(UpgradeVerdict.None, _sut.Score(Cand(Ammo(4, 99)), load));
    }

    // ---- shields: vanilla moves the target onto the first equipped shield (SPInventoryVM.cs:4151-4166) ----

    [TestMethod]
    public void Score_ShieldEquippedAndEmptySlot_ComparesFirstShield_Better()
    {
        var load = WithWeapons(Shield(5, 100));
        Assert.AreEqual(UpgradeVerdict.Better, _sut.Score(Cand(Shield(5, 101)), load));
        Assert.AreEqual(UpgradeVerdict.Better, _sut.Score(Cand(Shield(7, 99)), load));
    }

    [TestMethod]
    public void Score_ShieldEquippedAndEmptySlot_NotStronger_None()
    {
        var load = WithWeapons(Shield(5, 100));
        Assert.AreEqual(UpgradeVerdict.None, _sut.Score(Cand(Shield(5, 100)), load));
        Assert.AreEqual(UpgradeVerdict.None, _sut.Score(Cand(Shield(4, 100)), load));
    }

    [TestMethod]
    public void Score_TwoShieldsEquipped_ComparesTheFirst()
    {
        var load = WithWeapons(Weapon(Sword, 1), Shield(5, 50), Shield(5, 200), Weapon(Spear, 1));
        Assert.AreEqual(UpgradeVerdict.Better, _sut.Score(Cand(Shield(5, 100)), load));
    }

    [TestMethod]
    public void Score_NoShieldEquippedEmptySlot_Better() =>
        Assert.AreEqual(UpgradeVerdict.Better,
            _sut.Score(Cand(Shield(1, 1)), WithWeapons(Weapon(Sword, 1))));

    [TestMethod]
    public void Score_NoShieldEquippedAllFull_None() =>
        Assert.AreEqual(UpgradeVerdict.None,
            _sut.Score(Cand(Shield(99, 999)),
                WithWeapons(Weapon(Sword, 1), Weapon(Spear, 1), Weapon(Bow, 1), Ammo(1, 1))));
}
