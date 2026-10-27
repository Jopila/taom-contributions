using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.InnRounds.Domain;
using TAOM.Features.NotableServices.Domain;
using TAOM.Features.NotableServices.Hooks;

namespace TAOM.Features.InnRounds.Hooks;

/// <summary>
/// Thin dialog boundary for Inn Rounds (ADR-002, docs/features/inn-rounds.md): registers the lines
/// (<see cref="InnRoundsDialogLines"/>) and hands every condition and consequence to <see cref="InnRoundsService"/>. Each
/// callback runs behind the shared <see cref="ScreenFaultLog"/>. The module builds this with <c>new</c> for every campaign: it
/// owns this campaign's <see cref="RoundLedger"/> (a container singleton is constructed once per process) and saves its day and
/// count in <c>SyncData</c>. It also keeps the last reveal answer, which the keeper's reply lines read, and clears it when a
/// request starts and when any conversation ends. No condition writes: the work runs in the consequences, which re-check every gate.
/// </summary>
public sealed class InnRoundsDialogBehavior : CampaignBehaviorBase
{
    internal const string DayKey = "_taom_innrounds_day";
    internal const string CountKey = "_taom_innrounds_count";

    private readonly ScreenFaultLog _faults;
    private readonly InnRoundsService _service;
    private RevealOutcome _outcome = RevealOutcome.Unavailable;

    public InnRoundsDialogBehavior(InnRoundsService service, IModLogger logger)
    {
        _faults = new ScreenFaultLog(logger, "InnRounds");
        _service = service;
    }

    internal RoundLedger Ledger { get; } = new();

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // A load starts from the fresh ledger and a key miss leaves it there. A save made before this feature has no record for
        // this behavior at all, so SyncData never runs and the ledger keeps its fresh defaults.
        var day = dataStore.IsSaving ? Ledger.Day : RoundLedger.NoDay;
        var count = dataStore.IsSaving ? Ledger.Count : 0;
        dataStore.SyncData(DayKey, ref day);
        dataStore.SyncData(CountKey, ref count);
        if (dataStore.IsSaving)
            return;
        Ledger.Day = day;
        Ledger.Count = count;
    }

    private void OnSessionLaunched(CampaignGameStarter starter) =>
        _faults.Run("session launched", () => InnRoundsDialogLines.Register(starter, this));

    private void OnConversationEnded(IEnumerable<CharacterObject> characters) => _outcome = RevealOutcome.Unavailable;

    internal bool CanParty() => _faults.Condition("party round condition", () =>
    {
        var quote = _service.QuoteParty(Ledger);
        if (quote == null)
            return false;
        MBTextManager.SetTextVariable(InnRoundsDialogLines.PartyLineVariable, InnRoundsTexts.PartyLine(quote.Price));
        return true;
    });

    internal bool CanHouse() => _faults.Condition("house round condition", () =>
    {
        var quote = _service.QuoteHouse(Ledger);
        if (quote == null)
            return false;
        MBTextManager.SetTextVariable(InnRoundsDialogLines.HouseLineVariable, InnRoundsTexts.HouseLine(quote.Price));
        return true;
    });

    internal bool PartyClickable(out TextObject explanation) =>
        Clickable("party round clickable condition", () => _service.QuoteParty(Ledger), out explanation);

    internal bool HouseClickable(out TextObject explanation) =>
        Clickable("house round clickable condition", () => _service.QuoteHouse(Ledger), out explanation);

    private bool Clickable(string site, Func<RoundQuote?> quote, out TextObject explanation)
    {
        explanation = TextObject.GetEmpty();
        try
        {
            var asked = quote();
            if (asked == null || (!asked.CapReached && asked.CanAfford))
                return true;
            explanation = asked.CapReached ? InnRoundsTexts.CapReached() : NotableServicesTexts.NotEnoughGold();
            return false;
        }
        catch (Exception ex)
        {
            _faults.LogOnce(site, ex);
            return false;
        }
    }

    internal void Party() => _faults.Run("party round consequence", () => _service.StandParty(Ledger));

    internal void House() => _faults.Run("house round consequence", () => _service.StandHouse(Ledger));

    internal bool CanReveal() => _faults.Condition("reveal condition", () =>
    {
        if (!_service.OffersReveal())
            return false;
        MBTextManager.SetTextVariable(InnRoundsDialogLines.RevealLineVariable, NotableServicesTexts.RevealLine(_service.RevealPrice));
        return true;
    });

    internal bool RevealClickable(out TextObject explanation)
    {
        explanation = TextObject.GetEmpty();
        try
        {
            if (_service.CanAffordReveal())
                return true;
            explanation = NotableServicesTexts.NotEnoughGold();
            return false;
        }
        catch (Exception ex)
        {
            _faults.LogOnce("reveal clickable condition", ex);
            return false;
        }
    }

    internal void Reveal()
    {
        _outcome = RevealOutcome.Unavailable;
        _faults.Run("reveal consequence", () => _outcome = _service.Reveal());
    }

    internal bool Answered(RevealOutcome outcome) => _outcome == outcome;
}
