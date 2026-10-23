using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.TelcharsMark.UI;

/// <summary>
/// Owns Telchar's Mark for one inventory screen. UIExtenderEx builds this mixin before each SPInventoryVM
/// constructor returns, which is after InitializeInventory filled both lists (v1.5.5 SPInventoryVM.cs:3116-3125,
/// :4533), so the open-time rescore runs here once over full lists. The per-screen adapter lives on this object,
/// so nothing outlives the screen and no other feature's captured VM is borrowed. With the feature off at open
/// there is no adapter: the postfixes then find nothing to do and the checkbox is hidden.
/// </summary>
[ViewModelMixin]
internal sealed class UpgradeScreenMixin : BaseViewModelMixin<SPInventoryVM>
{
    private static readonly ConditionalWeakTable<SPInventoryVM, UpgradeScreenMixin> Registry =
        new ConditionalWeakTable<SPInventoryVM, UpgradeScreenMixin>();

    private readonly TelcharsMarkService? _service;
    private readonly InventoryUpgradeScreen? _screen;
    private bool _showOnlyUpgrades;

    public UpgradeScreenMixin(SPInventoryVM viewModel) : base(viewModel)
    {
        TaomShowOnlyUpgradesHint = new HintViewModel(new TextObject(
            "{=taom_tm_toggle_hint}Show only better items: upgrades (green) and sidegrades (yellow). The move-all buttons (Take, Give, Buy, Sell or Discard all) then move only the rows shown."));
        Registry.Remove(viewModel);
        Registry.Add(viewModel, this);

        try
        {
            var settings = IoC.Resolve<ITelcharsMarkSettingsProvider>();
            if (!settings.IsEnabled) return;

            _service = IoC.Resolve<TelcharsMarkService>();
            _screen = new InventoryUpgradeScreen(viewModel, UpgradeRowMixin.RowFor);
            _showOnlyUpgrades = settings.ShowOnlyUpgradesByDefault;

            var watch = Stopwatch.StartNew();
            Rescore("open");
            watch.Stop();
            IoC.Resolve<IModLogger>().LogDebug($"[TelcharsMark] open rescore: {watch.Elapsed.TotalMilliseconds:F1} ms, "
                + $"{viewModel.RightItemListVM?.Count ?? 0} right rows, {viewModel.LeftItemListVM?.Count ?? 0} left rows");
        }
        catch (Exception ex)
        {
            TelcharsMarkLog.Error("screen open", ex);
        }
    }

    public static UpgradeScreenMixin? Find(SPInventoryVM? vm) =>
        vm != null && Registry.TryGetValue(vm, out var mixin) ? mixin : null;

    /// <summary>Whether the filter postfix should hide non-upgrades on this screen.</summary>
    internal bool ShowOnlyUpgrades => _screen != null && _showOnlyUpgrades;

    /// <summary>Rescores this screen's rows; re-runs the filters when the hide is on. Called by the postfixes.</summary>
    internal void Rescore(string source)
    {
        if (_screen == null || _service == null) return;
        try
        {
            _service.Rescore(_screen, reapplyFilters: _showOnlyUpgrades);
        }
        catch (Exception ex)
        {
            TelcharsMarkLog.Error(source, ex);
        }
    }

    /// <summary>Bound to the checkbox (IsSelected); a change re-runs the filters so rows hide or reappear.</summary>
    [DataSourceProperty]
    public bool TaomShowOnlyUpgrades
    {
        get => _showOnlyUpgrades;
        set
        {
            if (_showOnlyUpgrades == value) return;
            _showOnlyUpgrades = value;
            OnPropertyChangedWithValue(value, nameof(TaomShowOnlyUpgrades));
            try
            {
                _screen?.ReapplyFilters();
            }
            catch (Exception ex)
            {
                TelcharsMarkLog.Error("toggle", ex);
            }
        }
    }

    /// <summary>Shows the checkbox only when this screen's adapter was built, so a visible box always works.</summary>
    [DataSourceProperty]
    public bool TaomUpgradeLensEnabled => _screen != null;

    [DataSourceProperty]
    public HintViewModel TaomShowOnlyUpgradesHint { get; }
}
