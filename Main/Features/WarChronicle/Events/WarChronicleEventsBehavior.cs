using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.WarChronicle.Events;

/// <summary>
/// Thin router for the War Chronicle's event chain (docs/features/war-chronicle.md "The event chain", #765): it turns
/// campaign events into calls on <see cref="ChronicleService"/> and drains the announcement queue. It holds
/// no state and saves nothing: <c>WarChronicleBehavior</c> is the one SyncData owner, and the chronicle's
/// rows travel in its payload. Built fresh in every campaign's OnGameStart (WarChronicleModule, pinned by
/// WarChronicleWiringTests), so its constructor clears the per-campaign state the save does not carry: the
/// announcement queue and the army focus.
///
/// <para>World work is the co-op authority's and writes save-backed state, so every chronicle call checks
/// <c>IsAuthority</c> and <c>MayWriteSaveBackedState</c>; the queue re-checks the authority when it shows.</para>
/// </summary>
public class WarChronicleEventsBehavior : CampaignBehaviorBase
{
    private readonly ChronicleService _chronicle;
    private readonly ChronicleAnnouncementQueue _announcements;
    private readonly ICoopSessionProvider _coop;

    public WarChronicleEventsBehavior(ChronicleService chronicle, ChronicleAnnouncementQueue announcements, ICoopSessionProvider coop)
    {
        _chronicle = chronicle;
        _announcements = announcements;
        _coop = coop;

        _announcements.ResetForNewSession();
        _chronicle.ResetTransientState();
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
        CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(this, OnSiegeEventStarted);
        CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // Nothing: the chronicle's rows are a section of WarChronicleBehavior's payload.
    }

    private bool MayRunWorldWork => _coop.IsAuthority && CoopSessionPolicy.MayWriteSaveBackedState(_coop.IsCoopClient);

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        if (MayRunWorldWork)
            _chronicle.LaunchChronicle();
    }

    private void OnDailyTick()
    {
        if (MayRunWorldWork)
            _chronicle.RunChronicleDay();
    }

    // Fires twice per conquest (capture, then the fief grant); the service's resolution is idempotent.
    private void OnSettlementOwnerChanged(
        Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero,
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        if (MayRunWorldWork)
            _chronicle.OnTargetOwnerChanged(settlement?.StringId);
    }

    private void OnSiegeEventStarted(SiegeEvent siegeEvent)
    {
        if (MayRunWorldWork)
            _chronicle.OnTargetBesieged(siegeEvent?.BesiegedSettlement?.StringId);
    }

    private void OnTick(float dt) => _announcements.TryShowNext();
}
