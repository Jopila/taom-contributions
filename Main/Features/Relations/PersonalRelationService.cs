using TAOM.Features.InitialChildGeneration;

namespace TAOM.Features.Relations;

/// <inheritdoc cref="IPersonalRelationService"/>
public sealed class PersonalRelationService : IPersonalRelationService
{
    private readonly IPersonalRelationSettingsProvider _settings;
    private readonly IRandomSource _random;
    private volatile bool _engaged;

    public PersonalRelationService(IPersonalRelationSettingsProvider settings, IRandomSource random)
    {
        _settings = settings;
        _random = random;
    }

    // Engaged by the seeding behavior only, and read through a volatile field so no lock is needed. The switch is one read
    // through the provider's cached settings reference; no co-op state is read here (those reads lock or use reflection).
    public bool IsActive => _engaged && _settings.Enabled;

    public void Engage(bool engaged) => _engaged = engaged;

    public PersonalPairSide Resolve(bool effective1IsPlayer, bool effective2IsPlayer) =>
        !IsActive || effective1IsPlayer == effective2IsPlayer ? PersonalPairSide.None
        : effective1IsPlayer ? PersonalPairSide.UnmapSecond
        : PersonalPairSide.UnmapFirst;

    public int? DisbandCost(bool isLeaderParty)
    {
        // Only the army leader is repriced; a member keeps vanilla's -1.
        if (!isLeaderParty || !IsActive)
            return null;

        var settings = _settings.Get();
        return settings.DisbandCostEnabled ? -_random.Next(settings.LeaderMin, settings.LeaderMax + 1) : null;
    }

    public int? InheritedRelationChange(bool relationHeroIsPlayer) => relationHeroIsPlayer && IsActive ? 0 : null;
}
