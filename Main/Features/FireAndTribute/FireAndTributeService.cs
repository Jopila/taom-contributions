using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy;
using TAOM.Features.Enlistment;
using TAOM.Features.Execution;
using TAOM.Features.InitialChildGeneration;
using Inv = System.Globalization.CultureInfo;

namespace TAOM.Features.FireAndTribute;

/// <summary>
/// Fire and Tribute's raiding hosts (#870, docs/features/fire-and-tribute.md). Once the War of the Ring is in Full War, a
/// side with no host and no cooldown has a small daily chance to send one: a party of an outlaw clan, sized from its
/// target kingdom's strength, that raids the target's villages, engages weaker parties of the target kingdom it meets, and
/// goes home on its quota, with no village left, or on a timeout. All engine work goes through
/// <see cref="IRaidHostAdapter"/>; this class holds no engine type and sees parties as ids. Single-player only: a co-op
/// session, a co-op mod TAOM cannot probe or a dedicated server releases every host's pin and stops the feature.
/// </summary>
/// <remarks>
/// Singleton state is the saved record, the planned-despawn set, the diagnostic counters and the own-side snapshot, all
/// per campaign: the behavior's constructor calls <see cref="ResetForNewSession"/>. Parts: this file (lifecycle, gates,
/// status), the Spawn part (daily roll and creation), the Steering part (hourly steps and events), the Envoy part (the
/// tribute envoy: schedule, popup, the player's answer, the console's "bring it now") and the OwnSide part (the snapshot
/// the party AI model reads). Main thread only, except the two <see cref="IRaidHostOwnSideQuery"/> members, which the AI
/// worker calls.
/// </remarks>
public sealed partial class FireAndTributeService : IFireAndTributeService
{
    private const string Tag = "[FireAndTribute]";

    private static readonly FactionSide[] Sides = { FactionSide.Free, FactionSide.Neutral, FactionSide.Evil };

    private readonly IRaidHostAdapter _adapter;
    private readonly IFireAndTributeNoticeAdapter _notices;
    private readonly IFireAndTributeConfigProvider _configProvider;
    private readonly IFireAndTributeSettings _settings;
    private readonly IKingdomWarSnapshotAdapter _snapshots;
    private readonly IAlignmentService _alignment;
    private readonly IWarOfTheRingService _warOfTheRing;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _dedicatedServer;
    private readonly IRandomSource _random;
    private readonly ITributeEnvoyAdapter _envoyPlayer;
    private readonly IEnvoyPresenterAdapter _envoyPresenter;
    private readonly IEnlistmentStateQuery _enlistment;
    private readonly IModLogger _logger;

    private FireAndTributeState _state = new FireAndTributeState();

    // Party ids the service is removing on purpose, so the destroy event the removal raises is not read as a broken host.
    private readonly HashSet<string> _planned = new HashSet<string>(StringComparer.Ordinal);

    private int _spawnedToday;
    private int _raidsToday;
    private double _maxHourMs;

    public FireAndTributeService(
        IRaidHostAdapter adapter,
        IFireAndTributeNoticeAdapter notices,
        IFireAndTributeConfigProvider configProvider,
        IFireAndTributeSettings settings,
        IKingdomWarSnapshotAdapter snapshots,
        IAlignmentService alignment,
        IWarOfTheRingService warOfTheRing,
        ICoopSessionProvider coop,
        IDedicatedServerProvider dedicatedServer,
        IRandomSource random,
        ITributeEnvoyAdapter envoyPlayer,
        IEnvoyPresenterAdapter envoyPresenter,
        IEnlistmentStateQuery enlistment,
        IModLogger logger)
    {
        _adapter = adapter;
        _notices = notices;
        _configProvider = configProvider;
        _settings = settings;
        _snapshots = snapshots;
        _alignment = alignment;
        _warOfTheRing = warOfTheRing;
        _coop = coop;
        _dedicatedServer = dedicatedServer;
        _random = random;
        _envoyPlayer = envoyPlayer;
        _envoyPresenter = envoyPresenter;
        _enlistment = enlistment;
        _logger = logger;
    }

