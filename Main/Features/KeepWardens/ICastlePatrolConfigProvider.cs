using System.Collections.Generic;

namespace TAOM.Features.KeepWardens;

public interface ICastlePatrolConfigProvider
{
    /// <summary>Days between patrol spawns at Guard House level 1, 2 and 3. Three finite numbers above 0, at most 60.</summary>
    IReadOnlyList<float> SpawnDays { get; }

    /// <summary>The castle patrol's size limit at Guard House level 1, 2 and 3. Three whole numbers from 1 to 500.</summary>
    IReadOnlyList<int> SizeLimits { get; }

    /// <summary>Security per day a castle with a live patrol gains at Guard House level 1, 2 and 3. Three finite numbers from 0 to 10.</summary>
    IReadOnlyList<float> SecurityPerDay { get; }

    /// <summary>The chance, from 0 to 1, that the AI builds a Guard House ahead of a random pick once it already has one.</summary>
    float GuardHousePriorityChance { get; }
}
