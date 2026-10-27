namespace TAOM.Features.InnRounds.Domain;

/// <summary>
/// The rounds the player has stood on one campaign day, kept by the campaign's behavior and saved with it. A day that is not
/// today's counts as no rounds. <see cref="Day"/> is the campaign day (<c>CampaignTime.ToDays</c>, whole), -1 before any round.
/// </summary>
public sealed class RoundLedger
{
    public const int NoDay = -1;

    public int Day { get; set; } = NoDay;

    public int Count { get; set; }

    /// <summary>The rounds stood on <paramref name="today"/>.</summary>
    public int UsedOn(int today) => Day == today ? Count : 0;

    /// <summary>Counts one more round for <paramref name="today"/>, starting the count over when the saved day is another one.</summary>
    public void Record(int today)
    {
        if (Day != today)
        {
            Day = today;
            Count = 0;
        }
        Count++;
    }
}
