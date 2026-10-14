namespace TAOM.Features.WarChronicle.Events;

/// <summary>One line of <c>taom.chronicle_status</c>: an event, its state and its days.</summary>
public sealed class ChronicleStatusRow
{
    public string Id { get; set; } = string.Empty;

    public ChronicleEventKind Kind { get; set; }

    public ChronicleEventState State { get; set; }

    public double? OpenDay { get; set; }

    public double? CloseDay { get; set; }

    public bool Assaulted { get; set; }

    public double? ResolvedDay { get; set; }

    public string? Target { get; set; }
}
