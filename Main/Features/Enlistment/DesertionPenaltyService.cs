using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content;

namespace TAOM.Features.Enlistment;

/// <summary>
/// What deserting during the minimum term costs (#833). Host only because both producers check the co-op
/// authority before requesting a desertion discharge. Skipped for a dead commander.
/// </summary>
public interface IDesertionPenaltyService
{
    /// <summary>
    /// Apply the cost toward the commander captured BEFORE the record reset. The relation and Honor
    /// values come from enlistment_config.json. A dead commander skips both: the dishonour is toward him.
    /// </summary>
    void Apply(string commanderHeroId, bool commanderAlive);
}

/// <inheritdoc />
public sealed class DesertionPenaltyService : IDesertionPenaltyService
{
    private readonly IEnlistmentContentConfigProvider _config;
    private readonly ICommanderLordAdapter _commander;
    private readonly IPlayerTraitAdapter _traits;
    private readonly IModLogger _logger;

    public DesertionPenaltyService(
        IEnlistmentContentConfigProvider config,
        ICommanderLordAdapter commander,
        IPlayerTraitAdapter traits,
        IModLogger logger)
    {
        _config = config;
        _commander = commander;
        _traits = traits;
        _logger = logger;
    }

    public void Apply(string commanderHeroId, bool commanderAlive)
    {
        if (string.IsNullOrEmpty(commanderHeroId) || !commanderAlive)
        {
            _logger?.LogInfo("[Enlistment] desertion cost skipped: no living commander to dishonour");
            return;
        }

        var cfg = _config.GetConfig();
        var relation = cfg.DesertionRelationPenalty;
        var honor = cfg.DesertionHonorXpPenalty;

        if (relation != 0 && !_commander.ApplyPlayerRelation(commanderHeroId, relation))
            _logger?.LogInfo($"[Enlistment] desertion relation cost not applied: '{commanderHeroId}' could not be reached");
        if (honor != 0)
            _traits.AddTraitXp("Honor", honor);
    }
}
