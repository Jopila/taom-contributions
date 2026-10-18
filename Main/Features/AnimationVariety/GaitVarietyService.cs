using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.AnimationVariety;

/// <summary>
/// Breaks the lockstep of orc and goblin soldiers in a battle line. The walk phase advances by speed over
/// clip stride, clip duration and <c>StepSize</c>, starts at 0 for every agent, and vanilla gives every
/// troop of a type the same <c>StepSize</c>. Nothing keeps soldiers apart once they stand still: the native
/// locomotion update (0x7496D0) holds a fully idle agent's walk phase at its forward clip's
/// <c>bip_mov_ik</c> snapping start, so soldiers on one clip re-align at every halt, and the rate is also
/// lifted toward the clip's own rate at low speed (0x74BF60). A step size difference therefore builds up
/// only while the soldiers keep walking. This service gives each soldier, once at spawn, two per-soldier
/// differences:
/// <list type="number">
/// <item><description>Random step size: the vanilla step size times a factor in
/// <c>[minFactor, 1.0]</c>. The factor multiplies vanilla's own step size, so the applied value can go
/// below 0.80.</description></item>
/// <item><description>Random gait set: the agent's action set replaced by the base set or one of its
/// configured variants (<see cref="GaitVariantCatalog"/>), equal weights, variants that are not installed
/// skipped.</description></item>
/// </list>
/// Both go out in ONE <see cref="IGaitVarietyAgent.ApplyGait"/> call. Only the three orc and goblin warrior
/// base sets are touched; humans, mounts, the player, riders and every other set are left alone.
/// <para>
/// The draw is a pure function of the troop's <c>StringId</c> and <c>Agent.BodyPropertiesSeed</c>, never of
/// <c>Agent.Index</c> (indices recycle), so a soldier gets the same gait on every peer that spawns it with
/// the same seed. One instance per mission (the module builds it with the behavior). Main thread only:
/// <c>OnAgentBuild</c> and the mission end both run there.
/// </para>
/// </summary>
public sealed class GaitVarietyService
{
    public const float DefaultMinFactor = 0.92f;
    public const float MinFactorFloor = 0.80f;
    public const float MinFactorCeiling = 1.00f;

    private const string Tag = "[AnimVariety]";
    private const float MinUsableVanillaStepSize = 0.01f;

    private readonly GaitVariantCatalog _catalog;
    private readonly IGaitVarietySettingsProvider _settings;
    private readonly IModLogger _logger;
    private readonly Dictionary<string, SetStats> _stats = new(StringComparer.Ordinal);

    private bool _started;
    private bool _ended;
    private bool _vanillaStepWarned;
    private bool _minFactorWarned;

