using System;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;

namespace TAOM.Features.Relations.Hooks;

/// <summary>
/// Thin boundary for the one-time seeding of personal relations (docs/features/personal-relations.md, ADR-002). It holds the
/// saved flag and asks <see cref="PersonalRelationSeedService"/> at the session start and on every daily tick, and it is the
/// only caller of <see cref="IPersonalRelationService.Engage"/>. The module builds it fresh for every campaign, so a new campaign starts with the flag unset.
/// </summary>
public sealed class PersonalRelationSeedBehavior : CampaignBehaviorBase
{
    private readonly PersonalRelationSeedService _service;
    private readonly IPersonalRelationService _personal;
    private readonly IModLogger _logger;
    private bool _seeded;

    public PersonalRelationSeedBehavior(PersonalRelationSeedService service, IPersonalRelationService personal, IModLogger logger)
    {
        _service = service;
        _personal = personal;
        _logger = logger;
        // The module builds this behavior in every campaign's OnGameStart: whatever the last campaign left armed is disarmed
        // before any relation is read, and stays so until this campaign's save is seeded.
        _personal.Engage(false);
    }

    /// <summary>True once the seeding has run (or a save says it did). Exposed for tests.</summary>
    internal bool Seeded => _seeded;

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
    }

    // The one persisted value. A save made before this feature has no key: the load leaves the flag false, so it seeds.
    public override void SyncData(IDataStore dataStore) => dataStore.SyncData("_taom_personalRelationsSeeded", ref _seeded);

    // Internal for TAOM.Tests (InternalsVisibleTo): the events cannot be raised outside a campaign.
    internal void OnSessionLaunched(CampaignGameStarter? starter) => Run();

    internal void OnDailyTick() => Run();

    private void Run()
    {
        try
        {
            _seeded = _service.TrySeed(_seeded);
            // Armed only in a single player campaign whose save is seeded. Every daily tick re-decides, so a switch seen off
            // re-arms the seeding (TrySeed returns false) and disarms the service until it has run again.
            _personal.Engage(_seeded && _service.IsSinglePlayer);
        }
        catch (Exception ex)
        {
            _personal.Engage(false);
            _logger.LogError($"[PersonalRelations] seeding failed: {ex.Message}");
        }
    }
}
