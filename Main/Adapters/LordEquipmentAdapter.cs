using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;
using TAOM.Features.HardQuestions.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="ILordEquipmentAdapter"/>. <c>Hero.BattleEquipment</c>,
/// <c>CivilianEquipment</c> and <c>StealthEquipment</c> fall back to one object shared by every hero when the
/// hero holds no copy of his own (Hero.cs:221-225), so each access checks <c>ReferenceEquals</c> against that
/// fallback and a shared set is never read from, written to or re-kitted.
/// </summary>
public sealed class LordEquipmentAdapter : ILordEquipmentAdapter
{
    private const int SlotCount = (int)EquipmentIndex.NumEquipmentSetSlots;

    private readonly IModLogger _logger;

    public LordEquipmentAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<LordEquipmentPiece> ReadPieces(string heroId)
    {
        var pieces = new List<LordEquipmentPiece>();
        var hero = Find(heroId);
        if (hero == null)
            return pieces;
        foreach (LordEquipmentSet set in Enum.GetValues(typeof(LordEquipmentSet)))
        {
            var equipment = Own(hero, set);
            if (equipment == null)
                continue;
            for (var slot = 0; slot < SlotCount; slot++)
            {
                var element = equipment[slot];
                if (element.Item != null)
                    pieces.Add(new LordEquipmentPiece(set, slot, element.Item.StringId, element.ItemModifier?.StringId));
            }
        }
        return pieces;
    }

    public bool ClearSlot(string heroId, LordEquipmentSet set, int slot)
    {
        var equipment = Own(Find(heroId), set);
        if (equipment == null || slot < 0 || slot >= SlotCount)
            return false;
        equipment[slot] = default;
        return true;
    }

    public bool IsSlotEmpty(string heroId, LordEquipmentSet set, int slot)
    {
        var equipment = Own(Find(heroId), set);
        return equipment != null && slot >= 0 && slot < SlotCount && equipment[slot].Item == null;
    }

    public bool SetItem(string heroId, LordEquipmentSet set, int slot, string itemId)
    {
        var equipment = Own(Find(heroId), set);
        var item = string.IsNullOrEmpty(itemId) ? null : MBObjectManager.Instance?.GetObject<ItemObject>(itemId);
        if (equipment == null || item == null || slot < 0 || slot >= SlotCount)
            return false;
        equipment[slot] = new EquipmentElement(item);
        return true;
    }

    /// <summary>
    /// Copies the engine's coming-of-age kit slot by slot into the hero's OWN battle and civilian sets, only where
    /// <see cref="KitSlotRule.CanFill"/> allows. <c>EquipmentHelper.AssignHeroEquipmentFromEquipment</c> would
    /// write all twelve slots and wipe a named piece the lord kept.
    /// </summary>
    public bool GiveFreshKit(string heroId, string ragItemId)
    {
        var hero = Find(heroId);
        var models = Campaign.Current?.Models.EquipmentSelectionModel;
        if (hero == null || models == null)
            return false;
        try
        {
            var done = false;
            foreach (var type in new[] { Equipment.EquipmentType.Battle, Equipment.EquipmentType.Civilian })
            {
                var kit = models.GetEquipmentForHeroComeOfAge(hero, type);
                var own = Own(hero, type == Equipment.EquipmentType.Battle ? LordEquipmentSet.Battle : LordEquipmentSet.Civilian);
                if (kit == null || own == null)
                    continue;
                for (var slot = 0; slot < SlotCount; slot++)
                    if (KitSlotRule.CanFill(own[slot].Item?.StringId, kit[slot].Item?.StringId, ragItemId))
                        own[slot] = new EquipmentElement(kit[slot].Item, kit[slot].ItemModifier);
                done = true;
            }
            return done;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[HardQuestions] GiveFreshKit('{heroId}') failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>The hero's own set, or null when he has none (the property then returns the shared fallback).</summary>
    private static Equipment? Own(Hero? hero, LordEquipmentSet set)
    {
        var campaign = Campaign.Current;
        if (hero == null || campaign == null)
            return null;
        return set switch
        {
            LordEquipmentSet.Battle => NotShared(hero.BattleEquipment, campaign.DeadBattleEquipment),
            LordEquipmentSet.Civilian => NotShared(hero.CivilianEquipment, campaign.DeadCivilianEquipment),
            _ => NotShared(hero.StealthEquipment, campaign.DefaultStealthEquipment),
        };
    }

    private static Equipment? NotShared(Equipment? own, Equipment? shared) =>
        own == null || ReferenceEquals(own, shared) ? null : own;

    private static Hero? Find(string heroId) =>
        string.IsNullOrEmpty(heroId) ? null : Campaign.Current?.CampaignObjectManager?.Find<Hero>(heroId);
}
