using System;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.TokensOfEsteem.Models;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="IGiftHeroAdapter"/>. Every value is a raw fact: whether the hero is a lord, a child, a
/// prisoner, a companion, in the party, in the player's clan or at war with the player is read, never decided, here. The
/// campaign statics are read through <c>Campaign.Current</c> first (adapters.md: <c>Hero.MainHero</c> throws without a
/// campaign). The race name is empty for an id the race table does not hold, because <c>GetRaceNameFromId</c> falls back to
/// "human" and a taste must not match a guess (csharp-architecture.md, "Lookup Functions With Fallbacks"). The relation is
/// the engine's integer <c>GetRelation</c>, which already goes through the diplomacy model.
/// </summary>
public sealed class GiftHeroAdapter : IGiftHeroAdapter
{
    private readonly IRaceManager _races;
    private readonly IModLogger _logger;

    public GiftHeroAdapter(IRaceManager races, IModLogger logger)
    {
        _races = races;
        _logger = logger;
    }

    public GiftPlayerFacts? ReadPlayer()
    {
        try
        {
            var hero = PlayerHero();
            if (hero == null || WholeDay(CampaignTime.Now.ToDays) is not { } today)
                return null;
            var faction = Clan.PlayerClan?.MapFaction;
            return new GiftPlayerFacts(hero.StringId, faction?.StringId ?? string.Empty,
                faction?.Culture?.StringId ?? string.Empty, hero.Gold, today);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Gifts] ReadPlayer failed: {ex.Message}");
            return null;
        }
    }

    public GiftRecipientFacts? ReadRecipient(string heroId)
    {
        try
        {
            var player = PlayerHero();
            var hero = Find(heroId);
            if (player == null || hero == null || hero.IsDead || hero == player)
                return null;

            // The hero whose relation with the player a change to this hero really moves: the cooldown is keyed on it.
            Campaign.Current.Models.DiplomacyModel.GetHeroesForEffectiveRelation(player, hero, out _, out var owner);
            var theirs = hero.MapFaction;
            var mine = player.MapFaction;
            var party = hero.PartyBelongedTo;
            var raceId = hero.CharacterObject?.Race ?? -1;
            return new GiftRecipientFacts(
                hero.StringId,
                (owner ?? hero).StringId,
                hero.IsLord,
                hero.IsChild,
                hero.IsPrisoner,
                hero.Clan != null && hero.Clan == Clan.PlayerClan,
                hero.IsPlayerCompanion,
                party != null && party == Campaign.Current.MainParty,
                theirs != null && mine != null && theirs.IsAtWarWith(mine),
                player.GetRelation(hero),
                _races.IsValidRaceId(raceId) ? _races.GetRaceNameFromId(raceId) : string.Empty,
                CultureId: hero.Culture?.StringId ?? string.Empty,
                SideFactionId: theirs?.StringId ?? string.Empty,
                SideCultureId: theirs?.Culture?.StringId ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Gifts] ReadRecipient('{heroId}') failed: {ex.Message}");
            return null;
        }
    }

    public bool PayGoldToHero(string heroId, int amount)
    {
        if (amount <= 0)
            return false;
        try
        {
            var player = PlayerHero();
            var hero = Find(heroId);
            if (player == null || hero == null || hero == player || player.Gold < amount)
                return false;

            GiveGoldAction.ApplyBetweenCharacters(player, hero, amount, disableNotification: true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Gifts] PayGoldToHero('{heroId}', {amount}) failed: {ex.Message}");
            return false;
        }
    }

    public void ShowRelationChange(string heroId, int change)
    {
        try
        {
            var player = PlayerHero();
            var hero = Find(heroId);
            if (change == 0 || player == null || hero == null)
                return;

            // Vanilla's "with notable" text and its variables, shown as a log line (InformationManager.DisplayMessage, as Bonds of
            // Battle shows its own): there is no quick popup and no sound.
            var text = change > 0
                ? GameTexts.FindText("str_your_relation_increased_with_notable")
                : GameTexts.FindText("str_your_relation_decreased_with_notable");
            StringHelpers.SetCharacterProperties("HERO", hero.CharacterObject, text);
            text.SetTextVariable("VALUE", player.GetRelation(hero));
            text.SetTextVariable("MAGNITUDE", MathF.Abs(change));
            InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Gifts] Showing the relation change failed: {ex.Message}");
        }
    }

    /// <summary>
    /// The whole campaign day for a day count, or null when the engine value is not a usable day number: <c>(int)double.NaN</c>
    /// and a cast past <c>int.MaxValue</c> are both <c>int.MinValue</c>, which would read as a day long ago. The player facts
    /// are then unavailable and the gift line hides.
    /// </summary>
    internal static int? WholeDay(double days) =>
        FiniteFloatValidator.IsFiniteInRange(days, 0d, int.MaxValue) ? (int)Math.Floor(days) : null;

    private static Hero? PlayerHero() =>
        Campaign.Current == null ? null : CharacterObject.PlayerCharacter?.HeroObject;

    /// <summary>The partner of the open conversation when it is the asked hero, else a lookup (as CaptiveLordAdapter does).</summary>
    private static Hero? Find(string heroId)
    {
        if (string.IsNullOrEmpty(heroId) || Campaign.Current == null)
            return null;
        var partner = Hero.OneToOneConversationHero;
        return partner != null && partner.StringId == heroId ? partner : Campaign.Current.CampaignObjectManager?.Find<Hero>(heroId);
    }
}
