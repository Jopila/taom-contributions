using System.Collections.Generic;
using TAOM.Features.HostReview.Domain;

namespace TAOM.Features.HostReview;

/// <summary>Opens the review scene for a lineup; false when it could not be opened.</summary>
public interface IHostReviewMissionLauncher
{
    bool TryOpen(IReadOnlyList<HostReviewSlot> lineup);
}
