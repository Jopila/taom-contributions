using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;
using TAOM.Features.TokensOfEsteem.Models;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="IGiftItemAdapter"/>. Held goods come from the main party's item roster, every stack
/// in a wanted category, with <see cref="HeldGood.IsModified"/> set from <c>ItemModifier != null</c>; the service lists only
/// the unmodified stack, the one <see cref="IPartyItemRosterAdapter.RemoveItem"/> takes from. The registries are the live
/// object manager's: item categories and cultures.
/// </summary>
public sealed class GiftItemAdapter : IGiftItemAdapter
{
    private readonly IModLogger _logger;

    public GiftItemAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public bool IsRegistryReady => Campaign.Current != null && MBObjectManager.Instance != null;

    public bool IsKnownCategory(string categoryId) =>
        !string.IsNullOrEmpty(categoryId) && MBObjectManager.Instance?.GetObject<ItemCategory>(categoryId) != null;

    public bool IsKnownCulture(string cultureId) =>
        !string.IsNullOrEmpty(cultureId) && MBObjectManager.Instance?.GetObject<CultureObject>(cultureId) != null;

    public IReadOnlyList<HeldGood> ReadHeldGoods(IReadOnlyCollection<string> categoryIds)
    {
        var goods = new List<HeldGood>();
        try
        {
            var roster = Campaign.Current?.MainParty?.Party?.ItemRoster;
            if (roster == null || categoryIds.Count == 0)
                return goods;

            var wanted = new HashSet<string>(categoryIds, StringComparer.Ordinal);
            for (var i = 0; i < roster.Count; i++)
            {
                var element = roster.GetElementCopyAtIndex(i);
                var held = element.EquipmentElement;
                var item = held.Item;
                var category = item?.ItemCategory?.StringId;
                if (item == null || element.Amount <= 0 || category == null || !wanted.Contains(category))
                    continue;
                // Every stack, flagged: the service skips the modified ones, so the decision is testable (ADR-007).
                goods.Add(new HeldGood(item.StringId, item.Name?.ToString() ?? item.StringId, category, item.Value, element.Amount,
                    held.ItemModifier != null));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Gifts] ReadHeldGoods failed: {ex.Message}");
            goods.Clear();
        }
        return goods;
    }

    public void CommitOpenPartyScreen()
    {
        try
        {
            GameStateManager.Current?.LastOrDefault<PartyState>()?.PartyScreenLogic?.DoneLogic(true);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Gifts] Committing the open party screen failed: {ex.Message}");
        }
    }
}
