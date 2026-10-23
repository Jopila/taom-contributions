using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <inheritdoc />
public sealed class BattleLootAdapter : IBattleLootAdapter
{
    private readonly IModLogger _logger;

    public BattleLootAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public float? GetExpectedLootValue(string characterId)
    {
        try
        {
            var model = Campaign.Current?.Models?.BattleRewardModel;
            if (model == null)
                return null;
            var character = Find(characterId);
            var hero = Hero.MainHero;
            if (character == null || hero == null)
                return null;
            var value = model.GetExpectedLootedItemValueFromCasualty(hero, character);
            return float.IsNaN(value) || float.IsInfinity(value) ? (float?)null : value;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] GetExpectedLootValue('{characterId}') failed: {ex.Message}");
            return null;
        }
    }

    public string GrantLootFrom(string characterId, float targetValue)
    {
        try
        {
            var character = Find(characterId);
            var model = Campaign.Current?.Models?.BattleRewardModel;
            var roster = Campaign.Current?.MainParty?.Party?.ItemRoster;
            // Vanilla's GetRandomItem dereferences a null Equipment when the troop has no battle set, so
            // the guard is ours. Its roll then walks the troop's merchandise items from most to least
            // valuable, accepts each with a chance that drops once the item is worth more than the
            // target, skips non-merchandise items, and often returns nothing.
            if (character?.FirstBattleEquipment == null || model == null || roster == null)
                return null;

            var element = model.GetLootedItemFromTroop(character, targetValue);
            if (element.Item == null || element.Item.HasBannerComponent)
                return null;

            // The (EquipmentElement, int) overload keeps the modifier (adapters.md modifier rule).
            roster.AddToCounts(element, 1);
            return element.GetModifiedItemName()?.ToString() ?? element.Item.Name?.ToString();
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] GrantLootFrom('{characterId}') failed: {ex.Message}");
            return null;
        }
    }

    private static CharacterObject Find(string characterId)
    {
        if (string.IsNullOrEmpty(characterId))
            return null;
        return MBObjectManager.Instance?.GetObject<CharacterObject>(characterId);
    }
}
