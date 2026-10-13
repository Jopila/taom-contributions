using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace TAOM.Adapters;

/// <summary>
/// The Eye of Sauron hunt's engine boundary (#765 M8; members verified against the installed v1.5.4 decompile:
/// MobileParty.cs, MobilePartyAi.cs, SetPartyAiAction.cs, NavigationHelper.cs, MobilePartyHelper.cs). Every call looks
/// its hero or party up by StringId in the live campaign, so a loaded save never meets an object from the last one.
/// Main thread only: the campaign's daily, hourly-party, frame and map-event callbacks.
/// </summary>
public sealed class HunterPartyAdapter : IHunterPartyAdapter
{
    private const int SpawnTries = 15;

    public double NowDays => CampaignTime.Now.ToDays;

    public string? PlayerKingdomId => Clan.PlayerClan?.Kingdom?.StringId;

    public float PlayerStrength
    {
        get
        {
            var main = Campaign.Current?.MainParty;
            if (main?.Party == null) return float.NaN;
            var army = main.Army;
            return army != null && army.LeaderParty == main ? army.EstimatedStrength : main.Party.EstimatedStrength;
        }
    }

    private static Hero? FindHero(string id) => Campaign.Current?.CampaignObjectManager.Find<Hero>(id);

    private static MobileParty? FindParty(string id) => Campaign.Current?.CampaignObjectManager.Find<MobileParty>(id);

    public HunterHeroSnapshot ReadHero(string heroId)
    {
        var hero = FindHero(heroId);
        if (hero == null) return HunterHeroSnapshot.Missing;

        var party = hero.PartyBelongedTo;
        var faction = hero.MapFaction;
        var playerKingdom = Clan.PlayerClan?.Kingdom;
        var settlement = hero.CurrentSettlement;
        return new HunterHeroSnapshot
        {
            Exists = true,
            IsAlive = hero.IsAlive,
            IsActive = hero.IsActive,
            IsFugitive = hero.IsFugitive,
            IsPrisoner = hero.IsPrisoner,
            IsHeldByAParty = hero.PartyBelongedToAsPrisoner != null,
            IsMarkedForDeath = hero.DeathMark != KillCharacterAction.KillCharacterActionDetail.None,
            FactionId = faction?.StringId,
            // Null-safe, and handles eliminated factions, constant war and stances (FactionManager.cs:144-159).
            AtWarWithPlayerKingdom = playerKingdom != null && FactionManager.IsAtWarAgainstFaction(playerKingdom, faction),
            IsInAParty = party != null,
            LedPartyId = party != null && party.LeaderHero == hero ? party.StringId : null,
            CanLeadParty = hero.CanLeadParty(),
            IsLord = hero.CharacterObject?.Occupation == Occupation.Lord,
            SettlementBlocksSpawn = settlement?.Town != null && (settlement.Town.HasTournament || settlement.IsUnderSiege),
        };
    }

    public HuntPartySnapshot ReadParty(string partyId)
    {
        var party = FindParty(partyId);
        if (party?.Party == null) return HuntPartySnapshot.Missing;

        var main = Campaign.Current?.MainParty;
        return new HuntPartySnapshot
        {
            Exists = true,
            IsActive = party.IsActive,
            LeaderHeroId = party.LeaderHero?.StringId,
            InMapEvent = party.MapEvent != null,
            InArmy = party.Army != null,
            // BesiegedSettlement dereferences BesiegerCamp.SiegeEvent unguarded (MobileParty.cs:1172).
            Besieging = party.BesiegerCamp != null,
            InSettlement = party.CurrentSettlement != null,
            IsDisbanding = party.IsDisbanding,
            AiPinned = party.Ai?.DoNotMakeNewDecisions ?? false,
            IsEngagingPlayer = main != null && party.DefaultBehavior == AiBehavior.EngageParty && party.TargetParty == main,
            HeadCount = party.MemberRoster?.TotalManCount ?? 0,
            Strength = party.Party.EstimatedStrength,
            Food = party.ItemRoster?.TotalFood ?? 0,
        };
    }

    public bool PinAndEngagePlayer(string partyId)
    {
        var party = FindParty(partyId);
        var main = Campaign.Current?.MainParty;
        if (party?.Ai == null || !party.IsActive || main == null || !main.IsActive) return false;

        // TargetSettlement stays null: a pinned party with a target settlement keeps vanilla's initiative exception
        // (MobilePartyAi.cs:486). The engage call resets all movement state, so the caller re-issues it only when lost.
        SetPartyAiAction.GetActionForEngagingParty(party, main, MobileParty.NavigationType.Default, false);
        party.Ai.CheckPartyNeedsUpdate();
        party.Ai.SetDoNotMakeNewDecisions(true);
        return true;
    }

    public void ReleasePin(string partyId)
    {
        var party = FindParty(partyId);
        if (party?.Ai == null) return;

        party.Ai.SetDoNotMakeNewDecisions(false);
        // A party still inside the ending battle refreshes on its own next AI tick.
        if (party.IsActive && party.MapEvent == null)
            party.Ai.CheckPartyNeedsUpdate();
    }

