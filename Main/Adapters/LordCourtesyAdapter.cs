using System;
using System.Collections.Generic;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;
using TAOM.Features.Messengers;
using TAOM.Features.LordCourtesies.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="ILordCourtesyAdapter"/>. Computed TaleWorlds getters are read through
/// <c>?.</c> (adapters.md). <see cref="Snapshot"/> is cheap and runs in dialog conditions. The gift screen is the
/// vanilla donate screen's setup (<c>PartyScreenHelper.OpenScreenAsDonateTroops</c>) with differences: the state is
/// not flagged as donating, so vanilla pays no influence (the Bonds of Battle hand-over patch reads that influence
/// and so also pays nothing; the service pays the hand-over itself), the done rule is written here because the
/// vanilla one is private, and talking is refused as on the manage-troops screen. The lord's live rosters sit on the
/// left, so a donation lands in his party directly.
/// </summary>
public sealed class LordCourtesyAdapter : ILordCourtesyAdapter
{
    private readonly IModLogger _logger;
    private readonly IMessengerConversationState _messenger;

    public LordCourtesyAdapter(IModLogger logger, IMessengerConversationState messenger)
    {
        _logger = logger;
        _messenger = messenger;
    }

    public string? ConversationPartnerId => Hero.OneToOneConversationHero?.StringId;

    public LordCourtesySnapshot? Snapshot(string heroId)
    {
        try
        {
            var hero = Find(heroId);
            if (hero == null || hero.IsDead || hero.IsPrisoner)
                return null;
            var clan = hero.Clan;
            var party = hero.PartyBelongedTo;
            var playerFaction = Hero.MainHero?.MapFaction;
            var faction = hero.MapFaction;
            return new LordCourtesySnapshot(hero.StringId)
            {
                ClanId = clan?.StringId,
                KingdomId = clan?.Kingdom?.StringId,
                CultureId = hero.Culture?.StringId,
                IsInPlayerClan = clan != null && clan == Clan.PlayerClan,
                AtWarWithPlayer = faction != null && playerFaction != null && faction.IsAtWarWith(playerFaction),
                LeadsOwnParty = party != null && party.IsLordParty && party.LeaderHero == hero,
                IsWithPlayer = IsWithPlayer(party),
                HasRoom = party != null && party.Party.NumberOfAllMembers < party.Party.PartySizeLimit,
                IsDisbanding = party != null && party.IsDisbanding,
                PlayerHasTroops = (MobileParty.MainParty?.MemberRoster?.TotalRegulars ?? 0) > 0,
                Relation = (int)hero.GetRelationWithPlayer(),
                Holdings = clan?.Settlements?.Count ?? 0,
                Fortifications = CountFortifications(clan),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"[LordCourtesies] Snapshot('{heroId}') failed: {ex.Message}");
            return null;
        }
    }

    public IReadOnlyList<TroopCount> MemberTroops(string heroId)
    {
        var roster = Find(heroId)?.PartyBelongedTo?.MemberRoster;
        if (roster == null)
            return new List<TroopCount>();
        var entries = new List<TroopCount>(roster.Count);
        for (var i = 0; i < roster.Count; i++)
        {
            var element = roster.GetElementCopyAtIndex(i);
            if (element.Character != null)
                entries.Add(new TroopCount(element.Character.StringId, element.Number, element.Character.IsHero));
        }
        return entries;
    }

    public string LordName(string heroId) => Find(heroId)?.Name?.ToString() ?? string.Empty;

    public string? RelationOwnerId(string heroId)
    {
        try
        {
            var hero = Find(heroId);
            var player = Hero.MainHero;
            if (hero == null || player == null)
                return null;
            Campaign.Current.Models.DiplomacyModel.GetHeroesForEffectiveRelation(player, hero, out _, out var effective);
            return (effective ?? hero).StringId;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[LordCourtesies] RelationOwnerId('{heroId}') failed: {ex.Message}");
            return null;
        }
    }

    // Mirrors PartyScreenLogic.cs:834-860: a float sum of count times the model's per-troop influence, truncated.
    public int GiftInfluence(IReadOnlyList<TroopCount> donated)
    {
        try
        {
            var model = Campaign.Current.Models.PrisonerDonationModel;
            var settlement = Hero.MainHero?.CurrentSettlement;
            var sum = 0f;
            foreach (var stack in donated)
            {
                var character = MBObjectManager.Instance?.GetObject<CharacterObject>(stack.CharacterId);
                if (character != null)
                    sum += stack.Count * model.CalculateInfluenceGainAfterTroopDonation(PartyBase.MainParty, character, settlement);
            }
            return (int)sum;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[LordCourtesies] GiftInfluence failed: {ex.Message}");
            return 0;
        }
    }

