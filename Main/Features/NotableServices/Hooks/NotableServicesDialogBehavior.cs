using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.NotableServices.Domain;

namespace TAOM.Features.NotableServices.Hooks;

/// <summary>
/// Thin dialog boundary for Notable Services (ADR-002, docs/features/notable-services.md): registers the lines
/// (<see cref="NotableServicesDialogLines"/>) and hands every condition and consequence to
/// <see cref="NotableServicesService"/>. Each callback runs behind the shared <see cref="ScreenFaultLog"/>. The module builds
/// this with <c>new</c> for every campaign; it keeps only the last answers, which the notable's reply lines read, clears
/// them when a request starts and when any conversation ends, and saves nothing. No condition writes: the work runs in the
/// consequences, which re-check every gate.
/// </summary>
public sealed class NotableServicesDialogBehavior : CampaignBehaviorBase
{
    private readonly ScreenFaultLog _faults;
    private readonly NotableServicesService _service;
    private bool _introduced;
    private RevealOutcome _outcome = RevealOutcome.Unavailable;

    public NotableServicesDialogBehavior(NotableServicesService service, IModLogger logger)
    {
        _faults = new ScreenFaultLog(logger, "NotableServices");
        _service = service;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
    }

    public override void SyncData(IDataStore dataStore) { }

    private void OnSessionLaunched(CampaignGameStarter starter) =>
        _faults.Run("session launched", () => NotableServicesDialogLines.Register(starter, this));

    // A flag that gates a custom reply must not outlive its conversation (lessons/state-lifecycle-save.md).
    private void OnConversationEnded(IEnumerable<CharacterObject> characters)
    {
        _introduced = false;
        _outcome = RevealOutcome.Unavailable;
    }

    internal bool CanGift() => _faults.Condition("gift condition", _service.OffersGift);

    internal void Gift() => _faults.Run("gift consequence", _service.QueueGift);

    // Shows the price in the line's text: a TextObject with its own variables, set as the one whole-line variable.
    internal bool CanIntroduce() => _faults.Condition("introduce condition", () =>
    {
        var quote = _service.QuoteIntroduce();
        if (quote == null)
            return false;
        MBTextManager.SetTextVariable("TAOM_NS_INTRODUCE_LINE", NotableServicesTexts.IntroduceLine(quote.Count, quote.Price));
        return true;
    });

    internal bool IntroduceClickable(out TextObject explanation)
    {
        explanation = TextObject.GetEmpty();
        try
        {
            if (_service.QuoteIntroduce()?.CanAfford != false)
                return true;
            explanation = NotableServicesTexts.NotEnoughGold();
            return false;
        }
        catch (Exception ex)
        {
            _faults.LogOnce("introduce clickable condition", ex);
            return false;
        }
    }

    internal void Introduce()
    {
        _introduced = false;
        _faults.Run("introduce consequence", () => _introduced = _service.Introduce());
    }

    internal bool Introduced() => _introduced;

    internal bool CanReveal() => _faults.Condition("reveal condition", () =>
    {
        if (!_service.OffersReveal())
            return false;
        MBTextManager.SetTextVariable("TAOM_NS_REVEAL_LINE", NotableServicesTexts.RevealLine(_service.RevealPrice));
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
