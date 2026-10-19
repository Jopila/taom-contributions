namespace TAOM.Features.TelcharsMark;

/// <summary>Pure comparison of a candidate item against the equipped loadout.</summary>
public sealed class ItemUpgradeScorer
{
    // Sidegrade weights. Both sides of every comparison use them, so only their ratios matter.
    private const float HeadWeight = 34f;
    private const float BodyWeight = 42f;
    private const float LegWeight = 12f;
    private const float ArmWeight = 12f;
    private const float HorseHitPointsFactor = 0.05f;

    public float WeightedTotal(ItemUpgradeStats s) =>
        s.HeadArmor * HeadWeight + s.BodyArmor * BodyWeight + s.LegArmor * LegWeight + s.ArmArmor * ArmWeight;

    public UpgradeVerdict Score(ItemCandidate candidate, InventoryLoadout loadout)
    {
        if (!(candidate.IsEquipable && candidate.CanCharacterUse)) return UpgradeVerdict.None;
        var s = candidate.Stats;
        if (s.Kind == UpgradeItemKind.None) return UpgradeVerdict.None;
        if (loadout.EquipmentMode == InventoryEquipmentMode.Civilian && !candidate.IsCivilian) return UpgradeVerdict.None;
        if (loadout.EquipmentMode == InventoryEquipmentMode.Stealth && !candidate.IsStealth) return UpgradeVerdict.None;

        switch (s.Kind)
        {
            case UpgradeItemKind.Head: return ScoreArmour(s, loadout.Head, e => e.HeadArmor);
            case UpgradeItemKind.Body: return ScoreArmour(s, loadout.Body, e => e.BodyArmor);
            case UpgradeItemKind.Leg: return ScoreArmour(s, loadout.Leg, e => e.LegArmor);
            case UpgradeItemKind.Gloves: return ScoreArmour(s, loadout.Gloves, e => e.ArmArmor);
            case UpgradeItemKind.Cape: return ScoreArmour(s, loadout.Cape, e => e.BodyArmor);
            case UpgradeItemKind.Harness: return ScoreHarness(s, loadout);
            case UpgradeItemKind.Horse: return ScoreHorse(s, loadout.Horse);
            case UpgradeItemKind.Shield: return ScoreShield(s, loadout);
            case UpgradeItemKind.Weapon:
            case UpgradeItemKind.Ammo:
                return ScoreWeaponOrAmmo(s, loadout);
            default: return UpgradeVerdict.None;
        }
    }

    private UpgradeVerdict ScoreArmour(ItemUpgradeStats cand, ItemUpgradeStats? equipped,
        System.Func<ItemUpgradeStats, int> main)
    {
        if (equipped == null) return UpgradeVerdict.Better;
        int candMain = main(cand);
        int eqMain = main(equipped);
        bool totalHigher = WeightedTotal(cand) > WeightedTotal(equipped);
        if (candMain > eqMain) return UpgradeVerdict.Better;
        if (candMain == eqMain) return totalHigher ? UpgradeVerdict.Better : UpgradeVerdict.None;
        return totalHigher ? UpgradeVerdict.Sidegrade : UpgradeVerdict.None;
    }

    private static UpgradeVerdict ScoreHarness(ItemUpgradeStats cand, InventoryLoadout loadout)
    {
        var horse = loadout.Horse;
        if (horse == null) return UpgradeVerdict.None;
        // A null family is unknown, not a mismatch; vanilla compares the two ints (SPInventoryVM.cs:4112).
        if (cand.MountFamily != null && horse.MountFamily != null && cand.MountFamily != horse.MountFamily)
            return UpgradeVerdict.None;
        var eq = loadout.Harness;
        if (eq == null) return UpgradeVerdict.Better;
        return cand.MountBodyArmor > eq.MountBodyArmor ? UpgradeVerdict.Better : UpgradeVerdict.None;
    }

    private static float HorseScore(ItemUpgradeStats h) =>
        h.MountSpeed + h.MountCharge + h.MountManeuver + h.MountHitPoints * HorseHitPointsFactor;

    private static UpgradeVerdict ScoreHorse(ItemUpgradeStats cand, ItemUpgradeStats? equipped)
    {
        if (equipped == null) return UpgradeVerdict.Better;
        return HorseScore(cand) > HorseScore(equipped) ? UpgradeVerdict.Better : UpgradeVerdict.None;
    }

    // The slot a one-click equip fills (SPInventoryVM.cs:4053-4088): the first empty weapon slot, else the first
    // slot holding the same item type, else none worth comparing.
    private static UpgradeVerdict ScoreWeaponOrAmmo(ItemUpgradeStats cand, InventoryLoadout loadout)
    {
        ItemUpgradeStats? firstSameType = null;
        foreach (var slot in loadout.Weapons)
        {
            if (slot == null) return UpgradeVerdict.Better;
            if (firstSameType == null && slot.Kind == cand.Kind && slot.WeaponTypeId == cand.WeaponTypeId)
                firstSameType = slot;
        }
        if (firstSameType == null) return UpgradeVerdict.None;
        return IsStronger(cand, firstSameType) ? UpgradeVerdict.Better : UpgradeVerdict.None;
    }

    // A shield replaces the first equipped shield even when a slot is empty (SPInventoryVM.cs:4151-4166).
    private static UpgradeVerdict ScoreShield(ItemUpgradeStats cand, InventoryLoadout loadout)
    {
        var anyEmpty = false;
        foreach (var slot in loadout.Weapons)
        {
            if (slot == null) { anyEmpty = true; continue; }
            if (slot.Kind == UpgradeItemKind.Shield)
                return IsStronger(cand, slot) ? UpgradeVerdict.Better : UpgradeVerdict.None;
        }
        return anyEmpty ? UpgradeVerdict.Better : UpgradeVerdict.None;
    }

    /// <summary>True when <paramref name="a"/> is strictly stronger than <paramref name="b"/> for its kind.</summary>
    private static bool IsStronger(ItemUpgradeStats a, ItemUpgradeStats b)
    {
        switch (a.Kind)
        {
            case UpgradeItemKind.Shield:
                return a.BodyArmor + a.ShieldHitPoints > b.BodyArmor + b.ShieldHitPoints;
            case UpgradeItemKind.Ammo:
                return a.MaxDamage > b.MaxDamage || (a.MaxDamage == b.MaxDamage && a.StackCount > b.StackCount);
            default:
                return a.MaxDamage > b.MaxDamage;
        }
    }
}
