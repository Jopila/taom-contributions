using TaleWorlds.CampaignSystem;

namespace TAOM.Adapters;

/// <inheritdoc />
public sealed class CampaignClockAdapter : ICampaignClockAdapter
{
    public double NowDays => CampaignTime.Now.ToDays;
}
