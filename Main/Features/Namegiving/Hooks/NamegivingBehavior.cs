using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.Namegiving.Hooks;

/// <summary>
/// The campaign entry point of Namegiving's lore names (#858, docs/features/namegiving.md): a settlement that changes hands is
/// reconciled against its holder's side, a clan that changes kingdom reconciles its own settlements (it keeps its fiefs with
/// no owner event), and the four lore settlements are reconciled once when a session launches, for a new campaign and a
/// loaded one alike. It persists nothing: a custom name is saved by the engine with the settlement. Every handler calls
/// <see cref="NamegivingService"/>, which catches its own faults, so a throw here cannot loop the loading screen
/// (docs/reviews/lessons/campaign-mechanics.md). Built fresh for every campaign start by <see cref="NamegivingModule"/>.
/// </summary>
public sealed class NamegivingBehavior : CampaignBehaviorBase
{
    private readonly NamegivingService _service;

    public NamegivingBehavior(NamegivingService service) => _service = service;

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => _service.ReconcileAllLore());
        CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
        CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
    }

    // The event fires after the new owner is set (ChangeOwnerOfSettlementAction.ApplyInternal), so the settlement's map faction is the new holder's.
    private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturer,
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        if (settlement != null)
            _service.ReconcileLore(settlement.StringId);
    }

    // Join, defection, creation and rebellion change clan.Kingdom and so each fief's map faction. Only that clan's settlements can have changed side.
    private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom,
        ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
    {
        var settlements = clan?.Settlements;
        if (settlements == null)
            return;
        foreach (var settlement in settlements)
            if (settlement != null)
                _service.ReconcileLore(settlement.StringId);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }
}
