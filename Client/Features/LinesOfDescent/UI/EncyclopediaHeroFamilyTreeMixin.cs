using System;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Library;
using TAOM.Core.Logging;

namespace TAOM.Features.LinesOfDescent.UI;

/// <summary>
/// Lines of Descent (#905) on the encyclopedia hero page: the Family Tree button above the Family divider. Hooked on Refresh
/// (the engine's own override, which refills the Family list, so it is the point where the list is known). The button shows when
/// the page lists any family, which vanilla fills only while the hero's information is not hidden. The hero is the page's
/// <c>Obj</c>, as in the Messengers mixin on the same view model.
/// </summary>
[ViewModelMixin("Refresh")]
internal sealed class EncyclopediaHeroFamilyTreeMixin : BaseViewModelMixin<EncyclopediaHeroPageVM>
{
    private bool _hasFamilyTree;
    private bool _logged;

    public EncyclopediaHeroFamilyTreeMixin(EncyclopediaHeroPageVM viewModel) : base(viewModel)
    {
        FamilyTreeText = FamilyTreeTexts.Button().ToString();
        // No OnRefresh() here: UIExtenderEx replays the view model constructor's Refresh call on the mixin as OnRefresh.
    }

    [DataSourceProperty]
    public string FamilyTreeText { get; }

    [DataSourceProperty]
    public bool HasFamilyTree
    {
        get => _hasFamilyTree;
        set
        {
            if (_hasFamilyTree == value)
                return;
            _hasFamilyTree = value;
            OnPropertyChangedWithValue(value, nameof(HasFamilyTree));
        }
    }

    public override void OnRefresh()
    {
        try
        {
            HasFamilyTree = ViewModel?.Family != null && ViewModel.Family.Count > 0;
        }
        catch (Exception ex)
        {
            HasFamilyTree = false;
            LogOnce("refresh", ex);
        }
    }

    [DataSourceMethod]
    public void ExecuteOpenFamilyTree()
    {
        try
        {
            if (ViewModel?.Obj is Hero hero)
                IoC.Resolve<FamilyTreePopup>().Open(hero.StringId, fromClanMembers: false);
        }
        catch (Exception ex)
        {
            LogOnce("click", ex);
        }
    }

    private void LogOnce(string site, Exception ex)
    {
        if (_logged)
            return;
        _logged = true;
        IoC.Resolve<IModLogger>().LogError($"[FamilyTree] hero page {site} threw. Logged once per page; later calls still run. Root cause: {ex}");
    }
}
