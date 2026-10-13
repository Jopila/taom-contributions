using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;

namespace TAOM.Features.WarChronicle.Events;

/// <summary>
/// The War Chronicle's event chain (docs/features/war-chronicle.md "Events", #765 M3): the state machine
/// over <see cref="ChronicleStateStore"/>. Times are campaign days (<see cref="IChronicleWorldAdapter.GetElapsedDays"/>).
///
/// <list type="bullet">
/// <item><b>Adoption (D5):</b> an event with no saved row gets one; when its open day is already past, or
/// its anchor was Skipped, it is Skipped with no retroactive effect.</item>
/// <item><b>Pending:</b> opens once its anchor is known and its day has come. Phase anchors use
/// <see cref="IWarOfTheRingService.GetEffectivePhaseDay"/> and also need the phase reached; an Event anchor
/// needs the anchor resolved, not Skipped, and with the required outcome. A failed precondition Skips it.</item>
/// <item><b>Narrative:</b> Occurred at once, effects applied.</item>
/// <item><b>Siege:</b> Active with a fixed window and an army focus; assaulted when an attacker besieges the
/// target; Fell the moment an attacker owns it; Lapsed if a third party does; at the close Held when
/// assaulted and still the defender's, otherwise Lapsed.</item>
/// <item><b>War Ended:</b> every Pending or Active event is Skipped and the focus clears.</item>
/// </list>
/// Every owner-changed handler is idempotent: the event fires twice per conquest, and the second fire finds
/// the window resolved. The MCM switch stops new work (nothing opens or resolves, the focus clears) and
/// keeps the saved states. The caller has checked that this peer may run world work; a throwing engine
/// read is logged and the step skipped, so the campaign tick never fails here.
/// </summary>
public sealed class ChronicleService
{
    private readonly IChronicleConfigProvider _config;
    private readonly ChronicleStateStore _store;
    private readonly IChronicleWorldAdapter _world;
    private readonly IAllianceAdapter _alliance;
    private readonly IWarOfTheRingService _wotr;
    private readonly ChronicleOutcomeService _outcomes;
    private readonly ChronicleAnnouncementQueue _announcements;
    private readonly ChronicleFocusSync _focus;
    private readonly IWarChronicleSettingsProvider _settings;
    private readonly IModLogger _logger;

