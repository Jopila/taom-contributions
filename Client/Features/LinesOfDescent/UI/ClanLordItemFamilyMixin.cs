using System;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Library;
using TAOM.Core.Logging;

namespace TAOM.Features.LinesOfDescent.UI;

/// <summary>
/// Lines of Descent (#905) on the clan screen's Members tab: the Family Tree button for the selected member, and the relation
/// line under the member's name written as the chain Describe builds ("Husband of Z, daughter of W") instead of vanilla's one
/// word. Hooked on UpdateProperties; UIExtenderEx 2.13.3 replays the constructor's own UpdateProperties call on the mixin as
/// OnRefresh, so the first label needs no call of ours. Where Describe has no path inside its hop cap, vanilla's text is left alone.
/// </summary>
[ViewModelMixin("UpdateProperties")]
internal sealed class ClanLordItemFamilyMixin : BaseViewModelMixin<ClanLordItemVM>
{
    private readonly IFamilyTreeService _service;
    private readonly FamilyTreePopup _popup;
    private readonly IModLogger _logger;
    private bool _logged;

    public ClanLordItemFamilyMixin(ClanLordItemVM viewModel) : base(viewModel)
    {
        _service = IoC.Resolve<IFamilyTreeService>();
        _popup = IoC.Resolve<FamilyTreePopup>();
        _logger = IoC.Resolve<IModLogger>();
        FamilyTreeText = FamilyTreeTexts.Button().ToString();
        // No OnRefresh() here: UIExtenderEx replays the view model constructor's UpdateProperties call on the mixin as OnRefresh.
    }

    [DataSourceProperty]
    public string FamilyTreeText { get; }

    public override void OnRefresh()
    {
        try
        {
            ApplyRelationLabel();
        }
        catch (Exception ex)
        {
            LogOnce("refresh", ex);
        }
    }

    [DataSourceMethod]
    public void ExecuteOpenFamilyTree()
    {
        try
        {
            var hero = ViewModel?.GetHero();
            if (hero != null)
                _popup.Open(hero.StringId, fromClanMembers: true);
        }
        catch (Exception ex)
        {
            LogOnce("click", ex);
        }
    }

    private void ApplyRelationLabel()
    {
        var vm = ViewModel;
        var hero = vm?.GetHero();
        var main = Hero.MainHero;
        if (vm == null || hero == null || main == null || hero == main)
            return;
        var phrase = _service.Describe(hero.StringId, main.StringId);
        if (phrase != null)
            vm.RelationToMainHeroText = FamilyTreeTexts.Render(phrase).ToString();
    }

    private void LogOnce(string site, Exception ex)
    {
        if (_logged)
            return;
        _logged = true;
        _logger.LogError($"[FamilyTree] clan member {site} threw. Logged once per row; later calls still run. Root cause: {ex}");
    }
}
