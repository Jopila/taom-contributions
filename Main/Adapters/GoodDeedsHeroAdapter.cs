using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TAOM.Core.Logging;
using TAOM.Features.GoodDeeds.Models;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="IGoodDeedsHeroAdapter"/>. Every value is a raw fact: whether the prisoner is a lord,
/// a hero alive, a clan the player's is read, never decided, here. The campaign statics are read through
/// <c>Campaign.Current</c> first (adapters.md: <c>Hero.MainHero</c> throws without a campaign). A side is a map faction's own
/// id with that faction's own culture, so <c>IAlignmentService.ResolveSide</c> sees one entity (the player's
/// <c>Clan.PlayerClan.MapFaction</c>, the hero's map faction, the settlement owner's map faction). The relation owner is the
/// second hero <c>GetHeroesForEffectiveRelation</c> returns, as <c>GiftHeroAdapter</c> reads it: the clan leader unless personal
/// relations are on. A read that faults is logged and answers null (the release source answers all false).
/// </summary>
public sealed class GoodDeedsHeroAdapter : IGoodDeedsHeroAdapter
{
    private readonly IModLogger _logger;

    public GoodDeedsHeroAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public ReleaseSource ReadReleaseSource(PartyBase? party)
    {
        try
        {
            var main = Campaign.Current?.MainParty?.Party;
            if (main == null)
                return ReleaseSource.None;

            if (party == null)
            {
                // A party-screen release (Done, or Talk then "Apply Changes") raises the event with no party, after the transfer already took the
                // prisoner out of the party, and while the PartyState is still the active state. PartyScreenHelper.GetActivePartyState
                // is never used: it Debug.FailedAsserts when no party screen is open.
                var screen = (GameStateManager.Current?.ActiveState as PartyState)?.PartyScreenLogic;
                return new ReleaseSource(false, screen != null && ReferenceEquals(screen.RightOwnerParty, main), false);
            }

            var owner = party.IsSettlement ? party.Settlement?.OwnerClan : null;
            return new ReleaseSource(ReferenceEquals(party, main), false, owner != null && owner == Clan.PlayerClan);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[GoodDeeds] ReadReleaseSource failed: {ex.Message}");
            return ReleaseSource.None;
        }
    }

    public ReleaseFacts? ReadRelease(string prisonerId)
    {
        try
        {
            var player = PlayerHero();
            var lord = FindHero(prisonerId);
            if (player == null || lord == null)
                return null;

            return new ReleaseFacts(
                FactionSides.OfPlayer(),
                FactionSides.Of(lord.MapFaction),
                lord.IsLord,
                DeedHeroOf(player, lord),
                lord.Spouse == null ? null : DeedHeroOf(player, lord.Spouse),
                lord.Clan?.Leader == null ? null : DeedHeroOf(player, lord.Clan.Leader));
        }
        catch (Exception ex)
        {
            _logger.LogError($"[GoodDeeds] ReadRelease('{prisonerId}') failed: {ex.Message}");
            return null;
        }
    }

    public DonationFacts? ReadDonation(string heroId, string settlementId)
    {
        try
        {
            var hero = FindHero(heroId);
            var settlement = FindSettlement(settlementId);
            if (hero == null || settlement == null)
                return null;

            return new DonationFacts(FactionSides.Of(hero.MapFaction), FactionSides.Of(settlement.OwnerClan?.MapFaction));
        }
        catch (Exception ex)
        {
            _logger.LogError($"[GoodDeeds] ReadDonation('{heroId}', '{settlementId}') failed: {ex.Message}");
            return null;
        }
    }

    public TournamentFacts? ReadTournament(string townSettlementId)
    {
        try
        {
            var player = PlayerHero();
            var settlement = FindSettlement(townSettlementId);
            if (player == null || settlement == null)
                return null;

            var owner = settlement.OwnerClan;
            var notables = new List<DeedNotable>();
            var living = settlement.Notables;
            if (living != null)
            {
                foreach (var notable in living)
                {
                    if (notable != null)
                        notables.Add(new DeedNotable(DeedHeroOf(player, notable), FactionSides.Of(notable.MapFaction)));
                }
            }

            return new TournamentFacts(
                settlement.Name?.ToString() ?? string.Empty,
                FactionSides.OfPlayer(),
                owner != null && owner == Clan.PlayerClan,
                owner?.Leader == null ? null : DeedHeroOf(player, owner.Leader),
                FactionSides.Of(owner?.MapFaction),
                notables);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[GoodDeeds] ReadTournament('{townSettlementId}') failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// The hero and the hero a change to him really moves. The player is never asked: the diplomacy model's pair rule is not
    /// meant for the player against himself.
    /// </summary>
    private static DeedHero DeedHeroOf(Hero player, Hero hero)
    {
        if (hero == player)
            return new DeedHero(hero.StringId, hero.StringId, hero.IsAlive, true);

        Campaign.Current.Models.DiplomacyModel.GetHeroesForEffectiveRelation(player, hero, out _, out var owner);
        return new DeedHero(hero.StringId, (owner ?? hero).StringId, hero.IsAlive, false);
    }

    private static Hero? PlayerHero() =>
        Campaign.Current == null ? null : CharacterObject.PlayerCharacter?.HeroObject;

    private static Hero? FindHero(string heroId) =>
        string.IsNullOrEmpty(heroId) || Campaign.Current == null ? null : Campaign.Current.CampaignObjectManager?.Find<Hero>(heroId);

    private static Settlement? FindSettlement(string settlementId) =>
        string.IsNullOrEmpty(settlementId) || Campaign.Current == null ? null : Settlement.Find(settlementId);
}
