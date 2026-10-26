using System;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Features.Encyclopedia.UI;

/// <summary>
/// The hero page's skills heading reads "Lvl: N, Skills:" for a hero of age (#907). Hooked on Refresh, which the engine's
/// RefreshValues calls right after it sets SkillsText, so the mixin's value is the last write. Below coming of age or with the
/// information hidden, vanilla's text stays. The hero is the page's <c>Obj</c>, as in the other mixins on this view model.
/// </summary>
[ViewModelMixin("Refresh")]
internal sealed class EncyclopediaHeroLevelHeaderMixin : BaseViewModelMixin<EncyclopediaHeroPageVM>
{
    private readonly IModLogger _logger;
    private bool _logged;

    public EncyclopediaHeroLevelHeaderMixin(EncyclopediaHeroPageVM viewModel) : base(viewModel)
    {
        _logger = IoC.Resolve<IModLogger>();
        // No OnRefresh() here: UIExtenderEx replays the view model constructor's Refresh call on the mixin as OnRefresh.
    }

    public override void OnRefresh()
    {
        try
        {
            if (ViewModel?.Obj is not Hero hero)
                return;

            var comesOfAge = Campaign.Current.Models.AgeModel.HeroComesOfAge;
            if (!HeroLevelHeader.ShouldShowLevel(hero.Age, comesOfAge, ViewModel.IsInformationHidden))
                return;

            ViewModel.SkillsText = new TextObject("{=taom_ency_hero_level_skills}Lvl: {LEVEL}, Skills:")
                .SetTextVariable("LEVEL", hero.Level)
                .ToString();
        }
        catch (Exception ex)
        {
            if (_logged)
                return;
            _logged = true;
            _logger.LogError($"[HeroLevelHeader] hero page refresh threw. Logged once per page; later calls still run. Root cause: {ex}");
        }
    }
}
