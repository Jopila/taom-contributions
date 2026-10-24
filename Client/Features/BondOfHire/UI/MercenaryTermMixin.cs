using System;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Clans;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.BondOfHire.UI;

/// <summary>
/// The contract line of the kingdom Clans panel (#868, docs/features/bond-of-hire.md). It follows the selected clan: every
/// selection goes through <c>KingdomClanVM.CurrentSelectedClan</c>'s public setter, which raises <c>PropertyChangedWithValue</c>
/// (never <c>PropertyChanged</c>), so the constructor subscribes there and <see cref="OnFinalize"/> unsubscribes, as the fief
/// Rename button does. The constructor's first selection (inside <c>RefreshClanList</c>) fires before any mixin exists, and
/// UIExtenderEx replays the view model constructor's RefreshValues call on the mixin as <see cref="OnRefresh"/>, which covers it.
/// KingdomClanVM declares its own RefreshValues and OnFinalize, so this mixin stacks no hook on the base ViewModel. Setting a
/// mixin property raises PropertyChanged (UIExtenderEx's SetField), not this event, so the handler never sees its own writes.
/// PropertyChangedWithValue carries only reference-typed properties, so int and bool changes such as SupportCost never reach
/// it. The clan is converted to plain values here (<see cref="MercenaryClanAdapter"/>, then <see cref="ContractTermRules"/>); read-only, it
/// changes nothing, so it needs no co-op guard. The line shows the engine's own date, whoever set it, and is hidden for a clan
/// that is not a mercenary and for the player's clan.
/// </summary>
[ViewModelMixin("RefreshValues")]
internal class MercenaryTermMixin : BaseViewModelMixin<KingdomClanVM>
{
    private readonly Func<Clan, IMercenaryClanAdapter> _adapterOf;
    private readonly IModLogger? _logger;
    private bool _logged;
    private bool _isVisible;
    private string _text = "";

    public MercenaryTermMixin(KingdomClanVM viewModel)
        : this(viewModel, clan => new MercenaryClanAdapter(clan), ResolveLogger())
    {
    }

    // The seam for TAOM.Tests: a real adapter reads the campaign clock.
    internal MercenaryTermMixin(KingdomClanVM viewModel, Func<Clan, IMercenaryClanAdapter> adapterOf, IModLogger? logger) : base(viewModel)
    {
        _adapterOf = adapterOf;
        _logger = logger;
        // UIExtenderEx passes the view model under construction; the add accessor only appends, so this cannot fault.
        viewModel.PropertyChangedWithValue += OnSelectionChanged;
        // No Refresh() here: UIExtenderEx replays the view model constructor's RefreshValues call on the mixin as OnRefresh.
    }

    [DataSourceProperty]
    public bool IsMercenaryTermVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value, nameof(IsMercenaryTermVisible));
    }

    [DataSourceProperty]
    public string MercenaryTermText
    {
        get => _text;
        set => SetField(ref _text, value, nameof(MercenaryTermText));
    }

    public override void OnRefresh() => Guard();

    public override void OnFinalize()
    {
        var vm = ViewModel;
        if (vm != null)
            vm.PropertyChangedWithValue -= OnSelectionChanged;
    }

    private void OnSelectionChanged(object sender, PropertyChangedWithValueEventArgs e)
    {
        if (e.PropertyName == "CurrentSelectedClan")
            Guard();
    }

    private void Guard()
    {
        try
        {
            Refresh();
        }
        catch (Exception ex)
        {
            IsMercenaryTermVisible = false;
            LogOnce(ex);
        }
    }

    private void Refresh()
    {
        var clan = ViewModel?.CurrentSelectedClan?.Clan;
        var term = clan == null
            ? new ContractTerm(ContractTermKind.Hidden, 0)
            : Describe(clan);
        MercenaryTermText = BondOfHireTexts.Term(term);
        IsMercenaryTermVisible = term.Kind != ContractTermKind.Hidden;
    }

    private ContractTerm Describe(Clan clan)
    {
        var adapter = _adapterOf(clan);
        return ContractTermRules.Describe(clan.IsUnderMercenaryService, adapter.IsPlayerClan, adapter.RemainingStayDays);
    }

    private static IModLogger? ResolveLogger()
    {
        try
        {
            return IoC.Resolve<IModLogger>();
        }
        catch (Exception)
        {
            return null; // no logger to report to; the line stays hidden, which is the safe outcome
        }
    }

    private void LogOnce(Exception ex)
    {
        if (_logged)
            return;
        _logged = true;
        _logger?.LogError($"[BondOfHire] contract line threw. This line is logged once per screen and later selections still run. Root cause: {ex}");
    }
}