    public void InvalidateSpeed(string partyId) => FindParty(partyId)?.UpdateVersionNo();

    public string? SpawnHunterParty(string heroId)
    {
        var hero = FindHero(heroId);
        var main = Campaign.Current?.MainParty;
        if (hero == null || main == null || !main.IsActive || hero.PartyBelongedTo != null) return null;

        if (!TryFindPointOutOfSight(main, out var at)) return null;
        if (hero.GovernorOf != null)
            ChangeGovernorAction.RemoveGovernorOf(hero);

        // Re-checked in the same tick, right before the call: SpawnLordParty does no hero check of its own.
        if (hero.PartyBelongedTo != null) return null;
        return MobilePartyHelper.SpawnLordParty(hero, at, 1f)?.StringId;
    }

    /// <summary>A reachable point between 1.6 and 2.5 of the player's sight from him, straight-line beyond 1.5.</summary>
    private static bool TryFindPointOutOfSight(MobileParty main, out CampaignVec2 point)
    {
        float sight = main.SeeingRange;
        point = main.Position;
        if (!(sight > 0f) || float.IsInfinity(sight)) return false;

        for (int i = 0; i < SpawnTries; i++)
        {
            var candidate = NavigationHelper.FindReachablePointAroundPosition(main.Position, MobileParty.NavigationType.Default,
                2.5f * sight, 1.6f * sight);
            if (!candidate.IsValid() || candidate.Distance(main.Position) < 1.5f * sight) continue;
            point = candidate;
            return true;
        }
        return false;
    }

    public float GetTroopPower(string troopId, string leaderHeroId)
    {
        var troop = MBObjectManager.Instance?.GetObject<CharacterObject>(troopId);
        var hero = FindHero(leaderHeroId);
        var model = Campaign.Current?.Models?.MilitaryPowerModel;
        if (troop == null || hero == null || model == null) return float.NaN;
        return model.GetTroopPower(troop, BattleSideEnum.Defender, MapEvent.PowerCalculationContext.Estimated, hero.PowerModifier);
    }

    public int AddTroops(string partyId, string troopId, int count)
    {
        var party = FindParty(partyId);
        var troop = MBObjectManager.Instance?.GetObject<CharacterObject>(troopId);
        if (party?.MemberRoster == null || troop == null || count <= 0) return 0;
        party.MemberRoster.AddToCounts(troop, count);
        return count;
    }

    public void TopUpFood(string partyId, int targetFood)
    {
        var party = FindParty(partyId);
        var grain = DefaultItems.Grain;
        if (party?.ItemRoster == null || grain == null) return;
        int missing = targetFood - party.ItemRoster.TotalFood;
        if (missing > 0)
            party.ItemRoster.AddToCounts(grain, missing);
    }

    public HuntSightReading ReadSight(string partyId)
    {
        var party = FindParty(partyId);
        var main = Campaign.Current?.MainParty;
        if (party?.Party == null || main?.Party == null) return HuntSightReading.Missing;

        return new HuntSightReading
        {
            Valid = true,
            HunterActive = party.IsActive,
            PlayerActive = main.IsActive,
            HunterInMapEvent = party.MapEvent != null,
            PlayerInMapEvent = main.MapEvent != null,
            PlayerAtSea = main.IsCurrentlyAtSea,
            SeeingRange = main.SeeingRange,
            Distance = party.Position.Distance(main.Position),
            // Speed asserts on an inactive party (MobileParty.cs:419-431).
            PlayerSpeed = main.IsActive ? main.Speed : 0f,
        };
    }

    public HuntStepContext ReadStepContext(string partyId)
    {
        var party = FindParty(partyId);
        var main = Campaign.Current?.MainParty;
        if (party?.Party == null || main?.Party == null) return HuntStepContext.Blocked("a party is missing");

        string? blocked = HunterBlock(party, main) ?? PlayerBlock(main);
        if (blocked != null) return HuntStepContext.Blocked(blocked);
        if (party.CurrentNavigationFace.FaceIslandIndex != main.CurrentNavigationFace.FaceIslandIndex)
            return HuntStepContext.Blocked("on another island");
        if (!party.RecalculateLongTermPath()) return HuntStepContext.Blocked("no path to the player");

        var path = party.Path;
        int n = path?.Size ?? 0;
        if (n <= 0) return HuntStepContext.Blocked("empty path");
        var xs = new float[n];
        var ys = new float[n];
        for (int i = 0; i < n; i++)
        {
            xs[i] = path!.PathPoints[i].x;
            ys[i] = path.PathPoints[i].y;
        }

        var hunterAt = party.Position.ToVec2();
        var playerAt = main.Position.ToVec2();
        return new HuntStepContext
        {
            SeeingRange = main.SeeingRange,
            HunterX = hunterAt.x,
            HunterY = hunterAt.y,
            PlayerX = playerAt.x,
            PlayerY = playerAt.y,
            PathX = xs,
            PathY = ys,
        };
    }

