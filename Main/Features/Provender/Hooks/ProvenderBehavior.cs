using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Adapters;

namespace TAOM.Features.Provender.Hooks;

/// <summary>
/// Thin campaign boundary of the Provender-master (ADR-002, docs/features/provender.md): it binds the dialog lines to this
/// campaign's <see cref="ProvenderConversationFlow"/>, runs the food order when the main party enters or leaves a settlement and
/// the slaughter order on the daily tick, and saves the two orders in <c>SyncData</c>. The module builds it with <c>new</c> for
/// every campaign, so a new campaign starts with both orders off and a load restores its own.
/// </summary>
public sealed class ProvenderBehavior : CampaignBehaviorBase
{
    internal const string AutoFoodKey = "_taom_provender_autoFood";
    internal const string SlaughterKey = "_taom_provender_slaughter";

    private readonly ProvenderService _service;
    private readonly ProvenderConversationFlow _flow;

    public ProvenderBehavior(ProvenderService service, IProvenderWorldAdapter world)
    {
        _service = service;
        _flow = new ProvenderConversationFlow(service, world);
    }

    internal ProvenderOrders Orders => _flow.Orders;

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
        CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeft);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // A load starts each value at off and a key miss leaves it there. A save made before this feature has no record for
        // this behavior at all, so SyncData never runs and the orders keep their fresh, off defaults.
        var autoFood = dataStore.IsSaving && Orders.AutoFood;
        var slaughter = dataStore.IsSaving && Orders.Slaughter;
        dataStore.SyncData(AutoFoodKey, ref autoFood);
        dataStore.SyncData(SlaughterKey, ref slaughter);
        if (dataStore.IsSaving)
            return;
        Orders.AutoFood = autoFood;
        Orders.Slaughter = slaughter;
    }

    private void OnSessionLaunched(CampaignGameStarter starter) => ProvenderDialogLines.Register(starter, _flow);

    private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
    {
        if (party != null && party == MobileParty.MainParty && settlement != null)
            _service.TopUpFood(Orders, settlement.StringId);
    }

    private void OnSettlementLeft(MobileParty party, Settlement settlement)
    {
        if (party != null && party == MobileParty.MainParty && settlement != null)
            _service.TopUpFood(Orders, settlement.StringId);
    }

    private void OnDailyTick() => _service.DailySlaughter(Orders);
}
