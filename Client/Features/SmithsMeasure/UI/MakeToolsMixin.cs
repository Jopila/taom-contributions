using System;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.SmithsMeasure.UI;

/// <summary>
/// The Make Tools card of the crafting screen (#910, docs/features/smiths-measure.md). It hooks the view model's private
/// <c>RefreshEnableMainAction</c>, which <c>UpdateAll</c> runs after every tab switch and which a hero change, a refinement
/// selection and a smelt selection run directly, so the card follows the tab, the hero and the stores. The hero is converted to
/// plain values at this boundary (<see cref="CraftingWorkshopAdapter"/>, then <see cref="MakeToolsService"/>). A press makes
/// the tools through the service and then calls the view model's own public <c>ExecuteSwitchToRefinement</c>, which runs
/// <c>UpdateAll</c> and so refreshes the material counts, the stamina bars and this card; nothing is reached by reflection.
/// The card shows on the refinement tab only. It writes the party's stores and the hero's stamina from this client, so it is
/// left out under co-op.
/// </summary>
[ViewModelMixin("RefreshEnableMainAction")]
[CoopSuppressedUi("Make Tools writes the party's stores and the hero's crafting stamina from this client, which a co-op host owns")]
internal class MakeToolsMixin : BaseViewModelMixin<CraftingVM>
{
    private readonly MakeToolsService _service;
    private readonly Func<Hero?, ICraftingWorkshopAdapter?> _workshopOf;
    private readonly Action<CraftingVM> _switchToRefinement;
    private readonly IModLogger? _logger;
    private bool _logged;
    private bool _isVisible;
    private bool _isEnabled;
    private string _text = "";
    private MakeToolsQuote _quote;
    private readonly BasicTooltipViewModel _hint;

    public MakeToolsMixin(CraftingVM viewModel)
        : this(viewModel, IoC.Resolve<MakeToolsService>(), hero => CraftingWorkshopAdapter.For(hero),
            vm => vm.ExecuteSwitchToRefinement(), ResolveLogger())
    {
    }

    // The seam for TAOM.Tests: a real adapter reads the campaign, and the real tab switch runs UpdateAll on a live screen.
    internal MakeToolsMixin(
        CraftingVM viewModel,
        MakeToolsService service,
        Func<Hero?, ICraftingWorkshopAdapter?> workshopOf,
        Action<CraftingVM> switchToRefinement,
        IModLogger? logger) : base(viewModel)
    {
        _service = service;
        _workshopOf = workshopOf;
        _switchToRefinement = switchToRefinement;
        _logger = logger;
        // One tooltip for the card's life, reading the latest quote on hover; replacing it each refresh would only add a rebind.
        _hint = new BasicTooltipViewModel(() => SmithsMeasureTexts.Hint(_quote));
    }

    [DataSourceProperty]
    public bool IsMakeToolsVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value, nameof(IsMakeToolsVisible));
    }

    [DataSourceProperty]
    public bool IsMakeToolsEnabled
    {
        get => _isEnabled;
        set => SetField(ref _isEnabled, value, nameof(IsMakeToolsEnabled));
    }

    [DataSourceProperty]
    public string MakeToolsText
    {
        get => _text;
        set => SetField(ref _text, value, nameof(MakeToolsText));
    }

    [DataSourceProperty]
    public BasicTooltipViewModel MakeToolsHint => _hint;

    public override void OnRefresh() => Guard(Refresh);

    [DataSourceMethod]
    public void ExecuteMakeTools() => Guard(Press);

    private void Press()
    {
        var vm = ViewModel;
        var workshop = vm != null && vm.IsInRefinementMode ? _workshopOf(vm.CurrentCraftingHero?.Hero) : null;
        if (vm == null || workshop == null)
            return;

        if (_service.TryMake(workshop))
            _switchToRefinement(vm);
        else
            Refresh();
    }

    private void Refresh()
    {
        var vm = ViewModel;
        var workshop = vm != null && vm.IsInRefinementMode ? _workshopOf(vm.CurrentCraftingHero?.Hero) : null;
        if (workshop == null)
        {
            Hide();
            return;
        }

        var quote = _service.Quote(workshop);
        if (quote.Verdict == MakeToolsVerdict.Disabled)
        {
            Hide();
            return;
        }

        MakeToolsText = SmithsMeasureTexts.Label();
        _quote = quote;
        IsMakeToolsEnabled = quote.Verdict == MakeToolsVerdict.Ready;
        IsMakeToolsVisible = true;
    }

    private void Hide()
    {
        IsMakeToolsVisible = false;
        IsMakeToolsEnabled = false;
    }

    private void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Hide();
            LogOnce(ex);
        }
    }

    private static IModLogger? ResolveLogger()
    {
        try
        {
            return IoC.Resolve<IModLogger>();
        }
        catch (Exception)
        {
            return null; // no logger to report to; the card stays hidden, which is the safe outcome
        }
    }

    private void LogOnce(Exception ex)
    {
        if (_logged)
            return;
        _logged = true;
        _logger?.LogError($"[SmithsMeasure] Make Tools card threw. This is logged once per screen and later refreshes still run. Root cause: {ex}");
    }
}
