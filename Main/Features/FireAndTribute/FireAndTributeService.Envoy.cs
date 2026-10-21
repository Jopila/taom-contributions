using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Validation;
using Inv = System.Globalization.CultureInfo;

namespace TAOM.Features.FireAndTribute;

/// <summary>
/// The tribute envoy half of <see cref="FireAndTributeService"/> (#870 slice 2). A host that rides against the player's
/// kingdom gets an envoy due 24 to 48 hours later by default. While the player's clan holds a village, the player is not
/// enlisted, the host is out of battle and the map is free, the envoy asks for gold in a single-choice popup, and the hourly
/// step retries until it can. One envoy per host. Paying makes the host spare the clan and the player's party at once;
/// seizing the envoy starts the side's grudge. All state lives on the host record (<see cref="RaidHostRecord"/>) and the
/// root of the save record, so a host that goes home or breaks takes its envoy with it. Nothing is transient: an open popup
/// is known by the map not being free, and a click on a popup that outlived a load finds its record gone from the state.
/// </summary>
public sealed partial class FireAndTributeService
{
    private const int HoursInDay = 24;

    /// <summary>The campaign hour now; false when the engine clock is not a finite number from 0 to the int range.</summary>
    private bool TryNowHour(out int hour)
    {
        hour = 0;
        double hours = _snapshots.GetNowHours();
        if (!FiniteFloatValidator.IsFiniteInRange(hours, 0d, int.MaxValue))
            return false;
        hour = (int)Math.Floor(hours);
        return true;
    }

    // ---- scheduling ------------------------------------------------------------------------------------------------

