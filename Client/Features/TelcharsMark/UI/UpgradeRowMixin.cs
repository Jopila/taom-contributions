using System.Runtime.CompilerServices;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Library;
using TAOM.Adapters;

namespace TAOM.Features.TelcharsMark.UI;

/// <summary>
/// One inventory row as Telchar's Mark scores it, plus the two flags the tint widgets in
/// <see cref="UpgradeTintPrefab"/> bind to. UIExtenderEx builds one mixin per runtime <c>SPItemVM</c> before
/// each constructor returns, so every list row has one. The weak registry never keeps a VM alive.
/// </summary>
[ViewModelMixin]
internal sealed class UpgradeRowMixin : BaseViewModelMixin<SPItemVM>, IUpgradeRow
{
    private static readonly ConditionalWeakTable<SPItemVM, UpgradeRowMixin> Registry =
        new ConditionalWeakTable<SPItemVM, UpgradeRowMixin>();

    private readonly SPItemVM _row;
    private UpgradeVerdict _verdict;
    private bool _isUpgrade;
    private bool _isSidegrade;

    public UpgradeRowMixin(SPItemVM viewModel) : base(viewModel)
    {
        _row = viewModel;
        Registry.Remove(viewModel);
        Registry.Add(viewModel, this);
    }

    public static UpgradeRowMixin? Find(SPItemVM? vm) =>
        vm != null && Registry.TryGetValue(vm, out var mixin) ? mixin : null;

    /// <summary>The registry lookup as the adapter's row source.</summary>
    public static IUpgradeRow? RowFor(SPItemVM vm) => Find(vm);

    public ItemCandidate Candidate => UpgradeCandidateReader.Read(_row);

    public UpgradeVerdict Verdict
    {
        get => _verdict;
        set
        {
            _verdict = value;
            TaomIsUpgrade = value == UpgradeVerdict.Better;
            TaomIsSidegrade = value == UpgradeVerdict.Sidegrade;
        }
    }

    [DataSourceProperty]
    public bool TaomIsUpgrade
    {
        get => _isUpgrade;
        set
        {
            if (_isUpgrade == value) return;
            _isUpgrade = value;
            OnPropertyChangedWithValue(value, nameof(TaomIsUpgrade));
        }
    }

    [DataSourceProperty]
    public bool TaomIsSidegrade
    {
        get => _isSidegrade;
        set
        {
            if (_isSidegrade == value) return;
            _isSidegrade = value;
            OnPropertyChangedWithValue(value, nameof(TaomIsSidegrade));
        }
    }
}
