using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.Relations;

/// <inheritdoc cref="IRelationGrantService"/>
public sealed class RelationGrantService : IRelationGrantService
{
    internal const int MaxMagnitude = 100;

    private readonly IHeroRelationAdapter _relations;
    private readonly IDedicatedServerProvider _server;
    private readonly IModLogger _logger;

    public RelationGrantService(IHeroRelationAdapter relations, IDedicatedServerProvider server, IModLogger logger)
    {
        _relations = relations;
        _server = server;
        _logger = logger;
    }

    public bool Grant(string heroId, int delta, string reasonForLog)
    {
        // A dedicated server has no player to earn or lose relation with.
        if (string.IsNullOrWhiteSpace(heroId) || delta == 0 || _server.IsDedicatedServer)
            return false;

        var clamped = Math.Max(-MaxMagnitude, Math.Min(MaxMagnitude, delta));
        if (!_relations.ApplyPlayerRelation(heroId, clamped))
            return false;

        _logger.LogInfo($"[Relations] {heroId} {(clamped > 0 ? "+" : "")}{clamped} ({reasonForLog}).");
        return true;
    }
}