    public void RunAfterConversation(Action action)
    {
        var manager = Campaign.Current?.ConversationManager;
        if (manager == null)
            return;
        manager.ConversationEndOneShot += () =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _logger.LogError($"[LordCourtesies] The after-conversation step failed: {ex.Message}");
            }
        };
    }

    public void OpenGiftScreen(string heroId, Action onDone)
    {
        var party = Find(heroId)?.PartyBelongedTo;
        if (party == null)
            return;
        try
        {
            var state = Game.Current.GameStateManager.CreateState<PartyState>();
            state.IsDonating = false;
            state.PartyScreenMode = PartyScreenHelper.PartyScreenMode.TroopsManage;
            var logic = new PartyScreenLogic();
            var data = PartyScreenLogicInitializationData.CreateBasicInitDataWithMainPartyAndOther(
                party,
                PartyScreenLogic.TransferState.Transferable,
                PartyScreenLogic.TransferState.NotTransferable,
                PartyScreenLogic.TransferState.Transferable,
                PartyScreenHelper.DonateModeTroopTransferableDelegate,
                state.PartyScreenMode,
                header: new TextObject("{=taom_lc_gift_header}Gift Troops"),
                partyPresentationDoneButtonDelegate: (_, _, _, _, _, _, _, _, _) => GiftDone(onDone),
                partyPresentationDoneButtonConditionDelegate: GiftDonePossible);
            logic.Initialize(data);
            logic.CanTalkToHeroDelegate = RefuseTalking;
            state.PartyScreenLogic = logic;
            Game.Current.GameStateManager.PushState(state);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[LordCourtesies] Opening the gift screen for '{heroId}' failed: {ex.Message}");
        }
    }

    public void ShowHoldingsMarked(int count)
    {
        try
        {
            var text = new TextObject("{=taom_lc_holdings_marked}{COUNT} {?IS_PLURAL}holdings{?}holding{\\?} marked on your map.");
            text.SetTextVariable("IS_PLURAL", count > 1 ? 1 : 0);
            text.SetTextVariable("COUNT", count);
            MBInformationManager.AddQuickInformation(text);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[LordCourtesies] Showing a notice failed: {ex.Message}");
        }
    }

    // The engine's commit step: PartyScreenLogic.DoneLogic calls it for Done and map-bar navigation (talking from this screen is refused). The
    // troops are already in the lord's live roster; the service diffs them and pays. A fault must never keep the
    // screen from closing, so it is logged and the commit goes through.
    private bool GiftDone(Action onDone)
    {
        try
        {
            onDone();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[LordCourtesies] Settling the gift failed: {ex.Message}");
        }
        return true;
    }

    // Only the lord's room is checked. The transfer delegate keeps heroes from moving and lets a troop move from the
    // player's side only, so nothing can be taken back; heroes and prisoners stay on the screen but cannot move
    // (the delegate; PrisonerTransferState is NotTransferable), and troop upgrades are off (isTroopUpgradesDisabled
    // defaults to true for this initialiser).
    private static Tuple<bool, TextObject> GiftDonePossible(TroopRoster leftMemberRoster, TroopRoster leftPrisonRoster,
        TroopRoster rightMemberRoster, TroopRoster rightPrisonRoster, int leftLimitNum, int rightLimitNum) =>
        leftMemberRoster.TotalManCount > leftLimitNum
            ? new Tuple<bool, TextObject>(false, new TextObject("{=taom_lc_gift_full}That party has no room for so many."))
            : new Tuple<bool, TextObject>(true, TextObject.GetEmpty());

    private static bool RefuseTalking(Hero hero, PartyScreenLogic.TroopType type, PartyScreenLogic.PartyRosterSide side,
        PartyBase party, out TextObject reason)
    {
        reason = new TextObject("{=taom_lc_gift_no_talk}You cannot talk to anyone while giving troops.");
        return false;
    }

    /// <summary>
    /// True when the lord's party is with the player's, after vanilla's donate menus: in the same settlement, or
    /// close enough to join the player's encounter, in the same map event (none counts), and no battle simulation
    /// running. Not copied from vanilla: its check that the encountered party is not at war with the player (the lord
    /// himself is refused at war instead). Always false while a messenger conversation is open: that conversation
    /// starts a real encounter and, for a target in a settlement, moves the main party into it, so the engine state
    /// looks like a visit; Messengers owns the flag that tells them apart.
    /// </summary>
    private bool IsWithPlayer(MobileParty? party)
    {
        var main = MobileParty.MainParty;
        if (_messenger.IsOpen || party == null || main == null || party.MapEvent != main.MapEvent
            || PlayerEncounter.Current?.BattleSimulation != null)
            return false;
        var here = main.CurrentSettlement;
        if (here != null || party.CurrentSettlement != null)
            return party.CurrentSettlement == here;
        var radius = Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius;
        return party.Position.DistanceSquared(main.MapEvent?.Position ?? main.Position) < radius * radius;
    }

    private static int CountFortifications(Clan? clan)
    {
        var count = 0;
        if (clan?.Settlements != null)
            foreach (var settlement in clan.Settlements)
                if (settlement.IsFortification)
                    count++;
        return count;
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
