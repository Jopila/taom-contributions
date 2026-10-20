using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.TokensOfEsteem.Models;

namespace TAOM.Features.TokensOfEsteem;

/// <summary>
/// The state of one gift conversation between the dialog lines and <see cref="GiftService"/>: which branch was chosen, the
/// gate the hero answered, how many things the list holds and what the last pick did. It also owns the cooldown ledger and
/// passes it to the service, so it is created by <c>GiftBehavior</c> for every campaign and a new campaign starts with an
/// empty one. Nothing here touches the engine except through <see cref="IGiftConversationAdapter"/>, so the whole flow runs
/// under test with fakes. A co-op client (<c>ShouldDeferToHost</c>) sees no line and takes no gift: a gift moves the player's
/// gold, inventory and relation, which a client's own player makes differ per peer.
/// </summary>
public sealed class GiftConversationFlow
{
    private static readonly GiftGate Closed = new(GiftGateKind.Hidden);

    private readonly GiftService _service;
    private readonly IGiftConversationAdapter _conversation;
    private readonly ICoopSessionProvider _coop;
    private readonly IModLogger _logger;
    private GiftEntry _entry;
    private GiftGate _gate = Closed;
    private int _optionCount;
    private GiftOutcome _outcome = GiftOutcome.Failed;

    public GiftConversationFlow(GiftService service, IGiftConversationAdapter conversation, ICoopSessionProvider coop, IModLogger logger)
    {
        _service = service;
        _conversation = conversation;
        _coop = coop;
        _logger = logger;
    }

    public GiftCooldownLedger Ledger { get; } = new();

    private string? Partner => _conversation.PartnerId;

    /// <summary>True when the entry line shows for the conversation partner (greyed during a cooldown).</summary>
    public bool CanOffer(GiftEntry entry) =>
        !_coop.ShouldDeferToHost && Partner is { } id && ReadGate(id, entry).Kind != GiftGateKind.Hidden;

    /// <summary>
    /// True when the entry line can be chosen. False during a cooldown (<paramref name="daysLeft"/> says how long) and when the
    /// line has gone hidden since it was drawn (no partner, a failed read, a co-op session that went live). A refusal is still
    /// a line the player may choose.
    /// </summary>
    public bool CanChoose(GiftEntry entry, out int daysLeft)
    {
        var gate = !_coop.ShouldDeferToHost && Partner is { } id ? ReadGate(id, entry) : Closed;
        daysLeft = gate.Kind == GiftGateKind.Cooldown ? gate.DaysLeft : 0;
        return gate.Kind != GiftGateKind.Cooldown && gate.Kind != GiftGateKind.Hidden;
    }

    /// <summary>The player chose the entry line: answers the gate (a hostile lord takes 1 relation) and hands over the list.</summary>
    public void Ask(GiftEntry entry)
    {
        _entry = entry;
        _outcome = GiftOutcome.Failed;
        _optionCount = 0;
        _gate = Closed;
        if (_coop.ShouldDeferToHost || Partner is not { } id)
            return;
        try
        {
            _gate = _service.Ask(id, entry, Ledger);
            if (_gate.Kind != GiftGateKind.Open)
                return;
            var options = _service.Options(id, entry);
            _optionCount = options.Count;
            _conversation.SetOptions(options);
        }
        catch (Exception ex)
        {
            _gate = Closed;
            _logger.LogError($"[Gifts] asking failed: {ex.Message}");
        }
    }

    public bool GateIs(GiftGateKind kind) => _gate.Kind == kind;

    public bool HasNoOptions() => _gate.Kind == GiftGateKind.Open && _optionCount == 0;

    /// <summary>Row condition: writes the current option's row text. Must return true for a row to be listed.</summary>
    public bool ListRow()
    {
        if (_conversation.CurrentOption is not { } option)
            return false;
        _conversation.WriteRow(option);
        return true;
    }

    /// <summary>Why the current row cannot be chosen; <see cref="GiftBlock.None"/> when it can; null when there is no current row.</summary>
    public GiftBlock? RowBlock() => _conversation.CurrentOption?.Block;

    /// <summary>The player chose a row: gives it. The service starts the cooldown when the gift changes hands.</summary>
    public void Pick()
    {
        _outcome = GiftOutcome.Failed;
        if (_coop.ShouldDeferToHost || Partner is not { } id || _conversation.SelectedOption is not { } option)
            return;
        try
        {
            _outcome = _service.Give(id, _entry, option, Ledger);
        }
        catch (Exception ex)
        {
            _outcome = GiftOutcome.Failed;
            _logger.LogError($"[Gifts] giving failed: {ex.Message}");
        }
    }

    /// <summary>True when a gift changed hands.</summary>
    public bool Gave() => _outcome.Result == GiftResult.Given;

    /// <summary>True when a gift changed hands and it suited the hero's taste.</summary>
    public bool GaveSuited() => _outcome.Result == GiftResult.Given && _outcome.Suited;

    private GiftGate ReadGate(string heroId, GiftEntry entry)
    {
        try
        {
            return _service.Gate(heroId, entry, Ledger);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Gifts] gate failed: {ex.Message}");
            return Closed;
        }
    }
}
