using System;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.TownManagement;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Features.ReevesLedger.UI;

/// <summary>
/// The +10K and -10K buttons beside the reserve's plus button (docs/features/reeves-ledger.md). A press asks
/// <see cref="ReevesLedgerService.PlanReserveStep"/> for the step from the reserve view model's own figures (the reserve it
/// holds and its capped purse), then runs the view model's own <c>ExecuteConfirm</c> on the new total: the engine's helper
/// charges a rise and refunds a fall in one call, so a withdrawal costs nothing, and the confirm refreshes the texts and fires
/// the vanilla callback that rebuilds the stat rows. The slider is never driven with a negative amount: its discrete widget
/// floors to hundreds and writes the value back, which would make gold.
/// Hooked on ExecuteConfirm, which runs after every reserve change, so the button states follow the purse and the reserve.
/// Each button has two hints: the one inside the button shows while it is enabled, the one beside it (the vanilla governor
/// button's shape) shows while it is disabled, because Gauntlet never hovers a disabled widget. All four are created once and
/// their text is updated in place.
/// </summary>
[ViewModelMixin("ExecuteConfirm")]
internal class ReserveStepMixin : BaseViewModelMixin<TownManagementReserveControlVM>
{
    private readonly ReevesLedgerService? _service;
    private readonly ScreenFaultLog? _faults;
    private readonly bool _enabled;
    private readonly TextObject _addNone = new("{=taom_reeve_add_none}You have no gold to add.");
    private readonly TextObject _withdrawNone = new("{=taom_reeve_withdraw_none}The reserve is empty.");
    private bool _isVisible;
    private bool _canAdd;
    private bool _canWithdraw;

    public ReserveStepMixin(TownManagementReserveControlVM viewModel) : base(viewModel)
    {
        try
        {
            // First, so a fault in anything below reaches the log.
            _faults = new ScreenFaultLog(IoC.Resolve<IModLogger>(), "ReevesLedger");
            _service = IoC.Resolve<ReevesLedgerService>();
            _enabled = IoC.Resolve<ReevesLedgerSettingsProvider>().StepsEnabled;
            AddReserveStepText = new TextObject("{=taom_reeve_add}+10K").ToString();
            WithdrawReserveStepText = new TextObject("{=taom_reeve_withdraw}-10K").ToString();
            AddReserveStepHint.HintText = new TextObject("{=taom_reeve_add_hint}Add up to 10,000 gold to the reserve.");
            WithdrawReserveStepHint.HintText = new TextObject("{=taom_reeve_withdraw_hint}Take up to 10,000 gold back from the reserve.");
            Refresh();
        }
        catch (Exception ex)
        {
            // The constructor runs inside the view model's own: a throw here would take the whole window down.
            _enabled = false;
            _faults?.LogOnce("constructor", ex);
        }
    }

    [DataSourceProperty]
    public string AddReserveStepText { get; } = "";

    [DataSourceProperty]
    public string WithdrawReserveStepText { get; } = "";

    [DataSourceProperty]
    public bool IsReserveStepVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value, nameof(IsReserveStepVisible));
    }

    [DataSourceProperty]
    public bool CanAddReserveStep
    {
        get => _canAdd;
        set => SetField(ref _canAdd, value, nameof(CanAddReserveStep));
    }

    [DataSourceProperty]
    public bool CanWithdrawReserveStep
    {
        get => _canWithdraw;
        set => SetField(ref _canWithdraw, value, nameof(CanWithdrawReserveStep));
    }

    /// <summary>Shown while the add button is enabled (a child of the button).</summary>
    [DataSourceProperty]
    public HintViewModel AddReserveStepHint { get; } = new();

    /// <summary>Shown while the add button is disabled (a sibling of the button); empty while it is enabled.</summary>
    [DataSourceProperty]
    public HintViewModel AddReserveStepDisabledHint { get; } = new();

    [DataSourceProperty]
    public HintViewModel WithdrawReserveStepHint { get; } = new();

    [DataSourceProperty]
    public HintViewModel WithdrawReserveStepDisabledHint { get; } = new();

    public override void OnRefresh()
    {
        if (_enabled)
            _faults!.Run("refresh", Refresh);
    }

    [DataSourceMethod]
    public void ExecuteAddReserveStep() => Step(ReserveDirection.Add, "add step");

    [DataSourceMethod]
    public void ExecuteWithdrawReserveStep() => Step(ReserveDirection.Withdraw, "withdraw step");

    private void Refresh()
    {
        var vm = ViewModel;
        if (vm == null)
            return;
        var visible = _enabled && _service!.MayWriteWorld;
        var add = _service!.PlanReserveStep(vm.CurrentReserveAmount, vm.MaxReserveAmount, ReserveDirection.Add);
        var withdraw = _service.PlanReserveStep(vm.CurrentReserveAmount, vm.MaxReserveAmount, ReserveDirection.Withdraw);
        IsReserveStepVisible = visible;
        CanAddReserveStep = visible && add.HasStep;
        CanWithdrawReserveStep = visible && withdraw.HasStep;
        AddReserveStepDisabledHint.HintText = add.HasStep ? TextObject.GetEmpty() : _addNone;
        WithdrawReserveStepDisabledHint.HintText = withdraw.HasStep ? TextObject.GetEmpty() : _withdrawNone;
    }

    private void Step(ReserveDirection direction, string site)
    {
        if (_enabled)
            _faults!.Run(site, () => StepCore(direction, site));
    }

    private void StepCore(ReserveDirection direction, string site)
    {
        var vm = ViewModel;
        if (vm == null || !_service!.MayWriteWorld)
            return;
        var step = _service.PlanReserveStep(vm.CurrentReserveAmount, vm.MaxReserveAmount, direction);
        if (!step.HasStep)
            return;
        // Load-bearing order: the confirm applies CurrentReserveAmount + CurrentGivenAmount through the engine's helper (a rise
        // is charged to the player, a fall refunded), so the given amount is zeroed first and the target is the whole total.
        vm.CurrentGivenAmount = 0;
        vm.CurrentReserveAmount = step.Target;
        vm.ExecuteConfirm();
        if (vm.CurrentReserveAmount != step.Target)
            _faults!.LogOnce(site + " check", $"the reserve is {vm.CurrentReserveAmount} after a step to {step.Target}; the engine changed it under the button");
    }
}
