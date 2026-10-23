using System;
using System.Collections.Generic;
using System.Linq;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Settlements;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Features.Namegiving.UI;

/// <summary>
/// The Rename button on the kingdom Fiefs panel (docs/features/namegiving.md). The prefab inserts it as the last child of the action column under the Annex button, with
/// <c>DataSource {..}</c>, which lands on this view model. The button follows the selected fief: KingdomSettlementVM raises
/// <c>PropertyChangedWithValue</c> (never <c>PropertyChanged</c>) when the selection changes, so the constructor subscribes there
/// and <see cref="OnFinalize"/> unsubscribes. RefreshSettlementList is never called: it resets the selection. After a rename the
/// fief's row view model takes the new name and refreshes its village cards. KingdomManagementVM declares its own OnFinalize,
/// so this mixin stacks no hook on the base ViewModel.
/// </summary>
[ViewModelMixin("RefreshValues")]
internal class FiefNamingMixin : BaseViewModelMixin<KingdomManagementVM>
{
    private readonly NamegivingService? _service;
    private readonly NamegivingPrompts? _prompts;
    private readonly IModLogger? _logger;
    private readonly KingdomSettlementVM? _settlements;
    private readonly HashSet<string> _logged = new(StringComparer.Ordinal);
    private bool _isVisible, _canRename;
    private int _cost;

    public FiefNamingMixin(KingdomManagementVM viewModel) : base(viewModel)
    {
        try
        {
            // First, so a later fault reaches the log.
            _logger = IoC.Resolve<IModLogger>();
            _service = IoC.Resolve<NamegivingService>();
            _prompts = new NamegivingPrompts(_service, LogOnce, Renamed);
            RenameText = NamegivingTexts.Button().ToString();
            RenameHint.HintText = new TextObject("{=!}" + NamegivingTexts.PickBody());
            _settlements = viewModel.Settlement;
            _settlements.PropertyChangedWithValue += OnSettlementChanged;
            // No Refresh() here: UIExtenderEx replays the view model constructor's RefreshValues call on the mixin as OnRefresh.
        }
        catch (Exception ex)
        {
            LogOnce("constructor", ex); // a throw here would take the kingdom screen down
        }
    }

    [DataSourceProperty]
    public string RenameText { get; } = "";

    [DataSourceProperty]
    public bool IsRenameVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value, nameof(IsRenameVisible));
    }

    [DataSourceProperty]
    public bool CanRename
    {
        get => _canRename;
        set => SetField(ref _canRename, value, nameof(CanRename));
    }

    [DataSourceProperty]
    public int RenameCost
    {
        get => _cost;
        set => SetField(ref _cost, value, nameof(RenameCost));
    }

    // The enabled-state hint is a child of the button; the disabled-state hint sits beside it and is empty while enabled.
    [DataSourceProperty]
    public HintViewModel RenameHint { get; } = new();

    [DataSourceProperty]
    public HintViewModel RenameDisabledHint { get; } = new();

    public override void OnRefresh() => Guard("refresh", Refresh);

    public override void OnFinalize()
    {
        if (_settlements != null)
            _settlements.PropertyChangedWithValue -= OnSettlementChanged;
    }

    [DataSourceMethod]
    public void ExecuteRenameFief() => Guard("click", () =>
    {
        var id = SelectedFiefId();
        if (id != null) _prompts?.Start(id);
    });

    private void OnSettlementChanged(object sender, PropertyChangedWithValueEventArgs e)
    {
        if (e.PropertyName == "CurrentSelectedSettlement")
            Guard("selection", Refresh);
    }

    private string? SelectedFiefId() => _settlements?.CurrentSelectedSettlement?.Settlement?.StringId;

    private void Refresh()
    {
        if (_service == null)
            return;
        var state = _service.ButtonState(SelectedFiefId());
        IsRenameVisible = state.Visible;
        CanRename = state.Enabled;
        RenameCost = state.Cost;
        RenameDisabledHint.HintText = state.Enabled ? TextObject.GetEmpty() : new TextObject("{=!}" + NamegivingTexts.Block(state.Block, state.Detail));
    }

    // Copy the new name into the fief's row (list row and panel title), refresh the village cards, re-read the spent influence.
    private void Renamed(string fiefId)
    {
        var item = _settlements?.Settlements.FirstOrDefault(i => i.Settlement.StringId == fiefId);
        if (item != null)
        {
            item.Name = item.Settlement.Name.ToString();
            item.RefreshValues();
        }

        Refresh();
    }

    private void Guard(string site, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            LogOnce(site, ex);
        }
    }

    private void LogOnce(string site, Exception ex)
    {
        if (_logged.Add(site))
            _logger?.LogError($"[Namegiving] {site} threw. This line is logged once per screen and later calls still run. Root cause: {ex}");
    }
}
