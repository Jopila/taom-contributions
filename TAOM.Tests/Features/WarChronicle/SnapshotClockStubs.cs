using NSubstitute;
using TAOM.Adapters;

namespace TAOM.Tests.Features.WarChronicle;

/// <summary>
/// Clock stubs for a substituted <see cref="IKingdomWarSnapshotAdapter"/>: a valid day answers both reads,
/// an invalid clock fails <see cref="IKingdomWarSnapshotAdapter.TryGetElapsedDay"/> the way the production
/// adapter does for a NaN, infinite or negative engine clock.
/// </summary>
internal static class SnapshotClockStubs
{
    public static void SetElapsedDay(this IKingdomWarSnapshotAdapter snapshots, int day)
    {
        snapshots.GetElapsedDay().Returns(day);
        snapshots.TryGetElapsedDay(out _).ReturnsForAnyArgs(c =>
        {
            c[0] = day;
            return true;
        });
    }

    public static void SetClockInvalid(this IKingdomWarSnapshotAdapter snapshots)
    {
        snapshots.GetElapsedDay().Returns(0);
        snapshots.TryGetElapsedDay(out _).ReturnsForAnyArgs(c =>
        {
            c[0] = 0;
            return false;
        });
    }
}
