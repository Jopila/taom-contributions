using System.Collections.Generic;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TAOM.Features.FromTheAshes;

namespace TAOM.Adapters;

/// <summary>
/// The engine side of From the Ashes. It runs only inside a campaign (the menus and the hourly tick exist only there) and
/// throws nothing of its own; the hooks and the service wrap each call (docs/features/from-the-ashes.md).
/// <see cref="CurrentSite"/> is the one read that guards a missing campaign, hero or party, because the hourly reconcile
/// can run while the player is dead or a prisoner.
/// </summary>
public sealed class AshesWorldAdapter : IAshesWorldAdapter
{
    // Names are TextObjects rendered through the localization manager; render each once per object and language, because
    // the wait menu looks at the site on every frame.
    private readonly NameRender _settlementNames = new();
    private readonly NameRender _buildingNames = new();

    public AshesSiteSnapshot? CurrentSite()
    {
        if (Campaign.Current == null)
            return null;
        var hero = Hero.MainHero;
        var party = MobileParty.MainParty;
        if (hero == null || party == null || hero.IsPrisoner)
            return null;
        var settlement = Settlement.CurrentSettlement;
        if (settlement == null)
            return null;

        var kingdom = Clan.PlayerClan?.Kingdom;
        var site = new AshesSiteSnapshot
        {
            SettlementId = settlement.StringId,
            SettlementName = _settlementNames.Of(settlement.Name),
            IsVillage = settlement.IsVillage,
            IsFortification = settlement.IsFortification,
            PlayerClanOwns = settlement.OwnerClan != null && settlement.OwnerClan == Clan.PlayerClan,
            SameMapFactionAsPlayer = ReferenceEquals(settlement.MapFaction, hero.MapFaction),
            UnderSiegeOrRaid = settlement.IsUnderSiege || settlement.IsUnderRaid
                || (settlement.IsVillage && settlement.Village.VillageState == Village.VillageStates.BeingRaided),
            HealthyTroops = party.Party.NumberOfHealthyMembers,
            Prisoners = HealthyCaptives(party.PrisonRoster),
            PlayerKingdomId = kingdom?.StringId ?? "",
            PlayerKingdomCultureId = kingdom?.Culture?.StringId ?? "",
            PlayerCultureId = hero.Culture?.StringId ?? "",
            InForeignArmy = party.Army != null && party.Army.LeaderParty != party,
            MainPartyInMapEvent = party.MapEvent != null,
        };

        if (settlement.IsVillage)
            site.IsLooted = settlement.Village.VillageState == Village.VillageStates.Looted;
        var town = settlement.IsFortification ? settlement.Town : null;
        if (town != null)
        {
            site.OwnerUnassigned = town.IsOwnerUnassigned;
            var head = HeadProject(town);
            site.HasProjectInProgress = head != null;
            site.ProjectName = head == null ? "" : _buildingNames.Of(head.Name);
        }

        return site;
    }

    public int CountMaterial(string itemId)
    {
        var item = FindItem(itemId);
        if (item == null)
            return 0;
        var count = MobileParty.MainParty.ItemRoster.GetItemNumber(item);
        var stash = UsableStash();
        return stash == null ? count : count + stash.GetItemNumber(item);
    }

    public bool TakeMaterial(string itemId, int count)
    {
        var item = FindItem(itemId);
        if (item == null || count < 0 || CountMaterial(itemId) < count)
            return false;

        var party = MobileParty.MainParty.ItemRoster;
        var fromParty = System.Math.Min(count, party.GetItemNumber(item));
        if (fromParty > 0)
            party.AddToCounts(item, -fromParty);
        var rest = count - fromParty;
        if (rest > 0)
            UsableStash()!.AddToCounts(item, -rest);
        return true;
    }

    public void ReturnMaterial(string itemId, int count)
    {
        var item = FindItem(itemId);
        if (item != null && count > 0)
            MobileParty.MainParty.ItemRoster.AddToCounts(item, count);
    }

