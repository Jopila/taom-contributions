using System;
using System.Collections.Generic;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TAOM.Core.Logging;

namespace TAOM.Features.LoremastersLine.UI;

/// <summary>
/// The data behind the Loremaster's Line on one skill of the character screen (#877). Vanilla calls
/// <c>SkillVM.RefreshWithCurrentValues</c> when the screen builds the skill and after every attribute click, focus click
/// and reset, so the line follows the preview before Confirm. The screen builds every hero's skill view models when it
/// opens (CharacterDeveloperVM) and a hero switch only rebinds them; each refreshes on its own hero's clicks.
/// The floor comes from probing the game model's learning rate (<see cref="LearningFloorCalculator"/>, 14 cheap calls per
/// refresh), so a model override is followed. Engine access is in <see cref="LoremastersLineSkillAccess"/>.
/// </summary>
[ViewModelMixin("RefreshWithCurrentValues")]
internal sealed class LoremastersLineSkillMixin : BaseViewModelMixin<SkillVM>
{
    // Process-wide, not per instance: there is one mixin per SkillVM and the screen builds every hero's skill view models
    // (twice at open), so a per-instance latch would log the same fault once per skill. Keyed per exception type.
    private static readonly HashSet<string> RefreshFailuresLogged = new HashSet<string>(StringComparer.Ordinal);
    private static readonly HashSet<string> TooltipFailuresLogged = new HashSet<string>(StringComparer.Ordinal);

    private readonly IModLogger? _logger;
    private readonly bool _enabled;
    private LearningFloor _floor = LearningFloor.None;
    private bool _hasLine;
    private int _barLevels;
    private int _bandLevels;

    public LoremastersLineSkillMixin(SkillVM viewModel) : base(viewModel)
    {
        try
        {
            _logger = IoC.Resolve<IModLogger>();
            _enabled = LoremastersLineSkillAccess.IsBound && IoC.Resolve<LoremastersLineSettingsProvider>().Enabled;
            LoremastersLineTooltip = new BasicTooltipViewModel(GetLineTooltip);
            if (_enabled)
                viewModel.LearningLimitTooltip = new BasicTooltipViewModel(GetLimitTooltip);
        }
        catch (Exception ex)
        {
            // The constructor runs inside the view model's own: a throw here would take the whole screen down.
            _enabled = false;
            LoremastersLineTooltip ??= new BasicTooltipViewModel();
            _logger?.LogError($"[LoremastersLine] skill mixin constructor threw. Root cause: {ex}");
        }
    }

    [DataSourceProperty]
    public bool HasLoremastersLine
    {
        get => _hasLine;
        set => SetField(ref _hasLine, value, nameof(HasLoremastersLine));
    }

    // The bar's width in levels (MaxLevel + 25) and the band's (floor to the end of the bar): the fill widget's
    // MaxAmount and InitialAmount.
    [DataSourceProperty]
    public int LoremastersLineBarLevels { get => _barLevels; set => SetField(ref _barLevels, value, nameof(LoremastersLineBarLevels)); }

    [DataSourceProperty]
    public int LoremastersLineBandLevels { get => _bandLevels; set => SetField(ref _bandLevels, value, nameof(LoremastersLineBandLevels)); }

    // Created once, so the band's hint widget keeps the same data source; the callback reads the latest floor.
    [DataSourceProperty]
    public BasicTooltipViewModel LoremastersLineTooltip { get; private set; } = null!;

    public override void OnRefresh()
    {
        if (!_enabled) return;
        try
        {
            Recompute();
        }
        catch (Exception ex)
        {
            HasLoremastersLine = false;
            if (!RefreshFailuresLogged.Add(ex.GetType().FullName ?? "(unknown)")) return;
            _logger?.LogError($"[LoremastersLine] skill mixin OnRefresh threw. Root cause: {ex}");
        }
    }

    private void Recompute()
    {
        var vm = ViewModel;
        var rate = LoremastersLineSkillAccess.RateFunction(vm);
        if (vm == null || rate == null)
        {
            HasLoremastersLine = false;
            return;
        }

        _floor = LearningFloorCalculator.Find(rate);
        var onBar = LearningFloorCalculator.IsOnBar(_floor, vm.MaxLevel);
        if (onBar)
        {
            // IsOnBar requires a bar above 0, so the fill widget's MaxAmount is never 0 while it shows; widths go first anyway.
            LoremastersLineBarLevels = LearningFloorCalculator.BarLevels(vm.MaxLevel);
            LoremastersLineBandLevels = LearningFloorCalculator.BandLevels(_floor, vm.MaxLevel);
        }

        HasLoremastersLine = onBar;
    }

    private List<TooltipProperty> GetLimitTooltip()
    {
        var vm = ViewModel;
        var heroItem = LoremastersLineSkillAccess.HeroItem(vm);
        if (vm == null || heroItem == null) return new List<TooltipProperty>();

        var result = CampaignUIHelper.GetLearningLimitTooltip(heroItem.CharacterAttributes, vm.CurrentFocusLevel, vm.Skill);
        try
        {
            // The extra line explains the red marker, so it shows only while the marker does.
            if (HasLoremastersLine) result.Add(LoremastersLineTooltips.LimitLine());
        }
        catch (Exception ex)
        {
            LogTooltipFailure(ex);
        }

        return result;
    }

    private List<TooltipProperty> GetLineTooltip()
    {
        try
        {
            return _floor.HasFloor ? LoremastersLineTooltips.ForFloor(_floor) : new List<TooltipProperty>();
        }
        catch (Exception ex)
        {
            LogTooltipFailure(ex);
            return new List<TooltipProperty>();
        }
    }

    private void LogTooltipFailure(Exception ex)
    {
        if (TooltipFailuresLogged.Add(ex.GetType().FullName ?? "(unknown)"))
            _logger?.LogError($"[LoremastersLine] tooltip callback threw. Root cause: {ex}");
    }
}
