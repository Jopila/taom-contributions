using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.BondsOfBattle;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.LordCourtesies.Domain;

namespace TAOM.Features.LordCourtesies;

/// <summary>
/// The two courtesies a player can pay a free lord in conversation (docs/features/lord-courtesies.md): a gift of
/// troops and a question about his clan's holdings. Eligibility reads the cheap <see cref="LordCourtesySnapshot"/>
/// once per check, so it is safe in a dialog condition. The gift screen waits for the conversation to end. The
/// gift is paid by Bonds of Battle's hand-over rule (<see cref="TroopHandoverService"/>) on what vanilla's donate
/// screen would report, settled at each commit of the screen.
/// </summary>
public sealed class LordCourtesiesService
{
    private readonly ILordCourtesyAdapter _lords;
    private readonly IArmouryPlayerAdapter _player;
    private readonly IAlignmentService _alignment;
    private readonly ILordCourtesiesSettingsProvider _settings;
    private readonly ILordCourtesiesConfigProvider _config;
    private readonly TroopHandoverService _handover;
    private readonly IMapRevealAdapter _reveal;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _server;
    private readonly IModLogger _logger;

    public LordCourtesiesService(ILordCourtesyAdapter lords, IArmouryPlayerAdapter player, IAlignmentService alignment,
        ILordCourtesiesSettingsProvider settings, ILordCourtesiesConfigProvider config,
        TroopHandoverService handover, IMapRevealAdapter reveal, ICoopSessionProvider coop,
        IDedicatedServerProvider server, IModLogger logger)
    {
        _lords = lords;
        _player = player;
        _alignment = alignment;
        _settings = settings;
        _config = config;
        _handover = handover;
        _reveal = reveal;
        _coop = coop;
        _server = server;
        _logger = logger;
    }

    /// <summary>
    /// A gift is allowed unless the two sides are Free against Evil; a Neutral side may give or receive. This is
    /// deliberately not <c>IAlignmentService.AreEnemyAlignments</c>, which calls Neutral the enemy of everyone.
    /// </summary>
    public static bool SidesAllowGift(FactionSide player, FactionSide lord) =>
        !((player == FactionSide.Free && lord == FactionSide.Evil) || (player == FactionSide.Evil && lord == FactionSide.Free));

    /// <summary>
    /// Whether the lord's reply to the holdings question is the refusal line. Revealed and Landless have their own
    /// replies; every other answer, including none, falls to the refusal so the reply state always has a line.
    /// </summary>
    public static bool RepliesWithRefusal(DomainOutcome outcome) =>
        outcome != DomainOutcome.Revealed && outcome != DomainOutcome.Landless;

    /// <summary>
    /// The per-character increase between two roster readings. Roster elements of one character are folded first;
    /// a decrease is not a donation and heroes are ignored, so only what the player added counts.
    /// </summary>
    public static IReadOnlyList<TroopCount> Donated(IReadOnlyList<TroopCount> before, IReadOnlyList<TroopCount> after)
    {
        var held = new Dictionary<string, int>();
        foreach (var element in before)
            held[element.CharacterId] = (held.TryGetValue(element.CharacterId, out var n) ? n : 0) + element.Count;

        var now = new Dictionary<string, int>();
        foreach (var element in after)
            if (!element.IsHero)
                now[element.CharacterId] = (now.TryGetValue(element.CharacterId, out var n) ? n : 0) + element.Count;

        var donated = new List<TroopCount>();
        foreach (var pair in now)
        {
            var increase = pair.Value - (held.TryGetValue(pair.Key, out var was) ? was : 0);
            if (increase > 0)
                donated.Add(new TroopCount(pair.Key, increase));
        }
        return donated;
    }

    public bool CanGift() => _settings.TroopGiftsEnabled && Giftable(Partner()) != null;

    /// <summary>Queues the gift screen for when the conversation has ended.</summary>
    public void QueueGift()
    {
        var lord = _settings.TroopGiftsEnabled ? Giftable(Partner()) : null;
        if (lord == null)
            return;
        var heroId = lord.Id;
        _lords.RunAfterConversation(() => OpenGift(heroId));
    }

    public bool CanAskDomain() => _settings.DomainRevealEnabled && Askable(Partner()) != null;

