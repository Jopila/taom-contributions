using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Features.HardQuestions.Domain;

namespace TAOM.Features.HardQuestions.Hooks;

/// <summary>
/// Thin dialog boundary for Hard Questions (ADR-002, docs/features/hard-questions.md): registers the lines
/// (<see cref="HardQuestionsDialogLines"/>) and hands every condition and consequence to
/// <see cref="InterrogationService"/>. A forced question and a strip run when the conversation ends
/// (the FieldCommission dismiss pattern), never inside the lord's own conversation. Built fresh for every
/// campaign by the feature module, so the pending hand-off cannot cross campaigns.
/// </summary>
public sealed class HardQuestionsDialogBehavior : CampaignBehaviorBase
{
    private readonly InterrogationService _service;
    private readonly ICaptiveLordAdapter _captives;
    private HardQuestionTopic _topic;

    public HardQuestionsDialogBehavior(InterrogationService service, ICaptiveLordAdapter captives)
    {
        _service = service;
        _captives = captives;
        _service.ResetPending();
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
    }

    public override void SyncData(IDataStore dataStore) { }

    private void OnSessionLaunched(CampaignGameStarter starter) => HardQuestionsDialogLines.Register(starter, this);

    private void OnConversationEnded(IEnumerable<CharacterObject> characters) => _service.CompletePending();

    private string? Partner => _captives.ConversationPartnerId;

    internal bool CanAsk(HardQuestionTopic topic) => _service.CanAsk(topic, Partner);

    internal void Choose(HardQuestionTopic topic) => _topic = topic;

    internal bool CanStrip() => _service.CanStrip(Partner);

    // Condition AND text setup: the price line names the sum, and a condition is the only hook that runs
    // before a line renders. Re-pushed on each render, since the variable is a global.
    internal bool PriceLine()
    {
        MBTextManager.SetTextVariable("PRICE", _service.Quote(_topic, Partner).ToString());
        return true;
    }

    internal bool PriceIsPayable(out TextObject? explanation)
    {
        var payable = _service.CanAfford(_topic, Partner);
        explanation = payable ? null : new TextObject("{=taom_hq_cannot_afford}You do not carry that much.");
        return payable;
    }

    internal void PayBribe() => _service.PayBribe(_topic, Partner);

    internal void Force() => _service.QueueForce(_topic, Partner);

    internal void Strip() => _service.QueueStrip(Partner);

    internal bool RepliesAs(BribeReplyKind kind) => _service.ReplyKind(Partner) == kind;
}
