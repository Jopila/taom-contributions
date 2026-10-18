using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TAOM.Adapters;
using TAOM.Core.Collections;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.AnimationVariety;

/// <summary>
/// Proof probe for the action-set <c>alternative_group</c> attribute: does the engine pick a random
/// swing clip among alternative action codes? It checks every action set once per mission for the
/// conventional alternative codes (<c>act_taom_&lt;base without "act_"&gt;_&lt;b..h&gt;</c>), warns about the
/// setups that crash or mistime, counts which code each swing plays, and records how far into the clip
/// a hit lands. Logging only; it changes nothing in the game. Every engine read goes through
/// <see cref="IAnimVarietyAdapter"/> and <see cref="IAnimVarietyAgent"/>. One instance per mission
/// (the module builds it with the behavior), so there is no state to reset.
/// <para>
/// Off unless <see cref="IAnimVarietyProbeSettingsProvider.SwingAlternativeProbe"/> is on, read once at the
/// first tick: a probe that is off looks up no action code, sweeps no agent and writes no line. When it is
/// on, any loaded <c>act_taom_*</c> alternative code arms a per-tick sweep of the mission's human agents.
/// It has never counted a swing in a real battle yet (no alternative code is shipped), so its summary
/// format is unproven in game.
/// </para>
/// <para>
/// Threading: <see cref="Tick"/> and <see cref="End"/> run on the main thread. <see cref="RecordHit"/>
/// mutates the same maps, so the mission behavior parks it through <c>DeferredCallbackQueue.RunOrDefer</c>
/// when native raises <c>OnAgentHit</c> off the main thread. <see cref="IsTracking"/> is a volatile flag
/// the hit callback may read from any thread.
/// </para>
/// </summary>
public sealed class AnimVarietyService
{
    private const string Tag = "[AnimVariety]";
    private const float TimingToleranceSeconds = 0.01f;
    private const float BlendOutToleranceProgress = 0.01f;
    private const float SummarySeconds = 30f;
    private const string AlternativeLetters = "bcdefgh";

    private static readonly string[] Families = { "release_", "quick_release_", "blocked_", "quick_blocked_" };
    private static readonly string[] Directions = { "overswing", "slashleft", "slashright", "thrust" };
    private static readonly string[] Suffixes = { "_1h", "_1h_left_stance" };

    private readonly IAnimVarietyAdapter _engine;
    private readonly IAnimVarietyProbeSettingsProvider _settings;
    private readonly IModLogger _logger;

    private readonly List<(string Base, string Alt)> _pairs = new();
    private readonly Dictionary<int, SetInfo> _sets = new();
    private readonly Dictionary<object, AgentState> _states = new(ReferenceIdentity.Instance);
    private readonly Dictionary<string, HitStat> _hits = new();
    private readonly List<object> _pruneBuffer = new();

    private volatile bool _tracking;
    private bool _initialized;
    private bool _off;
    private bool _ended;
    private int _stamp;
    private float _time;
    private float _nextSummary;