    /// <summary>
    /// Read live on every use, never cached (ICoopSessionProvider's contract). Stands down while a co-op session is live
    /// (host included), while a co-op mod TAOM cannot probe is loaded, and on a dedicated server.
    /// </summary>
    private bool IsSinglePlayer =>
        !_coop.IsSessionActive && !_coop.ShouldDeferToHost && !_dedicatedServer.IsDedicatedServer;

    private bool SideEnabled(FactionSide side) => _settings.Enabled && side switch
    {
        FactionSide.Free => _settings.FreeHostsEnabled,
        FactionSide.Neutral => _settings.NeutralHostsEnabled,
        _ => _settings.EvilHostsEnabled,
    };

    private bool TryToday(out int day) => _snapshots.TryGetElapsedDay(out day);

    private RaidHostRecord? HostOf(FactionSide side) => _state.Hosts.FirstOrDefault(h => h.Side == side);

    private RaidHostRecord? Find(string partyId) => _state.Hosts.FirstOrDefault(h => h.PartyId == partyId);

    /// <summary>The host's keyed name from the data, or the generic keyed one when the data no longer lists it.</summary>
    private string NameOf(RaidHostRecord record) =>
        _configProvider.GetConfig().Hosts.FirstOrDefault(h => h.Id == record.HostId)?.Name
        ?? RaidHostDef.FallbackName;

    /// <summary>
    /// Clears every per-campaign field. Called from the behavior's constructor, which the feature module runs for every
    /// campaign before any event or SyncData, so campaign B never inherits campaign A's hosts (csharp-architecture.md).
    /// Releases nothing: the previous campaign's parties are gone.
    /// </summary>
    public void ResetForNewSession()
    {
        _state = new FireAndTributeState();
        _hostSides = NoHosts;
        _planned.Clear();
        _spawnedToday = 0;
        _raidsToday = 0;
        _maxHourMs = 0;
    }

    public string CaptureState() => FireAndTributeSaveCodec.Serialize(_state);

    public void LoadFrom(string? json)
    {
        ResetForNewSession();
        var parsed = FireAndTributeSaveCodec.Parse(json);
        foreach (var warning in parsed.Warnings)
            _logger.LogWarning($"{Tag} save record: {warning}");
        _state = parsed;
        PublishOwnSide();
    }

    /// <summary>
    /// After a load: a host restored into a co-op session has its pin released, a host whose party the engine lost is
    /// dropped (its side rests), and a host whose side is switched off is retired (removed now, or at the first hour its
    /// party is out of a battle).
    /// </summary>
    public void OnSessionLaunched()
    {
        if (_state.Hosts.Count == 0)
            return;
        if (!IsSinglePlayer)
        {
            ReleaseAll("a co-op session or a dedicated server is live");
            return;
        }

        foreach (var record in _state.Hosts.ToList())
        {
            if (!SideEnabled(record.Side))
                Retire(record, "its switch is off");
            else if (!_adapter.Read(record.PartyId).Exists)
                Lost(record);
        }
    }

    /// <summary>
    /// Unpins every host and forgets it, leaving each an ordinary outlaw party; never despawns. Each released host's side
    /// rests, as for a host that went home, so the session that ends cannot be followed by a second host at once.
    /// </summary>
    private void ReleaseAll(string why)
    {
        foreach (var record in _state.Hosts.ToList())
        {
            _adapter.ReleasePin(record.PartyId);
            StartCooldown(record.Side);
            _logger.LogInfo($"{Tag} {record.HostId} ({record.PartyId}) released: {why}");
        }
        _state.Hosts.Clear();
        _hostSides = NoHosts;
    }

    /// <summary>
    /// Removes a host because the feature may no longer run it. A party the engine lost is dropped like any lost host. A
    /// party the engine will not remove yet (it is in a battle) keeps its record and its pin and is tried again next hour,
    /// as a returning host's dismissal is: dropping the record would leave a pinned, full-size host roaming untracked, and
    /// let a side switched off and on again send a second one.
    /// </summary>
    private void Retire(RaidHostRecord record, string why)
    {
        if (!_adapter.Read(record.PartyId).Exists)
        {
            Lost(record);
            return;
        }
        if (!RemovePlanned(record.PartyId))
        {
            _logger.LogDebug($"{Tag} {record.HostId} ({record.PartyId}) cannot be removed yet ({why}); it is tried again next hour");
            return;
        }
        RemoveHost(record);
        _logger.LogInfo($"{Tag} {record.HostId} ({record.PartyId}) removed: {why}");
    }

