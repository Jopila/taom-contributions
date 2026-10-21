using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Validation;
using TAOM.Features.FarWatch;
using TAOM.Features.FieldCamp;

namespace TAOM.Adapters;

/// <summary>
/// The campaign facts Far Watch reads and the one thing it writes (#847, docs/features/far-watch.md). Facts only: it never
/// decides who may ride, which party is reported or what ends a ride. It holds no engine object between calls (a process
/// singleton; FarWatchWorldAdapterTests pins the fields), reads the scout from the Scout ROLE (<c>GetRoleHolder</c>, never
/// <c>EffectiveScout</c>, which silently falls back to the leader), and finds heroes among the living by id (a hero built at
/// runtime is not in the object manager). Main thread only.
/// </summary>
public sealed class FarWatchWorldAdapter : IFarWatchWorldAdapter
{
    /// <summary>The locator's broad phase skips a party exactly on the radius, so the search reaches a little past the exact edge.</summary>
    private const float BroadPhaseMargin = 1.01f;

    private readonly IPartyIntelAdapter _intel;
    private readonly ICampService _camps;

    public FarWatchWorldAdapter(IPartyIntelAdapter intel, ICampService camps)
    {
        _intel = intel;
        _camps = camps;
    }

    public double NowHours
    {
        get
        {
            if (Campaign.Current == null) return -1d;
            double hours = CampaignTime.Now.ToHours;
            return FiniteFloatValidator.IsFinite(hours) ? hours : -1d;
        }
    }

    public bool IsMainPartyInMapEvent => Campaign.Current?.MainParty?.MapEvent != null;

    public bool InvolvesMainParty(PartyBase attacker, PartyBase defender)
    {
        var main = Campaign.Current?.MainParty?.Party;
        return main != null && (attacker == main || defender == main);
    }

    public ScoutCampFacts ReadCamp()
    {
        var main = Campaign.Current?.MainParty;
        if (main == null) return default;

        var camp = _camps.PlayerCamp;
        var hero = CharacterObject.PlayerCharacter?.HeroObject;
        var scout = main.GetRoleHolder(PartyRole.Scout);
        return new ScoutCampFacts
        {
            CampExists = camp != null,
            CampReady = camp != null && camp.IsReady,
            InEncounter = Campaign.Current.PlayerEncounter != null,
            InMapEvent = main.MapEvent != null,
            InSettlement = main.CurrentSettlement != null,
            MainHeroId = hero?.StringId,
            MainHeroName = hero?.Name?.ToString(),
            MainHeroSkill = hero?.GetSkillValue(DefaultSkills.Scouting) ?? 0,
            MainHeroWounded = hero != null && hero.IsWounded,
            PartyAlone = main.MemberRoster.TotalManCount == 1,
            AssignedScoutId = scout?.StringId,
            AssignedScoutName = scout?.Name?.ToString(),
            AssignedScoutSkill = scout?.GetSkillValue(DefaultSkills.Scouting) ?? 0,
            AssignedScoutWounded = scout != null && scout.IsWounded,
            AssignedScoutInParty = scout != null && scout.PartyBelongedTo == main && scout.IsAlive && !scout.IsPrisoner,
        };
    }

    public bool IsHeroWithParty(string heroId)
    {
        var hero = FindLivingHero(heroId);
        var main = Campaign.Current?.MainParty;
        return hero != null && main != null && hero.PartyBelongedTo == main && !hero.IsPrisoner;
    }

    public bool GrantScoutingXp(string heroId, float xp)
    {
        var hero = FindLivingHero(heroId);
        if (hero == null || !(FiniteFloatValidator.IsFinite(xp) && xp > 0f)) return false;
        hero.AddSkillXp(DefaultSkills.Scouting, xp);
        return true;
    }

    public void CollectSightings(List<ScoutSighting> into)
    {
        into.Clear();
        var main = Campaign.Current?.MainParty;
        if (main == null) return;

        var origin = main.Position.ToVec2();
        var data = MobileParty.StartFindingLocatablesAroundPosition(origin, main.SeeingRange * BroadPhaseMargin);
        for (var party = MobileParty.FindNextLocatable(ref data); party != null; party = MobileParty.FindNextLocatable(ref data))
        {
            if (!party.IsActive || !party.IsVisible) continue;
            into.Add(Sighting(party, origin));
        }
    }

