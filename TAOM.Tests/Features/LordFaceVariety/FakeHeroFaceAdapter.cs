using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;

namespace TAOM.Tests.Features.LordFaceVariety;

/// <summary>
/// In-memory <see cref="IHeroFaceAdapter"/>: a roll writes a key built from the hero's current key, the roll
/// kind, the seed and the variation, so two heroes on one key get different results only from different
/// seeds. With <see cref="StickyRolls"/> set the roll "succeeds" and the key stays the same.
/// </summary>
internal sealed class FakeHeroFaceAdapter : IHeroFaceAdapter
{
    private readonly Dictionary<string, HeroFaceSnapshot> _heroes = new(StringComparer.Ordinal);

    public List<(string Kind, string Id, int Seed, float Variation, float Age)> Calls { get; } = new();

    public HashSet<string> ThrowOn { get; } = new(StringComparer.Ordinal);

    public HashSet<string> RefuseOn { get; } = new(StringComparer.Ordinal);

    public bool StickyRolls { get; set; }

    public FakeHeroFaceAdapter Add(HeroFaceSnapshot snapshot)
    {
        _heroes[snapshot.Id] = snapshot;
        return this;
    }

    public HeroFaceSnapshot Get(string id) => _heroes[id];

    public IReadOnlyList<HeroFaceSnapshot> GetSnapshots() => _heroes.Values.ToList();

    public bool TryRollInRange(HeroFaceSnapshot hero, int seed) => Roll("range", hero, seed, 0f, 0f);

    public bool TryRollAroundOwnFace(HeroFaceSnapshot hero, int seed, float variation, float rollAge) =>
        Roll("own", hero, seed, variation, rollAge);

    private bool Roll(string kind, HeroFaceSnapshot snapshot, int seed, float variation, float age)
    {
        var id = snapshot.Id;
        Calls.Add((kind, id, seed, variation, age));
        if (ThrowOn.Contains(id)) throw new InvalidOperationException("roll failed for " + id);
        if (RefuseOn.Contains(id) || !_heroes.TryGetValue(id, out var hero)) return false;
        if (!StickyRolls)
            _heroes[id] = hero with { StaticKey = $"{kind}:{hero.StaticKey}:{seed}:{variation}", OnRangeMin = false };
        return true;
    }

    public static HeroFaceSnapshot Hero(
        string id,
        string key = "SHARED",
        string race = "human",
        bool female = false,
        bool player = false,
        bool original = true,
        bool leader = false,
        bool onMin = false,
        bool spread = false) =>
        new(id, race, female, player, original, leader, key, onMin, spread);
}
