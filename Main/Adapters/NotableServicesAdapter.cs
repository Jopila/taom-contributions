using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.NotableServices.Domain;
using Helpers;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="INotableServicesAdapter"/> (docs/features/notable-services.md). Computed TaleWorlds
/// getters are read through <c>?.</c> (adapters.md). <see cref="Snapshot"/> is cheap and runs in dialog conditions. The gift
/// screen is built from <c>PartyScreenLogicInitializationData.CreateBasicInitDataWithMainParty</c> with two dummy rosters on
/// the left, not from the loot screen, because the loot screen lets heroes of other clans move. The player's troops and
/// prisoners move into the dummy rosters (mode Shared, so both prisoner panels show), the screen's done step hands those rosters
/// to <c>onDone</c>, and Cancel resets the screen (<c>PartyScreenData.ResetUsing</c>), which puts every man back. Troop upgrades are off and the state is not donating,
/// so vanilla pays no influence and the Bonds of Battle hand-over patch (which reads that influence) sees nothing.
/// </summary>
public sealed class NotableServicesAdapter : INotableServicesAdapter
{
    private const string Tag = "[NotableServices]";

    private readonly IModLogger _logger;

    public NotableServicesAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public string? ConversationPartnerId => Hero.OneToOneConversationHero?.StringId;