    public void CollectFiefs(List<ScoutSite> into)
    {
        into.Clear();
        var main = Campaign.Current?.MainParty;
        if (main == null) return;

        var origin = main.Position.ToVec2();
        foreach (var town in Town.AllFiefs)
        {
            var settlement = town.Settlement;
            if (settlement == null) continue;
            var gate = settlement.GatePosition.ToVec2();
            into.Add(new ScoutSite
            {
                Id = settlement.StringId,
                Name = settlement.Name?.ToString() ?? string.Empty,
                OwnedByPlayerClan = settlement.OwnerClan != null && settlement.OwnerClan == Clan.PlayerClan,
                Dx = gate.x - origin.x,
                Dy = gate.y - origin.y,
                Distance = settlement.GatePosition.Distance(main.Position),
            });
        }
    }

    public ScoutSiteReport ReadSite(string settlementId)
    {
        var settlement = string.IsNullOrEmpty(settlementId) || MBObjectManager.Instance == null ? null : Settlement.Find(settlementId);
        if (settlement == null) return new ScoutSiteReport();

        var report = new ScoutSiteReport
        {
            Found = true,
            Name = settlement.Name?.ToString() ?? string.Empty,
            Intel = _intel.ReadSettlement(settlement),
        };
        var parties = settlement.Parties;
        for (int i = 0; i < parties.Count; i++)
        {
            var party = parties[i];
            if (!SettlementDefenders.IsDefender(settlement, party)) continue;
            int foot = 0, archers = 0, horse = 0, horseArchers = 0;
            AddRoster(party.MemberRoster, ref foot, ref archers, ref horse, ref horseArchers);
            report.Defenders.Add(new ScoutDefender
            {
                Kind = party.IsGarrison ? ScoutDefenderKind.Garrison : party.IsMilitia ? ScoutDefenderKind.Militia : ScoutDefenderKind.Lord,
                Name = party.Name?.ToString() ?? string.Empty,
                Count = party.Party.NumberOfHealthyMembers,
                Foot = foot, Archers = archers, Horse = horse, HorseArchers = horseArchers,
            });
        }
        return report;
    }

    private ScoutSighting Sighting(MobileParty party, Vec2 origin)
    {
        var at = party.Position.ToVec2();
        int foot = 0, archers = 0, horse = 0, horseArchers = 0;
        AddRoster(party.MemberRoster, ref foot, ref archers, ref horse, ref horseArchers);
        var attached = party.AttachedParties;
        for (int i = 0; i < attached.Count; i++)
            AddRoster(attached[i].MemberRoster, ref foot, ref archers, ref horse, ref horseArchers);

        return new ScoutSighting
        {
            Intel = _intel.Read(party),
            Name = PlateName(party),
            Dx = at.x - origin.x,
            Dy = at.y - origin.y,
            IsVillager = party.IsVillager,
            IsCaravan = party.IsCaravan,
            IsAttachedToArmy = party.AttachedTo != null,
            IsLordWithHero = party.IsLordParty && party.LeaderHero != null,
            InSettlement = party.CurrentSettlement != null,
            Foot = foot, Archers = archers, Horse = horse, HorseArchers = horseArchers,
        };
    }

    /// <summary>The name <c>PartyNameplateVM</c> prints for the party, so a report row reads like the plate on the map.</summary>
    private static string PlateName(MobileParty party)
    {
        var army = party.Army;
        return FarWatchMath.PlateName(army != null && army.DoesLeaderPartyAndAttachedPartiesContain(party),
            party.ArmyName?.ToString(), party.LeaderHero?.Name?.ToString(), party.Name?.ToString());
    }

    /// <summary>Adds the healthy men of a roster to the four kind counts (<see cref="FarWatchMath.AddMen"/> over the formation class).</summary>
    private static void AddRoster(TroopRoster roster, ref int foot, ref int archers, ref int horse, ref int horseArchers)
    {
        for (int i = 0; i < roster.Count; i++)
        {
            int healthy = roster.GetElementNumber(i) - roster.GetElementWoundedNumber(i);
            if (healthy <= 0) continue;
            FarWatchMath.AddMen((int)roster.GetCharacterAtIndex(i).GetFormationClass(), healthy, ref foot, ref archers, ref horse, ref horseArchers);
        }
    }

    private static Hero? FindLivingHero(string heroId)
    {
        if (string.IsNullOrEmpty(heroId) || Campaign.Current == null) return null;
        var heroes = Hero.AllAliveHeroes;
        for (int i = 0; i < heroes.Count; i++)
            if (heroes[i].StringId == heroId) return heroes[i];
        return null;
    }
}
