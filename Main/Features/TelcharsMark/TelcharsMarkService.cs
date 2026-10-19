using TAOM.Adapters;

namespace TAOM.Features.TelcharsMark;

/// <summary>Scores the rows of one inventory screen and decides which rows the filter hides.</summary>
public sealed class TelcharsMarkService
{
    private readonly ItemUpgradeScorer _scorer;

    public TelcharsMarkService(ItemUpgradeScorer scorer)
    {
        _scorer = scorer;
    }

    /// <summary>Scores every row against the shown loadout, then re-runs the filters when the hide is on.</summary>
    public void Rescore(IUpgradeScreen screen, bool reapplyFilters)
    {
        var loadout = screen.GetLoadout();
        foreach (var row in screen.GetRows()) row.Verdict = _scorer.Score(row.Candidate, loadout);
        if (reapplyFilters) screen.ReapplyFilters();
    }

    /// <summary>With "show only better items" on: hide a row vanilla left visible that is no upgrade.</summary>
    public static bool ShouldHide(IUpgradeRow row, bool vanillaFiltered) =>
        !vanillaFiltered && row.Verdict == UpgradeVerdict.None;
}
