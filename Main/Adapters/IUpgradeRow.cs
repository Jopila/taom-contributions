using TAOM.Features.TelcharsMark;

namespace TAOM.Adapters;

/// <summary>One inventory list row as Telchar's Mark scores it.</summary>
public interface IUpgradeRow
{
    /// <summary>The row's item and usability flags, read fresh on each access.</summary>
    ItemCandidate Candidate { get; }

    /// <summary>The row's verdict; setting it updates the row's tint.</summary>
    UpgradeVerdict Verdict { get; set; }
}
