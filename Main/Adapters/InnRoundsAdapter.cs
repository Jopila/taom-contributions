using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;
using TAOM.Features.InnRounds.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="IInnRoundsAdapter"/> (docs/features/inn-rounds.md). Computed TaleWorlds getters are read
/// through <c>?.</c> (adapters.md). <see cref="Keeper"/> is cheap and runs in dialog conditions: the tavern keeper is not a hero,
/// so it is read from the conversation's character, and the town's owner is the leader of the clan that holds it. The price comes
/// from the town's market data as a buyer (<c>TownMarketData.GetPrice</c>), the tavern headcount from the town's location complex,
/// and the gold goes to the town through <c>GiveGoldAction.ApplyForCharacterToSettlement</c>.
/// </summary>
public sealed class InnRoundsAdapter : IInnRoundsAdapter
{
    private const string Tag = "[InnRounds]";
    private const string TavernLocationId = "tavern";
    private const string DrinkTextId = "str_taom_round_drink";

    private readonly IModLogger _logger;

    public InnRoundsAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public TavernSnapshot? Keeper()
    {
        try
        {
            var character = Campaign.Current?.ConversationManager?.OneToOneConversationCharacter;
            if (character == null)
                return null;

            var snapshot = new TavernSnapshot { IsTavernkeeper = character.Occupation == Occupation.Tavernkeeper };
            var player = Player;
            var settlement = player?.CurrentSettlement;
            if (!snapshot.IsTavernkeeper || settlement == null || !settlement.IsTown)
                return snapshot;

            snapshot.InTown = true;
            snapshot.SettlementId = settlement.StringId;
            snapshot.CultureId = settlement.Culture?.StringId ?? string.Empty;
            var faction = settlement.MapFaction;
            var playerFaction = player?.MapFaction;
            snapshot.AtWarWithPlayer = faction != null && playerFaction != null && faction.IsAtWarWith(playerFaction);
            snapshot.Relation = settlement.OwnerClan?.Leader?.GetRelationWithPlayer() ?? 0f;
            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} Reading the tavern keeper failed: {ex.Message}");
            return null;
        }
    }

    public int Today => Campaign.Current == null ? 0 : (int)CampaignTime.Now.ToDays;

    public int? DrinkPrice(string settlementId, string itemId)
    {
        var market = FindSettlement(settlementId)?.Town?.MarketData;
        var item = MBObjectManager.Instance?.GetObject<ItemObject>(itemId);
        if (market == null || item == null)
            return null;

        var price = market.GetPrice(item, Campaign.Current?.MainParty, isSelling: false);
        return price > 0 ? price : null;
    }

    public int TavernHeadcount(string settlementId)
    {
        // The same test vanilla's board games make (MissionBoardGameLogic): the mission's location is the tavern's.
        var tavern = FindSettlement(settlementId)?.LocationComplex?.GetLocationWithId(TavernLocationId);
        return tavern != null && CampaignMission.Current?.Location == tavern ? tavern.CharacterCount : 0;
    }

    public int PartyHeadcount() => Campaign.Current?.MainParty?.MemberRoster?.TotalManCount ?? 0;

    public string? PlayerHeroId => Player?.StringId;

    public int PlayerGold => Player?.Gold ?? 0;

    public bool PayTown(string settlementId, int price)
    {
        var player = Player;
        var settlement = FindSettlement(settlementId);
        if (player == null || settlement == null || price <= 0 || player.Gold < price)
            return false;

        var before = player.Gold;
        try
        {
            GiveGoldAction.ApplyForCharacterToSettlement(player, settlement, price);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} Paying '{settlementId}' {price} gold failed: {ex.Message}");
        }

        // GiveGoldAction debits the purse before it raises its trade event, so a throw can arrive after the gold is gone.
        var taken = before - player.Gold;
        if (taken == price)
            return true;
        if (taken > 0)
            GiveBack(settlement, player, taken);
        return false;
    }

    public float AddMorale(float amount)
    {
        var party = Campaign.Current?.MainParty;
        if (party == null)
            return 0f;

        // The setter clamps to [-100, 100], so the change that landed is read back, never assumed.
        var before = party.RecentEventsMorale;
        party.RecentEventsMorale = before + amount;
        return party.RecentEventsMorale - before;
    }

    public void ShowPartyRoundNotice(string cultureId, int morale) =>
        Show(new TextObject("{=taom_round_party_notice}You stand a round of {DRINK} for your company. Good cheer goes round (recent events morale +{MORALE}).")
            .SetTextVariable("DRINK", DrinkName(cultureId)).SetTextVariable("MORALE", morale));

    public void ShowHouseRoundNotice(string cultureId) =>
        Show(new TextObject("{=taom_round_house_notice}You stand a round of {DRINK} for the whole house. Word of it will spread.")
            .SetTextVariable("DRINK", DrinkName(cultureId)));

    public void ShowCharmXpNotice(int xp) =>
        Show(new TextObject("{=taom_round_xp_notice}Standing a round teaches you a little about winning a room (+{XP} {SKILL_NAME} experience).")
            .SetTextVariable("XP", xp).SetTextVariable("SKILL_NAME", DefaultSkills.Charm.Name));

    /// <summary>The culture's own name for its drink (a game-text variation keyed by the culture id), or plain "ale".</summary>
    private static TextObject DrinkName(string cultureId) =>
        !string.IsNullOrEmpty(cultureId) && GameTexts.TryGetText(DrinkTextId, out var named, cultureId)
            ? named
            : new TextObject("{=taom_round_drink_default}ale");

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

    private void GiveBack(Settlement from, Hero to, int amount)
    {
        try
        {
            GiveGoldAction.ApplyForSettlementToCharacter(from, to, amount, disableNotification: true);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} Giving back {amount} gold failed: {ex.Message}");
        }
    }

    // The main hero without a campaign is null here, never a throw (adapters.md).
    private static Hero? Player => CharacterObject.PlayerCharacter?.HeroObject;

    private static Settlement? FindSettlement(string settlementId) => string.IsNullOrEmpty(settlementId) ? null : Settlement.Find(settlementId);
}
