using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.GoodDeeds.Models;

namespace TAOM.Features.GoodDeeds.Hooks;

/// <summary>
/// Thin boundary for the relation Good Deeds earns from two campaign events (docs/features/good-deeds.md, ADR-002). It
/// listens to <c>CampaignEvents.HeroPrisonerReleased</c>, reads the detail, and for a release by choice asks the adapter for
/// the raw facts about the event's party (the player's own party in conversation, no party while the player's party screen is
/// open, a settlement the player's clan owns); the service decides which of them pays, the co-op host rule included. It also
/// listens to <c>CampaignEvents.TournamentFinished</c> and hands the service whether the player won and the town's settlement
/// id. No Harmony: both events fire for every route. This listener is Good Deeds' own, never a step inside TournamentRewards'
/// listener; an <c>MbEvent</c> makes the newest listener the head of its list, so it runs before TournamentRewards' and vanilla's
/// and writes only relation, which no later listener reads, so no order depends on it. Saves nothing, and the service holds no per-campaign state; the module still
/// builds this behavior with <c>new</c> for every campaign, as the campaign registers the events on the instance.
/// </summary>
public sealed class GoodDeedsBehavior : CampaignBehaviorBase
{
    private readonly GoodDeedsService _service;
    private readonly IGoodDeedsHeroAdapter _heroes;
    private readonly IModLogger _logger;

    public GoodDeedsBehavior(GoodDeedsService service, IGoodDeedsHeroAdapter heroes, IModLogger logger)
    {
        _service = service;
        _heroes = heroes;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, OnHeroPrisonerReleased);
        CampaignEvents.TournamentFinished.AddNonSerializedListener(this, OnTournamentFinished);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    // Internal for TAOM.Tests (InternalsVisibleTo): the event cannot be raised outside a campaign.
    internal void OnHeroPrisonerReleased(Hero? prisoner, PartyBase? party, IFaction? capturerFaction, EndCaptivityDetail detail,
        bool showNotification) =>
        Handle(prisoner?.StringId, detail == EndCaptivityDetail.ReleasedByChoice, party);

    internal void Handle(string? prisonerId, bool releasedByChoice, PartyBase? party)
    {
        // An MbEvent invokes its listeners with no try, so a fault here would reach the engine's release action. The party is
        // read only for a release by choice: an AI party's events read nothing beyond the detail.
        try
        {
            var source = releasedByChoice ? _heroes.ReadReleaseSource(party) : ReleaseSource.None;
            _service.OnPrisonerReleased(prisonerId, releasedByChoice, source);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[GoodDeeds] release handling failed: {ex.Message}");
        }
    }

    internal void OnTournamentFinished(CharacterObject? winner, MBReadOnlyList<CharacterObject>? participants, Town? town, ItemObject? prize) =>
        HandleTournament(winner != null && winner == CharacterObject.PlayerCharacter, town?.Settlement?.StringId);

    internal void HandleTournament(bool playerWon, string? townSettlementId)
    {
        // The same reason: an MbEvent has no try, and a fault here must not skip the listeners behind this one.
        try
        {
            _service.OnTournamentFinished(playerWon, townSettlementId);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[GoodDeeds] tournament handling failed: {ex.Message}");
        }
    }
}
