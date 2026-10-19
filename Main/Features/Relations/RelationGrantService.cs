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

    public int Grant(string heroId, int delta, string reasonForLog)
    {
        // A dedicated server has no player to earn or lose relation with.
        if (string.IsNullOrWhiteSpace(heroId) || delta == 0 || _server.IsDedicatedServer)
            return 0;

        var clamped = Math.Max(-MaxMagnitude, Math.Min(MaxMagnitude, delta));
        var landed = _relations.ApplyPlayerRelation(heroId, clamped);
        if (landed != 0)
            _logger.LogInfo($"[Relations] {heroId} asked {clamped:+0;-0}, landed {landed:+0;-0} ({reasonForLog}).");
        return landed;
    }
}
