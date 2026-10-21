using TAOM.Adapters;

namespace TAOM.Features.FarWatch.Hooks;

/// <summary>
/// The one place a returned or ended ride is shown to the player (#847): the pick, the wait menu and the hourly look all end
/// rides, and all three show the same thing. A report opens as a paused inquiry (see <see cref="IFarWatchNotifier"/>); a ride
/// that ended early, or whose scout did not come back, is one line.
/// </summary>
internal sealed class FarWatchPresenter
{
    private readonly IFarWatchNotifier _notifier;

    public FarWatchPresenter(IFarWatchNotifier notifier) => _notifier = notifier;

    internal void Report(ScoutReport? report)
    {
        if (report == null) return;
        if (report.Ended != null)
        {
            End(report.Ended);
            return;
        }

        if (report.Lost)
            End(new ScoutEndNotice(ScoutEndReason.Lost, report.ScoutName));
        else
            _notifier.ShowReport(FarWatchReportText.Title(report), FarWatchReportText.Body(report), FarWatchReportText.Close());
    }

    internal void End(ScoutEndNotice? notice)
    {
        if (notice != null) _notifier.ShowMessage(FarWatchTexts.EndMessage(notice.Value));
    }
}
