using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// One hero's face facts, read once per pass (Lord Face Variety, #952). <paramref name="StaticKey"/> is the
/// hero's current static face key as text; <paramref name="OnRangeMin"/> says the hero still holds the
/// minimum of its character's base range, and <paramref name="RangeHasSpread"/> says that range's minimum and
/// maximum differ, so a roll inside it can give another face. <see cref="Handle"/> is the engine hero the
/// snapshot was read from (null in tests); only the adapter reads it.
/// </summary>
public sealed record HeroFaceSnapshot(
    string Id,
    string RaceKey,
    bool IsFemale,
    bool IsPlayer,
    bool IsOriginal,
    bool IsClanLeader,
    string StaticKey,
    bool OnRangeMin,
    bool RangeHasSpread)
{
    internal object? Handle { get; init; }
}

/// <summary>Reads and rewrites the static face of campaign heroes.</summary>
public interface IHeroFaceAdapter
{
    /// <summary>Every hero alive, dead or disabled. Throws when no campaign is running.</summary>
    IReadOnlyList<HeroFaceSnapshot> GetSnapshots();

    /// <summary>
    /// Gives the hero a random face between the minimum and maximum of its character's base range, and a weight
    /// and build between that range's, the roll <c>DefaultHeroCreationModel</c> and <c>HeroCreator</c> make for
    /// a hero born from a template. False when the snapshot does not come from this adapter.
    /// </summary>
    bool TryRollInRange(HeroFaceSnapshot hero, int seed);

    /// <summary>
    /// Gives the hero a face near its own current one: the hero's face is both ends of the range and
    /// <paramref name="variation"/> moves it. The roll is made at <paramref name="rollAge"/> instead of the
    /// hero's own age, which picks the hairstyle and beard tables. Weight and build are left alone. False when
    /// the snapshot does not come from this adapter.
    /// </summary>
    bool TryRollAroundOwnFace(HeroFaceSnapshot hero, int seed, float variation, float rollAge);
}