    public ChronicleService(
        IChronicleConfigProvider config, ChronicleStateStore store, IChronicleWorldAdapter world,
        IAllianceAdapter alliance, IWarOfTheRingService wotr, ChronicleOutcomeService outcomes,
        ChronicleAnnouncementQueue announcements, ChronicleFocusSync focus, IWarChronicleSettingsProvider settings,
        IModLogger logger)
    {
        _config = config;
        _store = store;
        _world = world;
        _alliance = alliance;
        _wotr = wotr;
        _outcomes = outcomes;
        _announcements = announcements;
        _focus = focus;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>Drops the focus an earlier campaign left: called from the events behavior's constructor.</summary>
    public void ResetTransientState() => _focus.Clear();

    /// <summary>Session launch: adopt missing rows, Skip events the campaign cannot host, push the focus again.</summary>
    public void LaunchChronicle() => Guarded("session launch", now =>
    {
        Adopt(now);
        foreach (var definition in _config.GetEvents())
        {
            var record = _store.Get(definition.Id);
            if (record != null && IsOpen(record.State) && !KnowsEveryId(definition, out var unknown))
            {
                _logger.LogWarning($"[WarChronicle] '{definition.Id}' names '{unknown}', which does not exist in this campaign, so it is skipped.");
                Resolve(definition, record, ChronicleEventState.Skipped, now, false, $"'{unknown}' does not exist in this campaign");
            }
        }

        if (_wotr.CurrentPhase == WarPhase.WarEnded)
            SkipEveryOpenEvent(now);
    });

    /// <summary>The daily step: open what is due, watch the Active windows, close what has run out.</summary>
    public void RunChronicleDay() => Guarded("daily step", now =>
    {
        Adopt(now);
        if (_wotr.CurrentPhase == WarPhase.WarEnded)
        {
            SkipEveryOpenEvent(now);
            return;
        }

        foreach (var definition in _config.GetEvents())
            Step(definition, now);
    });

    /// <summary>A settlement changed owner (twice per conquest): resolve an Active window on it.</summary>
    public void OnTargetOwnerChanged(string? settlementId)
    {
        if (settlementId == null)
            return;

        Guarded("owner change", now =>
        {
            foreach (var definition in ActiveSiegesAt(settlementId))
            {
                var owner = _world.GetOwnerKingdomId(settlementId);
                if (owner != null && definition.Attackers.Contains(owner))
                    Resolve(definition, _store.Get(definition.Id)!, ChronicleEventState.Fell, now, true, owner + " took it");
                else if (!string.Equals(owner, definition.Defender, StringComparison.Ordinal))
                    Resolve(definition, _store.Get(definition.Id)!, ChronicleEventState.Lapsed, now, true, (owner ?? "no kingdom") + " took it");
            }
        });
    }

    /// <summary>A siege began somewhere: an attacker besieging an Active target counts as the assault.</summary>
    public void OnTargetBesieged(string? settlementId)
    {
        if (settlementId == null)
            return;

        Guarded("siege start", _ =>
        {
            foreach (var definition in ActiveSiegesAt(settlementId))
                MarkAssaultIfBesieged(definition, _store.Get(definition.Id)!);
        });
    }

    /// <summary>Console: opens a Pending event now, ignoring its day but not its preconditions.</summary>
    public bool OpenChronicleEventNow(string? eventId, out string reason)
    {
        reason = string.Empty;
        if (!_settings.ChronicleEnabled)
        {
            reason = "The War Chronicle is off (MCM War of the Ring/War Chronicle).";
            return false;
        }

        var definition = _config.GetEvents().FirstOrDefault(d => string.Equals(d.Id, eventId, StringComparison.Ordinal));
        if (definition == null)
        {
            reason = $"No chronicle event '{eventId}'. Known: {string.Join(", ", _config.GetEvents().Select(d => d.Id))}.";
            return false;
        }

        var now = _world.GetElapsedDays();
        if (!FiniteFloatValidator.IsFinite(now))
        {
            reason = "The campaign clock is not readable.";
            return false;
        }

        if (_wotr.CurrentPhase == WarPhase.WarEnded)
        {
            reason = "The War of the Ring has ended; the chronicle is closed.";
            return false;
        }

        Adopt(now);
        var record = _store.Get(definition.Id)!;
        if (record.State != ChronicleEventState.Pending)
        {
            reason = $"'{definition.Id}' is {record.State}; only a Pending event can be opened.";
            return false;
        }

        if (!TryOpen(definition, record, now, out reason))
            return false;

        SyncFocus();
        reason = definition.IsSiege
            ? $"'{definition.Id}' is Active until day {Day(record.CloseDay)}."
            : $"'{definition.Id}' {record.State}.";
        return true;
    }

    /// <summary>Every configured event with its state and days, for <c>taom.chronicle_status</c>.</summary>
    public IReadOnlyList<ChronicleStatusRow> Status()
    {
        var rows = new List<ChronicleStatusRow>();
        foreach (var definition in _config.GetEvents())
        {
            var record = _store.Get(definition.Id);
            var state = record?.State ?? ChronicleEventState.Pending;
            rows.Add(new ChronicleStatusRow
            {
                Id = definition.Id,
                Kind = definition.Kind,
                State = state,
                OpenDay = record?.OpenDay ?? (state == ChronicleEventState.Pending ? OpenDayOf(definition, out _) : null),
                CloseDay = record?.CloseDay,
                Assaulted = record?.Assaulted ?? false,
                ResolvedDay = record?.ResolvedDay,
                Target = definition.TargetSettlementId,
            });
        }

        return rows;
    }

    private void Guarded(string step, Action<double> body)
    {
        if (!_settings.ChronicleEnabled)
        {
            _focus.Clear();
            return;
        }

        try
        {
            var now = _world.GetElapsedDays();
            // Positive requirement on the engine clock: a NaN day would pass no `>=` gate, but it must not
            // reach a stored window either.
            if (!FiniteFloatValidator.IsFinite(now))
                return;

            body(now);
            SyncFocus();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[WarChronicle] Chronicle {step} skipped ({ex.GetType().Name}: {ex.Message}).");
        }
    }

    private void Adopt(double now)
    {
        foreach (var definition in _config.GetEvents())
        {
            if (_store.Get(definition.Id) != null)
                continue;

            var record = new ChronicleEventRecord(definition.Id, ChronicleEventState.Pending);
            _store.Set(record);
            var openDay = OpenDayOf(definition, out var anchorFailure);
            if (anchorFailure != null)
                Resolve(definition, record, ChronicleEventState.Skipped, now, false, anchorFailure);
            else if (openDay.HasValue && openDay.Value < now)
                Resolve(definition, record, ChronicleEventState.Skipped, now, false, $"its day {Day(openDay)} had passed when the save first saw it");
        }
    }

    private void Step(ChronicleEventDefinition definition, double now)
    {
        var record = _store.Get(definition.Id);
        if (record == null)
            return;

        if (record.State == ChronicleEventState.Pending)
        {
            var openDay = OpenDayOf(definition, out var anchorFailure);
            if (anchorFailure != null)
                Resolve(definition, record, ChronicleEventState.Skipped, now, false, anchorFailure);
            else if (openDay.HasValue && now >= openDay.Value && !TryOpen(definition, record, now, out var why))
                Resolve(definition, record, ChronicleEventState.Skipped, now, false, why);
            return;
        }

        if (record.State != ChronicleEventState.Active)
            return;

        MarkAssaultIfBesieged(definition, record);
        if (record.CloseDay.HasValue && now >= record.CloseDay.Value)
            Close(definition, record, now);
    }

    /// <summary>The open day, or null while the anchor is unknown. A non-null failure means the event can never open.</summary>
    private double? OpenDayOf(ChronicleEventDefinition definition, out string? failure)
    {
        failure = null;
        switch (definition.Anchor)
        {
            case ChronicleAnchor.CampaignStart:
                return definition.OffsetDays;

            case ChronicleAnchor.Phase1:
                return PhaseDay(WarPhase.IsengardWar) + definition.OffsetDays;

            case ChronicleAnchor.Phase2:
                return PhaseDay(WarPhase.FullWar) + definition.OffsetDays;

            case ChronicleAnchor.Event:
                var anchor = _store.Get(definition.AnchorEventId);
                if (anchor == null || IsOpen(anchor.State))
                    return null;
                if (anchor.State == ChronicleEventState.Skipped)
                {
                    failure = $"its anchor '{definition.AnchorEventId}' was skipped";
                    return null;
                }

                if (definition.RequiresOutcome.HasValue && anchor.State != definition.RequiresOutcome.Value)
                {
                    failure = $"its anchor '{definition.AnchorEventId}' ended {anchor.State}, not {definition.RequiresOutcome.Value}";
                    return null;
                }

                return anchor.ResolvedDay + definition.OffsetDays;

            default:
                return null;
        }
    }

    /// <summary>The phase's effective day once the phase machine has reached it; null before.</summary>
    private double? PhaseDay(WarPhase phase) =>
        (int)_wotr.CurrentPhase >= (int)phase ? _wotr.GetEffectivePhaseDay(phase) : null;

    private bool TryOpen(ChronicleEventDefinition definition, ChronicleEventRecord record, double now, out string reason)
    {
        if (!PreconditionsHold(definition, out reason))
            return false;

        record.OpenDay = now;
        if (!definition.IsSiege)
        {
            Resolve(definition, record, ChronicleEventState.Occurred, now, true, "its day came");
            return true;
        }

        record.State = ChronicleEventState.Active;
        record.CloseDay = now + definition.WindowDays;
        record.Assaulted = false;
        MarkAssaultIfBesieged(definition, record);
        _announcements.Enqueue(new ChronicleAnnouncement(
            definition.Id, definition.Title, definition.OpenText, definition.TargetSettlementId, definition.WindowDays));
        _logger.LogInfo($"[WarChronicle] '{definition.Id}' opened on day {Day(now)}: {definition.TargetSettlementId}, "
                        + $"{string.Join("+", definition.Attackers)} against {definition.Defender}, closes day {Day(record.CloseDay)}.");
        return true;
    }

    private bool PreconditionsHold(ChronicleEventDefinition definition, out string reason)
    {
        reason = string.Empty;
        if (definition.RequiresOutcome.HasValue)
        {
            var anchor = _store.Get(definition.AnchorEventId);
            if (anchor == null || anchor.State != definition.RequiresOutcome.Value)
            {
                reason = $"'{definition.AnchorEventId}' has not ended {definition.RequiresOutcome.Value}";
                return false;
            }
        }

        if (!definition.IsSiege)
            return true;

        // D2: peace between Hostile pairs is blocked only in the Full War.
        if (_wotr.CurrentPhase != WarPhase.FullWar)
        {
            reason = $"a siege opens only in the Full War (the phase is {_wotr.CurrentPhase})";
            return false;
        }

        var live = new HashSet<string>(_alliance.GetAllKingdomIds() ?? new List<string>(), StringComparer.Ordinal);
        if (!live.Contains(definition.Defender!))
        {
            reason = $"the defender {definition.Defender} is gone";
            return false;
        }

        var attackers = definition.Attackers.Where(live.Contains).ToList();
        if (attackers.Count == 0)
        {
            reason = "every attacker is gone";
            return false;
        }

        var owner = _world.GetOwnerKingdomId(definition.TargetSettlementId!);
        if (!string.Equals(owner, definition.Defender, StringComparison.Ordinal))
        {
            reason = $"{definition.TargetSettlementId} is held by {owner ?? "no kingdom"}, not {definition.Defender}";
            return false;
        }

        if (!attackers.Any(a => _alliance.AreAtWar(definition.Defender!, a)))
        {
            reason = $"{definition.Defender} is at war with none of {string.Join(", ", attackers)}";
            return false;
        }

        return true;
    }

    private void Close(ChronicleEventDefinition definition, ChronicleEventRecord record, double now)
    {
        var owner = _world.GetOwnerKingdomId(definition.TargetSettlementId!);
        if (owner != null && definition.Attackers.Contains(owner))
            Resolve(definition, record, ChronicleEventState.Fell, now, true, owner + " holds it at the close");
        else if (record.Assaulted && string.Equals(owner, definition.Defender, StringComparison.Ordinal))
            Resolve(definition, record, ChronicleEventState.Held, now, true, "assaulted and held");
        else
            Resolve(definition, record, ChronicleEventState.Lapsed, now, true, record.Assaulted ? "lost to another" : "never assaulted");
    }

    private void MarkAssaultIfBesieged(ChronicleEventDefinition definition, ChronicleEventRecord record)
    {
        if (record.Assaulted)
            return;

        var besieger = _world.GetBesiegerKingdomId(definition.TargetSettlementId!);
        if (besieger != null && definition.Attackers.Contains(besieger))
        {
            record.Assaulted = true;
            _logger.LogInfo($"[WarChronicle] '{definition.Id}': {besieger} besieges {definition.TargetSettlementId}; the window counts as assaulted.");
        }
    }

    private void Resolve(ChronicleEventDefinition definition, ChronicleEventRecord record, ChronicleEventState outcome,
        double now, bool announce, string why)
    {
        record.State = outcome;
        record.ResolvedDay = now;
        _outcomes.Apply(definition, outcome, _world.GetNowHours());
        if (announce)
        {
            _announcements.Enqueue(new ChronicleAnnouncement(
                definition.Id, definition.Title, TextFor(definition, outcome), definition.TargetSettlementId, definition.WindowDays));
        }

        _logger.LogInfo($"[WarChronicle] '{definition.Id}' {outcome} on day {Day(now)} ({why}).");
    }

    private void SkipEveryOpenEvent(double now)
    {
        foreach (var definition in _config.GetEvents())
        {
            var record = _store.Get(definition.Id);
            if (record != null && IsOpen(record.State))
                Resolve(definition, record, ChronicleEventState.Skipped, now, false, "the War of the Ring has ended");
        }
    }

    private void SyncFocus()
    {
        var active = new List<ChronicleEventDefinition>();
        foreach (var definition in _config.GetEvents())
        {
            if (definition.IsSiege && _store.Get(definition.Id)?.State == ChronicleEventState.Active)
                active.Add(definition);
        }

        _focus.Push(active);
    }

    private IEnumerable<ChronicleEventDefinition> ActiveSiegesAt(string settlementId) =>
        _config.GetEvents()
            .Where(d => d.IsSiege && string.Equals(d.TargetSettlementId, settlementId, StringComparison.Ordinal)
                        && _store.Get(d.Id)?.State == ChronicleEventState.Active)
            .ToList();

    /// <summary>Every settlement and kingdom id the event names exists in this campaign; the first missing one otherwise.</summary>
    private bool KnowsEveryId(ChronicleEventDefinition definition, out string unknown)
    {
        unknown = string.Empty;
        if (definition.IsSiege)
        {
            if (!_world.SettlementExists(definition.TargetSettlementId!))
            {
                unknown = definition.TargetSettlementId!;
                return false;
            }

            foreach (var kingdom in definition.Attackers.Concat(new[] { definition.Defender! }))
            {
                if (!_world.KingdomExists(kingdom))
                {
                    unknown = kingdom;
                    return false;
                }
            }
        }

        foreach (var outcome in new[] { definition.Occurred, definition.Held, definition.Fell })
        {
            if (outcome == null)
                continue;
            foreach (var effect in outcome.Effects)
            {
                if (IsKeyword(effect.Target) || _world.KingdomExists(effect.Target))
                    continue;
                unknown = effect.Target;
                return false;
            }
        }

        return true;
    }

    private static bool IsKeyword(string target) =>
        target == ChronicleConfigValidator.TargetAttacker || target == ChronicleConfigValidator.TargetDefender
        || target == ChronicleConfigValidator.TargetFree || target == ChronicleConfigValidator.TargetEvil;

    private static bool IsOpen(ChronicleEventState state) =>
        state == ChronicleEventState.Pending || state == ChronicleEventState.Active;

    private static string? TextFor(ChronicleEventDefinition definition, ChronicleEventState outcome) =>
        outcome == ChronicleEventState.Lapsed ? definition.LapsedText : definition.OutcomeFor(outcome)?.Text;

    private static string Day(double? day) =>
        day.HasValue ? day.Value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) : "?";
}
