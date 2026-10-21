using System;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;

namespace TAOM.Features.MinorClanMigration.Hooks;

/// <summary>
/// Thin boundary for the minor-clan load migration (docs/features/minor-factions.md, ADR-002). It runs
/// <see cref="MinorClanMigrationService"/> once per loaded save. It saves nothing: the corrected fields are the
/// engine's own saved clan state.
/// </summary>
public sealed class MinorClanMigrationBehavior : CampaignBehaviorBase
{
    private readonly MinorClanMigrationService _service;
    private readonly IModLogger _logger;

    public MinorClanMigrationBehavior(MinorClanMigrationService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public override void RegisterEvents()
        => CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);

    public override void SyncData(IDataStore dataStore)
    {
    }

    // Internal for TAOM.Tests (InternalsVisibleTo): the event cannot be raised outside a campaign.
    internal void OnGameLoaded(CampaignGameStarter starter)
    {
        try
        {
            _service.Migrate();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[MinorClanMigration] migration failed: {ex.Message}");
        }
    }
}