    public bool RestoreVillage(string settlementId)
    {
        var settlement = Settlement.Find(settlementId);
        if (settlement == null || !settlement.IsVillage)
            return false;
        // The engine's own daily heal: it clamps to full health, sets the state back to Normal and adds the militia.
        // ChangeVillageStateAction alone would leave the health where the looting put it.
        IncreaseSettlementHealthAction.Apply(settlement, 1f);
        return settlement.Village.VillageState == Village.VillageStates.Normal;
    }

    public bool AddConstruction(string settlementId, float points)
    {
        var town = Settlement.Find(settlementId)?.Town;
        if (town == null || town.Settlement.IsUnderSiege)
            return false;
        // The head of the queue only, below level 3, the way BuildingsCampaignBehavior.TickCurrentBuildingForTown does it.
        var head = HeadProject(town);
        if (head == null)
            return false;
        head.BuildingProgress += points;
        BuildingHelper.CheckIfBuildingIsComplete(head);
        return true;
    }

    public void ChangeNotableRelations(string settlementId, int delta)
    {
        var notables = Settlement.Find(settlementId)?.Notables;
        if (notables == null || delta == 0)
            return;
        // A copy: a relation change can move a notable between rosters.
        foreach (var notable in new List<Hero>(notables))
            ChangeRelationAction.ApplyPlayerRelation(notable, delta, affectRelatives: false, showQuickNotification: false);
    }

    public void ChangeOwnerRelation(string settlementId, int delta)
    {
        var owner = Settlement.Find(settlementId)?.OwnerClan?.Leader;
        if (owner == null || owner == Hero.MainHero || delta == 0)
            return;
        ChangeRelationAction.ApplyPlayerRelation(owner, delta, affectRelatives: false, showQuickNotification: false);
    }

    public IReadOnlyList<string> PartyHeroIds()
    {
        var ids = new List<string>();
        var roster = MobileParty.MainParty?.MemberRoster;
        if (roster == null)
            return ids;
        for (var i = 0; i < roster.Count; i++)
        {
            var hero = roster.GetCharacterAtIndex(i)?.HeroObject;
            if (hero != null)
                ids.Add(hero.StringId);
        }

        return ids;
    }

    /// <summary>The thralls' labour: regular captives that are not wounded. A captive lord is a hero and never works.</summary>
    private static int HealthyCaptives(TroopRoster prisoners) =>
        prisoners == null ? 0 : prisoners.TotalRegulars - prisoners.TotalWoundedRegulars;

    /// <summary>The head of the build queue when the company can advance it: a project below level 3.</summary>
    private static Building? HeadProject(Town town)
    {
        var queue = town.BuildingsInProgress;
        if (queue == null || queue.Count == 0)
            return null;
        var head = queue.Peek();
        return head != null && head.CurrentLevel < 3 ? head : null;
    }

    /// <summary>
    /// The stash of the fortification that owns the settlement the party is in (the village's own castle or town, or the fief
    /// itself), only when the player's clan owns that fortification and its owner is assigned (vanilla's stash gate, which
    /// <c>PlayerTownVisitCampaignBehavior</c> applies to the keep's stash option). Null otherwise.
    /// </summary>
    private static ItemRoster? UsableStash()
    {
        var settlement = Settlement.CurrentSettlement;
        var fortification = settlement == null ? null : settlement.IsVillage ? settlement.Village.Bound : settlement;
        if (fortification == null || !fortification.IsFortification || fortification.OwnerClan != Clan.PlayerClan
            || fortification.Town.IsOwnerUnassigned)
            return null;
        return fortification.Stash;
    }

    private static ItemObject? FindItem(string itemId) =>
        string.IsNullOrEmpty(itemId) ? null : MBObjectManager.Instance?.GetObject<ItemObject>(itemId);

    /// <summary>Renders a TextObject once per object and active language; the same string instance comes back until either changes.</summary>
    private sealed class NameRender
    {
        private TextObject? _source;
        private int _language = -1;
        private string _text = "";

        public string Of(TextObject? name)
        {
            if (name == null)
                return "";
            var language = MBTextManager.GetActiveTextLanguageIndex();
            if (!ReferenceEquals(name, _source) || language != _language)
            {
                _source = name;
                _language = language;
                _text = name.ToString() ?? "";
            }

            return _text;
        }
    }
}