    /// <summary>Despawns the party with its destroy event marked as planned. False when the engine refused (battle, inactive).</summary>
    private bool RemovePlanned(string partyId)
    {
        _planned.Add(partyId);
        try
        {
            return _adapter.Despawn(partyId);
        }
        finally
        {
            _planned.Remove(partyId);
        }
    }

    /// <summary>The party is gone and no event said so: drop the record and let the side rest.</summary>
    private void Lost(RaidHostRecord record)
    {
        RemoveHost(record);
        StartCooldown(record.Side);
        _logger.LogWarning($"{Tag} {record.HostId} ({record.PartyId}) is no longer on the map; its side rests");
    }

    /// <summary>The side rests from today; an unreadable clock starts no cooldown rather than one from day 0.</summary>
    private void StartCooldown(FactionSide side)
    {
        if (!TryToday(out int day))
            return;
        _state.CooldownUntil[side] = FireAndTributeMath.CooldownEnd(day, _configProvider.GetConfig().Tuning.CooldownDays);
    }

    public IReadOnlyList<string> Describe()
    {
        var lines = new List<string>();
        var config = _configProvider.GetConfig();
        bool hasDay = TryToday(out int day);
        lines.Add($"Fire and Tribute: {(_settings.Enabled ? "on" : "OFF in MCM")}, "
            + $"{(IsSinglePlayer ? "single-player" : "NOT single-player (stood down)")}, day {(hasDay ? day.ToString(Inv.InvariantCulture) : "unreadable")}, "
            + $"{config.Hosts.Count} host(s) configured.");
        // The window and the first-seen day as the next daily roll would use them: it notes a Full War it has not yet seen
        // before it asks, so the projection is applied here too, without recording it.
        int seen = hasDay ? ProjectedWarSeenDay(day) : _state.WarSeenDay;
        lines.Add($"Spawn window: {(hasDay && WindowOpen(day, config) ? "open" : "closed")} (war of the Ring {(_warOfTheRing.IsEnabled ? "on, phase " + _warOfTheRing.CurrentPhase : "off")}, "
            + $"Full War first seen day {(seen >= 0 ? seen.ToString(Inv.InvariantCulture) : "never")}).");
        lines.AddRange(EnvoyLines());
        foreach (var side in Sides)
        {
            var host = HostOf(side);
            string rest = _state.CooldownUntil.TryGetValue(side, out int until) ? $"cooldown until day {until}" : "no cooldown";
            string switchText = SideEnabled(side) ? "on" : "off";
            if (host == null)
            {
                lines.Add($"{side} ({switchText}): no host, {rest}.");
                continue;
            }
            lines.Add($"{side} ({switchText}): {host.HostId} party {host.PartyId} against {host.TargetKingdomId}, "
                + $"raids {host.Raids}/{host.Quota}, since day {host.StartDay}, {(host.Returning ? "going home" : "raiding")}, {rest}; {EnvoyText(host)}.");
        }
        lines.Add($"Today: spawned {_spawnedToday}, raids {_raidsToday}, slowest hourly step {_maxHourMs.ToString("F3", Inv.InvariantCulture)} ms.");
        return lines;
    }

    private void LogDaily(int day)
    {
        _logger.LogDebug(string.Format(Inv.InvariantCulture,
            "{0} day {1}: hosts={2}, spawned={3}, raids={4}, maxHourMs={5:F3}",
            Tag, day, _state.Hosts.Count, _spawnedToday, _raidsToday, _maxHourMs));
        _spawnedToday = 0;
        _raidsToday = 0;
        _maxHourMs = 0;
    }

    /// <summary>Keeps the slowest hourly step of the day: the service's own time, one step per campaign hour.</summary>
    private void RecordHour(long elapsedTicks)
    {
        double ms = elapsedTicks * 1000.0 / Stopwatch.Frequency;
        if (ms > _maxHourMs)
            _maxHourMs = ms;
    }
}
