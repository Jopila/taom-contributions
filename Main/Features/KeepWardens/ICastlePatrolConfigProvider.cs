using System.Collections.Generic;

namespace TAOM.Features.KeepWardens;

public interface ICastlePatrolConfigProvider
{
    /// <summary>Days between patrol spawns at Guard House level 1, 2 and 3. Three finite numbers above 0, at most 60.</summary>
    IReadOnlyList<float> SpawnDays { get; }

    /// <summary>The castle patrol's size limit at Guard House level 1, 2 and 3. Three whole numbers from 1 to 500.</summary>
    IReadOnlyList<int> SizeLimits { get; }
}
