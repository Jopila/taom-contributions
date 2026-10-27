using System;
using System.Collections.Generic;

namespace TAOM.Features.FireAndTribute;

/// <summary>
/// The own-side half of <see cref="FireAndTributeService"/>: the snapshot that tells the party AI model which kingdoms leave
/// each live host alone. A host's clan is an outlaw clan at war with every kingdom, and a host rides out of its own side's
/// realms, so without this its own side's lords broke it before it reached its target (2026-10-26: 7 hosts, 0 raids).
/// Only the host's own party is spared; the clan's ordinary bandits stay vanilla's.
/// </summary>
public sealed partial class FireAndTributeService : IRaidHostOwnSideQuery
{
    private static readonly IReadOnlyDictionary<string, HashSet<string>> NoHosts =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

    // Host party id to the kingdom ids of its side. Written whole on the main thread, read on the AI worker.
    private volatile IReadOnlyDictionary<string, HashSet<string>> _ownSide = NoHosts;

    public bool AnyHost => _ownSide.Count > 0;

    public bool IsOwnSide(string? kingdomId, string? hostPartyId) =>
        kingdomId != null && hostPartyId != null
        && _ownSide.TryGetValue(hostPartyId, out var kingdoms) && kingdoms.Contains(kingdomId);

    /// <summary>Drops the record and its snapshot entry; copies the snapshot without reading a kingdom.</summary>
    private void RemoveHost(RaidHostRecord record)
    {
        _state.Hosts.Remove(record);
        var current = _ownSide;
        if (!current.ContainsKey(record.PartyId))
            return;
        var map = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var entry in current)
        {
            if (entry.Key != record.PartyId)
                map[entry.Key] = entry.Value;
        }
        _ownSide = map.Count == 0 ? NoHosts : map;
    }

    /// <summary>
    /// Rebuilds the snapshot from the live records and the kingdoms' sides: when a host spawns, at session launch, and
    /// once a day, so a kingdom that changes side is followed within a day. Not hourly: each kingdom snapshot reads the
    /// kingdom's strength, wars and prisoners.
    /// </summary>
    private void PublishOwnSide()
    {
        if (_state.Hosts.Count == 0)
        {
            _ownSide = NoHosts;
            return;
        }

        var kingdoms = _snapshots.GetKingdoms();
        var map = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var record in _state.Hosts)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kingdom in kingdoms)
            {
                if (kingdom != null && _alignment.ResolveSide(kingdom.Id, kingdom.CultureId) == record.Side)
                    ids.Add(kingdom.Id);
            }
            map[record.PartyId] = ids;
        }
        _ownSide = map;
    }
}
