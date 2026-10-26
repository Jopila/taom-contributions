using TAOM.Adapters;

namespace TAOM.Features.KeepWardens;

/// <summary>
/// Keep Wardens' decisions (#888, docs/features/keep-wardens.md), with no TaleWorlds type. A castle with a built Guard House
/// gets a patrol party like a town does; the model asks this service for the plan. The template calls the base model
/// (DefaultSettlementPatrolModel) only when the plan has no strength (a town, a naval request, a culture without the
/// template); Can and the spawn delay call base whenever the plan is not Allowed (base answers false for a castle, and
/// vanilla asks for the delay only after Can says yes). Towns keep vanilla's answer.
/// </summary>
public sealed class CastlePatrolService
{
    private const int Strengths = 3;

    private readonly ICastlePatrolConfigProvider _config;
    private readonly ICastlePatrolSettingsProvider _settings;

    public CastlePatrolService(ICastlePatrolConfigProvider config, ICastlePatrolSettingsProvider settings)
    {
        _config = config;
        _settings = settings;
    }

    /// <summary>
    /// A castle (not naval) whose owner culture has the patrol template for the clamped level gets a strength, whatever the
    /// switch says: vanilla's hourly refill asks for the template of a live patrol without asking whether the settlement may
    /// have one, and the base model answers null for every castle. Only a NEW patrol needs the switch on, a loyal owner and a
    /// built Guard House (<see cref="CastlePatrolPlan.Allowed"/>). Anything else is <c>default</c>.
    /// </summary>
    public CastlePatrolPlan Plan(CastlePatrolSnapshot castle, bool naval)
    {
        if (naval || !castle.IsCastle)
            return default;

        var strength = Clamp(castle.GuardHouseLevel);
        if (!castle.HasTemplateFor(strength))
            return default;

        var allowed = _settings.Enabled && !castle.OwnerIsRebel && castle.GuardHouseLevel >= 1;
        return new CastlePatrolPlan(allowed, strength, _config.SpawnDays[strength - 1]);
    }

    /// <summary>
    /// The size limit for a castle patrol whose base limit came back 0 (vanilla reads only the town Guard House), else 0 for
    /// "leave the base alone". It ignores the setting on purpose: a live patrol must never drop to a limit of 0.
    /// </summary>
    public int SizeLimitOverride(int guardHouseLevel, float baseResult) =>
        guardHouseLevel == CastlePatrolSnapshot.NotACastlePatrol || baseResult > 0f ? 0 : _config.SizeLimits[Clamp(guardHouseLevel) - 1];

    /// <summary>
    /// The daily security a castle gains for its live patrol (#889): the configured amount for its Guard House level, clamped to
    /// 1 to 3, while the patrol is alive and the Guard House is built. 0 for a town (vanilla adds its own line for towns, so a
    /// second one would double it), for no live patrol and for no Guard House. Ignores the Castle Patrols switch: it lives only
    /// while a patrol does, and turning the switch off already removes the patrol.
    /// </summary>
    public float SecurityPerDay(CastlePatrolSnapshot castle) =>
        castle.IsCastle && castle.PatrolAlive && castle.GuardHouseLevel >= 1 ? _config.SecurityPerDay[Clamp(castle.GuardHouseLevel) - 1] : 0f;

    /// <summary>
    /// True when the AI should queue the Guard House as its next build (#889): the setting is on, the building passes vanilla's
    /// own filter, and it is either not built yet (always) or the roll is under the configured chance. The roll comes from the
    /// adapter, so the decision stays pure.
    /// </summary>
    public bool PicksGuardHouse(GuardHouseCandidate candidate) =>
        _settings.BuildPriorityEnabled && candidate.Eligible && (candidate.Level == 0 || candidate.Roll < _config.GuardHousePriorityChance);

    private static int Clamp(int level) => level < 1 ? 1 : level > Strengths ? Strengths : level;
}
