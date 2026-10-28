using System;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Library;
using TAOM.Core.Logging;

namespace TAOM.Features.HearthRoll.UI;

/// <summary>
/// Hearth Roll (#906) on the clan screen's Members list: a red tint behind the row of a player companion whose relation with the
/// player is below zero, and the age in years beside the name of each child. Hooked on UpdateProperties like the Lines of
/// Descent mixin on the same view model; UIExtenderEx 2.13.3 replays the constructor's own UpdateProperties call on the mixin as
/// OnRefresh, so the first row needs no call of ours. The relation is read straight from the main hero (not through a hero-list
/// scan, which skips a Disabled companion away on a quest), and only when the toggle is on and the hero is the player's
/// companion. A fault leaves the row without tint or label and is logged once per row.
/// </summary>
[ViewModelMixin("UpdateProperties")]
internal sealed class ClanLordItemBadgeMixin : BaseViewModelMixin<ClanLordItemVM>
{
    private readonly HearthRollSettingsProvider _settings;
    private readonly IModLogger _logger;
    private bool _logged;
    private bool _isTaomUnhappyCompanion;
    private string _taomAgeText = "";

    public ClanLordItemBadgeMixin(ClanLordItemVM viewModel) : base(viewModel)
    {
        _settings = IoC.Resolve<HearthRollSettingsProvider>();
        _logger = IoC.Resolve<IModLogger>();
        // No OnRefresh() here: UIExtenderEx replays the view model constructor's UpdateProperties call on the mixin as OnRefresh.
    }

    [DataSourceProperty]
    public bool IsTaomUnhappyCompanion
    {
        get => _isTaomUnhappyCompanion;
        set => SetField(ref _isTaomUnhappyCompanion, value, nameof(IsTaomUnhappyCompanion));
    }

    [DataSourceProperty]
    public string TaomAgeText
    {
        get => _taomAgeText;
        set => SetField(ref _taomAgeText, value, nameof(TaomAgeText));
    }

    public override void OnRefresh()
    {
        try
        {
            var vm = ViewModel;
            var hero = vm?.GetHero();
            if (vm == null || hero == null)
            {
                IsTaomUnhappyCompanion = false;
                TaomAgeText = "";
                return;
            }

            var enabled = _settings.Enabled;
            var companion = enabled && hero.IsPlayerCompanion;
            var relation = companion ? Hero.MainHero?.GetRelation(hero) ?? 0 : 0;
            IsTaomUnhappyCompanion = HearthRollRules.ShowsUnhappyTint(enabled, hero.IsPlayerCompanion, relation);
            TaomAgeText = HearthRollRules.AgeYears(vm.IsChild, hero.Age) is int years ? HearthRollTexts.Age(years).ToString() : "";
        }
        catch (Exception ex)
        {
            IsTaomUnhappyCompanion = false;
            TaomAgeText = "";
            LogOnce(ex);
        }
    }

    private void LogOnce(Exception ex)
    {
        if (_logged)
            return;
        _logged = true;
        _logger.LogError($"[HearthRoll] clan member row refresh threw. Logged once per row; later refreshes still run. Root cause: {ex}");
    }
}
