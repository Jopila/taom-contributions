using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Library;
using TAOM.Core.Logging;

namespace TAOM.Features.SettlementServices.Hooks;

/// <summary>
/// Campaign entry point for Tidings of the Lists (#859, ADR-002, docs/features/settlement-services.md): the "Ask after
/// tournaments elsewhere" option in a town's arena menu, just above "Back to town center". It is added from
/// OnAfterSessionLaunched, which fires after every OnSessionLaunched listener, so the arena menu is fully built and the index read
/// from its options is final. Every decision is made by <see cref="TidingsService"/>; every write happens in the menu consequence
/// before the result inquiry opens, and the inquiry has an OK button and no callback. Each callback runs behind the shared
/// <see cref="ScreenFaultLog"/>. Holds no state and saves nothing.
/// </summary>
public sealed class TidingsMenuBehavior : CampaignBehaviorBase
{
    internal const string MenuId = "town_arena";
    internal const string OptionId = "taom_tidings_ask";
    internal const string BackOptionId = "town_arena_back";

    private readonly ScreenFaultLog _faults;
    private readonly TidingsService _service;

    public TidingsMenuBehavior(TidingsService service, IModLogger logger)
    {
        _faults = new ScreenFaultLog(logger, "SettlementServices");
        _service = service;
    }

    public override void RegisterEvents() =>
        CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, OnAfterSessionLaunched);

    public override void SyncData(IDataStore dataStore) { }

    private void OnAfterSessionLaunched(CampaignGameStarter starter) => _faults.Run("tidings session launched", () =>
    {
        // IndexOf is -1 when the back option is absent, and AddGameMenuOption appends for a negative index.
        var index = starter.GetPresumedGameMenu(MenuId).MenuOptions.Select(o => o.IdString).ToList().IndexOf(BackOptionId);
        starter.AddGameMenuOption(MenuId, OptionId,
            "{=taom_tidings_option}Ask after tournaments elsewhere ({TAOM_TIDINGS_FEE}{GOLD_ICON})",
            Condition, Consequence, isLeave: false, index: index);
    });

    // Runs when the menu is built or refreshed, not every frame. Hidden for every reason but a short purse, which greys it.
    private bool Condition(MenuCallbackArgs args) => _faults.Condition("menu condition", () =>
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
        var block = _service.Check();
        if (!TidingsService.IsShown(block))
            return false;
        SettlementServicesTexts.SetTidingsFee(TidingsService.Fee);
        if (block == TidingsBlock.NotEnoughGold)
        {
            args.IsEnabled = false;
            args.Tooltip = SettlementServicesTexts.NotEnoughGold();
        }

        return true;
    });

    private void Consequence(MenuCallbackArgs args) => _faults.Run("menu consequence", () =>
    {
        var result = _service.Ask();
        switch (result.Block)
        {
            case TidingsBlock.None:
                Show(SettlementServicesTexts.TidingsBody(result.Rows));
                break;
            case TidingsBlock.NoTournaments:
                Show(SettlementServicesTexts.TidingsNone());
                break;
            case TidingsBlock.NotEnoughGold:
                Show(SettlementServicesTexts.NotEnoughGold().ToString());
                break;
            default:
                // A problem, or a gate that closed between building the menu and the click: say so, change nothing.
                Show(SettlementServicesTexts.TidingsRefused());
                break;
        }
    });

    // Paused while it is up. Nothing mutates from the modal: the fee was taken before it opened.
    private static void Show(string body) =>
        InformationManager.ShowInquiry(new InquiryData(SettlementServicesTexts.TidingsTitle(), body, true, false,
            SettlementServicesTexts.Ok(), string.Empty, null, null), pauseGameActiveState: true);
}
