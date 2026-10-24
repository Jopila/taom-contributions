using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.SwornAnew.Hooks;

/// <summary>
/// Thin boundary for the one write Sworn Anew makes (docs/features/sworn-anew.md, ADR-002). A lord who agrees to join the player's
/// kingdom is released by the engine for their new kingdom (<c>PrisonerReleaseCampaignBehavior.ClanChangedKingdom</c>, a listener of
/// <c>OnClanChangedKingdomEvent</c>). When the conversation was opened from the party screen, leaving that screen with Escape
/// refills the prison roster from the screen's snapshot and puts the released lord back (adapters.md, party-screen writes). So this
/// listener only notes that the conversation partner's clan defected into the player's kingdom, and asks the adapter to commit the
/// party screen when the conversation ends. It cannot commit inside the event: an <c>MbEvent</c> runs the newest listener first, so
/// TAOM's runs before vanilla's release. It calls no kingdom action (enlistment.md: never inside <c>OnClanChangedKingdomEvent</c>),
/// saves nothing and holds no state; the module still builds it with <c>new</c> for every campaign, because a campaign registers its
/// behaviors' events on the instance.
/// </summary>
public sealed class SwornAnewBehavior : CampaignBehaviorBase
{
    private readonly SwornAnewService _service;
    private readonly ISwornAnewAdapter _adapter;
    private readonly IModLogger _logger;

    public SwornAnewBehavior(SwornAnewService service, ISwornAnewAdapter adapter, IModLogger logger)
    {
        _service = service;
        _adapter = adapter;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    // Internal for TAOM.Tests (InternalsVisibleTo): the event cannot be raised outside a campaign.
    internal void OnClanChangedKingdom(Clan? clan, Kingdom? oldKingdom, Kingdom? newKingdom,
        ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification) =>
        Handle(detail == ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdomByDefection, clan, newKingdom);

    internal void Handle(bool byDefection, Clan? clan, Kingdom? newKingdom)
    {
        // An MbEvent invokes its listeners with no try, so a fault here would reach the engine's kingdom action. The adapter is asked
        // only for a defection: every other clan change reads nothing.
        try
        {
            if (_service.CommitsPartyScreenAfter(byDefection && _adapter.IsConversationPartnerJoiningPlayersKingdom(clan, newKingdom)))
                _adapter.CommitOpenPartyScreenWhenConversationEnds();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SwornAnew] kingdom change handling failed: {ex.Message}");
        }
    }
}
