using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.Relations;

/// <summary>
/// Seeds an old save for personal relations, once. Vanilla keeps the player's earned relation on the clan leader and about 0 on
/// every other member of the clan; with personal relations on, those members would read 0. So each living lord whose raw
/// relation with the player is 0 takes his clan leader's raw value, and the caller then records that it is done.
/// </summary>
public sealed class PersonalRelationSeedService
{
    private readonly IPersonalRelationSeedAdapter _adapter;
    private readonly IPersonalRelationSettingsProvider _settings;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _server;
    private readonly IModLogger _logger;

    public PersonalRelationSeedService(IPersonalRelationSeedAdapter adapter, IPersonalRelationSettingsProvider settings,
        ICoopSessionProvider coop, IDedicatedServerProvider server, IModLogger logger)
    {
        _adapter = adapter;
        _settings = settings;
        _coop = coop;
        _server = server;
        _logger = logger;
    }

    /// <summary>
    /// Returns the new value of the caller's saved "seeded" flag. Outside single player (a co-op host, a client
    /// deferring to the host, a dedicated server) the loaded flag is handed back untouched and nothing is seeded. With the switch off it is
    /// false, so that switching on later seeds again. Otherwise it is true once the seeding is done (now, or already) and
    /// false when it could not run (the campaign could not be read).
    /// </summary>
    public bool TrySeed(bool alreadySeeded)
    {
        if (!IsSinglePlayer)
            return alreadySeeded;

        if (!_settings.Enabled)
            return false;

        if (alreadySeeded)
            return true;

        var rows = _adapter.Snapshot();
        if (rows == null)
            return false;

        var changes = Plan(rows);
        var written = 0;
        foreach (var change in changes)
        {
            if (_adapter.SetRelation(change.HeroId, change.Value))
                written++;
        }

        _logger.LogInfo($"[PersonalRelations] seeded {written} of {changes.Count} lord(s) from their clan leaders.");
        return true;
    }

    /// <summary>
    /// True for a single player campaign on this peer. Read live, on the main thread, once a day (ICoopSessionProvider's
    /// contract). The same predicate as the hunt: it stands down while a co-op session is live (host included), while a co-op
    /// mod TAOM cannot probe defers to the host, and on a dedicated server.
    /// </summary>
    public bool IsSinglePlayer =>
        !_coop.IsSessionActive && !_coop.ShouldDeferToHost && !_server.IsDedicatedServer;

    /// <summary>The pure rule: which raw relations to write for these rows.</summary>
    public static IReadOnlyList<SeedChange> Plan(IReadOnlyList<SeedRow> rows)
    {
        // A clan's value is its leader's. The player's clan and the clanless have no seeding.
        var leaders = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row.IsClanLeader && !row.IsPlayerClan && !string.IsNullOrEmpty(row.ClanId) && !leaders.ContainsKey(row.ClanId!))
                leaders[row.ClanId!] = row.Relation;
        }

        var changes = new List<SeedChange>();
        foreach (var row in rows)
        {
            // Only a member at exactly 0: any other value is a relation he earned himself, and is kept.
            if (row.IsClanLeader || row.IsPlayerClan || string.IsNullOrEmpty(row.ClanId) || row.Relation != 0)
                continue;
            if (leaders.TryGetValue(row.ClanId!, out var value) && value != 0)
                changes.Add(new SeedChange(row.HeroId, value));
        }

        return changes;
    }
}
