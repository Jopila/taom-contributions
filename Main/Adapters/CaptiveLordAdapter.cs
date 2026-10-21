using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Core.Logging;
using TAOM.Features.HardQuestions.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="ICaptiveLordAdapter"/>. Computed TaleWorlds getters are read through
/// <c>?.</c> (adapters.md): the captor's clan comes from the party's own owner, or the settlement's owner clan
/// for a dungeon, never from <c>Settlement.Owner</c> (<c>OwnerClan.Leader</c>, which throws without a clan).
/// <see cref="Snapshot"/> is cheap and runs in dialog conditions; <see cref="Circle"/> walks the world and runs
/// once per deed.
/// </summary>
public sealed class CaptiveLordAdapter : ICaptiveLordAdapter
{
    private readonly IModLogger _logger;

    public CaptiveLordAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public string? ConversationPartnerId => Hero.OneToOneConversationHero?.StringId;

    public bool IsPrisonerDecisionContext =>
        Campaign.Current?.CurrentConversationContext is ConversationContext.CapturedLord
            or ConversationContext.FreeOrCapturePrisonerHero;

    public bool IsAlive(string heroId) => Find(heroId)?.IsAlive ?? false;

    public CaptiveSnapshot? Snapshot(string heroId)
    {
        try
        {
            var hero = Find(heroId);
            if (hero == null || hero.IsDead || !hero.IsPrisoner)
                return null;
            var clan = hero.Clan;
            return new CaptiveSnapshot(Ref(hero))
            {
                ClanId = clan?.StringId,
                IsWounded = hero.IsWounded,
                IsInPlayerClan = clan != null && clan == Clan.PlayerClan,
                CaptorIsPlayerClan = CaptorClan(hero.PartyBelongedToAsPrisoner) is { } captor && captor == Clan.PlayerClan,
                CalculatingLevel = hero.GetTraitLevel(DefaultTraits.Calculating),
                ValorLevel = hero.GetTraitLevel(DefaultTraits.Valor),
                RansomValue = Campaign.Current.Models.RansomValueCalculationModel.PrisonerRansomValue(hero.CharacterObject, Hero.MainHero),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"[HardQuestions] Snapshot('{heroId}') failed: {ex.Message}");
            return null;
        }
    }

    public CaptiveCircle? Circle(string heroId)
    {
        try
        {
            var hero = Find(heroId);
            if (hero == null || hero.IsDead || !hero.IsPrisoner)
                return null;
            var playerClan = Clan.PlayerClan;
            var kingdom = hero.Clan?.Kingdom;
            return new CaptiveCircle(Ref(hero, withRelationOwner: true))
            {
                Family = Kin(hero).Where(h => Foreign(h, hero, playerClan)).Select(h => Ref(h, true)).ToList(),
                ClanLeader = hero.Clan?.Leader is { } chief && Foreign(chief, hero, playerClan) ? Ref(chief, true) : null,
                ClanLords = LordsOf(hero.Clan?.Heroes, hero, playerClan),
                KingdomLeader = kingdom?.Leader is { } king && Foreign(king, hero, playerClan) ? Ref(king, true) : null,
                KingdomLords = LordsOf(kingdom?.Heroes, hero, playerClan),
                HonorPositiveLords = Hero.AllAliveHeroes
                    .Where(h => h.IsLord && Foreign(h, hero, playerClan) && h.GetTraitLevel(DefaultTraits.Honor) > 0)
                    .Select(h => Ref(h, true)).ToList(),
                Companions = (playerClan?.Companions ?? Enumerable.Empty<Hero>())
                    .Where(c => c.IsAlive)
                    .Select(c => new CompanionRef(c.StringId, c.GetTraitLevel(DefaultTraits.Mercy), c.GetTraitLevel(DefaultTraits.Honor)))
                    .ToList(),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"[HardQuestions] Circle('{heroId}') failed: {ex.Message}");
            return null;
        }
    }

    private static Clan? CaptorClan(PartyBase? party)
    {
        if (party == null)
            return null;
        return party.IsSettlement ? party.Settlement?.OwnerClan : party.MobileParty?.Owner?.Clan;
    }

    private static IEnumerable<Hero> Kin(Hero hero)
    {
        var kin = new List<Hero?> { hero.Spouse, hero.Father, hero.Mother };
        kin.AddRange(hero.Siblings);
        kin.AddRange(hero.Children);
        return kin.Where(h => h != null).Cast<Hero>().Distinct();
    }

    private static List<LordRef> LordsOf(IEnumerable<Hero>? heroes, Hero captive, Clan? playerClan) =>
        (heroes ?? Enumerable.Empty<Hero>()).Where(h => h.IsLord && Foreign(h, captive, playerClan)).Select(h => Ref(h, true)).ToList();

    private static bool Foreign(Hero other, Hero captive, Clan? playerClan) =>
        other != captive && other.IsAlive && other.Clan != playerClan;

    /// <summary>
    /// A lord reference. With <paramref name="withRelationOwner"/> it asks the active DiplomacyModel whose relation
    /// with the player a change to this lord really moves (<c>GetHeroesForEffectiveRelation</c>: his clan leader
    /// under the vanilla mapping, the lord himself under Personal Relations).
    /// </summary>
    private static LordRef Ref(Hero hero, bool withRelationOwner = false)
    {
        string? relationId = null;
        if (withRelationOwner)
        {
            Campaign.Current.Models.DiplomacyModel.GetHeroesForEffectiveRelation(Hero.MainHero, hero, out _, out var effective);
            relationId = (effective ?? hero).StringId;
        }
        return new LordRef(hero.StringId, hero.Clan?.Kingdom?.StringId, FactionSides.OfHero(hero), relationId, hero.Clan?.StringId);
    }

    /// <summary>The partner of the open conversation when it is the asked hero, else a lookup.</summary>
    private static Hero? Find(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return null;
        var partner = Hero.OneToOneConversationHero;
        return partner != null && partner.StringId == heroId ? partner : Campaign.Current?.CampaignObjectManager?.Find<Hero>(heroId);
    }
}