    /// <summary>
    /// Asks the lord about his clan's holdings. A lord who does not know the player well enough refuses and costs
    /// relation; a willing lord whose clan holds nothing says so and costs relation; otherwise the clan's holdings
    /// get tracking marks on the map (towns and castles, counted in the snapshot). An engine fault is logged and answers Unavailable, which the dialog treats as a
    /// refusal line.
    /// </summary>
    public DomainOutcome AskDomain()
    {
        var lord = _settings.DomainRevealEnabled ? Askable(Partner()) : null;
        if (lord == null)
            return DomainOutcome.Unavailable;

        try
        {
            var config = _config.GetConfig();
            if (lord.Relation < config.RevealMinRelation)
            {
                Charge(lord.Id, config.RefusalCost);
                return DomainOutcome.Refused;
            }
            if (lord.Holdings <= 0)
            {
                Charge(lord.Id, config.LandlessCost);
                return DomainOutcome.Landless;
            }

            _reveal.RevealClan(lord.ClanId!);
            if (lord.Fortifications > 0)
                _lords.ShowHoldingsMarked(lord.Fortifications);
            return DomainOutcome.Revealed;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[LordCourtesies] The holdings question for '{lord.Id}' failed: {ex.Message}");
            return DomainOutcome.Unavailable;
        }
    }

    private bool Ready => !_coop.ShouldDeferToHost && !_server.IsDedicatedServer;

    private LordCourtesySnapshot? Partner()
    {
        var heroId = _lords.ConversationPartnerId;
        return string.IsNullOrEmpty(heroId) || !Ready ? null : _lords.Snapshot(heroId!);
    }

    private void OpenGift(string heroId)
    {
        var lord = _settings.TroopGiftsEnabled && Ready ? _lords.Snapshot(heroId) : null;
        if (Giftable(lord) == null)
            return;

        // The screen edits the lord's live roster in place and reports nothing, so each commit pays the troops that
        // arrived since the last one, read against this moving snapshot. Cancel never reaches it.
        var settled = _lords.MemberTroops(heroId);
        _lords.OpenGiftScreen(heroId, () =>
        {
            var now = _lords.MemberTroops(heroId);
            var donated = Donated(settled, now);
            settled = now;
            Pay(heroId, donated);
        });
    }

    private void Pay(string heroId, IReadOnlyList<TroopCount> donated)
    {
        if (donated.Count == 0)
            return;
        var influence = _lords.GiftInfluence(donated);
        _handover.OnHandover(heroId, _lords.LordName(heroId), influence, false, false, _lords.RelationOwnerId(heroId));
    }

    private void Charge(string heroId, float cost)
    {
        var amount = (int)Math.Round(cost);
        if (amount > 0)
            _player.ChangeRelationWith(heroId, -amount);
    }

    /// <summary>
    /// The lord when the player may give him troops: a willing free lord who leads his own party, is with the
    /// player's party and has room, is not being disbanded or of the player's kingdom, does not stand across the
    /// Free/Evil line, and the player has regular troops to give.
    /// </summary>
    private LordCourtesySnapshot? Giftable(LordCourtesySnapshot? snapshot)
    {
        var lord = Askable(snapshot);
        if (lord == null || !lord.LeadsOwnParty || !lord.IsWithPlayer || !lord.HasRoom || lord.IsDisbanding
            || !lord.PlayerHasTroops)
            return null;
        if (!string.IsNullOrEmpty(lord.KingdomId) && lord.KingdomId == _player.KingdomId)
            return null;
        var playerSide = _alignment.ResolveSide(_player.KingdomId ?? string.Empty, _player.CultureId ?? string.Empty);
        var lordSide = _alignment.ResolveSide(lord.KingdomId ?? string.Empty, lord.CultureId ?? string.Empty);
        return SidesAllowGift(playerSide, lordSide) ? lord : null;
    }

    /// <summary>The lord when he is a free hero of another clan and not at war with the player.</summary>
    private static LordCourtesySnapshot? Askable(LordCourtesySnapshot? lord) =>
        lord == null || lord.IsInPlayerClan || lord.AtWarWithPlayer || string.IsNullOrEmpty(lord.ClanId) ? null : lord;
}
