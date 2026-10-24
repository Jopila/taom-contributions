using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Features.SettlementServices.Hooks;

/// <summary>
/// Thin dialog boundary for Watch-Silver (#859, ADR-002, docs/features/settlement-services.md): registers the lines
/// (<see cref="WatchSilverDialogLines"/>) and hands every condition and consequence to <see cref="WatchSilverService"/>. Each
/// callback runs behind the shared <see cref="ScreenFaultLog"/>. The module builds this with <c>new</c> for every campaign, so the one
/// field it keeps, the outcome of the last payment, never crosses campaigns; it is cleared when an offer starts and when any
/// conversation ends. It saves nothing.
/// </summary>
public sealed class WatchSilverDialogBehavior : CampaignBehaviorBase
{
    private readonly ScreenFaultLog _faults;
    private readonly WatchSilverService _service;
    private WatchSilverResult? _outcome;

    public WatchSilverDialogBehavior(WatchSilverService service, IModLogger logger)
    {
        _faults = new ScreenFaultLog(logger, "SettlementServices");
        _service = service;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
    }

    public override void SyncData(IDataStore dataStore) { }

    private void OnSessionLaunched(CampaignGameStarter starter) =>
        _faults.Run("watch-silver session launched", () => WatchSilverDialogLines.Register(starter, this));

    // A flag that gates a custom reply must not outlive its conversation (lessons/state-lifecycle-save.md).
    private void OnConversationEnded(IEnumerable<CharacterObject> characters) => _outcome = null;

    // The offer is shown while the gate is open or only the ceiling stops it (then it is greyed with its reason).
    internal bool CanOffer() => _faults.Condition("offer condition", () => _service.Quote().IsOffered);

    internal bool OfferClickable(out TextObject explanation)
    {
        explanation = TextObject.GetEmpty();
        try
        {
            if (_service.Quote().Block != WatchSilverBlock.AtCeiling)
                return true;
            explanation = SettlementServicesTexts.AtCeiling();
            return false;
        }
        catch (Exception ex)
        {
            _faults.LogOnce("offer clickable condition", ex);
            return false;
        }
    }

    internal void StartOffer() => _outcome = null;

    // Always true: the offer line was shown, so the governor must answer. The variable is cleared first (text globals persist across
    // conversations), so a fault leaves the loyalty blank, never a stale number, and every amount hidden; "Never mind" remains.
    internal bool NameLoyalty()
    {
        _faults.Run("ask condition", () =>
        {
            MBTextManager.SetTextVariable("TAOM_WSILVER_LOYALTY", "");
            MBTextManager.SetTextVariable("TAOM_WSILVER_LOYALTY", (int)_service.Quote().Loyalty);
        });
        return true;
    }

    internal bool TierShown(int index) => _faults.Condition("tier " + index + " condition", () =>
    {
        var quote = _service.Quote();
        if (index >= quote.Tiers.Count)
            return false;
        var tier = quote.Tiers[index];
        MBTextManager.SetTextVariable("TAOM_WSILVER_TIER_" + index, SettlementServicesTexts.Tier(tier.Points, tier.Price));
        return true;
    });

    // Presentation only: Pay re-runs every check.
    internal bool TierClickable(int index, out TextObject explanation)
    {
        explanation = TextObject.GetEmpty();
        try
        {
            var quote = _service.Quote();
            if (index >= quote.Tiers.Count || _service.CanAfford(quote.Tiers[index].Price))
                return true;
            explanation = SettlementServicesTexts.NotEnoughGold();
            return false;
        }
        catch (Exception ex)
        {
            _faults.LogOnce("tier " + index + " clickable condition", ex);
            return false;
        }
    }

    internal void Pay(int index) => _faults.Run("tier " + index + " consequence", () => _outcome = _service.Apply(index));

    // The reply states what the payment did: the loyalty is the value read back from the fief.
    internal bool PaidOut() => _faults.Condition("done condition", () =>
    {
        var outcome = _outcome;
        if (outcome == null || !outcome.Success)
            return false;
        MBTextManager.SetTextVariable("LOYALTY", (int)outcome.Loyalty);
        return true;
    });
}