    /// <summary>
    /// Called once when a host has been created: schedules its envoy when it rides against the player's kingdom. Never
    /// throws, because the host already exists and must keep its notices and its first step.
    /// </summary>
    private void ScheduleEnvoy(RaidHostRecord record, FireAndTributeTuning t)
    {
        try
        {
            if (!_settings.EnvoysEnabled)
                return;
            var player = _envoyPlayer.ReadPlayer();
            if (!player.Exists || player.KingdomId == null || player.KingdomId != record.TargetKingdomId)
                return;
            if (!TryNowHour(out int now))
                return;

            int wait = _random.Next(t.EnvoyMinDelayHours, t.EnvoyMaxDelayHours + 1);
            record.EnvoyDueHour = FireAndTributeMath.DueHour(now, wait);
            _logger.LogInfo($"{Tag} {record.HostId} ({record.PartyId}) will send an envoy in {wait} h (hour {record.EnvoyDueHour})");
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} scheduling the envoy of {record.HostId} ({record.PartyId}) failed: {ex}");
        }
    }

    // ---- the hourly step -------------------------------------------------------------------------------------------

    /// <summary>
    /// Shows the first due envoy that can be shown. At most one popup is open at a time: while it is open the map is not
    /// free, so a second due envoy waits until the first is answered or hidden. A host whose envoy throws is logged and the
    /// rest are still tried. A host whose side is switched off is skipped, though its record may linger until the engine
    /// lets it be removed.
    /// </summary>
    private void RunEnvoys(FireAndTributeConfig config)
    {
        if (!_settings.EnvoysEnabled || !TryNowHour(out int now))
            return;
        foreach (var record in _state.Hosts)
        {
            if (record.EnvoyDone || record.EnvoyDueHour < 0 || now < record.EnvoyDueHour || !SideEnabled(record.Side))
                continue;
            try
            {
                if (OfferIfReady(record, config.Tuning))
                    return;
            }
            catch (Exception ex)
            {
                _logger.LogError($"{Tag} envoy for {record.HostId} ({record.PartyId}) failed: {ex}");
            }
        }
    }

    /// <summary>
    /// Settles the envoy of a host that is going home (nothing left to buy off), else shows the popup when nothing blocks
    /// it. False, with nothing changed, when something blocks it: the next hour tries again.
    /// </summary>
    private bool OfferIfReady(RaidHostRecord record, FireAndTributeTuning t)
    {
        if (record.Returning)
        {
            record.EnvoyDone = true;
            _logger.LogInfo($"{Tag} {record.HostId} ({record.PartyId}) turned for home before its envoy could ask; no tribute is due");
            return false;
        }

        var player = _envoyPlayer.ReadPlayer();
        if (EnvoyBlocker(record, player) != null)
            return false;
        Quote(record, player.Villages, t);
        Present(record, player, t);
        return true;
    }

    /// <summary>
    /// Why the envoy of this host cannot be shown now, or null when it can. The village count comes with the player
    /// reading, so no check reads the map's village list; the map-free test runs last, because it is the one that is false
    /// while an envoy popup is already open.
    /// </summary>
    private string? EnvoyBlocker(RaidHostRecord record, EnvoyPlayerReading player)
    {
        if (!player.Exists)
            return "no player is readable";
        if (player.KingdomId != record.TargetKingdomId)
            return $"you belong to {player.KingdomId ?? "no kingdom"}, but the host rides against {record.TargetKingdomId}";
        if (_enlistment.IsEnlisted)
            return "you are enlisted";
        if (player.Villages == 0)
            return "your clan owns no village";
        // A host in a battle may be raiding one of the clan's villages, and the player cannot answer an envoy mid-event.
        if (_adapter.Read(record.PartyId).InMapEvent)
            return $"{record.HostId} is in a battle";
        return _envoyPresenter.IsMapFree() ? null : "the map is not free";
    }

    /// <summary>
    /// The tribute a host asks: the quoted villages and price when it has been quoted (a delayed envoy keeps both), else the
    /// price for <paramref name="villages"/> now. The one expression behind the popup and the console's dry run.
    /// </summary>
    private (int Villages, int Price) QuoteFor(RaidHostRecord record, int villages, FireAndTributeTuning t) =>
        record.EnvoyPrice > 0
            ? (record.EnvoyVillages, record.EnvoyPrice)
            : (villages, FireAndTributeMath.TributePrice(FireAndTributeMath.TributeUnits(villages), t.EnvoyUnitPrice, _state.EnvoysSeized));

    /// <summary>The first quote fixes the village count and the price on the record; a delayed envoy keeps both.</summary>
    private void Quote(RaidHostRecord record, int villages, FireAndTributeTuning t)
    {
        if (record.EnvoyPrice > 0)
            return;
        var quote = QuoteFor(record, villages, t);
        record.EnvoyVillages = quote.Villages;
        record.EnvoyPrice = quote.Price;
    }

    /// <summary>Opens the popup for the host's quoted tribute. The click handler holds the record itself, so a click that outlives a load finds it gone.</summary>
    private void Present(RaidHostRecord record, EnvoyPlayerReading player, FireAndTributeTuning t)
    {
        var offer = new EnvoyOffer
        {
            KeyedHostName = NameOf(record),
            Villages = record.EnvoyVillages,
            Price = record.EnvoyPrice,
            Gold = player.Gold,
            CanHaggle = FireAndTributeMath.CanHaggle(FireAndTributeMath.TributeUnits(record.EnvoyVillages), record.EnvoyHaggled),
            DelayDays = t.EnvoyDelayDays,
        };
        _envoyPresenter.ShowOffer(offer, choice => OnEnvoyChosen(record, choice));
    }

    // ---- the player's answer ---------------------------------------------------------------------------------------

    private void OnEnvoyChosen(RaidHostRecord record, EnvoyChoice choice)
    {
        try
        {
            // A click after the host is gone, after a load (the loaded state holds other record objects, even of the same
            // save), after a co-op session went live, or a second click of one popup does nothing.
            if (!_state.Hosts.Contains(record) || record.EnvoyDone || !IsSinglePlayer)
                return;
            ApplyChoice(record, choice, _configProvider.GetConfig().Tuning);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} envoy choice {choice} for {record.HostId} ({record.PartyId}) failed: {ex}");
        }
    }

    private void ApplyChoice(RaidHostRecord record, EnvoyChoice choice, FireAndTributeTuning t)
    {
        switch (choice)
        {
            case EnvoyChoice.Pay:
                Pay(record, t);
                break;
            case EnvoyChoice.Haggle:
                Haggle(record, t);
                break;
            case EnvoyChoice.Delay:
                Delay(record, t);
                break;
            case EnvoyChoice.Refuse:
                Settle(record, EnvoyOutcome.Refused);
                break;
            case EnvoyChoice.Seize:
                Seize(record);
                break;
            default:
                Reoffer(record, t);
                break;
        }
    }

    /// <summary>Shows the same tribute again (a refused payment, a haggle just rolled, a choice that was not open).</summary>
    private void Reoffer(RaidHostRecord record, FireAndTributeTuning t) =>
        Present(record, _envoyPlayer.ReadPlayer(), t);

    private void Pay(RaidHostRecord record, FireAndTributeTuning t)
    {
        var player = _envoyPlayer.ReadPlayer();
        // The clan is read before the gold moves: gold taken without a clan to spare would be a theft.
        if (!player.Exists || string.IsNullOrEmpty(player.ClanId) || !_envoyPlayer.TryCharge(record.EnvoyPrice))
        {
            _envoyPresenter.ShowOutcome(EnvoyOutcome.CannotAfford, NameOf(record), record.EnvoyPrice, 0);
            Reoffer(record, t);
            return;
        }
        record.SparedClanId = player.ClanId!;
        record.EnvoyDone = true;
        _logger.LogInfo($"{Tag} {record.HostId} ({record.PartyId}) was paid {record.EnvoyPrice} denars and spares {record.SparedClanId}");
        TakePaymentEffect(record);
        _envoyPresenter.ShowOutcome(EnvoyOutcome.Paid, NameOf(record), record.EnvoyPrice, 0);
    }

    /// <summary>
    /// A payment is felt now, not at the next hourly step: the engine's party AI is told to leave the player's party alone
    /// for the rest of the host's life (twice the timeout, the longest a host stays out; vanilla's safe-passage bribe makes
    /// the same call), and the host is steered once more, outside the hourly step as a spawn is, so an order given earlier
    /// this hour (an engage on the player, a raid on a village now spared) is replaced. A failure here is logged: the payment
    /// stands and the next hourly step steers the host.
    /// </summary>
    private void TakePaymentEffect(RaidHostRecord record)
    {
        try
        {
            var config = _configProvider.GetConfig();
            _adapter.SpareMainParty(record.PartyId, SpareHours(config.Tuning));
            if (TryToday(out int day))
                Steer(record, config, day);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} steering {record.HostId} ({record.PartyId}) after its payment failed: {ex}");
        }
    }

    private static int SpareHours(FireAndTributeTuning t) =>
        (int)Math.Min(int.MaxValue, 2L * Math.Max(1, t.MaxHostDays) * HoursInDay);

    private void Haggle(RaidHostRecord record, FireAndTributeTuning t)
    {
        if (!FireAndTributeMath.CanHaggle(FireAndTributeMath.TributeUnits(record.EnvoyVillages), record.EnvoyHaggled))
        {
            Reoffer(record, t);
            return;
        }
        var player = _envoyPlayer.ReadPlayer();
        float chance = FireAndTributeMath.HaggleChance(
            player.Exists ? player.Charm : 0, t.HaggleBaseChance, t.HaggleChancePerCharm, t.HaggleMinChance, t.HaggleMaxChance);
        record.EnvoyHaggled = true;
        bool won = _random.NextDouble() < chance;
        if (won)
            record.EnvoyPrice = FireAndTributeMath.HaggledPrice(record.EnvoyPrice);
        _logger.LogInfo($"{Tag} {record.HostId} haggle {(won ? "won" : "lost")} at chance {chance.ToString("0.00", Inv.InvariantCulture)}: price {record.EnvoyPrice}");
        _envoyPresenter.ShowOutcome(won ? EnvoyOutcome.HaggleWon : EnvoyOutcome.HaggleLost, NameOf(record), record.EnvoyPrice, 0);
        Reoffer(record, t);
    }

    /// <summary>The envoy waits the configured days at the same price. Nothing is spared meanwhile.</summary>
    private void Delay(RaidHostRecord record, FireAndTributeTuning t)
    {
        if (TryNowHour(out int now))
            record.EnvoyDueHour = FireAndTributeMath.DueHour(now, t.EnvoyDelayDays * HoursInDay);
        _envoyPresenter.ShowOutcome(EnvoyOutcome.Delayed, NameOf(record), record.EnvoyPrice, t.EnvoyDelayDays);
    }

    private void Settle(RaidHostRecord record, EnvoyOutcome outcome)
    {
        record.EnvoyDone = true;
        _envoyPresenter.ShowOutcome(outcome, NameOf(record), record.EnvoyPrice, 0);
    }

    /// <summary>
    /// The side's next host rides against this kingdom first, and every later tribute costs one more full price. The count
    /// stops at the save format's limit, so a record that was written always reads back.
    /// </summary>
    private void Seize(RaidHostRecord record)
    {
        _state.EnvoysSeized = Math.Min(FireAndTributeSaveCodec.MaxSeized, _state.EnvoysSeized + 1);
        _state.Grudges[record.Side] = record.TargetKingdomId;
        _logger.LogInfo($"{Tag} the envoy of {record.HostId} was seized; {record.Side} holds a grudge against {record.TargetKingdomId} ({_state.EnvoysSeized} seized)");
        Settle(record, EnvoyOutcome.Seized);
    }

    // ---- the console -----------------------------------------------------------------------------------------------

    public string BringEnvoyNow(bool dryRun)
    {
        if (!IsSinglePlayer)
            return "Refused: single-player only, and a co-op session, a co-op mod TAOM cannot probe, or a dedicated server is live.";
        if (!_settings.EnvoysEnabled)
            return "Refused: Tribute envoys are switched off in MCM.";
        if (_state.Hosts.Count == 0)
            return "No host is out. Send one with taom.spawn_raid_host first.";
        if (!TryNowHour(out int now))
            return "Refused: the campaign clock is unreadable.";

        var player = _envoyPlayer.ReadPlayer();
        var open = _state.Hosts.Where(h => !h.EnvoyDone).ToList();
        if (open.Count == 0)
            return $"The envoy of {_state.Hosts[0].HostId} is already settled; each host sends one.";
        var live = open.Where(h => SideEnabled(h.Side)).ToList();
        if (live.Count == 0)
            return $"{open[0].HostId} sends no envoy: its side is switched off in MCM.";
        var record = live.FirstOrDefault(h => h.TargetKingdomId == player.KingdomId) ?? live[0];
        if (record.Returning)
            return $"{record.HostId} is going home, so no envoy would come.";

        string? blocker = EnvoyBlocker(record, player);
        if (blocker != null && player.KingdomId != record.TargetKingdomId)
            return $"No envoy would come for {record.HostId}: {blocker}.";

        var t = _configProvider.GetConfig().Tuning;
        var tribute = QuoteFor(record, player.Villages, t);
        string quote = $"{tribute.Villages} villages, {tribute.Price} denars";

        if (dryRun)
            return blocker == null
                ? $"Would show the envoy of {record.HostId} now: {quote}. Confirm sets it due at once."
                : $"Would set the envoy of {record.HostId} due now ({quote}), but it waits: {blocker}. It is tried every hour.";

        record.EnvoyDueHour = record.EnvoyDueHour >= 0 ? Math.Min(record.EnvoyDueHour, now) : now;
        if (blocker != null)
            return $"The envoy of {record.HostId} is due now but waits: {blocker}. It is tried every hour.";
        OfferIfReady(record, t);
        return $"The envoy of {record.HostId} is shown: {quote}.";
    }

    // ---- the status lines ------------------------------------------------------------------------------------------

    /// <summary>The envoy lines of <see cref="Describe"/>: the switch and the seized count, then any grudges.</summary>
    private IEnumerable<string> EnvoyLines()
    {
        yield return $"Tribute envoys: {(_settings.EnvoysEnabled ? "on" : "OFF in MCM")}, {_state.EnvoysSeized} seized.";
        if (_state.Grudges.Count > 0)
            yield return "Grudges: " + string.Join(", ", _state.Grudges.OrderBy(p => p.Key).Select(p => $"{p.Key} -> {p.Value}")) + ".";
    }

    /// <summary>One host's envoy as a clause of its status line.</summary>
    private string EnvoyText(RaidHostRecord host)
    {
        string spared = host.SparedClanId.Length > 0 ? $", spares {host.SparedClanId}" : string.Empty;
        string quote = host.EnvoyPrice > 0 ? $"{host.EnvoyPrice} denars for {host.EnvoyVillages} villages" : string.Empty;
        if (host.EnvoyDone)
            return (quote.Length > 0 ? $"envoy settled ({quote})" : "no envoy") + spared;
        if (host.EnvoyDueHour < 0)
            return "no envoy";

        string when = TryNowHour(out int now)
            ? (host.EnvoyDueHour > now ? $" (in {host.EnvoyDueHour - now} h)" : " (due now, waiting)")
            : string.Empty;
        string haggled = host.EnvoyHaggled ? ", haggled" : string.Empty;
        return $"envoy due at hour {host.EnvoyDueHour}{when}" + (quote.Length > 0 ? $", quoted {quote}{haggled}" : string.Empty);
    }
}