    public AnimVarietyService(IAnimVarietyAdapter engine, IAnimVarietyProbeSettingsProvider settings, IModLogger logger)
    {
        _engine = engine;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>True while the mission has alternative codes and the probe is counting.</summary>
    public bool IsTracking => _tracking;

    /// <summary>Agents the probe currently holds state for (test seam).</summary>
    internal int TrackedAgentCount => _states.Count;

    /// <param name="dt">Frame seconds; a non-finite or non-positive value does not advance the summary clock.</param>
    /// <param name="agentSource">Asked for the human agents only once the probe is on, so a mission without
    /// alternative codes never builds the list.</param>
    public void Tick(float dt, Func<IReadOnlyList<IAnimVarietyAgent>> agentSource)
    {
        if (_ended || _off) return;
        if (FiniteFloatValidator.IsFinite(dt) && dt > 0f) _time += dt;

        if (!_initialized)
        {
            Initialize();
            if (_off) return;
        }

        var agents = agentSource();
        _stamp++;
        for (int i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            if (agent == null || !agent.IsActive) continue;

            var handle = agent.Handle;
            if (!_states.TryGetValue(handle, out var state))
            {
                state = new AgentState(GetSet(agent.ActionSetKey));
                _states[handle] = state;
            }
            state.Stamp = _stamp;
            if (state.Set.Tracked) ObserveSwing(state, agent);
        }
        Prune();

        // Positive requirement: _time only ever holds finite values (the dt gate above).
        if (_time >= _nextSummary)
        {
            WriteSummary();
            _nextSummary = _time + SummarySeconds;
        }
    }

    /// <summary>
    /// Records where in the clip a hit landed for a tracked attacker. <paramref name="actionName"/> and
    /// <paramref name="progress"/> are the attacker's channel-1 values read when the hit arrived.
    /// A missile blow is ignored: the projectile lands long after the throw or shot, while channel 1 may
    /// already play a later swing, and it would be filed under that swing's clip.
    /// </summary>
    public void RecordHit(object attackerHandle, string actionName, float progress, bool isMissile)
    {
        if (isMissile || !_tracking || attackerHandle == null || string.IsNullOrEmpty(actionName)) return;
        if (!FiniteFloatValidator.IsFinite(progress)) return;
        if (!_states.TryGetValue(attackerHandle, out var state) || !state.Set.Tracked) return;

        if (!_hits.TryGetValue(actionName, out var stat))
        {
            stat = new HitStat();
            _hits[actionName] = stat;
        }
        stat.Add(progress);
    }

    /// <summary>Writes the final summary once; later calls and ticks do nothing.</summary>
    public void End()
    {
        if (_ended) return;
        _ended = true;
        _tracking = false;
        if (_initialized && !_off) WriteSummary();
    }

    private void Initialize()
    {
        _initialized = true;
        if (!_settings.SwingAlternativeProbe)
        {
            _off = true;
            return;
        }

        foreach (var family in Families)
        foreach (var direction in Directions)
        foreach (var suffix in Suffixes)
        {
            var baseCode = "act_" + family + direction + suffix;
            if (!_engine.ActionCodeExists(baseCode)) continue;
            foreach (var letter in AlternativeLetters)
            {
                var alt = "act_taom_" + baseCode.Substring(4) + "_" + letter;
                if (_engine.ActionCodeExists(alt)) _pairs.Add((baseCode, alt));
            }
        }

        if (_pairs.Count == 0)
        {
            _off = true;
            _logger.LogInfo(Tag + " no alternative groups found; swing tracking off");
            return;
        }
        _tracking = true;
        _nextSummary = _time + SummarySeconds;
    }

    private SetInfo GetSet(int key)
    {
        if (_sets.TryGetValue(key, out var set)) return set;
        set = new SetInfo(key, _engine.ActionSetName(key));
        _sets[key] = set;
        set.Tracked = CheckGroups(set);
        return set;
    }

    private bool CheckGroups(SetInfo set)
    {
        bool anyValid = false;
        foreach (var (baseCode, alt) in _pairs)
        {
            bool alternatives = _engine.AreAlternatives(set.Key, baseCode, alt);
            bool baseExists = _engine.ClipExists(set.Key, baseCode);
            bool altExists = _engine.ClipExists(set.Key, alt);
            float baseDuration = baseExists ? _engine.ClipDuration(set.Key, baseCode) : float.NaN;
            float altDuration = altExists ? _engine.ClipDuration(set.Key, alt) : float.NaN;
            // The blend-out start goes through the native logging slot lookup, so it is read only for a clip that exists.
            float baseBlendOut = baseExists ? _engine.ClipBlendOutStart(set.Key, baseCode) : float.NaN;
            float altBlendOut = altExists ? _engine.ClipBlendOutStart(set.Key, alt) : float.NaN;

            _logger.LogInfo($"{Tag} group set={set.Name} base={baseCode} alt={alt} alternatives={alternatives} " +
                $"baseClip={(baseExists ? _engine.ClipName(set.Key, baseCode) : "none")} baseClipExists={baseExists} " +
                $"baseDuration={FormatNumber(baseExists, baseDuration)} baseBlendOut={FormatNumber(baseExists, baseBlendOut)} " +
                $"altClip={(altExists ? _engine.ClipName(set.Key, alt) : "none")} altClipExists={altExists} " +
                $"altDuration={FormatNumber(altExists, altDuration)} altBlendOut={FormatNumber(altExists, altBlendOut)} " +
                "note=the melee table row of the alt code cannot be checked from managed code; a missing row crashes the first swing");

            string where = $"{Tag} WARNING set={set.Name} base={baseCode} alt={alt}: ";
            if (!alternatives)
                _logger.LogWarning(where + "group disabled: one member, or the base row lacks alternative_group");
            if (!baseExists)
                _logger.LogWarning(where + "CRASH RISK: no clip for the base code");
            // The parser writes the {clip, group} slot only after the clip resolves, so the group stays -1 and
            // the random pick never returns this code.
            if (!altExists)
                _logger.LogWarning(where + "no clip for the alt code: group disabled for that code (not a crash risk)");
            // Positive requirements: a NaN duration or blend-out start fails "within tolerance" and is reported.
            if (baseExists && altExists &&
                (!(Math.Abs(baseDuration - altDuration) <= TimingToleranceSeconds) ||
                 !(Math.Abs(baseBlendOut - altBlendOut) <= BlendOutToleranceProgress)))
                _logger.LogWarning(where + $"TIMING RISK: base {FormatNumber(true, baseDuration)} s with blend-out start " +
                    $"{FormatNumber(true, baseBlendOut)} vs alt {FormatNumber(true, altDuration)} s with blend-out start " +
                    $"{FormatNumber(true, altBlendOut)}, and the hit timing comes from the base clip's duration minus its blend-out period");

            if (alternatives) anyValid = true;
        }
        return anyValid;
    }

    private static void ObserveSwing(AgentState state, IAnimVarietyAgent agent)
    {
        int action = agent.Channel1ActionIndex;
        bool release = agent.Channel1IsAttackRelease;
        // A swing starts when channel 1 enters AttackRelease: a new code, or the same code after leaving it.
        if (release && (action != state.LastAction || !state.WasRelease))
        {
            var name = agent.Channel1ActionName ?? "act_none";
            var swings = state.Set.Swings;
            swings[name] = swings.TryGetValue(name, out var n) ? n + 1 : 1;
        }
        state.LastAction = action;
        state.WasRelease = release;
    }

    private void Prune()
    {
        _pruneBuffer.Clear();
        foreach (var pair in _states)
            if (pair.Value.Stamp != _stamp) _pruneBuffer.Add(pair.Key);
        for (int i = 0; i < _pruneBuffer.Count; i++) _states.Remove(_pruneBuffer[i]);
    }

    private void WriteSummary()
    {
        foreach (var set in _sets.Values.OrderBy(s => s.Name, StringComparer.Ordinal))
        {
            int total = set.Swings.Values.Sum();
            if (total <= 0) continue;
            var line = new StringBuilder(Tag).Append(" swings set=").Append(set.Name);
            foreach (var swing in set.Swings.OrderBy(s => s.Key, StringComparer.Ordinal))
            {
                int percent = (int)Math.Round(100.0 * swing.Value / total, MidpointRounding.AwayFromZero);
                line.Append(' ').Append(swing.Key).Append('=').Append(swing.Value).Append(" (").Append(percent).Append("%)");
            }
            _logger.LogInfo(line.ToString());
        }

        foreach (var hit in _hits.OrderBy(h => h.Key, StringComparer.Ordinal))
        {
            var stat = hit.Value;
            _logger.LogInfo($"{Tag} hits {hit.Key} n={stat.Count} progress mean={Format(stat.Sum / stat.Count)} " +
                $"min={Format(stat.Min)} max={Format(stat.Max)}");
        }
    }

    private static string Format(double value) => value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    private static string FormatNumber(bool exists, float value) =>
        exists ? value.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) : "n/a";

    private sealed class SetInfo
    {
        public SetInfo(int key, string name)
        {
            Key = key;
            Name = name ?? "<unnamed>";
        }

        public int Key { get; }
        public string Name { get; }
        public bool Tracked { get; set; }
        public Dictionary<string, int> Swings { get; } = new();
    }

    private sealed class AgentState
    {
        public AgentState(SetInfo set) => Set = set;

        public SetInfo Set { get; }
        public int Stamp { get; set; }
        public int LastAction { get; set; } = int.MinValue;
        public bool WasRelease { get; set; }
    }

    private sealed class HitStat
    {
        public int Count { get; private set; }
        public double Sum { get; private set; }
        public float Min { get; private set; }
        public float Max { get; private set; }

        public void Add(float progress)
        {
            if (Count == 0 || progress < Min) Min = progress;
            if (Count == 0 || progress > Max) Max = progress;
            Count++;
            Sum += progress;
        }
    }
}
