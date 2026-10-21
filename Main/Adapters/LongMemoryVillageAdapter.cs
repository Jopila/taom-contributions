using System;
using System.Collections.Generic;
using TAOM.Features.Relations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;
using TAOM.Features.LongMemory.Models;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="ILongMemoryVillageAdapter"/>. Every value is a raw fact: whether the village is the
/// player's clan's, whether the player's party leads, how much damage the raid did are read, never decided, here. The campaign
/// statics are read through <c>Campaign.Current</c> first (adapters.md: <c>Hero.MainHero</c> throws without a campaign). A side is
/// a map faction's own id with that faction's own culture, built by <see cref="FactionSides"/> alone (the player's
/// <c>Clan.PlayerClan.MapFaction</c>, the village owner clan's map faction). The relation owner is the second hero
/// <c>GetHeroesForEffectiveRelation</c> returns, as <c>GoodDeedsHeroAdapter</c> reads it: the clan leader unless personal relations
/// are on. A read that faults is logged and answers null.
/// </summary>
public sealed class LongMemoryVillageAdapter : ILongMemoryVillageAdapter
{
    private readonly IModLogger _logger;

    public LongMemoryVillageAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public VillageHostileActionFacts? ReadMapEvent(MapEventComponent? component)
    {
        try
        {
            var mapEvent = component?.MapEvent;
            var settlement = mapEvent?.MapEventSettlement;
            if (mapEvent == null || settlement == null)
                return null;

            // The raid listener of the engine reads the same leader party at the same moment (OnBeforeMapEventFinalize). An AI
            // lord's raid, a raid inside another lord's army and a raid an enlisted player joins under a commander are not led by the player's own party (an act the enlisted player starts personally is),
            // and they are the common case: leave before any name, side or notable is read.
            var main = Campaign.Current?.MainParty?.Party;
            if (main == null || !ReferenceEquals(mapEvent.AttackerSide?.LeaderParty, main))
                return null;

            var raid = component as RaidEventComponent;
            return Read(settlement, true, raid?.RaidDamage ?? 0f);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[LongMemory] ReadMapEvent failed: {ex.Message}");
            return null;
        }
    }

    public VillageHostileActionFacts? ReadCurrentVillage()
    {
        try
        {
            // No main party (no campaign) returns before the static CurrentSettlement is touched.
            var main = Campaign.Current?.MainParty;
            if (main == null)
                return null;

            var settlement = Settlement.CurrentSettlement;
            if (settlement == null)
                return null;

            // The menu is the player's own: the main party is not attached to another party and not inside another lord's army.
            var leads = main.AttachedTo == null && (main.Army == null || main.Army.LeaderParty == main);
            return Read(settlement, leads, 0f);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[LongMemory] ReadCurrentVillage failed: {ex.Message}");
            return null;
        }
    }

    private static VillageHostileActionFacts? Read(Settlement settlement, bool leads, float raidDamage)
    {
        var player = Campaign.Current == null ? null : CharacterObject.PlayerCharacter?.HeroObject;
        if (player == null)
            return null;

        var owner = settlement.OwnerClan;
        var notables = new List<RelationTarget>();
        var living = settlement.Notables;
        if (living != null)
        {
            foreach (var notable in living)
            {
                if (notable != null)
                    notables.Add(RelationTargets.Of(player, notable));
            }
        }

        return new VillageHostileActionFacts(
            settlement.Name?.ToString() ?? string.Empty,
            FactionSides.OfPlayer(),
            FactionSides.Of(owner?.MapFaction),
            settlement.IsVillage,
            owner != null && owner == Clan.PlayerClan,
            leads,
            raidDamage,
            owner?.Leader == null ? null : RelationTargets.Of(player, owner.Leader),
            notables);
    }
}