    public GaitVarietyService(GaitVariantCatalog catalog, IGaitVarietySettingsProvider settings, IModLogger logger)
    {
        _catalog = catalog;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Handles one freshly built agent. An engine exception from <see cref="IGaitVarietyAgent.ApplyGait"/>
    /// propagates, and that agent is not counted; the mission behavior is the boundary that catches it.
    /// </summary>
    public void OnAgentBuild(IGaitVarietyAgent agent)
    {
        if (_ended || agent == null) return;
        if (!agent.IsHuman || agent.IsMount || agent.IsPlayerControlled || agent.IsMounted) return;

        var baseSet = agent.ActionSetName;
        if (!_catalog.IsBaseSet(baseSet)) return;
        if (!_started) LogStart();

        // A positive requirement: NaN, infinity, zero, negatives and tiny values all fail it, and no lever
        // sends a bad step size to the engine.
        var vanilla = agent.VanillaStepSize;
        if (!FiniteFloatValidator.IsFiniteAtLeast(vanilla, MinUsableVanillaStepSize))
        {
            WarnVanillaStepOnce(vanilla);
            return;
        }

        bool stepOn = _settings.RandomStepSize;
        bool gaitOn = _settings.RandomGaitSet;
        if (!stepOn && !gaitOn) return;

        uint seed = Seed(agent.CharacterId, agent.BodyPropertiesSeed);

        // The factor is in [0.80, 1.0] and the vanilla step is finite and at least 0.01, so the product is
        // finite and positive.
        float step = vanilla;
        if (stepOn)
        {
            float min = EffectiveMinFactor();
            step = vanilla * (min + (1f - min) * UnitFromSeed(seed));
        }

        string chosen = baseSet;
        bool gaitActs = false;
        if (gaitOn)
        {
            var sets = _catalog.AvailableSets(baseSet);
            gaitActs = sets.Count > 1;
            if (gaitActs) chosen = sets[PickIndex(seed, sets.Count)];
        }

        if (!stepOn && !gaitActs) return;

        // A step lever alone re-sends the same set with the new step; a gait lever alone that drew the base
        // set changes nothing, so the engine (which restarts the walk phase and clears both channels on every
        // SetActionSet) is not called.
        if (stepOn || !string.Equals(chosen, baseSet, StringComparison.Ordinal))
            agent.ApplyGait(chosen, step);

        GetStats(baseSet).Add(chosen, stepOn ? step : (float?)null);
    }

    /// <summary>Writes one summary line per base set that had a lever act; later calls do nothing.</summary>
    public void End()
    {
        if (_ended) return;
        _ended = true;
        foreach (var pair in _stats.OrderBy(p => p.Key, StringComparer.Ordinal))
            _logger.LogInfo(pair.Value.Describe(pair.Key));
    }

    /// <summary>A stable 32-bit seed from the troop id and the agent's body properties seed.</summary>
    internal static uint Seed(string characterId, int bodyPropertiesSeed)
    {
        unchecked
        {
            uint hash = 2166136261u;
            if (characterId != null)
                foreach (char c in characterId)
                    hash = (hash ^ c) * 16777619u;
            return Mix(hash ^ (uint)bodyPropertiesSeed);
        }
    }

    /// <summary>A value in <c>[0, 1)</c>; the top 24 bits fit a float exactly, so 1.0 is unreachable.</summary>
    internal static float UnitFromSeed(uint seed) => (Mix(seed) >> 8) / 16777216f;

    /// <summary>An index in <c>[0, count)</c>, drawn independently of <see cref="UnitFromSeed"/>.</summary>
    internal static int PickIndex(uint seed, int count) => (int)(Mix(Mix(seed) ^ 0xD1B54A35u) % (uint)count);

    // Murmur3's 32-bit finalizer, offset so that seed 0 does not map to 0.
    private static uint Mix(uint value)
    {
        unchecked
        {
            value += 0x9E3779B9u;
            value = (value ^ (value >> 16)) * 0x85EBCA6Bu;
            value = (value ^ (value >> 13)) * 0xC2B2AE35u;
            return value ^ (value >> 16);
        }
    }

    private float EffectiveMinFactor()
    {
        float raw = _settings.StepSizeMinFactor;
        if (FiniteFloatValidator.IsFiniteInRange(raw, MinFactorFloor, MinFactorCeiling)) return raw;

        if (!_minFactorWarned)
        {
            _minFactorWarned = true;
            _logger.LogWarning($"{Tag} gait step size minimum factor {raw.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                $"is not a number from {Format(MinFactorFloor)} to {Format(MinFactorCeiling)}; using {Format(DefaultMinFactor)}");
        }
        return DefaultMinFactor;
    }

    private void WarnVanillaStepOnce(float vanilla)
    {
        if (_vanillaStepWarned) return;
        _vanillaStepWarned = true;
        _logger.LogWarning($"{Tag} gait vanilla step size {vanilla.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
            "is not a usable number; soldiers that carry it are left alone");
    }

    private void LogStart()
    {
        _started = true;
        // The factor is validated, and a bad one warned about, only while the step lever uses it.
        bool stepOn = _settings.RandomStepSize;
        double minFactor = stepOn ? EffectiveMinFactor() : _settings.StepSizeMinFactor;
        _logger.LogInfo($"{Tag} gait settings randomStepSize={stepOn} " +
            $"minFactor={Format(minFactor)} randomGaitSet={_settings.RandomGaitSet}");

        var variants = new StringBuilder(Tag).Append(" gait variants");
        foreach (var baseSet in GaitVariantCatalog.BaseSets)
            foreach (var variant in _catalog.ConfiguredVariants(baseSet))
                variants.Append(' ').Append(variant).Append(_catalog.VariantExists(variant) ? "=present" : "=missing");
        _logger.LogInfo(variants.ToString());
    }

    private SetStats GetStats(string baseSet)
    {
        if (!_stats.TryGetValue(baseSet, out var stats))
        {
            stats = new SetStats();
            _stats[baseSet] = stats;
        }
        return stats;
    }

    private static string Format(double value) => value.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);

    private sealed class SetStats
    {
        private readonly Dictionary<string, int> _variants = new(StringComparer.Ordinal);
        private int _agents;
        private int _steps;
        private float _min;
        private float _max;
        private double _sum;

        public void Add(string chosen, float? step)
        {
            _agents++;
            _variants[chosen] = _variants.TryGetValue(chosen, out var n) ? n + 1 : 1;
            if (step == null) return;
            float value = step.Value;
            if (_steps == 0 || value < _min) _min = value;
            if (_steps == 0 || value > _max) _max = value;
            _steps++;
            _sum += value;
        }

        public string Describe(string baseSet)
        {
            var line = new StringBuilder(Tag).Append(" gait set=").Append(baseSet)
                .Append(" n=").Append(_agents).Append(" variants");
            foreach (var variant in _variants.OrderBy(v => v.Key, StringComparer.Ordinal))
                line.Append(' ').Append(variant.Key).Append('=').Append(variant.Value);
            if (_steps == 0) return line.Append(" step off").ToString();
            return line.Append(" step n=").Append(_steps).Append(" min=").Append(Format(_min))
                .Append(" max=").Append(Format(_max)).Append(" mean=").Append(Format(_sum / _steps)).ToString();
        }
    }
}
