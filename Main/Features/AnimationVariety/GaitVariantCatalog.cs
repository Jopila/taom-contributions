using System;
using System.Collections.Generic;
using TAOM.Adapters;

namespace TAOM.Features.AnimationVariety;

/// <summary>
/// Which gait variant action sets exist. A base set (<c>as_orc_warrior</c>) has up to two configured
/// variants, <c>_b</c> and <c>_c</c>; a variant counts only when the engine loaded a set of exactly that
/// name. The engine's action sets are listed once per process by name
/// (<see cref="IGaitVarietyAdapter.LoadedActionSetNames"/>), and membership answers every question: asking
/// the engine for a single unknown id would make its native lookup log "Action set could not be found" for
/// each miss, a line the triage table reads as a broken base set.
/// <para>
/// The base sets are the three a soldier can spawn on. <c>as_orc_female_warrior</c> is not one: the Armory
/// <c>orc</c> Monster declares no <c>female_action_set</c>, so a female orc spawns on
/// <c>as_orc_warrior</c>; the goblin Monster does declare one.
/// </para>
/// <para>
/// Registered as a singleton: action sets load once per process, so the listing is read once. A missing
/// variant is reported by the <c>=missing</c> flag of <see cref="GaitVarietyService"/>'s variants line, once
/// per mission, and never warns here. Used from the main thread (<c>OnAgentBuild</c>).
/// </para>
/// </summary>
public sealed class GaitVariantCatalog
{
    private static readonly string[] BaseSetIds = { "as_orc_warrior", "as_goblin_warrior", "as_goblin_female_warrior" };

    private static readonly string[] VariantSuffixes = { "_b", "_c" };

    private readonly IGaitVarietyAdapter _engine;
    private readonly Dictionary<string, IReadOnlyList<string>> _available = new(StringComparer.Ordinal);
    private HashSet<string>? _loaded;

    public GaitVariantCatalog(IGaitVarietyAdapter engine)
    {
        _engine = engine;
    }

    /// <summary>The only action sets this feature touches.</summary>
    public static IReadOnlyList<string> BaseSets => BaseSetIds;

    public bool IsBaseSet(string actionSetName) => Array.IndexOf(BaseSetIds, actionSetName) >= 0;

    /// <summary>The configured variant ids of a base set, loaded or not; empty for any other name.</summary>
    public IReadOnlyList<string> ConfiguredVariants(string baseSet)
    {
        if (!IsBaseSet(baseSet)) return Array.Empty<string>();
        var variants = new string[VariantSuffixes.Length];
        for (int i = 0; i < variants.Length; i++)
            variants[i] = baseSet + VariantSuffixes[i];
        return variants;
    }

    /// <summary>True when the engine loaded an action set named exactly <paramref name="variantId"/>.</summary>
    public bool VariantExists(string variantId) => !string.IsNullOrEmpty(variantId) && Loaded().Contains(variantId);

    /// <summary>
    /// The base set followed by the variants that exist, in <c>_b</c>, <c>_c</c> order. Any other name
    /// resolves to itself.
    /// </summary>
    public IReadOnlyList<string> AvailableSets(string baseSet)
    {
        if (_available.TryGetValue(baseSet, out var cached)) return cached;
        var sets = new List<string> { baseSet };
        foreach (var variant in ConfiguredVariants(baseSet))
            if (VariantExists(variant)) sets.Add(variant);
        _available[baseSet] = sets;
        return sets;
    }

    private HashSet<string> Loaded()
    {
        if (_loaded != null) return _loaded;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in _engine.LoadedActionSetNames())
            if (!string.IsNullOrEmpty(name)) names.Add(name);
        _loaded = names;
        return names;
    }
}
