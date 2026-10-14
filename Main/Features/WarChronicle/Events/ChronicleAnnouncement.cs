namespace TAOM.Features.WarChronicle.Events;

/// <summary>
/// One queued chronicle announcement: texts as authored (<c>{=taom_wc_*}English</c>) plus the values
/// their variables take. The presenter resolves them at show time, so nothing here is localized and
/// nothing is saved.
/// </summary>
public sealed class ChronicleAnnouncement
{
    public ChronicleAnnouncement(string eventId, string title, string? text, string? targetSettlementId, int days)
    {
        EventId = eventId;
        Title = title;
        Text = text;
        TargetSettlementId = targetSettlementId;
        Days = days;
    }

    public string EventId { get; }

    public string Title { get; }

    /// <summary>The body line; null shows the title alone.</summary>
    public string? Text { get; }

    /// <summary>Fills <c>{TARGET}</c> with that settlement's name; null leaves it empty.</summary>
    public string? TargetSettlementId { get; }

    /// <summary>Fills <c>{DAYS}</c>.</summary>
    public int Days { get; }
}
