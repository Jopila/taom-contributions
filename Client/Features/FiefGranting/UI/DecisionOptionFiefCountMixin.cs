using System;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Features.FiefGranting.UI;

/// <summary>
/// The fief count on each candidate card of a fief vote (#866, docs/features/fief-granting.md). One mixin per option card; the
/// prefab appends a text row with a hover list beside the card, bound to the three properties below. It is read-only: it
/// changes no merit, no support and no vote, so it needs no co-op guard. The row shows only for a clan on a settlement-claimant
/// ballot (including <see cref="TaomSettlementClaimantDecision"/>); the abstain card (no outcome) and every other decision type
/// hide it. The number is <see cref="FiefGrantFactsBuilder"/>'s own fortification count for that clan, excluding the fief under
/// vote, so it matches what the scoring used. <c>OptionHint</c> is not reused: RefreshCanChooseOption overwrites it.
/// </summary>
[ViewModelMixin("RefreshValues")]
internal class DecisionOptionFiefCountMixin : BaseViewModelMixin<DecisionOptionVM>
{
    private bool _logged;
    private bool _show;
    private string _text = "";

    public DecisionOptionFiefCountMixin(DecisionOptionVM viewModel) : base(viewModel)
    {
        // No Refresh() here: UIExtenderEx replays the view model constructor's RefreshValues call on the mixin as OnRefresh.
    }

    [DataSourceProperty]
    public bool ShowFiefCount
    {
        get => _show;
        set => SetField(ref _show, value, nameof(ShowFiefCount));
    }

    [DataSourceProperty]
    public string FiefCountText
    {
        get => _text;
        set => SetField(ref _text, value, nameof(FiefCountText));
    }

    [DataSourceProperty]
    public HintViewModel FiefCountHint { get; } = new();

    public override void OnRefresh()
    {
        try
        {
            Refresh();
        }
        catch (Exception ex)
        {
            ShowFiefCount = false;
            LogOnce(ex);
        }
    }

    private void Refresh()
    {
        var claimant = ViewModel?.Decision as SettlementClaimantDecision;
        var clan = (ViewModel?.Option as SettlementClaimantDecision.ClanAsDecisionOutcome)?.Clan;
        if (claimant == null || clan == null)
        {
            ShowFiefCount = false;
            return;
        }

        var names = FiefGrantFactsBuilder.ClanFortificationNames(clan, claimant.Settlement);
        // The ruling clan's own fief under vote is already in its holdings after the siege; the count leaves it out, and the hover says so.
        var notCounted = claimant.Settlement.OwnerClan == clan ? claimant.Settlement.Name?.ToString() : null;
        FiefCountText = FiefGrantBallotTexts.Count(names.Count);
        FiefCountHint.HintText = new TextObject("{=!}" + FiefGrantBallotTexts.Hint(names, notCounted));
        ShowFiefCount = true;
    }

    private void LogOnce(Exception ex)
    {
        if (_logged)
            return;
        _logged = true;
        try
        {
            IoC.Resolve<IModLogger>().LogError($"[FiefGranting] ballot fief count threw. This line is logged once per card and later refreshes still run. Root cause: {ex}");
        }
        catch (Exception)
        {
            // No logger to report to; the row is already hidden, which is the safe outcome.
        }
    }
}