    public NotableSnapshot? Snapshot(string heroId)
    {
        try
        {
            var hero = Find(heroId);
            if (hero == null || hero.IsDead || hero.IsPrisoner)
                return null;
            var settlement = hero.CurrentSettlement;
            var player = Player;
            var snapshot = new NotableSnapshot(hero.StringId)
            {
                IsNotable = hero.IsNotable,
                IsPreacher = hero.IsPreacher,
                Relation = player == null ? 0f : hero.GetRelationWithPlayer(),
            };
            if (settlement == null)
                return snapshot;

            var faction = settlement.MapFaction;
            var playerFaction = player?.MapFaction;
            snapshot.InPlayersSettlement = player?.CurrentSettlement == settlement;
            snapshot.SettlementId = settlement.StringId;
            snapshot.Kind = settlement.IsVillage ? SettlementKind.Village
                : settlement.IsTown ? SettlementKind.Town
                : settlement.IsCastle ? SettlementKind.Castle
                : SettlementKind.Other;
            snapshot.SettlementSide = FactionSides.Of(faction);
            snapshot.AtWarWithPlayer = faction != null && playerFaction != null && faction.IsAtWarWith(playerFaction);
            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} Snapshot('{heroId}') failed: {ex.Message}");
            return null;
        }
    }

    public IReadOnlyList<string> UnmetNotables(string settlementId)
    {
        var unmet = new List<string>();
        var notables = string.IsNullOrEmpty(settlementId) ? null : Settlement.Find(settlementId)?.Notables;
        if (notables == null)
            return unmet;
        for (var i = 0; i < notables.Count; i++)
        {
            var notable = notables[i];
            if (notable != null && notable.IsAlive && !notable.HasMet)
                unmet.Add(notable.StringId);
        }
        return unmet;
    }

    // No try: the read cannot throw (a static field and a list walk), and a catch that answered false would fail open, showing
    // the gift line and reopening the stale-screen path.
    public bool PartyScreenIsOpen => GameStateManager.Current?.LastOrDefault<PartyState>() != null;

    public bool PlayerHasMenToGive()
    {
        var party = MobileParty.MainParty;
        return (party?.MemberRoster?.TotalRegulars ?? 0) > 0 || (party?.PrisonRoster?.TotalRegulars ?? 0) > 0;
    }

    public int PlayerGold => Player?.Gold ?? 0;

    public bool PayNotable(string heroId, int price)
    {
        var player = Player;
        var notable = Find(heroId);
        if (player == null || notable == null || notable.IsDead || price <= 0 || player.Gold < price)
            return false;

        var before = player.Gold;
        try
        {
            GiveGoldAction.ApplyBetweenCharacters(player, notable, price);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} Paying '{heroId}' {price} gold failed: {ex.Message}");
        }

        // GiveGoldAction debits the purse before it raises its trade event, so a throw can arrive after the gold is gone.
        var taken = before - player.Gold;
        if (taken == price)
            return true;
        if (taken > 0)
            GiveBack(notable, player, taken);
        return false;
    }

    public void MarkMet(IReadOnlyList<string> heroIds)
    {
        foreach (var id in heroIds)
        {
            try
            {
                Find(id)?.SetHasMet();
            }
            catch (Exception ex)
            {
                _logger.LogError($"{Tag} Marking '{id}' as met failed: {ex.Message}");
            }
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
                _logger.LogError($"{Tag} The after-conversation step failed: {ex.Message}");
            }
        };
    }

    public void OpenGiftScreen(string settlementId, int menLimit, Func<GiftTransfer, bool> canTransfer,
        Func<GiftTally, bool> canFinish, Action<GiftTally> onDone)
    {
        var settlement = string.IsNullOrEmpty(settlementId) ? null : Settlement.Find(settlementId);
        if (settlement == null)
            return;
        try
        {
            var leftMembers = TroopRoster.CreateDummyTroopRoster();
            var leftPrisoners = TroopRoster.CreateDummyTroopRoster();
            var state = Game.Current.GameStateManager.CreateState<PartyState>();
            state.IsDonating = false;
            // Shared, never TroopsManage or QuestTroopManage: PartyVM.cs:2059 hides both prisoner panels in those two modes, while
            // the D hotkey would still move prisoners the player cannot see.
            state.PartyScreenMode = PartyScreenHelper.PartyScreenMode.Shared;
            var logic = new PartyScreenLogic();
            var data = PartyScreenLogicInitializationData.CreateBasicInitDataWithMainParty(
                leftMembers,
                leftPrisoners,
                PartyScreenLogic.TransferState.Transferable,
                PartyScreenLogic.TransferState.Transferable,
                PartyScreenLogic.TransferState.NotTransferable,
                (character, type, side, _) => canTransfer(Describe(character, type, side, leftMembers, leftPrisoners)),
                state.PartyScreenMode,
                leftPartyName: settlement.Name,
                header: new TextObject("{=taom_ns_screen_header}Give Men"),
                leftPartyMembersSizeLimit: menLimit,
                leftPartyPrisonersSizeLimit: menLimit,
                partyPresentationDoneButtonDelegate: (lm, lp, _, _, _, _, _, _, _) => Done(lm, lp, onDone),
                partyPresentationDoneButtonConditionDelegate: (lm, lp, _, _, _, _) => Finishable(lm, lp, canFinish),
                isTroopUpgradesDisabled: true);
            data.CanTalkToTroopDelegate = RefuseTalking;
            logic.Initialize(data);
            state.PartyScreenLogic = logic;
            Game.Current.GameStateManager.PushState(state);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} Opening the gift screen for '{settlementId}' failed: {ex.Message}");
        }
    }

    public void AddMilitia(string settlementId, float amount)
    {
        var settlement = string.IsNullOrEmpty(settlementId) ? null : Settlement.Find(settlementId);
        if (settlement != null)
            settlement.Militia += amount;
    }

    public void AddHearth(string settlementId, float amount)
    {
        var village = string.IsNullOrEmpty(settlementId) ? null : Settlement.Find(settlementId)?.Village;
        if (village != null)
            village.Hearth += amount;
    }

    public void AddProsperity(string settlementId, float amount)
    {
        var town = string.IsNullOrEmpty(settlementId) ? null : Settlement.Find(settlementId)?.Town;
        if (town != null)
            town.Prosperity += amount;
    }

    public string? PlayerHeroId => Player?.StringId;

    public void ShowGiftNotice(string settlementId, int men) =>
        Show(Plural(new TextObject("{=taom_ns_gift_notice}{SETTLEMENT} takes in the {COUNT} {?IS_PLURAL}people{?}person{\\?} you gave."), men)
            .SetTextVariable("SETTLEMENT", Settlement.Find(settlementId)?.Name ?? TextObject.GetEmpty()).SetTextVariable("COUNT", men));

    // Vanilla's "with notable" text and its variables, shown as a log line, as Good Deeds and Tokens of Esteem do
    // (GiftHeroAdapter.ShowRelationChange): the VALUE is the new relation, the MAGNITUDE the change.
    public void ShowRelationNotice(string heroId, int change)
    {
        try
        {
            var player = Player;
            var hero = Find(heroId);
            if (change == 0 || player == null || hero == null)
                return;

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
            _logger.LogError($"{Tag} Showing the relation change failed: {ex.Message}");
        }
    }

    public void ShowRogueryNotice(int xp) =>
        Show(new TextObject("{=taom_ns_roguery_notice}Dealing in prisoners teaches you something of the trade (+{XP} {SKILL_NAME} experience).")
            .SetTextVariable("XP", xp).SetTextVariable("SKILL_NAME", DefaultSkills.Roguery.Name));

    public void ShowIntroducedNotice(int count) =>
        Show(Plural(new TextObject("{=taom_ns_introduced_notice}You are introduced to {COUNT} more {?IS_PLURAL}notables{?}notable{\\?} here."), count)
            .SetTextVariable("COUNT", count));

    private static TextObject Plural(TextObject text, int count) => text.SetTextVariable("IS_PLURAL", count > 1 ? 1 : 0);

    private void Show(TextObject text)
    {
        try
        {
            InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Green));
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} Showing a notice failed: {ex.Message}");
        }
    }

    // Reads the screen's live rosters: the gift side holds the dummy rosters, so the men given are their regulars (a hero is
    // never allowed across, and TotalRegulars leaves heroes out anyway).
    private static GiftTransfer Describe(CharacterObject character, PartyScreenLogic.TroopType type,
        PartyScreenLogic.PartyRosterSide side, TroopRoster leftMembers, TroopRoster leftPrisoners) =>
        new(character.IsHero, type == PartyScreenLogic.TroopType.Prisoner, side == PartyScreenLogic.PartyRosterSide.Right,
            FactionSides.OfCharacter(character), leftMembers.TotalRegulars + leftPrisoners.TotalRegulars);

    private static GiftTally Tally(TroopRoster leftMembers, TroopRoster leftPrisoners) =>
        new(leftMembers.TotalRegulars, leftPrisoners.TotalRegulars);

    // The engine's commit step: PartyScreenLogic.DoneLogic calls it for Done, and again for the forced DoneLogic(true) the party
    // screen runs when a companion is removed (GauntletPartyScreen.OnCompanionRemoved), which skips the done condition. The map
    // bar cannot commit it: any non-Normal party screen locks the map-bar buttons (PartyNavigationElement.cs:24). The service
    // pays only the increase over the last commit. A fault must never keep the screen from closing, so it is logged and the
    // commit goes through.
    private bool Done(TroopRoster leftMembers, TroopRoster leftPrisoners, Action<GiftTally> onDone)
    {
        try
        {
            onDone(Tally(leftMembers, leftPrisoners));
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} Settling the gift failed: {ex.Message}");
        }
        return true;
    }

    private Tuple<bool, TextObject> Finishable(TroopRoster leftMembers, TroopRoster leftPrisoners, Func<GiftTally, bool> canFinish)
    {
        try
        {
            if (canFinish(Tally(leftMembers, leftPrisoners)))
                return new Tuple<bool, TextObject>(true, TextObject.GetEmpty());
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} The gift screen's done check failed: {ex.Message}");
        }
        return new Tuple<bool, TextObject>(false, new TextObject("{=taom_ns_gift_cap}They can take in only so many at once."));
    }

    private static bool RefuseTalking(Hero hero, PartyScreenLogic.TroopType type, PartyScreenLogic.PartyRosterSide side,
        PartyBase party, out TextObject reason)
    {
        reason = new TextObject("{=taom_ns_gift_no_talk}You cannot talk to anyone while giving men.");
        return false;
    }

    private void GiveBack(Hero from, Hero to, int amount)
    {
        try
        {
            GiveGoldAction.ApplyBetweenCharacters(from, to, amount, disableNotification: true);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} Giving back {amount} gold failed: {ex.Message}");
        }
    }

    // The main hero without a campaign is null here, never a throw (adapters.md).
    private static Hero? Player => CharacterObject.PlayerCharacter?.HeroObject;

    /// <summary>The partner of the open conversation when it is the asked hero, else a lookup.</summary>
    private static Hero? Find(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return null;
        var partner = Hero.OneToOneConversationHero;
        return partner != null && partner.StringId == heroId ? partner : Campaign.Current?.CampaignObjectManager?.Find<Hero>(heroId);
    }
}
