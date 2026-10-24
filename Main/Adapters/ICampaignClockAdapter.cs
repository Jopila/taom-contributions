namespace TAOM.Adapters;

/// <summary>The campaign date in days (ADR-007 seam over CampaignTime), so a service can stamp an event and be tested.</summary>
public interface ICampaignClockAdapter
{
    double NowDays { get; }
}
