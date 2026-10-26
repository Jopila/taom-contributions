using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TAOM.Core.Logging;

namespace TAOM.Features.KeepWardens;

/// <summary>
/// The castle twin of a vanilla refresh (#888, docs/features/keep-wardens.md). <c>PatrolPartiesCampaignBehavior.OnBuildingLevelChanged</c>
/// bumps the patrol's roster version only for the town Guard House, and <c>PartyBase.PartySizeLimit</c> is cached on that version,
/// so a castle patrol kept its old limit and its old N/M readout after an upgrade. This listener bumps the version for the
/// castle Guard House. It only invalidates a cache, so it has no settings gate and no save data. Vanilla's first-patrol queue call
/// is private and not copied: a castle that builds its first Guard House still waits for the next spawn check.
/// </summary>
public sealed class CastleGuardHouseUpgradeBehavior : CampaignBehaviorBase
{
    private readonly IModLogger _logger;

    public CastleGuardHouseUpgradeBehavior(IModLogger logger) => _logger = logger;

    public override void RegisterEvents() =>
        CampaignEvents.OnBuildingLevelChangedEvent.AddNonSerializedListener(this, OnBuildingLevelChanged);

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnBuildingLevelChanged(Town town, Building building, int levelChange)
    {
        try
        {
            if (building.BuildingType == DefaultBuildingTypes.CastleGuardHouse)
                town.Settlement.PatrolParty?.Party.MemberRoster.UpdateVersion();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[KeepWardens] castle Guard House upgrade refresh threw {ex.GetType().Name}: {ex.Message}");
        }
    }
}