    private static string? HunterBlock(MobileParty party, MobileParty main)
    {
        if (!party.IsActive) return "hunter inactive";
        if (party.MapEvent != null) return "hunter in battle";
        if (party.BesiegerCamp != null) return "hunter in a siege";
        if (party.Army != null || party.AttachedTo != null) return "hunter in an army";
        if (party.CurrentSettlement != null) return "hunter in a settlement";
        if (party.IsCurrentlyAtSea || party.IsTransitionInProgress) return "hunter at sea";
        if (party.IsVisible) return "hunter visible";
        if (party.DefaultBehavior != AiBehavior.EngageParty || party.TargetParty != main) return "hunter not engaging the player";
        return null;
    }

    private static string? PlayerBlock(MobileParty main)
    {
        // IsActive is the enlistment guard: MobilePartyAttachmentAdapter hides an enlisted player that way.
        if (!main.IsActive) return "player inactive";
        if (main.MapEvent != null || main.BesiegerCamp != null) return "player in battle or siege";
        if (main.CurrentSettlement != null) return "player in a settlement";
        if (main.Army != null && main.Army.LeaderParty != main) return "player in another lord's army";
        if (main.IsCurrentlyAtSea) return "player at sea";
        if (PlayerEncounter.Current != null) return "player in an encounter";
        if (Campaign.Current?.TrueSight == true) return "true sight";
        return null;
    }

    public HuntLanding ResolveLanding(string partyId, float x, float y, float enemyClearance)
    {
        var party = FindParty(partyId);
        var main = Campaign.Current?.MainParty;
        if (party == null || main == null) return default;

        int island = main.CurrentNavigationFace.FaceIslandIndex;
        var point = new CampaignVec2(new Vec2(x, y), true);
        if (!OnIsland(point, island))
        {
            // On failure this returns the centre unchanged (NavigationHelper.cs:159-219).
            var nudged = NavigationHelper.FindReachablePointAroundPosition(point, MobileParty.NavigationType.Default, 2f);
            if (nudged == point || !OnIsland(nudged, island)) return default;
            point = nudged;
        }

        var hunterFaction = party.MapFaction;
        var search = MobileParty.StartFindingLocatablesAroundPosition(point.ToVec2(), enemyClearance);
        for (var other = MobileParty.FindNextLocatable(ref search); other != null; other = MobileParty.FindNextLocatable(ref search))
        {
            if (other != party && other.IsActive && hunterFaction != null && other.MapFaction != null
                && other.MapFaction.IsAtWarWith(hunterFaction))
                return default;
        }

        var at = point.ToVec2();
        return new HuntLanding(true, at.x, at.y);
    }

    private static bool OnIsland(CampaignVec2 point, int island) => point.IsValid() && point.Face.FaceIslandIndex == island;

    public void Relocate(string partyId, float x, float y)
    {
        var party = FindParty(partyId);
        if (party == null) return;

        var point = new CampaignVec2(new Vec2(x, y), true);
        // The setter only moves the locator (MobileParty.cs:1064-1078); the navigation face follows the position.
        party.Position = point;
        foreach (var attached in party.AttachedParties)
            attached.Position = point;
        // Without the rebuild a hunter chasing a still player walks back to its old waypoint (MobileParty.cs:3772-3795).
        party.RecalculateLongTermPath();
        party.Ai.CheckPartyNeedsUpdate();
    }

    public void Announce(HuntAnnouncement announcement, string heroId)
    {
        var text = announcement switch
        {
            HuntAnnouncement.StartFree => new TextObject("{=taom_eye_hunt_free}The Eye of Sauron has turned upon you. {HUNTER} rides out with a host to destroy you."),
            HuntAnnouncement.StartEvil => new TextObject("{=taom_eye_hunt_evil}Elrond has sent {HUNTER} with a host of the Eldar to bring you down."),
            HuntAnnouncement.Won => new TextObject("{=taom_eye_hunt_won}{HUNTER} is beaten. The hunt is over, for now."),
            _ => new TextObject("{=taom_eye_hunt_over}The hunt is over. The Eye turns away, for now."),
        };
        var name = FindHero(heroId)?.Name ?? new TextObject(heroId);
        text.SetTextVariable("HUNTER", name);
        string message = text.ToString();

        // Always a chat line; a one-button popup when the map is free (the Messenger feature's availability test),
        // otherwise the banner. Never a scene notification: it NREs for custom races.
        InformationManager.DisplayMessage(new InformationMessage(message));
        if (IsMapFree())
        {
            InformationManager.ShowInquiry(new InquiryData(name.ToString(), message, true, false,
                GameTexts.FindText("str_ok").ToString(), string.Empty, null, null), pauseGameActiveState: true);
        }
        else
        {
            MBInformationManager.AddQuickInformation(text, extraTimeInMs: 3000);
        }
    }

    /// <summary>On the open map, no encounter, no menu, and no other popup open (no stacking).</summary>
    private static bool IsMapFree() =>
        PartyBase.MainParty != null
        && PlayerEncounter.Current == null
        && GameStateManager.Current?.ActiveState is MapState { AtMenu: false }
        && !InformationManager.IsAnyInquiryActive();

    public string HeroName(string heroId) => FindHero(heroId)?.Name?.ToString() ?? heroId;
}
